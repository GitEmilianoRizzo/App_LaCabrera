"""
Toast POS Parser
Convierte reportes de Toast (copiados desde la web) al formato JSON de ingesta de La Cabrera.
"""
import re
from datetime import datetime
from typing import List, Dict, Any, Optional
from dataclasses import dataclass, field
from decimal import Decimal

from .base_parser import BaseParser, ParseResult


# ============================================================================
# Data Classes
# ============================================================================

@dataclass
class ItemLine:
    line_id: str
    product_code: str
    product_name: str
    product_category: str = "OTHER"
    product_family: Optional[str] = None
    modifiers: Optional[str] = None
    quantity: Decimal = Decimal("1")
    unit_price: Decimal = Decimal("0")
    gross_amount: Decimal = Decimal("0")
    discount_amount: Decimal = Decimal("0")
    net_amount: Decimal = Decimal("0")
    tax_amount: Decimal = Decimal("0")
    tax_rate: Optional[Decimal] = None
    is_voided: bool = False
    void_reason: Optional[str] = None


@dataclass
class ServiceCharge:
    name: str
    is_gratuity: bool
    amount: Decimal
    tax_amount: Decimal


@dataclass
class Payment:
    payment_method: str
    payment_brand: Optional[str]
    date: str
    amount: Decimal
    tip: Decimal
    gratuity: Decimal
    total: Decimal
    status: str


@dataclass
class TicketAmounts:
    gross_amount: Decimal = Decimal("0")
    discount_amount: Decimal = Decimal("0")
    net_amount: Decimal = Decimal("0")
    tax_amount: Decimal = Decimal("0")
    service_charge_amount: Decimal = Decimal("0")
    tip_amount: Decimal = Decimal("0")
    total_paid_amount: Decimal = Decimal("0")


@dataclass
class Order:
    order_id: str
    order_number: str
    check_id: str
    check_number: str
    status: str
    opened_at: str
    closed_at: Optional[str]
    business_date: str
    guests: int
    table_number: Optional[str]
    server_id: Optional[str]
    server_name: Optional[str]
    shift_info: Optional[str]
    revenue_center: Optional[str]
    source: str
    amounts: TicketAmounts = field(default_factory=TicketAmounts)
    items: List[ItemLine] = field(default_factory=list)
    service_charges: List[ServiceCharge] = field(default_factory=list)
    payments: List[Payment] = field(default_factory=list)


# ============================================================================
# Toast Parser Implementation
# ============================================================================

class ToastParserImpl(BaseParser):
    """Parser for Toast POS copy-pasted reports"""

    @property
    def code(self) -> str:
        return "TOAST_PARSER"

    @property
    def name(self) -> str:
        return "Toast POS Parser"

    @property
    def extensions(self) -> list:
        return [".txt"]

    def __init__(self):
        self.orders: List[Order] = []
        self.franchise_name: str = ""
        self.date_range: str = ""

    def parse(self, content: str, filename: str = "") -> ParseResult:
        """Parse Toast report content"""
        try:
            # Reset state
            self.orders = []
            self.franchise_name = ""
            self.date_range = ""

            self.extract_franchise_info(content)
            blocks = self.find_order_blocks(content)

            for i, block in enumerate(blocks):
                order = self.parse_order_block(block, i + 1)
                if order:
                    self.orders.append(order)

            if not self.orders:
                return ParseResult.fail("No orders found in file")

            data = self.to_ingestion_json()
            preview = self.generate_preview(data)

            return ParseResult.ok(data=data, preview=preview)

        except Exception as e:
            return ParseResult.fail(f"Parse error: {str(e)}")

    def parse_money(self, value: str) -> Decimal:
        """Parse money string like '$1,234.56' to Decimal"""
        if not value:
            return Decimal("0")
        cleaned = re.sub(r'[,$]', '', value.strip())
        try:
            return Decimal(cleaned) if cleaned else Decimal("0")
        except:
            return Decimal("0")

    def parse_date(self, date_str: str) -> tuple:
        """Parse date like '8/1/26, 11:48 AM' and return (datetime_str, business_date)"""
        if not date_str:
            return None, None
        try:
            dt = datetime.strptime(date_str.strip(), "%m/%d/%y, %I:%M %p")
            if dt.year < 100:
                dt = dt.replace(year=dt.year + 2000)
            iso_str = dt.isoformat()
            business_date = dt.strftime("%Y-%m-%d")
            return iso_str, business_date
        except:
            return date_str, None

    def clean_string(self, value: str) -> str:
        """Clean string by removing newlines and extra whitespace"""
        if not value:
            return ""
        cleaned = value.replace('\n', ' ').replace('\r', ' ')
        cleaned = re.sub(r'\s+', ' ', cleaned)
        return cleaned.strip()

    def extract_franchise_info(self, content: str):
        """Extract franchise name and date range from header"""
        match = re.search(r'(La Cabrera\s+(?:Midtown|Coconut\s*Grove|Brickell|Sunny\s*Isles))', content[:2000], re.IGNORECASE)
        if match:
            self.franchise_name = self.clean_string(match.group(1))
        else:
            match = re.search(r'(La Cabrera\s+\w+(?:\s+\w+)?)', content[:2000])
            if match:
                self.franchise_name = self.clean_string(match.group(1))

        match = re.search(r'From\s+(\d+/\d+/\d+)\s+to\s+(\d+/\d+/\d+)', content)
        if match:
            self.date_range = f"{match.group(1)} to {match.group(2)}"

    def find_order_blocks(self, content: str) -> List[str]:
        """Split content into individual order blocks"""
        pattern = r'(Order\s+#\d+\s*\(\s*(?:Closed|Open|Void)\s*\))'
        parts = re.split(pattern, content)

        blocks = []
        i = 1
        while i < len(parts):
            if i + 1 < len(parts):
                block = parts[i] + parts[i + 1]
                blocks.append(block)
            i += 2

        return blocks

    def parse_order_block(self, block: str, order_index: int) -> Optional[Order]:
        """Parse a single order block"""
        lines = block.split('\n')

        order_match = re.match(r'Order\s+#(\d+)\s*\(\s*(\w+)\s*\)', lines[0])
        if not order_match:
            return None

        order_number = order_match.group(1)
        status = order_match.group(2)

        order = Order(
            order_id="",
            order_number=order_number,
            check_id="",
            check_number=order_number,
            status=status.upper(),
            opened_at="",
            closed_at=None,
            business_date="",
            guests=1,
            table_number=None,
            server_id=None,
            server_name=None,
            shift_info=None,
            revenue_center=None,
            source="In Store"
        )

        content = '\n'.join(lines)

        # Order ID
        id_match = re.search(r'ID:\s*(\d+)', content)
        if id_match:
            order.order_id = id_match.group(1)

        # Check ID
        check_ids = re.findall(r'ID:\s*(\d+)', content)
        if len(check_ids) >= 2:
            order.check_id = check_ids[1]
        elif check_ids:
            order.check_id = check_ids[0]

        # Guests
        guests_match = re.search(r'Guests:\s*(\d+)', content)
        if guests_match:
            order.guests = int(guests_match.group(1))

        # Source
        source_match = re.search(r'Source:\s*(.+?)(?:\n|$)', content)
        if source_match:
            order.source = source_match.group(1).strip()

        # Revenue Center
        rc_match = re.search(r'Revenue Center:\s*(.+?)(?:\n|Source|$)', content)
        if rc_match:
            order.revenue_center = rc_match.group(1).strip()
            if order.revenue_center == "None":
                order.revenue_center = None

        # Time
        time_match = re.search(r'(\d{1,2}/\d{1,2}/\d{2},\s*\d{1,2}:\d{2}\s*[AP]M)', content)
        if time_match:
            opened_at, business_date = self.parse_date(time_match.group(1))
            order.opened_at = opened_at or ""
            order.business_date = business_date or ""
            if status == "Closed":
                order.closed_at = opened_at

        # Server
        server_match = re.search(r'\d{1,2}:\d{2}\s*[AP]M\s*\n\s*([A-Za-z][\w\s]+?)(?:\n|$)', content)
        if server_match:
            order.server_name = server_match.group(1).strip()
            order.server_id = order.server_name.replace(" ", "_").lower()

        # Table
        table_match = re.search(r'(?:Table:\s*|PM\)\s*\n\s*)(\d+)\s*\n', content)
        if table_match:
            order.table_number = table_match.group(1)
        else:
            shift_end = content.find('PM)')
            if shift_end > 0:
                after_shift = content[shift_end+3:shift_end+50]
                num_match = re.search(r'^\s*(\d+)\s*$', after_shift, re.MULTILINE)
                if num_match:
                    order.table_number = num_match.group(1)

        # Shift info
        shift_match = re.search(r'\((\d{1,2}/\d{1,2}/\d{2},\s*\d{1,2}:\d{2}\s*[AP]M\s*-\s*\d{1,2}:\d{2}\s*[AP]M)\)', content)
        if shift_match:
            order.shift_info = shift_match.group(1)

        # Amounts
        amounts_match = re.search(
            r'Discounts:\s*\n'
            r'Credits:\s*\n'
            r'Subtotal:\s*\n'
            r'Tax:\$?([\d,]+\.?\d*)\s*\n'
            r'\$?([\d,]+\.?\d*)\s*\n'
            r'\$?([\d,]+\.?\d*)\s*\n'
            r'\$?([\d,]+\.?\d*)TOTAL:\s*\n'
            r'Balance Due:\s*\n'
            r'Tip:\$?([\d,]+\.?\d*)\s*\n'
            r'\$?([\d,]+\.?\d*)\s*\n'
            r'\$?([\d,]+\.?\d*)',
            content
        )
        if amounts_match:
            order.amounts.discount_amount = self.parse_money(amounts_match.group(1))
            order.amounts.net_amount = self.parse_money(amounts_match.group(3))
            order.amounts.tax_amount = self.parse_money(amounts_match.group(4))
            order.amounts.total_paid_amount = self.parse_money(amounts_match.group(5))
            order.amounts.tip_amount = self.parse_money(amounts_match.group(7))

        order.amounts.gross_amount = order.amounts.net_amount + order.amounts.discount_amount

        # Parse Items
        items_section = self.extract_section(content, "Items", ["Service Charges", "Payments"])
        if items_section:
            order.items = self.parse_items(items_section, order_index)

        # Parse Service Charges
        sc_section = self.extract_section(content, "Service Charges", ["Payments"])
        if sc_section:
            order.service_charges = self.parse_service_charges(sc_section)
            order.amounts.service_charge_amount = sum(sc.amount for sc in order.service_charges)

        # Parse Payments
        payments_section = self.extract_section(content, "Payments", ["Order #"])
        if payments_section:
            order.payments = self.parse_payments(payments_section)

        return order

    def extract_section(self, content: str, start_marker: str, end_markers: List[str]) -> Optional[str]:
        """Extract a section between markers"""
        start_pattern = rf'^{start_marker}\s*$'
        start_match = re.search(start_pattern, content, re.MULTILINE)
        if not start_match:
            return None

        start_pos = start_match.end()
        end_pos = len(content)

        for marker in end_markers:
            pattern = rf'^{marker}'
            match = re.search(pattern, content[start_pos:], re.MULTILINE)
            if match:
                end_pos = min(end_pos, start_pos + match.start())

        return content[start_pos:end_pos].strip()

    def parse_items(self, section: str, order_index: int) -> List[ItemLine]:
        """Parse items from the Items section"""
        items = []
        lines = section.split('\n')

        header_found = False
        line_num = 0

        for line in lines:
            line = line.strip()
            if not line:
                continue

            if 'Menu Item' in line and 'Price' in line:
                header_found = True
                continue

            if not header_found:
                continue

            parts = line.split('\t')
            if len(parts) < 8:
                parts = re.split(r'\s{2,}', line)

            if len(parts) < 8:
                continue

            line_num += 1

            try:
                product_name = parts[0].strip()
                modifiers = parts[1].strip() if len(parts) > 1 else None
                unit_price = self.parse_money(parts[2]) if len(parts) > 2 else Decimal("0")
                quantity = Decimal(parts[3].strip()) if len(parts) > 3 and parts[3].strip() else Decimal("1")
                discount = self.parse_money(parts[4]) if len(parts) > 4 else Decimal("0")
                net_amount = self.parse_money(parts[5]) if len(parts) > 5 else Decimal("0")
                tax_amount = self.parse_money(parts[6]) if len(parts) > 6 else Decimal("0")
                is_voided = parts[8].strip().lower() == 'true' if len(parts) > 8 else False
                void_reason = parts[9].strip() if len(parts) > 9 and parts[9].strip() else None

                gross_amount = net_amount + discount

                tax_rate = None
                if net_amount > 0 and tax_amount > 0:
                    tax_rate = (tax_amount / net_amount).quantize(Decimal("0.0001"))

                item = ItemLine(
                    line_id=f"{order_index}_{line_num}",
                    product_code=self.generate_product_code(product_name),
                    product_name=product_name,
                    product_category="OTHER",
                    modifiers=modifiers if modifiers else None,
                    quantity=quantity,
                    unit_price=unit_price,
                    gross_amount=gross_amount,
                    discount_amount=discount,
                    net_amount=net_amount,
                    tax_amount=tax_amount,
                    tax_rate=tax_rate,
                    is_voided=is_voided,
                    void_reason=void_reason
                )
                items.append(item)
            except Exception:
                continue

        return items

    def generate_product_code(self, name: str) -> str:
        """Generate a product code from the name"""
        code = re.sub(r'[^a-zA-Z0-9]', '_', name.upper())
        code = re.sub(r'_+', '_', code).strip('_')
        return code[:20]

    def parse_service_charges(self, section: str) -> List[ServiceCharge]:
        """Parse service charges section"""
        charges = []
        lines = section.split('\n')

        header_found = False
        for line in lines:
            line = line.strip()
            if not line:
                continue

            if 'Name' in line and 'Amount' in line:
                header_found = True
                continue

            if not header_found:
                continue

            parts = re.split(r'\t+', line)
            if len(parts) < 4:
                parts = re.split(r'\s{2,}', line)

            if len(parts) >= 4:
                try:
                    name = parts[0].strip()
                    is_gratuity = parts[1].strip().lower() == 'yes'
                    amount = self.parse_money(parts[2])
                    tax = self.parse_money(parts[3])

                    charges.append(ServiceCharge(
                        name=name,
                        is_gratuity=is_gratuity,
                        amount=amount,
                        tax_amount=tax
                    ))
                except:
                    continue

        return charges

    def parse_payments(self, section: str) -> List[Payment]:
        """Parse payments section"""
        payments = []
        lines = section.split('\n')

        header_found = False
        for line in lines:
            line = line.strip()
            if not line:
                continue

            if 'Payment' in line and 'Date' in line and 'Amount' in line:
                header_found = True
                continue

            if not header_found:
                continue

            parts = re.split(r'\t+', line)
            if len(parts) < 7:
                parts = re.split(r'\s{2,}', line)

            if len(parts) >= 7:
                try:
                    payment_str = parts[0].strip()
                    pm_match = re.match(r'(\w+):\s*(\w+)(?:\s+(\d+))?', payment_str)
                    if pm_match:
                        method = pm_match.group(1)
                        brand = pm_match.group(2)
                    else:
                        method = payment_str
                        brand = None

                    date = parts[1].strip()
                    amount = self.parse_money(parts[2])
                    tip = self.parse_money(parts[3])
                    gratuity = self.parse_money(parts[4])
                    total = self.parse_money(parts[5])
                    status = parts[7].strip() if len(parts) > 7 else "CAPTURED"

                    payments.append(Payment(
                        payment_method=method,
                        payment_brand=brand,
                        date=date,
                        amount=amount,
                        tip=tip,
                        gratuity=gratuity,
                        total=total,
                        status=status
                    ))
                except:
                    continue

        return payments

    def to_ingestion_json(self) -> Dict[str, Any]:
        """Convert parsed orders to ingestion JSON format"""
        if not self.orders:
            return {}

        business_date = self.orders[0].business_date if self.orders else datetime.now().strftime("%Y-%m-%d")

        clean_name = re.sub(r'[^a-zA-Z0-9_]', '_', self.franchise_name.replace(' ', '_'))
        clean_name = re.sub(r'_+', '_', clean_name).strip('_')
        batch_id = f"TOAST_{clean_name}_{business_date}_{datetime.now().strftime('%H%M%S')}"

        total_tickets = len(self.orders)
        total_items = sum(len(o.items) for o in self.orders)
        gross_sales = sum(o.amounts.gross_amount for o in self.orders)
        discount_amount = sum(o.amounts.discount_amount for o in self.orders)
        net_sales = sum(o.amounts.net_amount for o in self.orders)
        tax_amount = sum(o.amounts.tax_amount for o in self.orders)
        service_charges = sum(o.amounts.service_charge_amount for o in self.orders)
        tips = sum(o.amounts.tip_amount for o in self.orders)
        covers = sum(o.guests for o in self.orders)
        voided_count = len([o for o in self.orders if o.status == "VOID"])

        tickets = []
        for order in self.orders:
            ticket = {
                "ticket_id": order.order_id or order.order_number,
                "ticket_number": order.order_number,
                "external_order_id": order.check_id,
                "status": order.status,
                "opened_at": order.opened_at,
                "closed_at": order.closed_at,
                "business_date": order.business_date,
                "meal_period": self.determine_meal_period(order.opened_at),
                "table": {
                    "table_number": order.table_number,
                    "table_area": None
                } if order.table_number else None,
                "waiter": {
                    "waiter_id": order.server_id,
                    "waiter_name": order.server_name
                } if order.server_name else None,
                "covers": order.guests,
                "currency": "USD",
                "amounts": {
                    "gross_amount": float(order.amounts.gross_amount),
                    "discount_amount": float(order.amounts.discount_amount),
                    "net_amount": float(order.amounts.net_amount),
                    "net_amount_excl_tax": float(order.amounts.net_amount),
                    "tax_amount": float(order.amounts.tax_amount),
                    "service_charge_amount": float(order.amounts.service_charge_amount),
                    "tip_amount": float(order.amounts.tip_amount),
                    "total_paid_amount": float(order.amounts.total_paid_amount)
                },
                "payment_methods": [
                    {
                        "payment_method": p.payment_method,
                        "payment_brand": p.payment_brand,
                        "amount": float(p.total)
                    }
                    for p in order.payments
                ],
                "items": [
                    {
                        "line_id": item.line_id,
                        "product_code": item.product_code,
                        "product_name": item.product_name,
                        "product_category": item.product_category,
                        "product_family": item.modifiers,
                        "quantity": float(item.quantity),
                        "unit_price": float(item.unit_price),
                        "gross_amount": float(item.gross_amount),
                        "discount_amount": float(item.discount_amount),
                        "net_amount": float(item.net_amount),
                        "net_amount_excl_tax": float(item.net_amount),
                        "tax_amount": float(item.tax_amount),
                        "tax_rate": float(item.tax_rate) if item.tax_rate else None,
                        "is_voided": item.is_voided,
                        "void_reason": item.void_reason
                    }
                    for item in order.items
                ]
            }
            tickets.append(ticket)

        result = {
            "schema_version": "1.2",
            "batch_header": {
                "batch_id": batch_id,
                "business_date": business_date,
                "generated_at": datetime.utcnow().isoformat() + "Z",
                "source_system": {
                    "system_name": "Toast POS",
                    "system_version": "Web Export",
                    "exported_by": "toast_parser.py"
                },
                "franchise": {
                    "franchise_code": self.generate_franchise_code(),
                    "franchise_name": self.franchise_name,
                    "country": "US",
                    "city": "Miami",
                    "timezone": "America/New_York",
                    "currency": "USD"
                },
                "upload_type": "FULL_DAY",
                "amounts_include_tax": False,
                "control_totals": {
                    "ticket_count": total_tickets,
                    "item_line_count": total_items,
                    "gross_sales_amount": float(gross_sales),
                    "discount_amount": float(discount_amount),
                    "net_sales_amount": float(net_sales),
                    "tax_amount": float(tax_amount),
                    "service_charge_amount": float(service_charges),
                    "tip_amount": float(tips),
                    "covers_total": covers,
                    "cancelled_ticket_count": voided_count
                }
            },
            "tickets": tickets
        }

        return result

    def generate_franchise_code(self) -> str:
        """Generate franchise code from name"""
        name_lower = self.franchise_name.lower()
        if "midtown" in name_lower:
            return "MIAMI_MIDTOWN"
        elif "coconut" in name_lower:
            return "MIAMI_COCONUT"
        elif "brickell" in name_lower:
            return "MIAMI_BRICKELL"
        elif "sunny" in name_lower:
            return "MIAMI_SUNNY"
        else:
            code = re.sub(r'[^a-zA-Z0-9]', '_', self.franchise_name.upper())
            return code[:30]

    def determine_meal_period(self, opened_at: str) -> str:
        """Determine meal period from opening time"""
        if not opened_at:
            return "DINNER"
        try:
            dt = datetime.fromisoformat(opened_at.replace('Z', ''))
            hour = dt.hour
            if hour < 11:
                return "BREAKFAST"
            elif hour < 16:
                return "LUNCH"
            else:
                return "DINNER"
        except:
            return "DINNER"
