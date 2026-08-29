# LA CABRERA - Esquema de Integracion JSON

## Descripcion General

Este documento describe el Modelo JSON para la integracion de datos de ventas diarias entre las franquicias de LA CABRERA y el sistema central.

## Objetivo

Cada franquicia, independientemente del sistema POS que utilice, debe poder enviar diariamente sus ventas usando un unico formato JSON estandarizado.

## Metodos de Envio

### 1. API REST (Recomendado)

**Endpoint:**
```
POST /api/v1/sales/daily-batch
```

**Headers:**
```
Content-Type: application/json
X-API-KEY: <tu_api_key>
```

**Ejemplo cURL:**
```bash
curl -X POST https://api.lacabrera.com/api/v1/sales/daily-batch \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: LC-DEMO-PARAGUAY-ASU01-2026" \
  -d @lacabrera_daily_sales_batch_example.json
```

### 2. Archivo en Google Drive

La franquicia puede depositar el archivo JSON diario en una carpeta compartida de Google Drive.

**Nomenclatura del archivo:**
```
LC_<franchise_code>_<business_date>_<batch_id>.json
```

**Ejemplo:**
```
LC_PARAGUAY_ASU01_2026-07-14_BATCH-20260714-001.json
```

## Estructura del JSON

### Nivel Superior

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `schema_version` | string | Si | Version del schema. Actualmente "1.0" |
| `batch_header` | object | Si | Metadatos del batch |
| `tickets` | array | Si | Array de tickets/comandas |

### batch_header

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `batch_id` | string | Si | ID unico del batch. Formato: `LC-<franchise>-<date>-<seq>` |
| `business_date` | string | Si | Fecha de negocio (YYYY-MM-DD) |
| `generated_at` | string | Si | Timestamp ISO 8601 con zona horaria |
| `source_system` | object | No | Info del sistema origen |
| `franchise` | object | Si | Identificacion de franquicia |
| `extraction_window` | object | No | Ventana de tiempo de extraccion |
| `upload_type` | string | No | FULL_DAY, INCREMENTAL, CORRECTION |
| `control_totals` | object | Si | Totales de control para validacion |

### franchise (dentro de batch_header)

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `franchise_code` | string | Si | Codigo unico de franquicia |
| `franchise_name` | string | No | Nombre de la franquicia |
| `economic_group_code` | string | No | Codigo del grupo economico |
| `economic_group_name` | string | No | Nombre del grupo economico |
| `country` | string | No | Pais |
| `city` | string | No | Ciudad |
| `timezone` | string | Si | Zona horaria IANA (ej: America/Asuncion) |
| `currency` | string | Si | Codigo ISO 4217 (PYG, USD, EUR) |

### control_totals

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `ticket_count` | integer | Si | Cantidad de tickets en el batch |
| `item_line_count` | integer | Si | Cantidad total de lineas |
| `gross_sales_amount` | number | No | Suma de ventas brutas |
| `discount_amount` | number | No | Suma de descuentos |
| `net_sales_amount` | number | No | Suma de ventas netas |
| `tax_amount` | number | No | Suma de impuestos |
| `covers_total` | integer | No | Total de cubiertos |
| `cancelled_ticket_count` | integer | No | Tickets cancelados |

### tickets[]

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `ticket_id` | string | Si | ID unico del ticket en sistema origen |
| `ticket_number` | string | Si | Numero visible del ticket |
| `external_order_id` | string | No | ID de pedido externo (delivery) |
| `fiscal_document` | object | No | Documento fiscal |
| `status` | string | Si | OPEN, CLOSED, CANCELLED, VOIDED, REFUNDED |
| `opened_at` | string | Si | Fecha/hora apertura ISO 8601 |
| `closed_at` | string | Si* | Fecha/hora cierre (* null si OPEN) |
| `business_date` | string | Si | Fecha de negocio |
| `meal_period` | string | No | BREAKFAST, LUNCH, TEA, DINNER, LATE_NIGHT |
| `table` | object | No | Info de mesa |
| `waiter` | object | No | Info de mozo |
| `covers` | integer | No | Cantidad de cubiertos |
| `currency` | string | Si | Moneda del ticket |
| `amounts` | object | Si | Importes del ticket |
| `discounts` | array | No | Descuentos aplicados |
| `payment_methods` | array | No | Medios de pago |
| `items` | array | Si | Lineas de productos |

### items[] (dentro de ticket)

| Campo | Tipo | Requerido | Descripcion |
|-------|------|-----------|-------------|
| `line_id` | string | Si | ID unico de linea dentro del ticket |
| `product_code` | string | Si | Codigo del producto |
| `product_name` | string | Si | Nombre del producto |
| `product_category` | string | Si | Categoria (ver valores) |
| `product_family` | string | No | Familia (CARNES, ENTRADAS, etc.) |
| `product_subfamily` | string | No | Subfamilia |
| `quantity` | number | Si | Cantidad |
| `unit_price` | number | Si | Precio unitario |
| `gross_amount` | number | Si | Importe bruto |
| `discount_amount` | number | Si | Descuento en linea |
| `net_amount` | number | Si | Importe neto |
| `tax_amount` | number | No | Impuesto |
| `is_discounted` | boolean | No | Tiene descuento |
| `is_voided` | boolean | No | Esta anulado |
| `void_reason` | string | No | Motivo anulacion |
| `ordered_at` | string | No | Fecha/hora pedido |
| `served_at` | string | No | Fecha/hora servido |
| `notes` | string | No | Notas especiales |

## Valores Normalizados

### ticket.status

| Valor | Descripcion |
|-------|-------------|
| `OPEN` | Ticket abierto, en curso |
| `CLOSED` | Ticket cerrado y pagado |
| `CANCELLED` | Ticket cancelado |
| `VOIDED` | Ticket anulado |
| `REFUNDED` | Ticket reembolsado |

### meal_period

| Valor | Descripcion | Horario Tipico |
|-------|-------------|----------------|
| `BREAKFAST` | Desayuno | 06:00 - 11:00 |
| `LUNCH` | Almuerzo | 11:00 - 16:00 |
| `TEA` | Merienda | 16:00 - 19:00 |
| `DINNER` | Cena | 19:00 - 00:00 |
| `LATE_NIGHT` | Trasnoche | 00:00 - 06:00 |
| `UNKNOWN` | No determinado | - |

### product_category

| Valor | Descripcion |
|-------|-------------|
| `STARTER` | Entrada |
| `MAIN_COURSE` | Plato principal |
| `SIDE_DISH` | Guarnicion |
| `DESSERT` | Postre |
| `COFFEE` | Cafe |
| `BEVERAGE` | Bebida |
| `WINE` | Vino |
| `COCKTAIL` | Coctel |
| `OTHER` | Otros |

### payment_method

| Valor | Descripcion |
|-------|-------------|
| `CASH` | Efectivo |
| `DEBIT_CARD` | Tarjeta de debito |
| `CREDIT_CARD` | Tarjeta de credito |
| `BANK_TRANSFER` | Transferencia bancaria |
| `QR` | Pago QR |
| `MERCADO_PAGO` | Mercado Pago |
| `DELIVERY_APP` | App de delivery |
| `OTHER` | Otro |

### upload_type

| Valor | Descripcion |
|-------|-------------|
| `FULL_DAY` | Carga completa del dia |
| `INCREMENTAL` | Carga incremental |
| `CORRECTION` | Correccion de datos previos |

## Validaciones

### Validaciones Criticas (Rechazo)

1. `schema_version` debe ser "1.0"
2. `batch_id` obligatorio y unico por franquicia
3. `business_date` obligatorio y formato valido
4. `franchise.franchise_code` debe coincidir con API Key
5. `franchise.currency` obligatorio
6. `tickets` no puede estar vacio (salvo CORRECTION)
7. Cada ticket debe tener `ticket_id`, `status`, `amounts`, `items`
8. Cada item debe tener `line_id`, `product_code`, `product_name`, `product_category`

### Validaciones con Warning (Acepta con advertencia)

1. Diferencia entre `control_totals` y suma real < 1%
2. Tickets sin cubiertos
3. Tickets sin mozo
4. Tickets sin mesa
5. Items sin tiempos (`ordered_at`, `served_at`)

## Idempotencia

El sistema garantiza idempotencia:

- Si se reenvia el mismo `batch_id` de la misma franquicia, se ignora
- Si se reenvia el mismo `ticket_id` de la misma franquicia, no se duplica
- Si se reenvia la misma linea (`ticket_id` + `line_id`), no se duplica

## Respuestas de la API

### Exito (200 OK)

```json
{
  "success": true,
  "batch_id": "LC-PARAGUAY-ASU01-20260714-001",
  "ingestion_batch_id": 12345,
  "status": "ACCEPTED",
  "message": "Batch procesado exitosamente",
  "summary": {
    "tickets_received": 2,
    "tickets_processed": 2,
    "items_processed": 5,
    "gross_amount": 625000.00,
    "net_amount": 580000.00
  }
}
```

### Aceptado con Warnings (200 OK)

```json
{
  "success": true,
  "batch_id": "LC-PARAGUAY-ASU01-20260714-001",
  "ingestion_batch_id": 12346,
  "status": "ACCEPTED_WITH_WARNINGS",
  "message": "Batch procesado con advertencias",
  "warnings": [
    {
      "code": "MISSING_COVERS",
      "message": "Ticket POS-001 no tiene datos de cubiertos",
      "ticket_id": "POS-001"
    }
  ]
}
```

### Rechazo (400 Bad Request)

```json
{
  "success": false,
  "status": "REJECTED",
  "message": "Batch rechazado por errores de validacion",
  "errors": [
    {
      "code": "INVALID_FRANCHISE",
      "message": "El franchise_code no coincide con la API Key",
      "field": "batch_header.franchise.franchise_code"
    }
  ]
}
```

### Duplicado (409 Conflict)

```json
{
  "success": false,
  "status": "DUPLICATE",
  "message": "El batch ya fue procesado anteriormente",
  "existing_ingestion_batch_id": 12340
}
```

## Mapeo JSON a SQL Server

| Campo JSON | Tabla SQL | Columna SQL |
|------------|-----------|-------------|
| `batch_header.batch_id` | `stg.IngestionBatch` | `BatchId` |
| `batch_header.business_date` | `stg.IngestionBatch` | `FechaNegocio` |
| `franchise.franchise_code` | `dim.Franquicia` | `Codigo` |
| `ticket.ticket_id` | `fact.VentaTicket` | `ExternalTicketId` |
| `ticket.ticket_number` | `fact.VentaTicket` | `NumeroTicket` |
| `ticket.status` | `fact.VentaTicket` | `Estado` |
| `ticket.amounts.gross_amount` | `fact.VentaTicket` | `ImporteBruto` |
| `ticket.amounts.net_amount` | `fact.VentaTicket` | `ImporteNeto` |
| `ticket.covers` | `fact.VentaTicket` | `CantidadCubiertos` |
| `item.line_id` | `fact.VentaTicketDetalle` | `ExternalLineId` |
| `item.product_code` | `fact.VentaTicketDetalle` | `CodigoProducto` |
| `item.product_category` | `fact.VentaTicketDetalle` | `CategoriaProducto` |

## Ejemplos

### Archivo minimo valido

```json
{
  "schema_version": "1.0",
  "batch_header": {
    "batch_id": "LC-DEMO-20260714-001",
    "business_date": "2026-07-14",
    "generated_at": "2026-07-15T03:00:00Z",
    "franchise": {
      "franchise_code": "PARAGUAY_ASU01",
      "timezone": "America/Asuncion",
      "currency": "PYG"
    },
    "control_totals": {
      "ticket_count": 1,
      "item_line_count": 1
    }
  },
  "tickets": [
    {
      "ticket_id": "T-001",
      "ticket_number": "001",
      "status": "CLOSED",
      "opened_at": "2026-07-14T20:00:00-04:00",
      "closed_at": "2026-07-14T21:30:00-04:00",
      "business_date": "2026-07-14",
      "currency": "PYG",
      "amounts": {
        "gross_amount": 100000,
        "discount_amount": 0,
        "net_amount": 100000
      },
      "items": [
        {
          "line_id": "1",
          "product_code": "CAR-101",
          "product_name": "Bife de Chorizo",
          "product_category": "MAIN_COURSE",
          "quantity": 1,
          "unit_price": 100000,
          "gross_amount": 100000,
          "discount_amount": 0,
          "net_amount": 100000
        }
      ]
    }
  ]
}
```

## Soporte

Para consultas sobre la integracion, contactar a:
- Email: emiliano@pillow.com.ar
