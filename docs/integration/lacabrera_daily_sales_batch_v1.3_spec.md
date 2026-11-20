# LA CABRERA · Daily Sales Push API · Specification v1.3

Audience: POS providers that push daily sales to La Cabrera (first implementation: Aquarius POS, Manila).
Status: v1.3, November 2026. Replaces the v1.0 model document for new integrations. Earlier versions (1.0–1.2) remain accepted for existing integrations, with their previous behaviour.

---

## 1. Overview

The POS sends **one batch per branch and business day**: a JSON document with a header and the list of tickets (sales, voids and refunds) of that day.

| | |
|---|---|
| Endpoint | `POST {base_url}/api/v1/sales/daily-batch` |
| Status of a batch | `GET {base_url}/api/v1/sales/daily-batch/{batch_id}/status` |
| Health check | `GET {base_url}/health` |
| Format | `Content-Type: application/json`, UTF-8 |
| Authentication | Header `X-API-KEY: <api key of the branch>` |
| Size | Keep each request under 20 MB (one business day per request). |

`base_url` and the API keys are delivered separately through a secure channel. **Each branch has its own API key**, and it only accepts batches whose `batch_header.franchise.franchise_code` is that branch.

---

## 2. Rules at a glance

1. `schema_version` = `"1.3"` and `batch_header.amounts_include_tax` = `true` (all amounts as charged, VAT included).
2. Line: `gross_amount = quantity × unit_price` and `net_amount = gross_amount − discount_amount`.
3. Ticket: `gross_amount`, `discount_amount` and `net_amount` = sum of its (non-voided) lines.
4. Send `net_amount_excl_tax`, `tax_amount` and `tax_rate` explicitly on every line, and `net_amount_excl_tax` and `tax_amount` on every ticket.
5. Service charge and tips go only in `service_charge_amount` and `tip_amount`. They are **not** part of `net_amount`.
6. Only `REFUNDED` tickets carry negative amounts and quantities. Sales, open tickets and voids are always positive.
7. `business_date` is the business day closed by the EOD. Timestamps are ISO 8601 with offset (`+08:00`).
8. `FULL_DAY` replaces the whole day of that branch. `INCREMENTAL` and `CORRECTION` insert or update tickets by `ticket_id` + `business_date`.

---

## 3. Request structure

### 3.1 Top level

| Field | Type | Req. | Description |
|---|---|---|---|
| `schema_version` | string | Yes | `"1.3"` |
| `batch_header` | object | Yes | Batch metadata (3.2) |
| `tickets` | array | Yes | Tickets of the business day (3.5). May be empty only for `CORRECTION`. |

### 3.2 batch_header

| Field | Type | Req. | Description |
|---|---|---|---|
| `batch_id` | string (≤200) | Yes | Unique per branch, e.g. `LC-MANILA_SHANGRILA-20261117-001`. A batch_id already received returns **409** and the batch is not processed. |
| `business_date` | string `YYYY-MM-DD` | Yes | Business day of the batch. **All tickets must have this same `business_date`.** |
| `generated_at` | string ISO 8601 | Yes | When the file was generated, with offset. |
| `source_system` | object | No | `system_name`, `system_version`, `exported_by`. |
| `franchise` | object | Yes | 3.3 |
| `upload_type` | string | No | `FULL_DAY` (default), `INCREMENTAL`, `CORRECTION`. See section 6. |
| `amounts_include_tax` | boolean | Yes | `true`: amounts include VAT. |
| `extraction_window` | object | No | `from` and `to`, ISO 8601. |
| `control_totals` | object | Yes | 3.4 |

### 3.3 franchise

| Field | Type | Req. | Description |
|---|---|---|---|
| `franchise_code` | string | Yes | Branch code given by La Cabrera (e.g. `MANILA_SHANGRILA`, `MANILA_AYALA`). Must match the API key. |
| `timezone` | string | Yes | IANA timezone: `Asia/Manila`. |
| `currency` | string | Yes | ISO 4217: `PHP`. |
| `franchise_name`, `country`, `city`, `economic_group_code`, `economic_group_name` | string | No | Informational. |

### 3.4 control_totals

Sum of **all tickets in the batch, including voids and refunds** (refunds add negative values). If a total differs from the sum by more than the tolerance, the batch is accepted with a `CONTROL_TOTALS_MISMATCH` warning.

| Field | Type | Req. |
|---|---|---|
| `ticket_count` | integer | Yes |
| `item_line_count` | integer | Yes |
| `gross_sales_amount`, `discount_amount`, `net_sales_amount`, `tax_amount`, `service_charge_amount`, `tip_amount` | number | Recommended |
| `covers_total`, `cancelled_ticket_count` | integer | Recommended |

### 3.5 tickets[]

| Field | Type | Req. | Description |
|---|---|---|---|
| `ticket_id` | string (≤200) | Yes | Unique per branch and business day, and **unique within the batch**. Recommended: `<TERMINAL_ID>-<TRANSACTION_NO>` (e.g. `POS01-000123`). Must never change once sent. |
| `ticket_number` | string | Yes | Number printed on the receipt (INVOICE_NO). |
| `fiscal_document` | object | No | `document_type` (e.g. `SALES_INVOICE`) and `document_number`. |
| `status` | string | Yes | `CLOSED`, `OPEN`, `VOIDED`, `CANCELLED`, `REFUNDED` (section 5). |
| `original_ticket_id` | string | REFUNDED: Yes | `ticket_id` of the sale being refunded. |
| `opened_at` | string ISO 8601 | Yes | With offset. |
| `closed_at` | string ISO 8601 | Yes* | With offset. `null` only for `OPEN`. |
| `business_date` | string `YYYY-MM-DD` | Yes | Same as `batch_header.business_date`. |
| `meal_period` | string | No | `BREAKFAST`, `LUNCH`, `TEA`, `DINNER`, `LATE_NIGHT`. |
| `shift` | object | No | `shift_id`, `shift_name`, `opened_at`, `closed_at`. |
| `table` | object | No | `table_number`, `table_area`. A missing table produces a `MISSING_TABLE` warning. |
| `waiter` | object | No | `waiter_id`, `waiter_name`. A missing waiter produces a `MISSING_WAITER` warning. |
| `covers` | integer | Recommended | Number of guests (pax). `0` for refunds. Missing covers (except on refunds) produce a `MISSING_COVERS` warning. |
| `currency` | string | Yes | `PHP` |
| `amounts` | object | Yes | 3.6 |
| `tax_breakdown` | object | Recommended | 3.7 |
| `discounts` | array | No | 3.8 |
| `payment_methods` | array | Yes** | 3.9. (** not for `VOIDED`, `CANCELLED` or `OPEN`.) |
| `items` | array | Yes | 3.10 |

### 3.6 amounts (ticket)

| Field | Type | Req. | Description |
|---|---|---|---|
| `gross_amount` | number | Yes | Σ line `gross_amount` (VAT included). |
| `discount_amount` | number | Yes | Σ line `discount_amount`. |
| `net_amount` | number | Yes | `gross_amount − discount_amount` (VAT included, **without** service charge and tips). |
| `net_amount_excl_tax` | number | Yes | `net_amount` without VAT. |
| `tax_amount` | number | Yes | VAT included in `net_amount` (0 if exempt). |
| `service_charge_amount` | number | No | Service charge, outside `net_amount`. |
| `tip_amount` | number | No | Tips, outside `net_amount`. |
| `total_paid_amount` | number | Yes | `net_amount + service_charge_amount + tip_amount`. `0` for `VOIDED` and `CANCELLED`. |

### 3.7 tax_breakdown (ticket, BIR)

Values after discounts, as shown on the BIR receipt.

| Field | Type | Description |
|---|---|---|
| `vatable_sales` | number | VATable sales, without VAT. |
| `vat_amount` | number | VAT on the VATable sales. |
| `vat_exempt_sales` | number | VAT-exempt sales (e.g. Senior Citizen / PWD), after the discount. |
| `zero_rated_sales` | number | Zero-rated sales. |

### 3.8 discounts[] (ticket)

Informational list of the discounts applied. The amounts must also be **prorated to the lines** (`items[].discount_amount`), so the lines add up to the ticket.

| Field | Type | Description |
|---|---|---|
| `discount_code` | string | e.g. `SENIOR_CITIZEN`, `PWD`, `PROMO10` |
| `discount_name` | string | Description |
| `discount_type` | string | `PERCENTAGE` or `AMOUNT` |
| `discount_value` | number | e.g. `20` for 20% |
| `discount_amount` | number | Amount discounted |

### 3.9 payment_methods[] (ticket)

One entry per tender. The sum of `amount` must equal `amounts.total_paid_amount` (otherwise there is a `PAYMENTS_MISMATCH` warning). Refund payments are negative.

| Field | Type | Req. | Description |
|---|---|---|---|
| `payment_method` | string | Yes | `CASH`, `DEBIT_CARD`, `CREDIT_CARD`, `BANK_TRANSFER`, `QR`, `DELIVERY_APP`, `OTHER`. E-wallets (GCash, Maya) are sent as `QR`. |
| `payment_brand` | string | No | e.g. `VISA`, `MASTERCARD`, `AMEX`, `GCASH`, `MAYA` |
| `amount` | number | Yes | Amount of this tender. |
| `reference` | string (≤200) | No | Approval or reference number. |

### 3.10 items[] (lines)

| Field | Type | Req. | Description |
|---|---|---|---|
| `line_id` | string | Yes | Unique within the ticket and stable across re-sends. |
| `product_code` | string | Yes | POS product code. |
| `product_name` | string | Yes | |
| `product_category` | string | Yes | `STARTER`, `MAIN_COURSE`, `SIDE_DISH`, `DESSERT`, `COFFEE`, `BEVERAGE`, `WINE`, `COCKTAIL`, `OTHER`. |
| `product_family`, `product_subfamily` | string | No | POS grouping. |
| `quantity` | number | Yes | Negative only in `REFUNDED`. |
| `unit_price` | number | Yes | Always positive. VAT included. For VAT-exempt lines, the price without VAT. |
| `gross_amount` | number | Yes | `quantity × unit_price` |
| `discount_amount` | number | Yes | Line discount (including its share of ticket discounts). |
| `net_amount` | number | Yes | `gross_amount − discount_amount` |
| `net_amount_excl_tax` | number | Yes | `net_amount` without VAT. |
| `tax_amount` | number | Yes | VAT of the line (0 if exempt). |
| `tax_rate` | number | Yes | `0.12` VATable, `0` exempt or zero-rated. |
| `is_voided` | boolean | No | `true` for a voided item (section 5). |
| `void_reason` | string | No | Reason of the void. |
| `is_discounted` | boolean | No | |
| `ordered_at`, `served_at` | string ISO 8601 | No | |
| `notes` | string | No | |

---

## 4. VAT, discounts and service charge

- **VATable line** (menu price PHP 1,680.00): `unit_price` 1680, `gross_amount` 1680, `net_amount` 1680, `net_amount_excl_tax` 1500, `tax_amount` 180, `tax_rate` 0.12.
- **Senior Citizen / PWD line** (same dish): VAT is removed first, then the 20% discount. `unit_price` 1500, `gross_amount` 1500, `discount_amount` 300, `net_amount` 1200, `net_amount_excl_tax` 1200, `tax_amount` 0, `tax_rate` 0. Add `{ "discount_code": "SENIOR_CITIZEN", "discount_type": "PERCENTAGE", "discount_value": 20, "discount_amount": 300 }` to the ticket `discounts`.
- **Never** send `net_amount` already without VAT. `net_amount` includes VAT, and `net_amount_excl_tax` is the value without it.
- **Service charge and tips**: only in `service_charge_amount` and `tip_amount`, and included in `total_paid_amount` and in the payments.

---

## 5. Ticket status and signs

| Case | status | Amounts | Payments | Effect in La Cabrera |
|---|---|---|---|---|
| Sale | `CLOSED` | positive | positive, Σ = total_paid | Counts as a sale |
| Open ticket at EOD | `OPEN` | positive | none | Counts as a sale; send it again once closed (`INCREMENTAL`/`CORRECTION`) |
| Whole ticket voided / cancelled | `VOIDED` / `CANCELLED` | original, positive; `total_paid_amount` 0; lines with `is_voided: true` and `void_reason` | none | Kept, excluded from sales |
| One item voided on a valid ticket | `CLOSED` | line with `is_voided: true`, positive, **excluded** from the ticket totals | | The line is excluded |
| Refund / return | `REFUNDED` | new ticket, own `ticket_id`, `original_ticket_id`; negative `quantity`, `gross_amount`, `discount_amount`, `net_amount`, `net_amount_excl_tax`, `tax_amount`; positive `unit_price`; `covers` 0 | negative | Subtracts from sales of its business date |

The original sale is never modified by a refund.

---

## 6. Upload types and idempotency

Key of a ticket: **branch (API key) + `ticket_id` + `business_date`**.

| upload_type | Behaviour |
|---|---|
| `FULL_DAY` (default) | Complete day. **Replaces** everything previously loaded for that branch and `business_date`. Tickets not present in the new batch are removed (`removed_ticket_ids`). |
| `INCREMENTAL` | Part of the day (e.g. hourly). New tickets are added, existing ones are **replaced** by the new version. Other tickets of the day are kept. |
| `CORRECTION` | Same as `INCREMENTAL`; use it to resend corrected tickets. |

- Each request needs a **new `batch_id`**. A repeated `batch_id` returns 409 and nothing is processed.
- Historical data: one batch per business date, each with a new `batch_id`. `FULL_DAY` is recommended.
- The batch is processed in a single transaction. If a ticket cannot be stored, only that ticket is rejected (`rejected_tickets`, with the reason) and the rest is loaded. If **no** ticket of a `FULL_DAY` can be stored, the batch is rejected and the data already loaded for that day is kept.
- Replaced or removed tickets are backed up on our side.

---

## 7. Responses

`status` and `code` are the contract. `message` texts are informational and may be in Spanish.

### 200 · accepted

```json
{
  "success": true,
  "batch_id": "LC-MANILA_SHANGRILA-20261117-001",
  "ingestion_batch_id": 12345,
  "status": "ACCEPTED",
  "message": "Batch procesado exitosamente",
  "summary": {
    "tickets_received": 5,
    "tickets_processed": 5,
    "items_processed": 9,
    "gross_amount": 7874.00,
    "net_amount": 7574.00,
    "tickets_accepted": 5,
    "tickets_updated": 0,
    "tickets_rejected": 0,
    "tickets_removed": 0,
    "updated_ticket_ids": [],
    "removed_ticket_ids": [],
    "rejected_tickets": []
  }
}
```

- `tickets_accepted`: new tickets. `tickets_updated` and `updated_ticket_ids`: tickets that already existed and were replaced.
- `tickets_removed` and `removed_ticket_ids`: only for `FULL_DAY`, tickets of that day that were not in the batch.

### 200 · accepted with warnings

`status: "ACCEPTED_WITH_WARNINGS"`, the same `summary`, plus:

```json
"warnings": [
  { "code": "PAYMENTS_MISMATCH", "message": "ticket POS01-000124: sum of payment_methods = 4500.00 but total_paid_amount = 4594.00", "ticket_id": "POS01-000124", "line_id": null }
],
"errors": [
  { "code": "TICKET_PROCESSING_ERROR", "message": "ticket POS01-000141 not loaded: line_id 1 is repeated in the ticket", "field": "tickets[1]", "ticket_id": "POS01-000141" }
]
```

| Warning code | Meaning |
|---|---|
| `LINE_AMOUNT_MISMATCH` | `quantity × unit_price ≠ gross_amount` or `gross_amount − discount_amount ≠ net_amount` |
| `TICKET_LINES_MISMATCH` | Sum of lines ≠ ticket `gross_amount`, `discount_amount` or `net_amount` |
| `PAYMENTS_MISMATCH` | Sum of payments ≠ `total_paid_amount` |
| `MISSING_NET_EXCL_TAX` | Ticket without `net_amount_excl_tax` and `tax_amount` |
| `CONTROL_TOTALS_MISMATCH` | `control_totals` differ from the sum of the tickets |
| `UNKNOWN_PAYMENT_METHOD` | `payment_method` outside the accepted values (stored without mapping) |
| `MISSING_TABLE`, `MISSING_WAITER`, `MISSING_COVERS` | Optional data missing |

Warnings never reject a ticket. Ticket rejections appear in `errors` and `summary.rejected_tickets`.

### 400 · rejected (nothing is processed)

```json
{
  "success": false,
  "status": "REJECTED",
  "message": "Batch rechazado por errores de validacion",
  "errors": [
    { "code": "NEGATIVE_AMOUNT_NOT_REFUNDED", "message": "ticket POS01-000123: negative amounts are only allowed when status is REFUNDED (amounts.gross_amount)", "field": "Tickets[0].Amounts.GrossAmount" }
  ]
}
```

| Code | Meaning |
|---|---|
| `FRANCHISE_MISMATCH` | `franchise_code` does not match the API key |
| `NEGATIVE_AMOUNT_NOT_REFUNDED` | Negative amount or quantity in a ticket that is not `REFUNDED` |
| `INVALID_REFUND` | `REFUNDED` without `original_ticket_id`, or with positive amounts or quantities |
| `BUSINESS_DATE_MISMATCH` | A ticket `business_date` differs from `batch_header.business_date` |
| `DUPLICATE_TICKET_ID` | The same `ticket_id` appears twice in the batch |
| `MISSING_AMOUNTS_INCLUDE_TAX` | `batch_header.amounts_include_tax` missing |
| `VALIDATION_ERROR` | Other required field or format error (see `message` and `field`) |

A `FULL_DAY` in which no ticket could be stored also returns 400 `REJECTED`, with the reasons in `errors`.

### 401 · API key missing or invalid

```json
{ "error": "API Key invalida o no proporcionada" }
```

### 409 · batch_id already received

```json
{ "success": false, "batch_id": "LC-MANILA_SHANGRILA-20261117-001", "ingestion_batch_id": 12340, "status": "DUPLICATE", "message": "El batch ya fue procesado anteriormente" }
```

### GET status

`GET /api/v1/sales/daily-batch/{batch_id}/status` with the same `X-API-KEY` returns 200 with `status`, `error_count`, `warning_count`, `tickets_received`, `tickets_processed`, `received_at` and `processed_at`, or 404 if the batch is unknown.

---

## 8. Example

`examples/manila_v1.3_full_day_example.json`: a `FULL_DAY` for `MANILA_SHANGRILA`, business date 2026-11-17, with:

| ticket_id | Scenario |
|---|---|
| `POS01-000123` | Normal sale, cash |
| `POS01-000124` | Split payment (VISA + GCash). Closed at 00:35 on the 18th, so it belongs to business date 17. |
| `POS02-000057` | Second terminal. One line Senior Citizen (VAT-exempt, 20% discount) |
| `POS01-000125` | Voided ticket |
| `POS01-000131` | Refund of the wine of `POS01-000124` |

All amounts in the example are consistent: line arithmetic, ticket = Σ lines, net without VAT + VAT = net, payments = total paid, and control totals.

---

## 9. Changes from v1.2

- New: `original_ticket_id`, `tax_breakdown`, `payment_methods[].reference`.
- `REFUNDED` with negative amounts subtracts from sales. Negative values outside `REFUNDED` are rejected.
- `FULL_DAY` replaces the day. `INCREMENTAL`/`CORRECTION` update existing tickets (in v1.2 they were ignored).
- `amounts_include_tax` required. One `business_date` per batch, and a `ticket_id` cannot repeat within a batch.
- Response: `tickets_accepted`, `tickets_updated`, `tickets_rejected`, `tickets_removed` and their ids, plus consistency warnings.
- `service_charge_amount` is explicitly outside `net_amount`.
