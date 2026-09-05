"""
Colombia JSON Parser
Transforma archivos JSON de franquicias Colombia, ajustando discount_amount
para que sea consistente con gross_amount y net_amount (ambos incluyen IVA).

En Colombia, el POS registra:
- gross_amount: precio con IVA incluido
- discount_amount: descuento SIN IVA (base gravable)
- net_amount: monto final con IVA incluido

Esto causa que gross_amount - discount_amount != net_amount

Este parser recalcula:
  discount_amount = gross_amount - net_amount
para que la ecuacion sea consistente.
"""
import json
from typing import Dict, Any
from .base_parser import BaseParser, ParseResult


class ColombiaJsonParserImpl(BaseParser):
    """Parser para archivos JSON de Colombia con ajuste de descuentos"""

    @property
    def code(self) -> str:
        return "COLOMBIA_JSON"

    @property
    def name(self) -> str:
        return "Colombia JSON Parser (ajuste descuentos IVA)"

    @property
    def extensions(self) -> list:
        return [".json"]

    def parse(self, content: str, filename: str = "") -> ParseResult:
        """
        Parsea JSON de Colombia y ajusta discount_amount.
        """
        try:
            data = json.loads(content)
        except json.JSONDecodeError as e:
            return ParseResult.fail(f"JSON invalido: {str(e)}")

        # Validar estructura basica
        if "schema_version" not in data:
            return ParseResult.fail("Falta schema_version en el JSON")

        if "batch_header" not in data:
            return ParseResult.fail("Falta batch_header en el JSON")

        if "tickets" not in data:
            return ParseResult.fail("Falta tickets en el JSON")

        # Procesar y transformar
        try:
            transformed = self._transform_data(data)
            preview = self.generate_preview(transformed)
            return ParseResult.ok(transformed, preview)
        except Exception as e:
            return ParseResult.fail(f"Error transformando datos: {str(e)}")

    def _transform_data(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Transforma los datos ajustando discount_amount en items y tickets.
        """
        # Acumuladores para recalcular control_totals
        total_gross = 0.0
        total_discount = 0.0
        total_net = 0.0
        total_tax = 0.0
        total_items = 0

        tickets = data.get("tickets", [])

        for ticket in tickets:
            # Procesar items del ticket
            items = ticket.get("items", [])
            ticket_gross = 0.0
            ticket_discount = 0.0
            ticket_net = 0.0
            item_count = 0

            for item in items:
                # Solo procesar items no anulados para totales
                is_voided = item.get("is_voided", False)

                gross = float(item.get("gross_amount", 0))
                net = float(item.get("net_amount", 0))

                # Recalcular discount_amount
                discount_original = float(item.get("discount_amount", 0))
                discount_corrected = gross - net

                # Solo corregir si hay diferencia significativa (> 0.01)
                if abs(discount_original - discount_corrected) > 0.01:
                    item["discount_amount"] = round(discount_corrected, 2)
                    item["_discount_original"] = discount_original  # Guardar original para auditoria

                if not is_voided:
                    ticket_gross += gross
                    ticket_net += net
                    ticket_discount += item.get("discount_amount", 0)
                    item_count += 1

            # Actualizar amounts del ticket
            if "amounts" in ticket:
                amounts = ticket["amounts"]
                ticket_gross_header = float(amounts.get("gross_amount", 0))
                ticket_net_header = float(amounts.get("net_amount", 0))
                discount_original = float(amounts.get("discount_amount", 0))
                discount_corrected = ticket_gross_header - ticket_net_header

                if abs(discount_original - discount_corrected) > 0.01:
                    amounts["discount_amount"] = round(discount_corrected, 2)
                    amounts["_discount_original"] = discount_original

                # Acumular para control_totals
                total_gross += ticket_gross_header
                total_net += ticket_net_header
                total_discount += amounts.get("discount_amount", 0)
                total_tax += float(amounts.get("tax_amount", 0))

            total_items += item_count

        # Actualizar control_totals en batch_header
        if "batch_header" in data and "control_totals" in data["batch_header"]:
            control = data["batch_header"]["control_totals"]

            # Recalcular discount_amount en control_totals
            ct_gross = float(control.get("gross_sales_amount", 0))
            ct_net = float(control.get("net_sales_amount", 0))
            ct_discount_original = float(control.get("discount_amount", 0))
            ct_discount_corrected = ct_gross - ct_net

            if abs(ct_discount_original - ct_discount_corrected) > 0.01:
                control["discount_amount"] = round(ct_discount_corrected, 2)
                control["_discount_original"] = ct_discount_original

        return data

    def generate_preview(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Genera preview del archivo transformado"""
        header = data.get('batch_header', {})
        control = header.get('control_totals', {})
        franchise = header.get('franchise', {})

        # Mostrar si hubo ajustes
        had_adjustments = "_discount_original" in control

        return {
            "franchise_code": franchise.get('franchise_code'),
            "franchise_name": franchise.get('franchise_name'),
            "business_date": header.get('business_date'),
            "ticket_count": control.get('ticket_count', 0),
            "net_sales_amount": control.get('net_sales_amount', 0),
            "tax_amount": control.get('tax_amount', 0),
            "currency": franchise.get('currency', 'COP'),
            "discount_adjusted": had_adjustments,
            "discount_original": control.get('_discount_original') if had_adjustments else None,
            "discount_corrected": control.get('discount_amount') if had_adjustments else None
        }
