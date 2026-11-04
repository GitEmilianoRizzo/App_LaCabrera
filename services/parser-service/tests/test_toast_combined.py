"""
Pruebas del parser combinado de Toast (HTML de órdenes + ZIP del Sales Summary).

La orden de ejemplo tiene dos cuentas (checks) con servicio, propina y descuento, para verificar que:
- se leen los ítems, servicios y pagos de todas las cuentas, no solo de la primera;
- net_amount es el "Net sales" de Toast y net_amount_excl_tax = neto + servicio
  (la plataforma resta el servicio para mostrar la venta neta);
- el total cobrado cierra como en el Sales Summary: Net sales + Gratuity + Tax + Tips;
- los importes nunca se escalan para forzar el total del CSV.
"""
import io
import zipfile

from parsers.toast_combined_parser import ToastCombinedParserImpl as ToastCombinedParser
from parsers.toast_categorias import categorizar


def fila_item(nombre, precio, cant, desc, neto, imp):
    celdas = [nombre, '', f'${precio}', str(cant), f'${desc}', f'${neto}', f'${imp}', '', 'false']
    return '<tr>' + ''.join(f'<td>{c}</td>' for c in celdas) + '</tr>'


def cuenta(items, servicio, pago):
    sc_neto, sc_imp = servicio
    monto, propina, gratuity, total = pago
    return f"""
      <table id="order-details-item-table"><tbody>{''.join(fila_item(*i) for i in items)}</tbody></table>
      <table id="order-details-service-charges-table"><tbody>
        <tr><td>Service Charge (18.00%)</td><td>Yes</td><td>${sc_neto}</td><td>${sc_imp}</td></tr>
      </tbody></table>
      <table id="order-details-payments-table"><tbody>
        <tr><td>Credit</td><td></td><td>${monto}</td><td>${propina}</td><td>${gratuity}</td><td>${total}</td><td>$0.00</td><td>CAPTURED</td></tr>
        <tr><td>Credit</td><td></td><td>${monto}</td><td>$5.00</td><td>${gratuity}</td><td>${total}</td><td>$0.00</td><td>DENIED</td></tr>
      </tbody></table>"""


HTML = f"""
<html><body>
<div class="order-border" id="order-number-101">
  <h4 id="order-summary-header">Order #101</h4>
  <div class="order-detail-meta-id">ID: 800000000000000101</div>
  <div class="row-fluid">Time Opened<div class="span4">10/10/26, 8:15 PM|Server 7</div></div>
  <div id="order-summary"><input id="num-guests" value="4"/><div id="revenue-center-name">Dining Room</div></div>
  {cuenta([('ENTRAÑA / OUTSIDE SKIRT', '68.00', 1, '0.00', '68.00', '5.44'),
           ('Papas Fritas / French Fries', '8.50', 1, '0.00', '8.50', '0.68')],
          ('13.77', '1.10'), ('97.49', '10.00', '13.77', '107.49'))}
  {cuenta([('FILET MIGNON OZ MENU', '70.00', 1, '7.00', '63.00', '5.04'),
           ('San Pellegrino', '8.00', 2, '0.00', '16.00', '1.28')],
          ('14.22', '1.14'), ('100.68', '0.00', '14.22', '100.68'))}
</div>
</body></html>
"""

# Totales de la orden: neto 155.50, servicio 27.99, impuestos 12.44 + 2.24, propina 10.00
NETO, SERVICIO, IMPUESTOS, PROPINA = 155.50, 27.99, 14.68, 10.00


def zip_sales_summary(net_sales=NETO, ordenes=1):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w') as zf:
        zf.writestr('Net sales summary.csv', f'Gross sales,Sales discounts,Net sales\n{net_sales + 7},-7.00,{net_sales}\n')
        zf.writestr('Revenue summary.csv',
                    f'Net sales,Gratuity,Tax amount,Tips,Total\n{net_sales},{SERVICIO},{IMPUESTOS},{PROPINA},'
                    f'{net_sales + SERVICIO + IMPUESTOS + PROPINA}\n')
        zf.writestr('Sales by day.csv', f'yyyyMMdd,Net sales,Total orders,Total guests\n20261010,{net_sales},{ordenes},4\n')
    return buffer.getvalue()


def parsear(**kwargs):
    resultado = ToastCombinedParser().parse(HTML, 'toast_order_details_Sunny.html', zip_sales_summary(**kwargs))
    assert resultado.success, resultado.error
    return resultado.data


def test_lee_todas_las_cuentas_de_la_orden():
    ticket = parsear()['tickets'][0]
    assert [i['product_name'] for i in ticket['items']] == [
        'ENTRAÑA / OUTSIDE SKIRT', 'Papas Fritas / French Fries', 'FILET MIGNON OZ MENU', 'San Pellegrino']
    assert len(ticket['service_charges']) == 2
    # Los pagos rechazados (DENIED) no cuentan, ni su propina
    assert len(ticket['payment_methods']) == 2


def test_importes_cierran_como_el_sales_summary():
    a = parsear()['tickets'][0]['amounts']
    assert a['net_amount'] == NETO
    assert a['service_charge_amount'] == SERVICIO
    assert round(a['net_amount_excl_tax'] - a['service_charge_amount'], 2) == NETO
    assert round(a['tax_amount'], 2) == IMPUESTOS
    assert a['tip_amount'] == PROPINA
    assert a['discount_amount'] == 7.0
    assert round(a['total_paid_amount'], 2) == round(NETO + SERVICIO + IMPUESTOS + PROPINA, 2)


def test_sin_diferencias_contra_el_csv_queda_validado():
    data = parsear()
    validacion = data['batch_header']['validation']
    assert validacion['status'] == 'VALIDATED', validacion
    assert data['batch_header']['control_totals']['service_charge_amount'] == SERVICIO


def test_si_no_cierra_contra_el_csv_informa_y_no_toca_los_importes():
    data = parsear(net_sales=170.00, ordenes=3)
    validacion = data['batch_header']['validation']
    assert validacion['status'] == 'MISMATCH'
    assert any(d['metric'] == 'net_sales' for d in validacion['differences'])
    assert 'orders_warning' in validacion
    assert data['tickets'][0]['amounts']['net_amount'] == NETO


def test_categorias():
    assert categorizar('ENTRAÑA / OUTSIDE SKIRT') == 'MAIN_COURSE'
    assert categorizar('FILET MIGNON OZ MENU') == 'MAIN_COURSE'
    assert categorizar('VACIO 12 OZ LUNCH') == 'MAIN_COURSE'
    assert categorizar('Papas Fritas / French Fries') == 'SIDE_DISH'
    assert categorizar('San Pellegrino') == 'BEVERAGE'
    assert categorizar('Catena Classic MB') == 'WINE'
    assert categorizar('Chorizo') == 'STARTER'


def test_orden_despues_de_medianoche_es_del_dia_anterior():
    html = HTML.replace('10/10/26, 8:15 PM', '10/11/26, 12:18 AM')
    data = ToastCombinedParser().parse(html, 'toast_order_details_Sunny.html', zip_sales_summary()).data
    assert data['tickets'][0]['business_date'] == '2026-10-10'
    assert data['tickets'][0]['opened_at'] == '2026-10-11T00:18:00'
