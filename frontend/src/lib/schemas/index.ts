/**
 * Schemas predefinidos de sistemas origen y destino
 * para usar en el configurador de mapeo de campos
 */

import type { SourceSchema, TargetSchema, TransformationType } from '@/types/conexiones'

// ============================================
// SCHEMA ORIGEN: AGORA POS v8.7.2
// ============================================
export const AGORA_SCHEMA: SourceSchema = {
  name: 'AGORA_POS',
  version: '8.7.2',
  entities: [
    {
      name: 'Invoice',
      path: 'Invoices[*]',
      fields: [
        { name: 'Serie', path: 'Serie', type: 'string', description: 'Serie de factura', example: 'F' },
        { name: 'Number', path: 'Number', type: 'number', description: 'Numero de factura', example: 123 },
        { name: 'GlobalId', path: 'GlobalId', type: 'string', description: 'ID unico global (GUID)', example: 'f990d051-538e-4438-8406-7170f6e60820' },
        { name: 'BusinessDay', path: 'BusinessDay', type: 'date', description: 'Fecha de negocio (YYYY-MM-DD)', example: '2024-07-14' },
        { name: 'Date', path: 'Date', type: 'datetime', description: 'Fecha y hora de creacion', example: '2024-07-14T21:30:00' },
        { name: 'DocumentType', path: 'DocumentType', type: 'string', description: 'Tipo de documento', example: 'StandardInvoice' },
        { name: 'VatIncluded', path: 'VatIncluded', type: 'boolean', description: 'Impuestos incluidos', example: true },
        { name: 'PrintCount', path: 'PrintCount', type: 'number', description: 'Veces impresa', example: 1 },
      ]
    },
    {
      name: 'Invoice.Workplace',
      path: 'Invoices[*].Workplace',
      fields: [
        { name: 'Id', path: 'Workplace.Id', type: 'number', description: 'ID del local', example: 1 },
        { name: 'Name', path: 'Workplace.Name', type: 'string', description: 'Nombre del local', example: 'Local Principal' },
      ]
    },
    {
      name: 'Invoice.Pos',
      path: 'Invoices[*].Pos',
      fields: [
        { name: 'Id', path: 'Pos.Id', type: 'number', description: 'ID del TPV', example: 1 },
        { name: 'Name', path: 'Pos.Name', type: 'string', description: 'Nombre del TPV', example: 'TPV Barra' },
      ]
    },
    {
      name: 'Invoice.User',
      path: 'Invoices[*].User',
      fields: [
        { name: 'Id', path: 'User.Id', type: 'number', description: 'ID del usuario/mozo', example: 5 },
        { name: 'Name', path: 'User.Name', type: 'string', description: 'Nombre del usuario', example: 'Carlos' },
      ]
    },
    {
      name: 'Invoice.Customer',
      path: 'Invoices[*].Customer',
      fields: [
        { name: 'Id', path: 'Customer.Id', type: 'number', description: 'ID del cliente', example: 2 },
        { name: 'FiscalName', path: 'Customer.FiscalName', type: 'string', description: 'Nombre fiscal', example: 'Sol y Sombra, S.L.' },
        { name: 'Cif', path: 'Customer.Cif', type: 'string', description: 'CIF/NIF', example: 'B0018912' },
      ]
    },
    {
      name: 'InvoiceItem',
      path: 'Invoices[*].InvoiceItems[*]',
      fields: [
        { name: 'ContentType', path: 'InvoiceItems[*].ContentType', type: 'string', description: 'T=Ticket, D=Albaran', example: 'T' },
        { name: 'BusinessDay', path: 'InvoiceItems[*].BusinessDay', type: 'date', description: 'Fecha de negocio', example: '2024-07-14' },
        { name: 'Guests', path: 'InvoiceItems[*].Guests', type: 'number', description: 'Numero de comensales', example: 2 },
        { name: 'Date', path: 'InvoiceItems[*].Date', type: 'datetime', description: 'Fecha/hora del item', example: '2024-07-14T20:10:30' },
        { name: 'GlobalId', path: 'InvoiceItems[*].GlobalId', type: 'string', description: 'ID global del ticket', example: 'f990d051-538e-4438-8406-7170f6e60820' },
      ]
    },
    {
      name: 'InvoiceItem.SaleCenter',
      path: 'Invoices[*].InvoiceItems[*].SaleCenter',
      fields: [
        { name: 'Id', path: 'SaleCenter.Id', type: 'number', description: 'ID centro de venta', example: 1 },
        { name: 'Name', path: 'SaleCenter.Name', type: 'string', description: 'Nombre centro', example: 'Barra' },
        { name: 'Location', path: 'SaleCenter.Location', type: 'string', description: 'Ubicacion/Mesa', example: 'B3' },
      ]
    },
    {
      name: 'Line',
      path: 'Invoices[*].InvoiceItems[*].Lines[*]',
      fields: [
        { name: 'Index', path: 'Lines[*].Index', type: 'number', description: 'Indice de linea (0-based)', example: 0 },
        { name: 'Type', path: 'Lines[*].Type', type: 'string', description: 'Standard, MenuHeader, MenuItem', example: 'Standard' },
        { name: 'CreationDate', path: 'Lines[*].CreationDate', type: 'datetime', description: 'Fecha de creacion', example: '2024-07-14T20:08:00' },
        { name: 'ProductId', path: 'Lines[*].ProductId', type: 'number', description: 'ID del producto', example: 16 },
        { name: 'ProductName', path: 'Lines[*].ProductName', type: 'string', description: 'Nombre del producto', example: 'Hamburguesa doble' },
        { name: 'SaleFormatId', path: 'Lines[*].SaleFormatId', type: 'number', description: 'ID formato de venta', example: 16 },
        { name: 'SaleFormatName', path: 'Lines[*].SaleFormatName', type: 'string', description: 'Nombre formato', example: 'Hamburguesa doble' },
        { name: 'ProductPrice', path: 'Lines[*].ProductPrice', type: 'number', description: 'Precio unitario producto (sin addins)', example: 3.25 },
        { name: 'Quantity', path: 'Lines[*].Quantity', type: 'number', description: 'Cantidad', example: 1 },
        { name: 'UnitPrice', path: 'Lines[*].UnitPrice', type: 'number', description: 'Precio unitario (con addins)', example: 4.25 },
        { name: 'DiscountRate', path: 'Lines[*].DiscountRate', type: 'number', description: 'Descuento en tanto por uno', example: 0.1 },
        { name: 'CashDiscount', path: 'Lines[*].CashDiscount', type: 'number', description: 'Descuento en moneda', example: 0 },
        { name: 'TotalAmount', path: 'Lines[*].TotalAmount', type: 'number', description: 'Importe total linea', example: 4.25 },
        { name: 'VatId', path: 'Lines[*].VatId', type: 'number', description: 'ID del impuesto', example: 3 },
        { name: 'VatRate', path: 'Lines[*].VatRate', type: 'number', description: 'Tasa de impuesto', example: 0.1 },
        { name: 'FamilyId', path: 'Lines[*].FamilyId', type: 'number', description: 'ID de familia', example: 1 },
        { name: 'FamilyName', path: 'Lines[*].FamilyName', type: 'string', description: 'Nombre de familia', example: 'Hamburguesas' },
        { name: 'PLU', path: 'Lines[*].PLU', type: 'string', description: 'Codigo PLU', example: '598' },
        { name: 'Notes', path: 'Lines[*].Notes', type: 'string', description: 'Notas de preparacion', example: 'Muy hecha' },
        { name: 'UserId', path: 'Lines[*].UserId', type: 'number', description: 'ID usuario que agrego', example: 1 },
      ]
    },
    {
      name: 'Line.Addin',
      path: 'Invoices[*].InvoiceItems[*].Lines[*].Addins[*]',
      fields: [
        { name: 'ProductId', path: 'Addins[*].ProductId', type: 'number', description: 'ID del addin', example: 18 },
        { name: 'ProductName', path: 'Addins[*].ProductName', type: 'string', description: 'Nombre del addin', example: 'Bacon' },
        { name: 'ProductPrice', path: 'Addins[*].ProductPrice', type: 'number', description: 'Precio del addin', example: 0.5 },
      ]
    },
    {
      name: 'Payment',
      path: 'Invoices[*].Payments[*]',
      fields: [
        { name: 'MethodId', path: 'Payments[*].MethodId', type: 'number', description: 'ID forma de pago', example: 2 },
        { name: 'MethodName', path: 'Payments[*].MethodName', type: 'string', description: 'Nombre forma de pago', example: 'Tarjeta' },
        { name: 'Amount', path: 'Payments[*].Amount', type: 'number', description: 'Importe a pagar', example: 13.85 },
        { name: 'PaidAmount', path: 'Payments[*].PaidAmount', type: 'number', description: 'Importe pagado', example: 13.85 },
        { name: 'ChangeAmount', path: 'Payments[*].ChangeAmount', type: 'number', description: 'Cambio', example: 0 },
        { name: 'Tip', path: 'Payments[*].Tip', type: 'number', description: 'Propina', example: 2.0 },
        { name: 'IsPrepayment', path: 'Payments[*].IsPrepayment', type: 'boolean', description: 'Es prepago', example: false },
        { name: 'ExtraInformation', path: 'Payments[*].ExtraInformation', type: 'string', description: 'Info extra (tipo tarjeta)', example: 'Tipo de Tarjeta: MASTERVISA' },
      ]
    },
    {
      name: 'Totals',
      path: 'Invoices[*].Totals',
      fields: [
        { name: 'GrossAmount', path: 'Totals.GrossAmount', type: 'number', description: 'Importe CON impuestos, DESPUES de descuentos', example: 13.25 },
        { name: 'NetAmount', path: 'Totals.NetAmount', type: 'number', description: 'Importe neto (sin impuestos)', example: 11.63 },
        { name: 'VatAmount', path: 'Totals.VatAmount', type: 'number', description: 'Cuota de impuesto', example: 1.62 },
        { name: 'SurchargeAmount', path: 'Totals.SurchargeAmount', type: 'number', description: 'Recargo de equivalencia', example: 0 },
      ]
    },
    {
      name: 'InvoiceItem.Discounts',
      path: 'Invoices[*].InvoiceItems[*].Discounts',
      fields: [
        { name: 'DiscountRate', path: 'Discounts.DiscountRate', type: 'number', description: 'Descuento en tanto por uno', example: 0.05 },
        { name: 'CashDiscount', path: 'Discounts.CashDiscount', type: 'number', description: 'Descuento en moneda', example: 0 },
      ]
    },
  ]
}

// ============================================
// SCHEMA ORIGEN: VINSON CLOUD v2.0
// ============================================
export const VINSON_SCHEMA: SourceSchema = {
  name: 'VINSON_CLOUD',
  version: '2.0',
  entities: [
    {
      name: 'Transaction',
      path: 'transactions[*]',
      fields: [
        { name: 'idTransaction', path: 'idTransaction', type: 'number', description: 'ID unico de transaccion', example: 12345 },
        { name: 'transactionNumber', path: 'transactionNumber', type: 'string', description: 'Numero de transaccion', example: 'TRX-001' },
        { name: 'transactionDate', path: 'transactionDate', type: 'date', description: 'Fecha de transaccion', example: '2024-08-07' },
        { name: 'openDate', path: 'openDate', type: 'datetime', description: 'Fecha/hora apertura', example: '2024-08-07T19:30:00' },
        { name: 'closeDate', path: 'closeDate', type: 'datetime', description: 'Fecha/hora cierre', example: '2024-08-07T20:45:00' },
        { name: 'startHour', path: 'startHour', type: 'string', description: 'Hora inicio (HH:mm:ss)', example: '19:30:00' },
        { name: 'endHour', path: 'endHour', type: 'string', description: 'Hora fin (HH:mm:ss)', example: '20:45:00' },
        { name: 'status', path: 'status', type: 'number', description: 'Estado (0=open, 1=closed, 3=cancelled)', example: 1 },
        { name: 'totalAmount', path: 'totalAmount', type: 'number', description: 'Monto total con IVA', example: 15000.50 },
        { name: 'netAmount', path: 'netAmount', type: 'number', description: 'Monto neto sin IVA', example: 12397.11 },
        { name: 'taxNetAmount', path: 'taxNetAmount', type: 'number', description: 'Monto de impuestos', example: 2603.39 },
        { name: 'empInicio', path: 'empInicio', type: 'number', description: 'ID empleado que inicio', example: 5 },
        { name: 'nombreMozo', path: 'nombreMozo', type: 'string', description: 'Nombre del mozo', example: 'Juan Perez' },
        { name: 'cantidadPersonas', path: 'cantidadPersonas', type: 'number', description: 'Cantidad de comensales', example: 4 },
        { name: 'mesa', path: 'mesa', type: 'string', description: 'Numero/nombre de mesa', example: 'Mesa 12' },
        { name: 'sector', path: 'sector', type: 'string', description: 'Sector/area', example: 'Salon Principal' },
      ]
    },
    {
      name: 'Product',
      path: 'transactions[*].products[*]',
      fields: [
        { name: 'productID', path: 'products[*].productID', type: 'number', description: 'ID del producto', example: 101 },
        { name: 'productName', path: 'products[*].productName', type: 'string', description: 'Nombre del producto', example: 'Bife de Chorizo' },
        { name: 'quantity', path: 'products[*].quantity', type: 'number', description: 'Cantidad', example: 2 },
        { name: 'price', path: 'products[*].price', type: 'number', description: 'Precio unitario', example: 5500.00 },
        { name: 'totalLine', path: 'products[*].totalLine', type: 'number', description: 'Total de linea', example: 11000.00 },
        { name: 'group', path: 'products[*].group', type: 'string', description: 'Grupo del producto', example: 'ALIMENTOS' },
        { name: 'category', path: 'products[*].category', type: 'string', description: 'Categoria del producto', example: 'CABRERA Comidas' },
        { name: 'familyId', path: 'products[*].familyId', type: 'number', description: 'ID de familia', example: 10 },
        { name: 'canceledItem', path: 'products[*].canceledItem', type: 'string', description: 'Anulado (True/False)', example: 'False' },
        { name: 'notes', path: 'products[*].notes', type: 'string', description: 'Notas del producto', example: 'A punto' },
      ]
    },
    {
      name: 'Payment',
      path: 'transactions[*].paymentModes[*]',
      fields: [
        { name: 'paymentType', path: 'paymentModes[*].paymentType', type: 'string', description: 'Tipo de pago', example: 'Visa' },
        { name: 'amount', path: 'paymentModes[*].amount', type: 'number', description: 'Monto pagado', example: 15000.50 },
        { name: 'paymentId', path: 'paymentModes[*].paymentId', type: 'number', description: 'ID del metodo de pago', example: 3 },
      ]
    },
  ]
}

// ============================================
// SCHEMA DESTINO: LA CABRERA v1.0
// ============================================
export const LACABRERA_SCHEMA: TargetSchema = {
  name: 'LA_CABRERA_DAILY_BATCH',
  version: '1.0',
  entities: [
    {
      name: 'BATCH_HEADER',
      fields: [
        { name: 'batch_id', type: 'string', required: true, description: 'ID unico del batch' },
        { name: 'business_date', type: 'date', required: true, description: 'Fecha de negocio (YYYY-MM-DD)' },
        { name: 'generated_at', type: 'datetime', required: true, description: 'Timestamp ISO 8601' },
        { name: 'upload_type', type: 'string', required: false, description: 'Tipo de carga', enum_values: ['FULL_DAY', 'INCREMENTAL', 'CORRECTION'] },
      ]
    },
    {
      name: 'TICKET',
      fields: [
        { name: 'ticket_id', type: 'string', required: true, description: 'ID unico del ticket' },
        { name: 'ticket_number', type: 'string', required: true, description: 'Numero visible del ticket' },
        { name: 'external_order_id', type: 'string', required: false, description: 'ID de pedido externo' },
        { name: 'status', type: 'string', required: true, description: 'Estado del ticket', enum_values: ['OPEN', 'CLOSED', 'CANCELLED', 'VOIDED', 'REFUNDED'] },
        { name: 'opened_at', type: 'datetime', required: true, description: 'Fecha/hora apertura' },
        { name: 'closed_at', type: 'datetime', required: true, description: 'Fecha/hora cierre' },
        { name: 'business_date', type: 'date', required: true, description: 'Fecha de negocio' },
        { name: 'meal_period', type: 'string', required: false, description: 'Periodo de comida', enum_values: ['BREAKFAST', 'LUNCH', 'TEA', 'DINNER', 'LATE_NIGHT', 'UNKNOWN'] },
        { name: 'table.table_number', type: 'string', required: false, description: 'Numero de mesa' },
        { name: 'table.table_area', type: 'string', required: false, description: 'Area de la mesa' },
        { name: 'waiter.waiter_id', type: 'string', required: false, description: 'ID del mozo' },
        { name: 'waiter.waiter_name', type: 'string', required: false, description: 'Nombre del mozo' },
        { name: 'covers', type: 'number', required: false, description: 'Cantidad de cubiertos' },
        { name: 'currency', type: 'string', required: true, description: 'Codigo ISO moneda' },
        { name: 'amounts.gross_amount', type: 'number', required: true, description: 'Total ANTES de descuentos' },
        { name: 'amounts.discount_amount', type: 'number', required: true, description: 'Total descuentos' },
        { name: 'amounts.net_amount', type: 'number', required: true, description: 'Total despues de descuentos' },
        { name: 'amounts.tax_amount', type: 'number', required: false, description: 'Total impuestos' },
        { name: 'amounts.tip_amount', type: 'number', required: false, description: 'Total propinas' },
        { name: 'amounts.total_paid_amount', type: 'number', required: false, description: 'Total pagado' },
      ]
    },
    {
      name: 'ITEM',
      fields: [
        { name: 'line_id', type: 'string', required: true, description: 'ID unico de linea' },
        { name: 'product_code', type: 'string', required: true, description: 'Codigo del producto' },
        { name: 'product_name', type: 'string', required: true, description: 'Nombre del producto' },
        { name: 'product_category', type: 'string', required: true, description: 'Categoria', enum_values: ['STARTER', 'MAIN_COURSE', 'SIDE_DISH', 'DESSERT', 'COFFEE', 'BEVERAGE', 'WINE', 'COCKTAIL', 'OTHER'] },
        { name: 'product_family', type: 'string', required: false, description: 'Familia' },
        { name: 'product_subfamily', type: 'string', required: false, description: 'Subfamilia' },
        { name: 'quantity', type: 'number', required: true, description: 'Cantidad' },
        { name: 'unit_price', type: 'number', required: true, description: 'Precio unitario' },
        { name: 'gross_amount', type: 'number', required: true, description: 'Importe bruto' },
        { name: 'discount_amount', type: 'number', required: true, description: 'Descuento en linea' },
        { name: 'net_amount', type: 'number', required: true, description: 'Importe neto' },
        { name: 'tax_amount', type: 'number', required: false, description: 'Impuesto' },
        { name: 'is_discounted', type: 'boolean', required: false, description: 'Tiene descuento' },
        { name: 'is_voided', type: 'boolean', required: false, description: 'Esta anulado' },
        { name: 'void_reason', type: 'string', required: false, description: 'Motivo anulacion' },
        { name: 'ordered_at', type: 'datetime', required: false, description: 'Fecha/hora pedido' },
        { name: 'served_at', type: 'datetime', required: false, description: 'Fecha/hora servido' },
        { name: 'notes', type: 'string', required: false, description: 'Notas especiales' },
      ]
    },
    {
      name: 'PAYMENT',
      fields: [
        { name: 'payment_method', type: 'string', required: true, description: 'Metodo de pago', enum_values: ['CASH', 'DEBIT_CARD', 'CREDIT_CARD', 'BANK_TRANSFER', 'QR', 'MERCADO_PAGO', 'DELIVERY_APP', 'OTHER'] },
        { name: 'payment_brand', type: 'string', required: false, description: 'Marca (VISA, MC, etc)' },
        { name: 'amount', type: 'number', required: true, description: 'Importe' },
      ]
    },
    {
      name: 'DISCOUNT',
      fields: [
        { name: 'discount_code', type: 'string', required: false, description: 'Codigo descuento' },
        { name: 'discount_name', type: 'string', required: false, description: 'Nombre descuento' },
        { name: 'discount_type', type: 'string', required: true, description: 'Tipo', enum_values: ['PERCENTAGE', 'FIXED_AMOUNT', 'PROMO', 'OTHER'] },
        { name: 'discount_value', type: 'number', required: true, description: 'Valor (% o monto)' },
        { name: 'discount_amount', type: 'number', required: true, description: 'Monto aplicado' },
      ]
    },
  ]
}

// ============================================
// MAPEO DEFAULT AGORA -> LA CABRERA
// ============================================
export const DEFAULT_AGORA_MAPPINGS = [
  // TICKET
  { target_entity: 'TICKET', target_field: 'ticket_id', source_path: 'Invoice.GlobalId', required: true },
  { target_entity: 'TICKET', target_field: 'ticket_number', source_path: 'Invoice.Serie + Invoice.Number', transformation: { type: 'CONCAT', params: { fields: ['Serie', 'Number'], separator: '-' } }, required: true },
  { target_entity: 'TICKET', target_field: 'status', source_path: 'Invoice.DocumentType', transformation: { type: 'MAP_VALUE', params: { mapping: { 'StandardInvoice': 'CLOSED', 'BasicInvoice': 'CLOSED', 'StandardRefund': 'REFUNDED', 'BasicRefund': 'REFUNDED' }, default: 'CLOSED' } }, required: true },
  { target_entity: 'TICKET', target_field: 'opened_at', source_path: 'InvoiceItems[0].Date', transformation: { type: 'ADD_TIMEZONE', params: { timezone: 'Europe/Madrid' } }, required: true },
  { target_entity: 'TICKET', target_field: 'closed_at', source_path: 'Invoice.Date', transformation: { type: 'ADD_TIMEZONE', params: { timezone: 'Europe/Madrid' } }, required: true },
  { target_entity: 'TICKET', target_field: 'business_date', source_path: 'Invoice.BusinessDay', required: true },
  { target_entity: 'TICKET', target_field: 'table.table_number', source_path: 'InvoiceItems[0].SaleCenter.Location', required: false },
  { target_entity: 'TICKET', target_field: 'table.table_area', source_path: 'InvoiceItems[0].SaleCenter.Name', required: false },
  { target_entity: 'TICKET', target_field: 'waiter.waiter_id', source_path: 'Invoice.User.Id', transformation: { type: 'CONCAT', params: { prefix: 'USR-' } }, required: false },
  { target_entity: 'TICKET', target_field: 'waiter.waiter_name', source_path: 'Invoice.User.Name', required: false },
  { target_entity: 'TICKET', target_field: 'covers', source_path: 'InvoiceItems[0].Guests', required: false },
  { target_entity: 'TICKET', target_field: 'amounts.gross_amount', source_path: 'Invoice.Totals.GrossAmount', description: 'ATENCION: En Agora es CON IVA y DESPUES de descuentos', required: true },
  { target_entity: 'TICKET', target_field: 'amounts.net_amount', source_path: 'Invoice.Totals.GrossAmount', required: true },
  { target_entity: 'TICKET', target_field: 'amounts.discount_amount', source_path: 'InvoiceItems[0].Discounts.CashDiscount', default_value: 0, required: true },
  { target_entity: 'TICKET', target_field: 'amounts.tax_amount', source_path: 'Invoice.Totals.VatAmount', required: false },

  // ITEM
  { target_entity: 'ITEM', target_field: 'line_id', source_path: 'Lines[*].Index', transformation: { type: 'CONCAT', params: { prefix: 'L-' } }, required: true },
  { target_entity: 'ITEM', target_field: 'product_code', source_path: 'Lines[*].ProductId', required: true },
  { target_entity: 'ITEM', target_field: 'product_name', source_path: 'Lines[*].ProductName', required: true },
  { target_entity: 'ITEM', target_field: 'product_family', source_path: 'Lines[*].FamilyName', required: false },
  { target_entity: 'ITEM', target_field: 'quantity', source_path: 'Lines[*].Quantity', required: true },
  { target_entity: 'ITEM', target_field: 'unit_price', source_path: 'Lines[*].UnitPrice', required: true },
  { target_entity: 'ITEM', target_field: 'gross_amount', source_path: 'Lines[*].TotalAmount', transformation: { type: 'CALCULATE', params: { formula: 'Quantity * UnitPrice' } }, required: true },
  { target_entity: 'ITEM', target_field: 'discount_amount', source_path: 'Lines[*].CashDiscount', default_value: 0, required: true },
  { target_entity: 'ITEM', target_field: 'net_amount', source_path: 'Lines[*].TotalAmount', required: true },
  { target_entity: 'ITEM', target_field: 'notes', source_path: 'Lines[*].Notes', required: false },
  { target_entity: 'ITEM', target_field: 'ordered_at', source_path: 'Lines[*].CreationDate', transformation: { type: 'ADD_TIMEZONE', params: { timezone: 'Europe/Madrid' } }, required: false },

  // PAYMENT
  { target_entity: 'PAYMENT', target_field: 'amount', source_path: 'Payments[*].Amount', required: true },
  { target_entity: 'PAYMENT', target_field: 'payment_brand', source_path: 'Payments[*].ExtraInformation', transformation: { type: 'REGEX_EXTRACT', params: { pattern: 'Tipo de Tarjeta: (\\w+)', group: 1 } }, required: false },
]

// ============================================
// MAPEO DEFAULT VINSON -> LA CABRERA
// ============================================
export const DEFAULT_VINSON_MAPPINGS = [
  // TICKET
  { target_entity: 'TICKET', target_field: 'ticket_id', source_path: 'Transaction.idTransaction', required: true },
  { target_entity: 'TICKET', target_field: 'ticket_number', source_path: 'Transaction.transactionNumber', required: true },
  { target_entity: 'TICKET', target_field: 'status', source_path: 'Transaction.status', transformation: { type: 'MAP_VALUE', params: { mapping: { '0': 'OPEN', '1': 'CLOSED', '2': 'CLOSED', '3': 'CANCELLED', '4': 'CANCELLED', '5': 'VOIDED' }, default: 'CLOSED' } }, required: true },
  { target_entity: 'TICKET', target_field: 'opened_at', source_path: 'Transaction.openDate', transformation: { type: 'ADD_TIMEZONE', params: { timezone: 'America/Argentina/Buenos_Aires' } }, required: true },
  { target_entity: 'TICKET', target_field: 'closed_at', source_path: 'Transaction.closeDate', transformation: { type: 'ADD_TIMEZONE', params: { timezone: 'America/Argentina/Buenos_Aires' } }, required: true },
  { target_entity: 'TICKET', target_field: 'business_date', source_path: 'Transaction.transactionDate', required: true },
  { target_entity: 'TICKET', target_field: 'table.table_number', source_path: 'Transaction.mesa', required: false },
  { target_entity: 'TICKET', target_field: 'table.table_area', source_path: 'Transaction.sector', required: false },
  { target_entity: 'TICKET', target_field: 'waiter.waiter_id', source_path: 'Transaction.empInicio', transformation: { type: 'CONCAT', params: { prefix: 'EMP-' } }, required: false },
  { target_entity: 'TICKET', target_field: 'waiter.waiter_name', source_path: 'Transaction.nombreMozo', required: false },
  { target_entity: 'TICKET', target_field: 'covers', source_path: 'Transaction.cantidadPersonas', required: false },
  { target_entity: 'TICKET', target_field: 'amounts.gross_amount', source_path: 'Transaction.totalAmount', description: 'Monto total CON IVA', required: true },
  { target_entity: 'TICKET', target_field: 'amounts.net_amount', source_path: 'Transaction.totalAmount', required: true },
  { target_entity: 'TICKET', target_field: 'amounts.discount_amount', default_value: 0, required: true },
  { target_entity: 'TICKET', target_field: 'amounts.tax_amount', source_path: 'Transaction.taxNetAmount', required: false },

  // ITEM
  { target_entity: 'ITEM', target_field: 'line_id', source_path: 'Product.productID', transformation: { type: 'CONCAT', params: { prefix: 'P-' } }, required: true },
  { target_entity: 'ITEM', target_field: 'product_code', source_path: 'Product.productID', required: true },
  { target_entity: 'ITEM', target_field: 'product_name', source_path: 'Product.productName', required: true },
  { target_entity: 'ITEM', target_field: 'product_family', source_path: 'Product.category', description: 'Usa la categoria de Vinson como familia', required: false },
  { target_entity: 'ITEM', target_field: 'quantity', source_path: 'Product.quantity', required: true },
  { target_entity: 'ITEM', target_field: 'unit_price', source_path: 'Product.price', required: true },
  { target_entity: 'ITEM', target_field: 'gross_amount', source_path: 'Product.totalLine', required: true },
  { target_entity: 'ITEM', target_field: 'discount_amount', default_value: 0, required: true },
  { target_entity: 'ITEM', target_field: 'net_amount', source_path: 'Product.totalLine', required: true },
  { target_entity: 'ITEM', target_field: 'is_voided', source_path: 'Product.canceledItem', transformation: { type: 'MAP_VALUE', params: { mapping: { 'True': true, 'False': false }, default: false } }, required: false },
  { target_entity: 'ITEM', target_field: 'notes', source_path: 'Product.notes', required: false },

  // PAYMENT
  { target_entity: 'PAYMENT', target_field: 'amount', source_path: 'Payment.amount', required: true },
]

// ============================================
// TRANSFORMACIONES DISPONIBLES
// ============================================
export const TRANSFORMATIONS: { type: TransformationType; label: string; description: string; hasParams: boolean }[] = [
  { type: 'NONE', label: 'Sin transformacion', description: 'Usar el valor tal cual viene', hasParams: false },
  { type: 'UPPERCASE', label: 'Mayusculas', description: 'Convertir a MAYUSCULAS', hasParams: false },
  { type: 'LOWERCASE', label: 'Minusculas', description: 'Convertir a minusculas', hasParams: false },
  { type: 'TRIM', label: 'Quitar espacios', description: 'Eliminar espacios al inicio y final', hasParams: false },
  { type: 'CONCAT', label: 'Concatenar', description: 'Unir varios campos con un separador', hasParams: true },
  { type: 'SPLIT', label: 'Separar', description: 'Obtener parte de un texto separado', hasParams: true },
  { type: 'REPLACE', label: 'Reemplazar', description: 'Buscar y reemplazar texto', hasParams: true },
  { type: 'DATE_FORMAT', label: 'Formatear fecha', description: 'Cambiar formato de fecha', hasParams: true },
  { type: 'ADD_TIMEZONE', label: 'Agregar timezone', description: 'Agregar zona horaria a fecha', hasParams: true },
  { type: 'NUMBER_FORMAT', label: 'Formatear numero', description: 'Ajustar decimales o multiplicar', hasParams: true },
  { type: 'INVERT_SIGN', label: 'Invertir signo', description: 'Cambiar positivo/negativo', hasParams: false },
  { type: 'MAP_VALUE', label: 'Mapear valor', description: 'Traducir valores especificos', hasParams: true },
  { type: 'CALCULATE', label: 'Calcular', description: 'Formula matematica', hasParams: true },
  { type: 'REGEX_EXTRACT', label: 'Extraer con regex', description: 'Extraer con expresion regular', hasParams: true },
]

// Helper para obtener campos de una entidad
export function getSourceFields(entityName: string): { name: string; path: string; type: string; description?: string }[] {
  const entity = AGORA_SCHEMA.entities.find(e => e.name === entityName)
  return entity?.fields || []
}

export function getTargetFields(entityName: string): { name: string; type: string; required: boolean; description?: string; enum_values?: string[] }[] {
  const entity = LACABRERA_SCHEMA.entities.find(e => e.name === entityName)
  return entity?.fields || []
}

export function getAllSourcePaths(): { path: string; description: string; type: string }[] {
  const paths: { path: string; description: string; type: string }[] = []
  AGORA_SCHEMA.entities.forEach(entity => {
    entity.fields.forEach(field => {
      paths.push({
        path: `${entity.path.replace('[*]', '')}.${field.name}`,
        description: `${entity.name}: ${field.description || field.name}`,
        type: field.type
      })
    })
  })
  return paths
}
