# Conector NEO – La Cabrera Málaga (T-381)

POS NEO (NeoKernel API v2026.3.0) del local de Málaga. Shop `0001` ("LA CABRERA"), moneda EUR, jornadas en hora de España.

## API

- Base: `http://restaurantelacabrera.webreports.cloud:3001`. Es HTTP plano: el HTTPS del puerto 3002 da timeout.
- Auth: header `x-api-key`. La key **no** va en la base ni en el repo. El conector la lee de la variable de entorno `NEO_MALAGA_API_KEY`, configurable en `connection.api_key_env`.
- **Solo GET.** Nunca llamar a `workingdaydone` ni a `workingdayticketdone` (POST): marcan jornadas o tickets como exportados del lado del proveedor.
- Una llamada por vez y al menos 1 s entre llamadas, porque el servidor es el del local.

| Endpoint | Uso |
|---|---|
| `/api/v1/b2b/sales/ticketheaders?shop&date` | Cabecera: `orderId` (clave), `total` (con IVA), `netAmount`, `taxAmount`, horas, mesa, zona, turno |
| `/api/v1/b2b/sales/ticketlines?shop&date` | Líneas: `amount = quantity × unitPrice` (con IVA). Solo suman las líneas con `affectAmount = 'S'` |
| `/api/v1/b2b/sales/ticketpayments?shop&date` | Pagos: `TCR` (tarjeta) y `CG` (efectivo; el vuelto viene negativo). `affectAmount = 'N'` es propina |
| `/api/v1/b2b/sales/tickettaxes?shop&date` | IVA por ticket. Control: Σ = `taxAmount` |
| `/api/v1/b2b/sales/cashreports?shop&date` | Arqueo de caja. Solo se informa: puede cerrarse antes del último cobro |
| `/api/v1/b2b/sales/dailysummary?shop&date` | Totales de la jornada (base, IVA y total por serie fiscal). Es el control principal |
| `/api/v1/b2b/sales/docs?shop&date&offset&limit` | Documento completo. Se usan los comensales (`buyerParty.totalClients`) y el estado |
| `/api/v1/b2b/products?offset&limit`, `/families`, `/subfamilies` | Catálogo para la familia de cada línea. Pesa ~32 MB, así que se baja como mucho cada 12 h |

- Envelope de todas las respuestas: `{apiVersion, systemTime, data, error}`.
- `docs` y `products` agregan `total` y paginan con `offset`/`limit`. Los demás endpoints devuelven la jornada completa e ignoran `offset`/`limit`.
- Las horas vienen en formato `HHmmss`, sobre la jornada (`yyyyMMdd`).

## Carga

Servicio: `NeoExtractorService` (cálculo puro en `NeoImportes`, con tests en `NeoImportesTests`).

**Cabecera (`fact.VentaTicket`):**

| Campo NEO | Campo destino |
|---|---|
| `orderId` | `ExternalTicketId` |
| `invoiceNumber` | `NumeroTicket` |
| `series` + `invoiceNumber` | `NumeroDocumentoFiscal` |
| `total` | `ImporteBruto` (con IVA) |
| `netAmount` | `ImporteNeto` y `ImporteNetoSinImpuesto` (lo que usan el Dashboard y Alertas) |
| `taxAmount` | `ImporteImpuesto` |
| `spot` | `NumeroMesa` |
| `zone` | `AreaMesa` |
| `turn` | `TurnoCodigo` |
| `operator` | `NombreMozo` |
| `docs.totalClients` | `CantidadCubiertos` |

- La propina sale de los pagos con `affectAmount = 'N'`.

**Detalle (`fact.VentaTicketDetalle`):**
- Se guardan todas las líneas.
- Las que tienen `affectAmount = 'N'` (subtotales, componentes de pack) quedan con `EstaAnulado = 1` y `MotivoAnulacion = NO_AFECTA_IMPORTE`, así no suman.
- La familia y la subfamilia salen del catálogo, y la categoría (`MAIN_COURSE`, `WINE`, …) se deriva de la familia.
- Las notas de cocina ("4-Al Punto") van a `Notas`.

**Pagos (`fact.VentaTicketMedioPago`):**
- Se netean por medio dentro de cada ticket (pago − vuelto).
- El mapeo `TCR → CREDIT_CARD` y `CG → CASH` está en `dim.MapeoMedioPago`. Un medio nuevo queda en `dim.ValorNoMapeado`.

**Crudo de cada jornada:**
- Se guarda en `stg.IngestionBatchRawJson` con `SchemaVersion = NEO_V1`, sin nombres de clientes; de `docs` solo pedido, estado y comensales.
- Se puede reprocesar sin llamar a la API: `POST /api/v1/conexiones/{id}/reprocesar-rango`.

**Idempotencia:**
- Upsert de la cabecera por `orderId`. Las líneas y los pagos del ticket se reemplazan.
- No depende de marcas del lado de NEO.

**Controles por jornada** (se cargan igual; quedan como WARNING en `stg.IngestionError` y `log.EjecucionNodo`):
- Σ líneas = total
- Σ pagos = total
- neto + IVA = total
- Σ tickettaxes = IVA
- Σ tickets = dailysummary
- arqueo vs Σ tickets: solo informativo

**Corrida diaria** (`ConectoresSyncJob`, 10:00 hora argentina, solo con `sync.auto = true`):
- Relee las últimas `sync.dias_releer` jornadas (7) para tomar cierres tardíos.
- Pide también las jornadas sin crudo desde `sync.fecha_inicio`. Eso hace de carga inicial y no se repite: una jornada con crudo ya no se vuelve a pedir, salvo que esté entre las 7 últimas.
- Como mucho pide `max_dias_por_corrida` jornadas.
- No carga la jornada en curso (hora de España).

**Endpoints manuales** (los mismos que los demás conectores):
- `POST /api/v1/conexiones/{id}/ejecutar?fechaNegocio=`
- `POST /api/v1/conexiones/{id}/ejecutar-rango?fechaDesde&fechaHasta`
- `POST /api/v1/conexiones/{id}/ejecutar-desde-ultimo`

## Datos que NEO no trae (quedan nulos)

- Canal de venta. `businessSource` trae `T`/`S`, pero falta confirmar con el proveedor qué significa (¿terraza/salón?).
- Motivo de anulación. En la jornada probada no hubo anulados ni devoluciones.
- Mozo real: el operador viene como "Manager" en todos los tickets, así que `TieneDatosMozo = 0`.

## Carga de prueba sep–oct 2026 (desarrollo)

- 61 jornadas, 2.311 tickets, 19.954 líneas, 366.761,50 EUR con IVA (333.419,46 sin IVA) y 6.161 cubiertos.
- Las 61 jornadas coinciden al centavo con `dailysummary`. No hubo avisos a nivel ticket (líneas, pagos, IVA).
- El arqueo (`cashreports`) no sirve de control: 15 jornadas no lo tienen, y en las demás suele cerrarse antes de los últimos cobros.
- Hubo 7 devoluciones (serie `FR`, facturas rectificativas, total negativo, −841 EUR). Se cargan con `Estado = REFUNDED` y restan.
- Hay 3 medios de pago sin mapear, que quedan en `dim.ValorNoMapeado` como `OTHER`: `FORK` (¿TheFork?), `TR` y `MET`. Hay que confirmar con el local qué son antes de mapearlos.
- 12 líneas no tienen familia (productos fuera del catálogo actual) y 35 tickets no tienen comensales (`totalClients = 0`, por eso quedan nulos).

## Activación en producción (la hace Infra con el OK de Emiliano)

1. `cd /opt/lacabrera && git pull --ff-only` (después del merge de la rama).
2. Agregar `NEO_MALAGA_API_KEY=<key>` al `.env` del servidor, sin mostrarla. El `docker-compose.prod.yml` ya la pasa al contenedor de la API.
3. Migración: `database/migrations/035_alta_neo_malaga.sql`. Crea `NODO_MALAGA` en `PENDING_CONFIG` con `sync.auto = false` y marca la franquicia `MALAGA` con `SistemaOrigen = 'NEO'`.
4. `docker compose -f docker-compose.prod.yml up -d --build api` (nunca `--remove-orphans`).
5. Activar el nodo:
   ```sql
   UPDATE dim.NodoConexion SET Estado = 'ACTIVE' WHERE Codigo = 'NODO_MALAGA';
   ```
6. Probar una jornada: `POST /api/v1/conexiones/{id}/ejecutar?fechaNegocio=2026-10-31`. Tiene que dar 42 tickets y 6.367,00 EUR.
7. Carga inicial: `POST /api/v1/conexiones/{id}/ejecutar-rango?fechaDesde=2026-09-01&fechaHasta=<ayer>`. La IP de salida de la VPS tiene que tener acceso al puerto 3001.
8. Encender la sync diaria:
   ```sql
   UPDATE dim.NodoConexion
   SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.sync.auto', CAST(1 AS BIT))
   WHERE Codigo = 'NODO_MALAGA';
   ```
