# LA CABRERA - Datos Demo

Documentacion de los datos de demostracion para el sistema de integracion de franquicias.

## Estructura de Scripts

Los datos demo se cargan en el siguiente orden:

```
1. 06_seed_demo_data.sql           - Datos base (franquicias, mozos, mesas, productos basicos)
2. 06b_seed_extended_products.sql  - Catalogo extendido de productos
3. 07_seed_realistic_sales_data.sql - Datos de ventas realistas (30 dias)
```

## Franquicias Demo

| Franquicia | Codigo | Pais | Moneda | Mesas | Capacidad |
|------------|--------|------|--------|-------|-----------|
| La Cabrera Asuncion Centro | PARAGUAY_ASU01 | Paraguay | PYG | 25 | 100 |
| La Cabrera Miami Beach | MIAMI_01 | United States | USD | 35 | 140 |

## Volumenes de Datos Generados

### Por Franquicia (30 dias)

| Franquicia | Tickets/Dia | Total Tickets | Items Aprox |
|------------|-------------|---------------|-------------|
| Paraguay | 35-70 | ~1,500 | ~7,500 |
| Miami | 45-90 | ~2,000 | ~12,000 |

### Patrones de Consumo

- **Dias de semana (Lun-Jue)**: Volumen base
- **Viernes**: +40% sobre base
- **Sabado-Domingo**: +60% sobre base

### Horarios

- **Almuerzo**: 12:00-15:00 (30% de tickets)
- **Cena**: 19:00-23:00 (70% de tickets)
- **Duracion promedio**: 45-120 minutos

## Productos

### Paraguay (en Guaranies - PYG)

| Categoria | Cantidad | Rango Precios |
|-----------|----------|---------------|
| Entradas | 7 | 25,000 - 95,000 |
| Carnes | 8 | 115,000 - 320,000 |
| Guarniciones | 4 | 32,000 - 42,000 |
| Postres | 4 | 28,000 - 42,000 |
| Cafeteria | 3 | 12,000 - 18,000 |
| Bebidas | 5 | 12,000 - 35,000 |
| Vinos | 5 | 42,000 - 280,000 |

### Miami (en USD)

| Categoria | Cantidad | Rango Precios |
|-----------|----------|---------------|
| Appetizers | 6 | $14 - $24 |
| Steaks | 7 | $48 - $145 |
| Sides | 4 | $12 - $16 |
| Desserts | 4 | $10 - $15 |
| Coffee | 3 | $5 - $6 |
| Beverages | 4 | $5 - $12 |
| Wines | 5 | $16 - $95 |
| Cocktails | 2 | $16 - $18 |

## Descuentos

- ~15% de tickets en Paraguay tienen descuento
- ~12% de tickets en Miami tienen descuento
- Tipos: 10% y 15%

## Medios de Pago

Distribucion aproximada:
- Tarjeta Credito: 40%
- Tarjeta Debito: 25%
- Efectivo: 20%
- QR/Digital: 10%
- Otros: 5%

## API Keys de Prueba

```
Paraguay: LC-DEMO-PARAGUAY-ASU01-2026
Miami:    LC-DEMO-MIAMI-01-2026
```

**IMPORTANTE**: Estas API keys son solo para desarrollo y pruebas. No usar en produccion.

## Como Cargar los Datos

### Opcion 1: Script SQL (Recomendado para primera carga)

```bash
# Conectar a SQL Server y ejecutar en orden:
sqlcmd -S localhost -d LaCabreraDB -i 06_seed_demo_data.sql
sqlcmd -S localhost -d LaCabreraDB -i 06b_seed_extended_products.sql
sqlcmd -S localhost -d LaCabreraDB -i 07_seed_realistic_sales_data.sql
```

### Opcion 2: Via API (Para probar la integracion)

```bash
# Enviar batch de ejemplo
curl -X POST http://localhost:5000/api/v1/sales/daily-batch \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: LC-DEMO-MIAMI-01-2026" \
  -d @docs/integration/lacabrera_daily_sales_batch_full_day_example.json
```

## Como Resetear los Datos

```sql
-- Ejecutar para limpiar solo las ventas (mantiene dimensiones)
USE [LaCabreraDB]
GO

DELETE FROM [fact].[VentaTicketMedioPago]
DELETE FROM [fact].[VentaTicketDescuento]
DELETE FROM [fact].[VentaTicketDetalle]
DELETE FROM [fact].[VentaTicket]
DELETE FROM [stg].[IngestionError]
DELETE FROM [stg].[IngestionBatchRawJson]
DELETE FROM [stg].[IngestionBatch]

-- Luego volver a ejecutar 07_seed_realistic_sales_data.sql
```

Para un reset completo (incluyendo dimensiones), usar:
```sql
-- database/scripts/99_drop_or_reset_demo_data.sql
```

## Validacion de Datos

Despues de cargar los datos, verificar con estas consultas:

```sql
-- Resumen por franquicia
SELECT * FROM [fact].[vw_HomeDashboard]

-- Ventas diarias
SELECT * FROM [fact].[vw_VentasResumenDiario]
ORDER BY FechaNegocio DESC

-- Productos mas vendidos
SELECT * FROM [fact].[vw_VentasPorProducto]
ORDER BY VentaNeta DESC

-- Estado de integracion
SELECT * FROM [fact].[vw_EstadoIntegracionFranquicias]
```

## Escenarios de Prueba

### 1. Dashboard Operativo
Los datos permiten visualizar:
- KPIs YTD, MTD y diarios
- Comparativas entre franquicias
- Tendencias de venta por periodo
- Top productos y mozos

### 2. Prueba de Idempotencia
Enviar el mismo batch multiples veces y verificar que no se duplican datos:
```bash
# Enviar 3 veces el mismo archivo
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/v1/sales/daily-batch \
    -H "Content-Type: application/json" \
    -H "X-API-KEY: LC-DEMO-MIAMI-01-2026" \
    -d @docs/integration/lacabrera_daily_sales_batch_example.json
done

# Verificar que solo hay un batch
curl http://localhost:5000/api/v1/sales/daily-batch/LC-MIAMI-01-20260714-001/status \
  -H "X-API-KEY: LC-DEMO-MIAMI-01-2026"
```

### 3. Prueba de Validacion
Enviar datos con errores para verificar validaciones:
- Batch sin tickets
- Tickets sin items
- Control totals incorrectos
- Moneda no coincidente

## Notas Tecnicas

- Los precios en Paraguay estan en Guaranies (PYG), donde 1 USD ~ 7,300 PYG
- Miami incluye propinas (~18-22% del neto) como es costumbre en USA
- El tiempo de ejecucion del script 07 es de 2-5 minutos dependiendo del hardware
- Los datos cubren del 15 de Junio al 14 de Julio de 2026
