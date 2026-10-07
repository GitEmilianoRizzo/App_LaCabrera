"""
Toast Combined Parser - Parses HTML for details + CSV ZIP for official totals
Cross-validates and adjusts to ensure data integrity.
"""
import re
import csv
import zipfile
import io
from datetime import datetime
from typing import Dict, Any, List, Optional, Tuple
from decimal import Decimal, ROUND_HALF_UP
from bs4 import BeautifulSoup
from .base_parser import BaseParser, ParseResult


class ToastCombinedParserImpl(BaseParser):
    """
    Combined parser that uses:
    - HTML Order Details for ticket/item granularity
    - CSV Sales Summary ZIP for official totals (source of truth)

    Cross-validates and adjusts HTML data to match CSV totals.
    """

    @property
    def code(self) -> str:
        return "TOAST_COMBINED"

    @property
    def name(self) -> str:
        return "Toast Combined (HTML + CSV)"

    @property
    def extensions(self) -> list:
        return [".html", ".htm"]

    def parse(self, content: str, filename: str = "", csv_zip_content: bytes = None) -> ParseResult:
        """
        Parse Toast data from HTML with optional CSV validation.
        """
        try:
            # Step 1: Parse HTML for ticket details
            html_result = self._parse_html(content, filename)
            if not html_result['success']:
                return ParseResult.fail(html_result['error'])

            tickets = html_result['tickets']

            # Step 2: If CSV ZIP provided, parse and validate
            csv_totals = None
            csv_date_range = None
            validation_result = None

            if csv_zip_content:
                csv_result = self._parse_csv_zip(csv_zip_content)
                if csv_result['success']:
                    csv_totals = csv_result['totals']
                    csv_date_range = csv_result.get('date_range', {})

                    # Filter tickets to match CSV date range
                    if csv_date_range:
                        filtered_tickets = self._filter_tickets_by_date(
                            tickets,
                            csv_date_range.get('from'),
                            csv_date_range.get('to')
                        )
                    else:
                        filtered_tickets = tickets

                    # Calculate HTML totals for filtered tickets only
                    html_totals = self._calculate_totals(filtered_tickets)

                    # Validate and adjust
                    validation_result = self._validate_and_adjust(
                        filtered_tickets, html_totals, csv_totals
                    )

                    # Update tickets with adjusted values
                    tickets = filtered_tickets
            else:
                html_totals = html_result['totals']

            # Step 3: Build final output
            franchise_code = self._extract_franchise_from_filename(filename)

            # Determine date range from tickets
            dates = [t.get('business_date') for t in tickets if t.get('business_date')]
            min_date = min(dates) if dates else datetime.now().strftime('%Y-%m-%d')
            max_date = max(dates) if dates else min_date

            # Use CSV totals if available, otherwise HTML totals
            final_totals = csv_totals if csv_totals else html_totals

            data = {
                "schema_version": "1.1",
                "batch_header": {
                    "batch_id": f"TOAST_{franchise_code}_{min_date}_{datetime.now().strftime('%H%M%S')}",
                    "upload_type": "FULL_DAY",
                    "generated_at": datetime.now().isoformat(),
                    "business_date": min_date,
                    "date_range": {
                        "from": min_date,
                        "to": max_date
                    },
                    "source_system": {
                        "system_name": "TOAST_POS",
                        "system_version": "HTML_CSV_COMBINED"
                    },
                    "franchise": {
                        "franchise_code": franchise_code,
                        "franchise_name": self._franchise_name_from_code(franchise_code),
                        "country": "US",
                        "currency": "USD",
                        "timezone": "America/New_York"
                    },
                    "control_totals": {
                        "ticket_count": len(tickets),
                        "item_line_count": sum(len(t.get('items', [])) for t in tickets),
                        "gross_sales_amount": float(final_totals.get('gross_sales', 0)),
                        "discount_amount": float(final_totals.get('discounts', 0)),
                        "net_sales_amount": float(final_totals.get('net_sales', 0)),
                        "tax_amount": float(final_totals.get('tax', 0)),
                        "gratuity_amount": float(final_totals.get('gratuity', 0)),
                        "tip_amount": float(final_totals.get('tips', 0)),
                        "covers_total": int(final_totals.get('guests', 0))
                    },
                    "validation": validation_result,
                    "amounts_include_tax": False
                },
                "tickets": tickets
            }

            preview = self.generate_preview(data)
            return ParseResult.ok(data, preview)

        except Exception as e:
            import traceback
            return ParseResult.fail(f"Error en parser combinado: {str(e)}\n{traceback.format_exc()}")

    def _filter_tickets_by_date(self, tickets: List[Dict], from_date: str, to_date: str) -> List[Dict]:
        """Filter tickets to only include those within the date range"""
        if not from_date or not to_date:
            return tickets

        filtered = []
        for ticket in tickets:
            biz_date = ticket.get('business_date', '')
            if biz_date and from_date <= biz_date <= to_date:
                filtered.append(ticket)

        return filtered

    def _calculate_totals(self, tickets: List[Dict]) -> Dict[str, Any]:
        """Calculate totals from a list of tickets"""
        totals = {
            'gross_sales': Decimal('0'),
            'discounts': Decimal('0'),
            'net_sales': Decimal('0'),
            'tax': Decimal('0'),
            'gratuity': Decimal('0'),
            'tips': Decimal('0'),
            'guests': 0,
            'items_count': 0
        }

        for ticket in tickets:
            for item in ticket.get('items', []):
                totals['net_sales'] += Decimal(str(item.get('net_amount', 0)))
                totals['tax'] += Decimal(str(item.get('tax_amount', 0)))
                totals['discounts'] += Decimal(str(item.get('discount_amount', 0)))
                totals['items_count'] += 1

            amounts = ticket.get('amounts', {})
            totals['gratuity'] += Decimal(str(amounts.get('service_charge_amount', 0)))
            totals['tips'] += Decimal(str(amounts.get('tip_amount', 0)))
            totals['guests'] += ticket.get('covers', 1)

        totals['gross_sales'] = totals['net_sales'] + totals['discounts']

        return totals

    def _parse_html(self, content: str, filename: str) -> Dict[str, Any]:
        """Parse HTML order details"""
        try:
            soup = BeautifulSoup(content, 'html.parser')
            order_blocks = soup.find_all('div', class_='order-border')

            if not order_blocks:
                return {'success': False, 'error': 'No se encontraron ordenes en el HTML'}

            tickets = []

            for order_block in order_blocks:
                ticket = self._parse_order(order_block)
                if ticket:
                    tickets.append(ticket)

            totals = self._calculate_totals(tickets)

            return {
                'success': True,
                'tickets': tickets,
                'totals': totals
            }

        except Exception as e:
            return {'success': False, 'error': str(e)}

    def _parse_order(self, order_block) -> Optional[Dict[str, Any]]:
        """Parse a single order block from HTML"""
        order_id = None
        order_header = order_block.find('h4', id='order-summary-header')
        if order_header:
            match = re.search(r'Order #(\d+)', order_header.get_text())
            if match:
                order_id = match.group(1)

        if not order_id:
            div_id = order_block.get('id', '')
            match = re.search(r'order-number-(\d+)', div_id)
            if match:
                order_id = match.group(1)

        if not order_id:
            return None

        toast_id = None
        meta_div = order_block.find('div', class_='order-detail-meta-id')
        if meta_div:
            match = re.search(r'ID:\s*(\d+)', meta_div.get_text())
            if match:
                toast_id = match.group(1)

        check_details = self._parse_check_details(order_block)
        items = self._parse_items(order_block)
        service_charges = self._parse_service_charges(order_block)
        payments = self._parse_payments(order_block)

        items_net = sum(Decimal(str(item.get('net_amount', 0))) for item in items)
        items_tax = sum(Decimal(str(item.get('tax_amount', 0))) for item in items)
        service_charge_amount = sum(Decimal(str(sc.get('amount', 0))) for sc in service_charges)

        raw_date = check_details.get('date')
        business_date = datetime.now().strftime('%Y-%m-%d')
        opened_at_iso = datetime.now().isoformat()

        if raw_date:
            try:
                dt = datetime.strptime(raw_date, '%m/%d/%y, %I:%M %p')
                if dt.year < 100:
                    dt = dt.replace(year=dt.year + 2000)
                business_date = dt.strftime('%Y-%m-%d')
                opened_at_iso = dt.isoformat()
            except:
                pass

        ticket = {
            "ticket_id": f"T{order_id}",
            "ticket_number": order_id,
            "external_order_id": toast_id or order_id,
            "status": "CLOSED",
            "opened_at": opened_at_iso,
            "closed_at": None,
            "business_date": business_date,
            "meal_period": check_details.get('revenue_center'),
            "table": {
                "table_number": check_details.get('table'),
                "table_area": check_details.get('revenue_center')
            } if check_details.get('table') else None,
            "waiter": {
                "waiter_id": check_details.get('server', 'UNKNOWN'),
                "waiter_name": check_details.get('server')
            } if check_details.get('server') else None,
            "covers": check_details.get('guests', 1),
            "currency": "USD",
            "amounts": {
                "gross_amount": float(items_net),
                "discount_amount": float(check_details.get('discount', 0)),
                "net_amount": float(items_net),
                "tax_amount": float(items_tax),
                "service_charge_amount": float(service_charge_amount),
                "tip_amount": float(check_details.get('tip', 0)),
                "total_paid_amount": float(items_net + items_tax + service_charge_amount)
            },
            "items": items,
            "service_charges": service_charges,
            "payment_methods": self._convert_payments(payments)
        }

        return ticket

    def _parse_check_details(self, order_block) -> Dict[str, Any]:
        """Parse check header details"""
        details = {'guests': 1}

        check_rows = order_block.find_all('div', class_='row-fluid')
        for row in check_rows:
            text = row.get_text()
            if 'Time Opened' in text:
                spans = row.find_all('div', class_='span4')
                if spans:
                    values = [v.strip() for v in spans[0].get_text(separator='|').split('|') if v.strip()]
                    if values:
                        details['date'] = values[0]
                        if len(values) > 1:
                            details['server'] = values[1]

        summary_div = order_block.find('div', id='order-summary')
        if summary_div:
            guests_input = summary_div.find('input', id='num-guests')
            if guests_input:
                try:
                    details['guests'] = int(guests_input.get('value', 1))
                except:
                    pass

            rc_div = summary_div.find('div', id='revenue-center-name')
            if rc_div:
                details['revenue_center'] = rc_div.get_text().strip()

        table_divs = order_block.find_all('div', class_='span1')
        for div in table_divs:
            text = div.get_text().strip()
            if text.isdigit() and len(text) <= 3:
                details['table'] = text
                break

        for div in order_block.find_all('div', class_='span1'):
            text = div.get_text()
            if '$' in text:
                values = re.findall(r'\$([\d,]+\.?\d*)', text)
                if len(values) >= 3:
                    try:
                        details['tip'] = float(values[2].replace(',', ''))
                    except:
                        pass

        return details

    def _parse_items(self, order_block) -> List[Dict[str, Any]]:
        """Parse items from order"""
        items = []

        items_table = order_block.find('table', id='order-details-item-table')
        if not items_table:
            items_table = order_block.find('table', class_='order-details-table')

        if not items_table:
            return items

        tbody = items_table.find('tbody')
        if not tbody:
            return items

        for idx, row in enumerate(tbody.find_all('tr')):
            cells = row.find_all('td')
            if len(cells) < 8:
                continue

            def parse_money(text):
                text = str(text).strip().replace('$', '').replace(',', '')
                try:
                    return Decimal(text) if text else Decimal('0')
                except:
                    return Decimal('0')

            menu_item = cells[0].get_text().strip()
            modifiers = cells[1].get_text().strip()
            unit_price = parse_money(cells[2].get_text())

            try:
                qty = int(float(cells[3].get_text().strip() or '1'))
            except:
                qty = 1

            discount = parse_money(cells[4].get_text())
            net = parse_money(cells[5].get_text())
            tax = parse_money(cells[6].get_text())

            voided = False
            if len(cells) > 8:
                voided = cells[8].get_text().strip().lower() == 'true'

            if voided:
                continue

            items.append({
                "line_id": f"L{idx + 1}",
                "product_code": menu_item.upper().replace(' ', '_')[:20],
                "product_name": menu_item,
                "product_category": self._categorize_item(menu_item),
                "modifiers": modifiers if modifiers else None,
                "quantity": qty,
                "unit_price": float(unit_price),
                "gross_amount": float(net + discount),
                "discount_amount": float(discount),
                "net_amount": float(net),
                "tax_amount": float(tax)
            })

        return items

    def _parse_service_charges(self, order_block) -> List[Dict[str, Any]]:
        """Parse service charges"""
        charges = []

        sc_table = order_block.find('table', id='order-details-service-charges-table')
        if not sc_table:
            return charges

        tbody = sc_table.find('tbody')
        if not tbody:
            return charges

        for idx, row in enumerate(tbody.find_all('tr')):
            cells = row.find_all('td')
            if len(cells) < 4:
                continue

            def parse_money(text):
                text = str(text).strip().replace('$', '').replace(',', '')
                try:
                    return float(text) if text else 0.0
                except:
                    return 0.0

            charges.append({
                "charge_id": f"SC{idx + 1}",
                "name": cells[0].get_text().strip(),
                "is_gratuity": cells[1].get_text().strip().lower() == 'yes',
                "amount": parse_money(cells[2].get_text()),
                "tax_amount": parse_money(cells[3].get_text())
            })

        return charges

    def _parse_payments(self, order_block) -> List[Dict[str, Any]]:
        """Parse payments"""
        payments = []

        pay_table = order_block.find('table', id='order-details-payments-table')
        if not pay_table:
            return payments

        tbody = pay_table.find('tbody')
        if not tbody:
            return payments

        for idx, row in enumerate(tbody.find_all('tr')):
            cells = row.find_all('td')
            if len(cells) < 6:
                continue

            def parse_money(text):
                text = str(text).strip().replace('$', '').replace(',', '')
                try:
                    return float(text) if text else 0.0
                except:
                    return 0.0

            payment_text = cells[0].get_text().strip()

            payments.append({
                "payment_id": f"P{idx + 1}",
                "payment_method": self._parse_payment_method(payment_text),
                "amount": parse_money(cells[2].get_text()),
                "tip_amount": parse_money(cells[3].get_text()),
                "gratuity_amount": parse_money(cells[4].get_text()),
                "total_amount": parse_money(cells[5].get_text())
            })

        return payments

    def _parse_csv_zip(self, zip_content: bytes) -> Dict[str, Any]:
        """Parse CSV ZIP file for official totals"""
        try:
            totals = {
                'gross_sales': Decimal('0'),
                'discounts': Decimal('0'),
                'net_sales': Decimal('0'),
                'tax': Decimal('0'),
                'gratuity': Decimal('0'),
                'tips': Decimal('0'),
                'guests': 0,
                'orders': 0,
                'items_count': 0
            }

            date_range = {'from': None, 'to': None}
            daily_data = []

            with zipfile.ZipFile(io.BytesIO(zip_content), 'r') as zf:
                file_list = zf.namelist()

                # Parse Net sales summary.csv
                net_sales_file = next((f for f in file_list if 'net sales summary' in f.lower()), None)
                if net_sales_file:
                    with zf.open(net_sales_file) as f:
                        content = f.read().decode('utf-8')
                        reader = csv.DictReader(io.StringIO(content))
                        for row in reader:
                            totals['gross_sales'] = Decimal(str(row.get('Gross sales', 0) or 0))
                            totals['discounts'] = abs(Decimal(str(row.get('Sales discounts', 0) or 0)))
                            totals['net_sales'] = Decimal(str(row.get('Net sales', 0) or 0))

                # Parse Revenue summary.csv
                revenue_file = next((f for f in file_list if 'revenue summary' in f.lower()), None)
                if revenue_file:
                    with zf.open(revenue_file) as f:
                        content = f.read().decode('utf-8')
                        reader = csv.DictReader(io.StringIO(content))
                        for row in reader:
                            totals['gratuity'] = Decimal(str(row.get('Gratuity', 0) or 0))
                            totals['tax'] = Decimal(str(row.get('Tax amount', 0) or 0))
                            totals['tips'] = Decimal(str(row.get('Tips', 0) or 0))

                # Parse Sales by day.csv for date range, orders, and guests
                sales_day_file = next((f for f in file_list if 'sales by day' in f.lower()), None)
                if sales_day_file:
                    with zf.open(sales_day_file) as f:
                        content = f.read().decode('utf-8')
                        reader = csv.DictReader(io.StringIO(content))
                        for row in reader:
                            date_str = row.get('yyyyMMdd', '')
                            if date_str and len(date_str) == 8:
                                # Convert YYYYMMDD to YYYY-MM-DD
                                formatted_date = f"{date_str[:4]}-{date_str[4:6]}-{date_str[6:8]}"
                                daily_data.append(formatted_date)

                            totals['orders'] += int(float(row.get('Total orders', 0) or 0))
                            totals['guests'] += int(float(row.get('Total guests', 0) or 0))

                # Determine date range from daily data
                if daily_data:
                    date_range['from'] = min(daily_data)
                    date_range['to'] = max(daily_data)

                # Parse Revenue center summary.csv for items count
                rc_file = next((f for f in file_list if 'revenue center summary' in f.lower()), None)
                if rc_file:
                    with zf.open(rc_file) as f:
                        content = f.read().decode('utf-8')
                        reader = csv.DictReader(io.StringIO(content))
                        for row in reader:
                            if row.get('Revenue center', '').lower() == 'total':
                                totals['items_count'] = int(float(row.get('Items', 0) or 0))

            return {'success': True, 'totals': totals, 'date_range': date_range}

        except Exception as e:
            return {'success': False, 'error': str(e)}

    def _validate_and_adjust(
        self,
        tickets: List[Dict],
        html_totals: Dict,
        csv_totals: Dict
    ) -> Dict[str, Any]:
        """
        Validate HTML data against CSV totals and adjust if needed.
        CSV is the source of truth.
        """
        validation = {
            'status': 'VALIDATED',
            'csv_source_of_truth': True,
            'html_tickets_count': len(tickets),
            'csv_orders_count': int(csv_totals.get('orders', 0)),
            'comparisons': {},
            'adjustments': []
        }

        metrics = ['net_sales', 'gratuity', 'tax', 'tips']

        for metric in metrics:
            html_val = float(html_totals.get(metric, 0))
            csv_val = float(csv_totals.get(metric, 0))
            diff = html_val - csv_val
            pct_diff = (diff / csv_val * 100) if csv_val != 0 else 0

            validation['comparisons'][metric] = {
                'html': round(html_val, 2),
                'csv': round(csv_val, 2),
                'difference': round(diff, 2),
                'pct_difference': round(pct_diff, 2)
            }

            if abs(pct_diff) > 0.5:  # More than 0.5% difference
                validation['status'] = 'ADJUSTED'
                validation['adjustments'].append({
                    'metric': metric,
                    'from': round(html_val, 2),
                    'to': round(csv_val, 2),
                    'adjustment': round(csv_val - html_val, 2)
                })

        # If we need to adjust net_sales, calculate factor and apply
        if validation['status'] == 'ADJUSTED':
            html_net = float(html_totals.get('net_sales', 0))
            csv_net = float(csv_totals.get('net_sales', 0))

            if html_net > 0:
                adjustment_factor = csv_net / html_net
                validation['adjustment_factor'] = round(adjustment_factor, 6)

                # Only apply if factor is reasonable (between 0.8 and 1.2)
                if 0.8 <= adjustment_factor <= 1.2:
                    self._apply_adjustment(tickets, adjustment_factor, csv_totals)
                    validation['tickets_adjusted'] = True
                else:
                    validation['tickets_adjusted'] = False
                    validation['warning'] = f"Adjustment factor {adjustment_factor:.2f} outside safe range (0.8-1.2). Manual review required."

        return validation

    def _apply_adjustment(
        self,
        tickets: List[Dict],
        factor: float,
        csv_totals: Dict
    ):
        """Apply proportional adjustment to tickets"""
        if factor == 1.0:
            return

        for ticket in tickets:
            amounts = ticket.get('amounts', {})

            if 'net_amount' in amounts:
                amounts['net_amount'] = round(amounts['net_amount'] * factor, 2)
            if 'gross_amount' in amounts:
                amounts['gross_amount'] = round(amounts['gross_amount'] * factor, 2)

            for item in ticket.get('items', []):
                if 'net_amount' in item:
                    item['net_amount'] = round(item['net_amount'] * factor, 2)
                if 'gross_amount' in item:
                    item['gross_amount'] = round(item['gross_amount'] * factor, 2)

    def _parse_payment_method(self, text: str) -> str:
        """Parse payment method from text"""
        text_upper = text.upper()
        if 'CREDIT' in text_upper:
            return 'CREDIT_CARD'
        elif 'DEBIT' in text_upper:
            return 'DEBIT_CARD'
        elif 'CASH' in text_upper:
            return 'CASH'
        elif 'GIFT' in text_upper:
            return 'GIFT_CARD'
        return 'OTHER'

    def _convert_payments(self, payments: List[Dict]) -> List[Dict]:
        """Convert payments to standard format"""
        return [
            {
                "payment_method": p.get('payment_method', 'OTHER'),
                "amount": p.get('total_amount', 0)
            }
            for p in payments
        ]

    def _categorize_item(self, name: str) -> str:
        """Categorize item based on name"""
        name_lower = name.lower()

        if any(w in name_lower for w in ['coke', 'sprite', 'agua', 'water', 'soda', 'juice']):
            return 'BEVERAGE'
        if any(w in name_lower for w in ['wine', 'vino', 'malbec', 'cabernet']):
            return 'WINE'
        if any(w in name_lower for w in ['beer', 'cerveza']):
            return 'BEVERAGE'
        if any(w in name_lower for w in ['flan', 'dulce', 'helado', 'postre', 'dessert']):
            return 'DESSERT'
        if any(w in name_lower for w in ['bife', 'ribeye', 'steak', 'lomo', 'asado']):
            return 'MAIN_COURSE'
        if any(w in name_lower for w in ['empanada', 'provoleta', 'ensalada']):
            return 'STARTER'
        if any(w in name_lower for w in ['papas', 'fries', 'pure', 'side']):
            return 'SIDE_DISH'

        return 'OTHER'

    def _extract_franchise_from_filename(self, filename: str) -> str:
        """Extract franchise code from filename"""
        name_lower = filename.lower()
        if 'sunny' in name_lower:
            return 'MIAMI_SUNNY'
        elif 'midtown' in name_lower:
            return 'MIAMI_MIDTOWN'
        elif 'coconut' in name_lower or 'grove' in name_lower:
            return 'MIAMI_COCONUT'
        return 'MIAMI_UNKNOWN'

    def _franchise_name_from_code(self, code: str) -> str:
        """Get franchise name from code"""
        mapping = {
            'MIAMI_SUNNY': 'Miami Sunny Isles',
            'MIAMI_MIDTOWN': 'Miami Midtown',
            'MIAMI_COCONUT': 'Miami Coconut Grove',
        }
        return mapping.get(code, code)
