"""
Excel de conciliación de una carga de Toast: lo que sale del Order Details (HTML, ya procesado por el
parser combinado) contra el Sales Summary (ZIP de CSV) del mismo rango.

Hojas:
- Resumen: Net sales, Gratuity, Tax, Tips, Total y órdenes/cubiertos, HTML vs Sales Summary.
- Por día: neto, órdenes y cubiertos por día de negocio contra "Sales by day".
- Órdenes: una fila por orden, con los importes que se cargan.
- Ítems: una fila por producto vendido, con su categoría.
Las diferencias se marcan en rojo.
"""
import csv
import io
import zipfile
from collections import defaultdict
from typing import Any, Dict, Optional

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

ROJO = PatternFill('solid', fgColor='F8CBAD')
VERDE = PatternFill('solid', fgColor='C6EFCE')
ENCABEZADO = PatternFill('solid', fgColor='1F3864')
MONEDA = '#,##0.00'
TOLERANCIA = 0.01


def _num(valor) -> float:
    try:
        return float(str(valor).replace(',', '') or 0)
    except ValueError:
        return 0.0


def leer_sales_summary(zip_content: Optional[bytes]) -> Dict[str, Any]:
    """Totales del Revenue summary y filas de Sales by day del ZIP de Toast."""
    resumen: Dict[str, Any] = {'totales': {}, 'por_dia': {}}
    if not zip_content:
        return resumen
    with zipfile.ZipFile(io.BytesIO(zip_content)) as zf:
        def filas(nombre):
            archivo = next((f for f in zf.namelist() if nombre in f.lower()), None)
            return list(csv.DictReader(io.StringIO(zf.read(archivo).decode('utf-8')))) if archivo else []

        for fila in filas('revenue summary'):
            resumen['totales'] = {
                'net_sales': _num(fila.get('Net sales')),
                'gratuity': _num(fila.get('Gratuity')),
                'tax': _num(fila.get('Tax amount')),
                'tips': _num(fila.get('Tips')),
                'total': _num(fila.get('Total')),
            }
        for fila in filas('sales by day'):
            dia = fila.get('yyyyMMdd', '')
            if len(dia) == 8:
                resumen['por_dia'][f'{dia[:4]}-{dia[4:6]}-{dia[6:]}'] = {
                    'net_sales': _num(fila.get('Net sales')),
                    'orders': int(_num(fila.get('Total orders'))),
                    'guests': int(_num(fila.get('Total guests'))),
                }
    return resumen


def _hoja(wb: Workbook, titulo: str, columnas, primera: bool = False):
    ws = wb.active if primera else wb.create_sheet()
    ws.title = titulo
    ws.append(columnas)
    for celda in ws[1]:
        celda.font = Font(bold=True, color='FFFFFF')
        celda.fill = ENCABEZADO
        celda.alignment = Alignment(horizontal='center', wrap_text=True)
    ws.freeze_panes = 'A2'
    return ws


def _anchos(ws, anchos):
    for i, ancho in enumerate(anchos, start=1):
        ws.column_dimensions[get_column_letter(i)].width = ancho


def armar_excel(data: Dict[str, Any], csv_zip_content: Optional[bytes]) -> bytes:
    tickets = data.get('tickets', [])
    toast = leer_sales_summary(csv_zip_content)
    header = data.get('batch_header', {})

    def suma(campo):
        return round(sum(t['amounts'].get(campo, 0) or 0 for t in tickets), 2)

    html = {
        'net_sales': suma('net_amount'),
        'gratuity': suma('service_charge_amount'),
        'tax': suma('tax_amount'),
        'tips': suma('tip_amount'),
        'total': suma('total_paid_amount'),
    }

    wb = Workbook()

    # Resumen
    ws = _hoja(wb, 'Resumen', ['Concepto', 'Order Details (HTML)', 'Sales Summary (Toast)', 'Diferencia', 'Estado'], primera=True)
    etiquetas = [('net_sales', 'Net sales'), ('gratuity', 'Gratuity (servicio)'), ('tax', 'Tax amount'),
                 ('tips', 'Tips'), ('total', 'Total')]
    for clave, etiqueta in etiquetas:
        declarado = toast['totales'].get(clave)
        _fila_control(ws, etiqueta, html[clave], declarado, MONEDA)
    _fila_control(ws, 'Órdenes', len(tickets),
                  sum(d['orders'] for d in toast['por_dia'].values()) if toast['por_dia'] else None, '0')
    _fila_control(ws, 'Cubiertos', sum(t.get('covers') or 0 for t in tickets),
                  sum(d['guests'] for d in toast['por_dia'].values()) if toast['por_dia'] else None, '0')
    ws.append([])
    ws.append(['Local', header.get('franchise', {}).get('franchise_name')])
    rango = header.get('date_range', {})
    ws.append(['Período (días de negocio)', f"{rango.get('from')} a {rango.get('to')}"])
    ws.append(['Venta neta que muestra la plataforma', 'Net sales (sin impuestos, servicio ni propina)'])
    _anchos(ws, [38, 22, 22, 16, 12])

    # Por día
    ws = _hoja(wb, 'Por día', ['Día', 'Net sales HTML', 'Net sales Toast', 'Dif. neto',
                               'Órdenes HTML', 'Órdenes Toast', 'Cubiertos HTML', 'Cubiertos Toast', 'Estado'])
    por_dia = defaultdict(lambda: {'net': 0.0, 'orders': 0, 'guests': 0})
    for t in tickets:
        d = por_dia[t['business_date']]
        d['net'] += t['amounts'].get('net_amount', 0) or 0
        d['orders'] += 1
        d['guests'] += t.get('covers') or 0
    for dia in sorted(set(por_dia) | set(toast['por_dia'])):
        h = por_dia.get(dia, {'net': 0.0, 'orders': 0, 'guests': 0})
        c = toast['por_dia'].get(dia)
        if c:
            ok = abs(h['net'] - c['net_sales']) <= TOLERANCIA and h['orders'] == c['orders'] and h['guests'] == c['guests']
            ws.append([dia, round(h['net'], 2), c['net_sales'], round(h['net'] - c['net_sales'], 2),
                       h['orders'], c['orders'], h['guests'], c['guests'], 'OK' if ok else 'REVISAR'])
        else:
            ok = False
            ws.append([dia, round(h['net'], 2), None, None, h['orders'], None, h['guests'], None, 'SIN DATO TOAST'])
        fila = ws._current_row
        for col in (2, 3, 4):
            ws.cell(fila, col).number_format = MONEDA
        ws.cell(fila, 9).fill = VERDE if ok else ROJO
    _anchos(ws, [12, 16, 16, 12, 13, 13, 15, 15, 16])

    # Órdenes
    ws = _hoja(wb, 'Órdenes', ['Día de negocio', 'Apertura', 'Orden', 'ID Toast', 'Salón', 'Mesa', 'Mozo',
                               'Cubiertos', 'Net sales', 'Descuento', 'Gratuity', 'Tax', 'Tips', 'Total', 'Ítems'])
    for t in sorted(tickets, key=lambda x: (x['business_date'], x.get('opened_at') or '')):
        a = t['amounts']
        ws.append([t['business_date'], t.get('opened_at'), t.get('ticket_number'), t.get('external_order_id'),
                   t.get('meal_period'), (t.get('table') or {}).get('table_number'),
                   (t.get('waiter') or {}).get('waiter_name'), t.get('covers'),
                   a.get('net_amount'), a.get('discount_amount'), a.get('service_charge_amount'),
                   a.get('tax_amount'), a.get('tip_amount'), a.get('total_paid_amount'), len(t.get('items', []))])
        fila = ws._current_row
        for col in range(9, 15):
            ws.cell(fila, col).number_format = MONEDA
    _anchos(ws, [13, 18, 8, 22, 14, 7, 20, 10, 12, 11, 11, 10, 10, 12, 7])

    # Ítems
    ws = _hoja(wb, 'Ítems', ['Día de negocio', 'Orden', 'Producto', 'Categoría', 'Modificadores',
                             'Cantidad', 'Precio unitario', 'Descuento', 'Neto', 'Impuesto'])
    for t in sorted(tickets, key=lambda x: (x['business_date'], x.get('opened_at') or '')):
        for i in t.get('items', []):
            ws.append([t['business_date'], t.get('ticket_number'), i.get('product_name'), i.get('product_category'),
                       i.get('modifiers'), i.get('quantity'), i.get('unit_price'), i.get('discount_amount'),
                       i.get('net_amount'), i.get('tax_amount')])
            fila = ws._current_row
            for col in (7, 8, 9, 10):
                ws.cell(fila, col).number_format = MONEDA
    _anchos(ws, [13, 8, 40, 14, 30, 9, 14, 11, 11, 10])

    salida = io.BytesIO()
    wb.save(salida)
    return salida.getvalue()


def _fila_control(ws, etiqueta, calculado, declarado, formato):
    if declarado is None:
        ws.append([etiqueta, calculado, None, None, 'SIN DATO TOAST'])
        ok = False
    else:
        diferencia = round(calculado - declarado, 2)
        ok = abs(diferencia) <= TOLERANCIA
        ws.append([etiqueta, calculado, declarado, diferencia, 'OK' if ok else 'REVISAR'])
    fila = ws._current_row
    for col in (2, 3, 4):
        ws.cell(fila, col).number_format = formato
    ws.cell(fila, 5).fill = VERDE if ok else ROJO
