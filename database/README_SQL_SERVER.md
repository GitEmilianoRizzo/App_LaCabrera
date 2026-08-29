# LA CABRERA - Base de Datos SQL Server

## Descripcion

Base de datos central para el sistema de integracion de franquicias de LA CABRERA. Almacena ventas diarias de multiples franquicias internacionales y permite generar indicadores de gestion.

## Requisitos

- SQL Server 2019 o superior
- SQL Server Management Studio (SSMS) o Azure Data Studio
- Usuario con permisos de creacion de base de datos

## Estructura de Esquemas

| Esquema | Descripcion |
|---------|-------------|
| `cfg` | Configuracion del sistema |
| `dim` | Dimensiones (datos maestros) |
| `fact` | Hechos (transacciones de venta) |
| `stg` | Staging, ingesta cruda y logs |
| `api` | Control de integraciones y API keys |

## Orden de Ejecucion de Scripts

Ejecutar los scripts en el siguiente orden:

```
=== SETUP INICIAL ===
1. 00_create_database.sql      - Crea la base de datos
2. 01_create_schemas.sql       - Crea los esquemas
3. 02_create_tables.sql        - Crea todas las tablas
4. 03_create_constraints_indexes.sql - PKs, FKs e indices
5. 04_create_views_dashboard.sql     - Vistas para dashboard
6. 05_create_stored_procedures_ingestion.sql - SPs de ingesta
7. 06_seed_demo_data.sql       - Datos de ejemplo (opcional)
8. 07_seed_realistic_sales_data.sql  - Datos de venta realistas (opcional)
9. 08_configurar_nodo_aeroparque.sql - Configuracion nodo Aeroparque

=== LOTE MEJORAS_20260822 ===
10. 09_create_tipocambio_tables.sql     - Tablas de tipo de cambio (dim.TipoCambio)
11. 10_alter_fact_eje_impuesto.sql      - Columnas de impuesto en fact.*
12. 11_alter_fact_turno.sql             - Columnas de turno/meal period en fact.*
13. 12_create_cfg_franjas_alicuotas.sql - Franjas horarias y alicuotas
14. 13_alter_dim_franquicia_fechainicio.sql - FechaInicioDatos en dim.Franquicia
```

## Instrucciones de Ejecucion

### Opcion 1: SQL Server Management Studio (SSMS)

1. Conectarse al servidor SQL Server
2. Abrir cada script en orden
3. Ejecutar con F5 o el boton "Execute"
4. Verificar que no haya errores

### Opcion 2: Linea de Comandos (sqlcmd)

```bash
# Desde la carpeta /database/scripts
sqlcmd -S localhost -U sa -P "tu_password" -i 00_create_database.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 01_create_schemas.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 02_create_tables.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 03_create_constraints_indexes.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 04_create_views_dashboard.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 05_create_stored_procedures_ingestion.sql
sqlcmd -S localhost -d LaCabreraDB -U sa -P "tu_password" -i 06_seed_demo_data.sql
```

### Opcion 3: Docker con SQL Server

```bash
# Levantar contenedor SQL Server
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name sqlserver-lacabrera \
  -d mcr.microsoft.com/mssql/server:2022-latest

# Ejecutar scripts
docker exec -it sqlserver-lacabrera /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -i /scripts/00_create_database.sql
```

## Tablas Principales

### Dimensiones (dim)

| Tabla | Descripcion |
|-------|-------------|
| `dim.Moneda` | Monedas (USD, PYG, ARS, EUR) |
| `dim.GrupoEconomico` | Grupos de franquicias |
| `dim.Franquicia` | Franquicias individuales (incluye FechaInicioDatos) |
| `dim.TipoPlato` | Categorias de productos |
| `dim.Producto` | Productos por franquicia |
| `dim.Mozo` | Mozos/camareros por franquicia |
| `dim.Mesa` | Mesas por franquicia |
| `dim.MedioPago` | Metodos de pago |
| `dim.PeriodoComida` | Periodos (almuerzo, cena, etc.) |
| `dim.TipoCambio` | **NUEVO** Tasas de cambio diarias por moneda |

### Hechos (fact)

| Tabla | Descripcion |
|-------|-------------|
| `fact.VentaTicket` | Tickets/comandas |
| `fact.VentaTicketDetalle` | Lineas de productos vendidos |
| `fact.VentaTicketDescuento` | Descuentos aplicados |
| `fact.VentaTicketMedioPago` | Medios de pago utilizados |

### Staging (stg)

| Tabla | Descripcion |
|-------|-------------|
| `stg.IngestionBatch` | Batches de ingesta |
| `stg.IngestionBatchRawJson` | JSON crudo recibido |
| `stg.IngestionError` | Errores de validacion |
| `stg.ApiIngestaLog` | Log de llamadas API |
| `stg.TipoCambioIngestaLog` | **NUEVO** Log de ejecuciones del job FX |

### Configuracion (cfg)

| Tabla | Descripcion |
|-------|-------------|
| `cfg.TipoCambioProveedor` | **NUEVO** Proveedores de tipo de cambio (BCRA, etc.) |
| `cfg.FranjaHorariaFranquicia` | **NUEVO** Horarios almuerzo/cena por franquicia |
| `cfg.AlicuotaImpuestoFranquicia` | **NUEVO** Alicuotas de impuesto por franquicia |
| `cfg.PreferenciaColumnas` | Preferencias de columnas del usuario |

## Vistas para Dashboard

| Vista | Descripcion |
|-------|-------------|
| `fact.vw_HomeDashboard` | Matriz principal del home |
| `fact.vw_VentasResumenDiario` | Resumen diario de ventas |
| `fact.vw_VentasPorFranquicia` | Ventas acumuladas por franquicia |
| `fact.vw_VentasPorGrupoEconomico` | Ventas por grupo economico |
| `fact.vw_VentasPorMozo` | Ranking de mozos |
| `fact.vw_VentasPorProducto` | Ventas por producto |
| `fact.vw_VentasPorTipoPlato` | Ventas por categoria |
| `fact.vw_OcupacionMesas` | Ocupacion y rotacion de mesas |
| `fact.vw_IndicadoresPorCubierto` | Metricas por cubierto |
| `fact.vw_EstadoIntegracionFranquicias` | Estado de sincronizacion |
| `fact.vw_VentasPorHoraDiaSemana` | Heatmap de ventas |

## Stored Procedures de Ingesta

| Procedimiento | Descripcion |
|---------------|-------------|
| `stg.sp_RegistrarBatchIngesta` | Registra un nuevo batch |
| `stg.sp_GuardarJsonCrudo` | Guarda JSON original |
| `stg.sp_ActualizarEstadoBatch` | Actualiza estado del batch |
| `stg.sp_RegistrarErrorIngesta` | Registra error de validacion |
| `stg.sp_ObtenerEstadoBatch` | Consulta estado de batch |
| `dim.sp_InsertarActualizarMozo` | Upsert de mozo |
| `dim.sp_InsertarActualizarProducto` | Upsert de producto |
| `dim.sp_InsertarActualizarMesa` | Upsert de mesa |
| `fact.sp_InsertarTicketIdempotente` | Inserta ticket sin duplicar |
| `fact.sp_InsertarTicketDetalle` | Inserta detalle de ticket |
| `api.sp_ObtenerFranquiciaPorApiKey` | Valida API key |

## Idempotencia

El sistema garantiza idempotencia mediante constraints unicos:

- **Batch**: `FranquiciaId + BatchId`
- **Ticket**: `FranquiciaId + ExternalTicketId`
- **Linea**: `VentaTicketId + ExternalLineId`

Si se reenvia el mismo batch o ticket, no se duplican los datos.

## API Keys Demo

Para pruebas, el script `06_seed_demo_data.sql` crea:

| Franquicia | API Key |
|------------|---------|
| Paraguay Asuncion | `LC-DEMO-PARAGUAY-ASU01-2026` |
| Miami Beach | `LC-DEMO-MIAMI-01-2026` |

**IMPORTANTE**: Estas API keys son solo para demo. En produccion, generar keys seguras.

## Limpieza de Datos

Usar `99_drop_or_reset_demo_data.sql` para:

1. Limpiar solo datos demo (por defecto)
2. Limpiar todos los datos transaccionales
3. Eliminar todo incluyendo datos maestros
4. Eliminar la base de datos completa

## Notas de Configuracion

El script `00_create_database.sql` asume la ruta `C:\SQLData\`. Ajustar segun el servidor:

```sql
-- Modificar estas lineas segun el ambiente
FILENAME = N'C:\SQLData\LaCabreraDB.mdf'
FILENAME = N'C:\SQLData\LaCabreraDB_log.ldf'
```

Para SQL Server en Linux:
```sql
FILENAME = N'/var/opt/mssql/data/LaCabreraDB.mdf'
FILENAME = N'/var/opt/mssql/data/LaCabreraDB_log.ldf'
```

## Contacto

Proyecto: LA CABRERA - Central de Informacion para Franquicias
