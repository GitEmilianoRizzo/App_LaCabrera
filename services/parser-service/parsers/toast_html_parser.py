"""
Toast HTML Parser - Parses Toast POS Order Details HTML export
"""
import re
import html
from datetime import datetime
from typing import Dict, Any, List, Optional
from decimal import Decimal
from bs4 import BeautifulSoup
from .base_parser import BaseParser, ParseResult


class ToastHtmlParserImpl(BaseParser):
    """Parser for Toast POS HTML Order Details export"""

    @property
    def code(self) -> str:
        return "TOAST_HTML"

    @property
    def name(self) -> str:
        return "Toast HTML Order Details"

    @property
    def extensions(self) -> list:
        return [".html", ".htm"]

    def parse(self, content: str, filename: str = "") -> ParseResult:
        """Parse Toast HTML order details export"""
        try:
            soup = BeautifulSoup(content, 'html.parser')

            # Find all order blocks
            order_blocks = soup.find_all('div', class_='order-border')

            if not order_blocks:
                return ParseResult.fail("No se encontraron ordenes en el archivo HTML")

            tickets = []
            total_net = Decimal('0')
            total_tax = Decimal('0')
            total_gross = Decimal('0')

            # Determine date range from orders
            min_date = None
            max_date = None

            for order_block in order_blocks:
                try:
                    ticket = self._parse_order(order_block)
                    if ticket:
                        tickets.append(ticket)

                        # Accumulate totals from amounts object
                        amounts = ticket.get('amounts', {})
                        total_net += Decimal(str(amounts.get('net_amount', 0)))
                        total_tax += Decimal(str(amounts.get('tax_amount', 0)))
                        total_gross += Decimal(str(amounts.get('gross_amount', 0)))

                        # Track date range
                        ticket_date = ticket.get('business_date')
                        if ticket_date:
                            if min_date is None or ticket_date < min_date:
                                min_date = ticket_date
                            if max_date is None or ticket_date > max_date:
                                max_date = ticket_date

                except Exception as e:
                    # Log but continue with other orders
                    print(f"Error parsing order: {e}")
                    continue

            if not tickets:
                return ParseResult.fail("No se pudieron parsear ordenes del archivo HTML")

            # Extract franchise info from filename
            franchise_code = self._extract_franchise_from_filename(filename)

            # Build batch structure
            business_date = min_date or datetime.now().strftime('%Y-%m-%d')

            data = {
                "schema_version": "1.0",
                "batch_header": {
                    "batch_id": f"TOAST_HTML_{franchise_code}_{business_date}_{datetime.now().strftime('%H%M%S')}",
                    "upload_type": "FULL_DAY",
                    "generated_at": datetime.now().isoformat(),
                    "business_date": business_date,
                    "source_system": {
                        "system_name": "TOAST_POS",
                        "system_version": "HTML_EXPORT"
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
                        "net_sales_amount": float(total_net),
                        "tax_amount": float(total_tax),
                        "gross_sales_amount": float(total_gross)
                    },
                    "amounts_include_tax": False  # US: prices don't include tax
                },
                "tickets": tickets
            }

            preview = self.generate_preview(data)
            return ParseResult.ok(data, preview)

        except Exception as e:
            return ParseResult.fail(f"Error parseando HTML: {str(e)}")

    def _parse_order(self, order_block) -> Optional[Dict[str, Any]]:
        """Parse a single order block"""

        # Get order number from h4 or div id
        order_id = None
        order_header = order_block.find('h4', id='order-summary-header')
        if order_header:
            order_text = order_header.get_text()
            match = re.search(r'Order #(\d+)', order_text)
            if match:
                order_id = match.group(1)

        if not order_id:
            # Try from div id
            div_id = order_block.get('id', '')
            match = re.search(r'order-number-(\d+)', div_id)
            if match:
                order_id = match.group(1)

        if not order_id:
            return None

        # Get Toast internal ID
        toast_id = None
        meta_div = order_block.find('div', class_='order-detail-meta-id')
        if meta_div:
            match = re.search(r'ID:\s*(\d+)', meta_div.get_text())
            if match:
                toast_id = match.group(1)

        # Parse check details
        check_details = self._parse_check_details(order_block)

        # Parse items
        items = self._parse_items(order_block)

        # Parse service charges
        service_charges = self._parse_service_charges(order_block)

        # Parse payments
        payments = self._parse_payments(order_block)

        # Calculate totals
        net_amount = sum(Decimal(str(item.get('net_amount', 0))) for item in items)
        tax_amount = sum(Decimal(str(item.get('tax_amount', 0))) for item in items)

        # Add service charge amounts
        for sc in service_charges:
            net_amount += Decimal(str(sc.get('amount', 0)))
            tax_amount += Decimal(str(sc.get('tax_amount', 0)))

        gross_amount = net_amount + tax_amount

        # Get business date and opened_at from check details
        raw_date = check_details.get('date')
        business_date = datetime.now().strftime('%Y-%m-%d')
        opened_at_iso = datetime.now().isoformat()

        if raw_date:
            try:
                # Parse date like "8/1/26, 12:43 PM" -> ISO 8601
                dt = datetime.strptime(raw_date, '%m/%d/%y, %I:%M %p')
                # Adjust year if needed (26 -> 2026)
                if dt.year < 100:
                    dt = dt.replace(year=dt.year + 2000)
                business_date = dt.strftime('%Y-%m-%d')
                opened_at_iso = dt.isoformat()
            except:
                try:
                    # Try just the date part
                    dt = datetime.strptime(raw_date.split(',')[0], '%m/%d/%y')
                    if dt.year < 100:
                        dt = dt.replace(year=dt.year + 2000)
                    business_date = dt.strftime('%Y-%m-%d')
                    opened_at_iso = dt.isoformat()
                except:
                    pass

        # Build ticket in expected format
        discount_amount = float(check_details.get('discount', 0))
        tip_amount = float(check_details.get('tip', 0))
        service_charge_amount = sum(float(sc.get('amount', 0)) for sc in service_charges)

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
                "gross_amount": float(gross_amount),
                "discount_amount": discount_amount,
                "net_amount": float(net_amount),
                "tax_amount": float(tax_amount),
                "service_charge_amount": service_charge_amount,
                "tip_amount": tip_amount,
                "total_paid_amount": float(gross_amount) + tip_amount
            },
            "items": items,
            "payment_methods": self._convert_payments_to_methods(payments)
        }

        return ticket

    def _parse_check_details(self, order_block) -> Dict[str, Any]:
        """Parse check header details"""
        details = {}

        # Find check section (row-fluid after Check #)
        check_rows = order_block.find_all('div', class_='row-fluid')

        for row in check_rows:
            text = row.get_text()

            # Time Opened / Server / Table
            if 'Time Opened' in text:
                spans = row.find_all('div', class_='span4')
                if spans:
                    values_text = spans[0].get_text(separator='|')
                    values = [v.strip() for v in values_text.split('|') if v.strip()]
                    if values:
                        details['date'] = values[0] if values else None
                        details['opened_at'] = values[0] if values else None
                        details['server'] = values[1] if len(values) > 1 else None
                        # Find table
                        if 'Table' in text:
                            for i, v in enumerate(values):
                                if re.match(r'^\d+$', v):
                                    details['table'] = v
                                    break

        # Parse summary values
        summary_div = order_block.find('div', id='order-summary')
        if summary_div:
            # Guests
            guests_input = summary_div.find('input', id='num-guests')
            if guests_input:
                try:
                    details['guests'] = int(guests_input.get('value', 1))
                except:
                    details['guests'] = 1

            # Revenue Center
            rc_div = summary_div.find('div', id='revenue-center-name')
            if rc_div:
                details['revenue_center'] = rc_div.get_text().strip()

        # Source
        source_divs = order_block.find_all('div', class_='summary-value-padded')
        for div in source_divs:
            text = div.get_text().strip()
            if text in ['In Store', 'Online Ordering', 'Third Party']:
                details['source'] = text
                break

        # Parse totals from check section
        spans = order_block.find_all('span', class_=re.compile(r'check-'))
        for span in spans:
            class_name = ' '.join(span.get('class', []))
            value_text = span.get_text().strip().replace('$', '').replace(',', '')
            try:
                value = float(value_text)
            except:
                value = 0

            if 'check-subtotal' in class_name:
                details['subtotal'] = value
            elif 'check-tax' in class_name:
                details['tax'] = value
            elif 'check-discounts' in class_name:
                details['discount'] = value

        # Parse tip from the totals section
        for div in order_block.find_all('div', class_='span1'):
            text = div.get_text()
            if '$' in text:
                values = re.findall(r'\$[\d,]+\.?\d*', text)
                if len(values) >= 3:  # TOTAL, Balance Due, Tip
                    try:
                        details['tip'] = float(values[2].replace('$', '').replace(',', ''))
                    except:
                        pass

        return details

    def _parse_items(self, order_block) -> List[Dict[str, Any]]:
        """Parse items from the order"""
        items = []

        # Find the items table
        items_table = order_block.find('table', id='order-details-item-table')
        if not items_table:
            # Try finding by class
            items_table = order_block.find('table', class_='order-details-table')

        if not items_table:
            return items

        tbody = items_table.find('tbody')
        if not tbody:
            return items

        rows = tbody.find_all('tr')

        for idx, row in enumerate(rows):
            cells = row.find_all('td')
            if len(cells) < 8:
                continue

            # Extract values
            menu_item = html.unescape(cells[0].get_text().strip())
            modifiers = html.unescape(cells[1].get_text().strip())

            def parse_money(text):
                text = text.strip().replace('$', '').replace(',', '')
                try:
                    return float(text)
                except:
                    return 0.0

            def parse_int(text):
                text = text.strip()
                try:
                    return int(float(text))
                except:
                    return 1

            price = parse_money(cells[2].get_text())
            qty = parse_int(cells[3].get_text())
            discount = parse_money(cells[4].get_text())
            net = parse_money(cells[5].get_text())
            tax = parse_money(cells[6].get_text())
            total = parse_money(cells[7].get_text())

            # Check if voided
            voided = False
            if len(cells) > 8:
                voided_text = cells[8].get_text().strip().lower()
                voided = voided_text == 'true'

            if voided:
                continue  # Skip voided items

            item = {
                "line_id": f"L{idx + 1}",
                "product_code": menu_item.upper().replace(' ', '_')[:20],
                "product_name": menu_item,
                "product_category": self._categorize_item(menu_item),
                "quantity": qty,
                "unit_price": price,
                "gross_amount": total,
                "discount_amount": discount,
                "net_amount": net,
                "tax_amount": tax,
                "notes": modifiers if modifiers else None
            }

            items.append(item)

        return items

    def _parse_service_charges(self, order_block) -> List[Dict[str, Any]]:
        """Parse service charges from the order"""
        charges = []

        # Find service charges table
        sc_table = order_block.find('table', id='order-details-service-charges-table')
        if not sc_table:
            return charges

        tbody = sc_table.find('tbody')
        if not tbody:
            return charges

        rows = tbody.find_all('tr')

        for idx, row in enumerate(rows):
            cells = row.find_all('td')
            if len(cells) < 4:
                continue

            name = cells[0].get_text().strip()
            is_gratuity = cells[1].get_text().strip().lower() == 'yes'

            def parse_money(text):
                text = text.strip().replace('$', '').replace(',', '')
                try:
                    return float(text)
                except:
                    return 0.0

            amount = parse_money(cells[2].get_text())
            tax = parse_money(cells[3].get_text())

            charge = {
                "charge_id": f"SC{idx + 1}",
                "name": name,
                "is_gratuity": is_gratuity,
                "amount": amount,
                "tax_amount": tax
            }

            charges.append(charge)

        return charges

    def _parse_payments(self, order_block) -> List[Dict[str, Any]]:
        """Parse payments from the order"""
        payments = []

        # Find payments table
        pay_table = order_block.find('table', id='order-details-payments-table')
        if not pay_table:
            return payments

        tbody = pay_table.find('tbody')
        if not tbody:
            return payments

        rows = tbody.find_all('tr')

        for idx, row in enumerate(rows):
            cells = row.find_all('td')
            if len(cells) < 6:
                continue

            payment_text = cells[0].get_text().strip()
            date_text = cells[1].get_text().strip()

            def parse_money(text):
                text = text.strip().replace('$', '').replace(',', '')
                try:
                    return float(text)
                except:
                    return 0.0

            amount = parse_money(cells[2].get_text())
            tip = parse_money(cells[3].get_text())
            gratuity = parse_money(cells[4].get_text())
            total = parse_money(cells[5].get_text())

            status = 'COMPLETED'
            if len(cells) > 7:
                status_text = cells[7].get_text().strip()
                if status_text:
                    status = status_text

            # Determine payment method
            payment_method = self._parse_payment_method(payment_text)

            payment = {
                "payment_id": f"P{idx + 1}",
                "payment_method": payment_method['method'],
                "card_brand": payment_method.get('brand'),
                "card_last_four": payment_method.get('last_four'),
                "amount": amount,
                "tip_amount": tip,
                "gratuity_amount": gratuity,
                "total_amount": total,
                "status": status,
                "payment_date": date_text
            }

            payments.append(payment)

        return payments

    def _parse_payment_method(self, text: str) -> Dict[str, Any]:
        """Parse payment method from text like 'CREDIT: Visa 9984'"""
        result = {'method': 'OTHER'}

        text_upper = text.upper()

        if 'CREDIT' in text_upper:
            result['method'] = 'CREDIT_CARD'
            # Try to extract card brand
            brands = ['VISA', 'MASTERCARD', 'AMEX', 'AMERICAN EXPRESS', 'DISCOVER']
            for brand in brands:
                if brand in text_upper:
                    result['brand'] = brand
                    break
            # Extract last 4 digits
            match = re.search(r'(\d{4})\s*$', text)
            if match:
                result['last_four'] = match.group(1)

        elif 'DEBIT' in text_upper:
            result['method'] = 'DEBIT_CARD'
            match = re.search(r'(\d{4})\s*$', text)
            if match:
                result['last_four'] = match.group(1)

        elif 'CASH' in text_upper:
            result['method'] = 'CASH'

        elif 'GIFT' in text_upper:
            result['method'] = 'GIFT_CARD'

        return result

    def _categorize_item(self, name: str) -> str:
        """Categorize item based on name"""
        name_lower = name.lower()

        # Beverages
        if any(w in name_lower for w in ['coke', 'sprite', 'fanta', 'agua', 'water', 'soda', 'juice', 'jugo']):
            return 'BEVERAGE'
        if any(w in name_lower for w in ['coffee', 'cafe', 'espresso', 'cappuccino', 'latte']):
            return 'COFFEE'
        if any(w in name_lower for w in ['wine', 'vino', 'malbec', 'cabernet', 'merlot', 'chardonnay']):
            return 'WINE'
        if any(w in name_lower for w in ['beer', 'cerveza', 'ipa', 'lager']):
            return 'BEVERAGE'
        if any(w in name_lower for w in ['cocktail', 'mojito', 'margarita', 'martini', 'whisky', 'gin']):
            return 'COCKTAIL'

        # Desserts
        if any(w in name_lower for w in ['flan', 'dulce', 'helado', 'ice cream', 'torta', 'cake', 'brownie', 'alfajor', 'postre', 'dessert']):
            return 'DESSERT'

        # Main courses (meats)
        if any(w in name_lower for w in ['bife', 'entra', 'chorizo', 'ojo de bife', 'ribeye', 'steak', 'lomo', 'asado', 'vacio', 'new york', 'skirt', 'costilla', 'mollejas']):
            return 'MAIN_COURSE'

        # Starters
        if any(w in name_lower for w in ['empanada', 'provoleta', 'chorizo', 'morcilla', 'ensalada', 'salad']):
            return 'STARTER'

        # Sides
        if any(w in name_lower for w in ['papas', 'fries', 'pure', 'vegetales', 'vegetables', 'side', 'warmies', 'calentitos']):
            return 'SIDE_DISH'

        # Kids
        if any(w in name_lower for w in ['nino', 'kids', 'infantil']):
            return 'MAIN_COURSE'

        return 'OTHER'

    def _extract_franchise_from_filename(self, filename: str) -> str:
        """Extract franchise code from filename"""
        # Expected format: toast_order_details_Sunny.html
        name_lower = filename.lower()

        if 'sunny' in name_lower:
            return 'MIAMI_SUNNY'
        elif 'midtown' in name_lower:
            return 'MIAMI_MIDTOWN'
        elif 'coconut' in name_lower or 'grove' in name_lower:
            return 'MIAMI_COCONUT'

        # Default
        return 'MIAMI_UNKNOWN'

    def _franchise_name_from_code(self, code: str) -> str:
        """Get franchise name from code"""
        mapping = {
            'MIAMI_SUNNY': 'Miami Sunny',
            'MIAMI_MIDTOWN': 'Miami Midtown',
            'MIAMI_COCONUT': 'Miami Coconut',
        }
        return mapping.get(code, code)

    def _convert_payments_to_methods(self, payments: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Convert internal payments to payment_methods format expected by backend"""
        methods = []
        for payment in payments:
            method = {
                "payment_method": payment.get('payment_method', 'OTHER'),
                "payment_brand": payment.get('card_brand'),
                "amount": payment.get('total_amount', 0)
            }
            methods.append(method)
        return methods
