# LA CABRERA - Documento de Arquitectura

## Vision General

El sistema LA CABRERA Central de Franquicias es una plataforma de integracion de datos diseñada para consolidar informacion de ventas de multiples franquicias internacionales.

## Principios de Diseño

1. **Idempotencia**: Los datos pueden reenviarse sin duplicar ventas
2. **Flexibilidad**: Soporta diferentes sistemas POS
3. **Escalabilidad**: Preparado para 33+ franquicias
4. **Calidad de Datos**: Registra completitud de informacion
5. **Auditoria**: Trazabilidad completa de ingesta

## Componentes

### Backend API (ASP.NET Core 8.0)

```
LaCabrera.Api/
├── Controllers/         # Endpoints REST
│   ├── SalesController.cs
│   ├── DashboardController.cs
│   └── IntegrationController.cs
├── Services/            # Logica de negocio
│   ├── IngestionService.cs
│   └── DashboardService.cs
├── Repositories/        # Acceso a datos (Dapper)
│   ├── IngestionRepository.cs
│   └── DashboardRepository.cs
├── Validators/          # FluentValidation
│   └── DailySalesBatchRequestValidator.cs
├── Models/DTOs/         # Data Transfer Objects
└── Middleware/          # Manejo de errores
```

### Frontend Dashboard (React + TypeScript)

```
frontend/src/
├── pages/               # Vistas principales
│   ├── DashboardHome.tsx
│   ├── VentasFranquicia.tsx
│   └── EstadoIntegracion.tsx
├── components/          # Componentes reutilizables
│   ├── charts/          # Graficos (Recharts)
│   ├── dashboard/       # KPIs, tablas
│   └── layout/          # Header, Sidebar
├── services/            # Cliente API
├── hooks/               # Custom hooks
└── types/               # TypeScript interfaces
```

### Base de Datos (SQL Server)

```
Schemas:
├── cfg      # Configuracion (parametros)
├── dim      # Dimensiones (franquicias, productos, mozos)
├── fact     # Hechos (tickets, detalles, pagos)
├── stg      # Staging (batches, errores, logs)
└── api      # API Keys
```

## Flujo de Datos

### Ingesta via API

```
1. Franquicia genera JSON diario
         │
         ▼
2. POST /api/v1/sales/daily-batch
   Headers: X-API-KEY
         │
         ▼
3. Validacion API Key
   └─ Verificar franquicia activa
   └─ Verificar franchise_code coincide
         │
         ▼
4. Validacion de Estructura
   └─ FluentValidation
   └─ Schema version soportado
         │
         ▼
5. Registro de Batch
   └─ stg.IngestionBatch (metadata)
   └─ stg.IngestionBatchRawJson (JSON completo)
         │
         ▼
6. Verificacion Idempotencia
   └─ ¿batch_id ya existe para esta franquicia?
   └─ Si existe: retornar ID existente
         │
         ▼
7. Procesamiento de Tickets
   └─ Upsert dimensiones (productos, mozos, mesas)
   └─ Insertar tickets (fact.VentaTicket)
   └─ Insertar detalles (fact.VentaTicketDetalle)
   └─ Insertar pagos (fact.VentaTicketMedioPago)
   └─ Insertar descuentos (fact.VentaTicketDescuento)
         │
         ▼
8. Validacion Control Totals
   └─ Comparar header.control_totals vs suma real
   └─ Tolerancia configurable (1% por defecto)
         │
         ▼
9. Actualizar Estado
   └─ ACCEPTED / ACCEPTED_WITH_WARNINGS / REJECTED
   └─ Actualizar ultima sincronizacion de franquicia
         │
         ▼
10. Respuesta
    └─ batch_id, status, warnings, errors
```

### Consulta Dashboard

```
1. Usuario accede al dashboard
         │
         ▼
2. Frontend solicita datos
   GET /api/v1/dashboard/home
         │
         ▼
3. Backend consulta vistas SQL
   SELECT * FROM fact.vw_HomeDashboard
         │
         ▼
4. Las vistas agregan datos de:
   └─ fact.VentaTicket
   └─ fact.VentaTicketDetalle
   └─ dim.Franquicia
   └─ stg.IngestionBatch
         │
         ▼
5. Frontend renderiza:
   └─ KPIs (totales MTD, YTD, dia)
   └─ Graficos (lineas, barras, donut)
   └─ Tablas (ranking, detalles)
```

## Modelo de Datos

### Dimensiones

| Tabla | Descripcion | Relaciones |
|-------|-------------|------------|
| dim.Moneda | Monedas ISO 4217 | - |
| dim.GrupoEconomico | Grupos de franquicias | 1:N Franquicia |
| dim.Franquicia | Franquicias individuales | 1:N Producto, Mozo, Mesa |
| dim.TipoPlato | Categorias de productos | 1:N Producto |
| dim.Producto | Catalogo por franquicia | N:1 Franquicia, TipoPlato |
| dim.Mozo | Personal por franquicia | N:1 Franquicia |
| dim.Mesa | Mesas por franquicia | N:1 Franquicia |
| dim.MedioPago | Tipos de pago | - |
| dim.PeriodoComida | Turnos (almuerzo, cena) | - |

### Hechos

| Tabla | Descripcion | Granularidad |
|-------|-------------|--------------|
| fact.VentaTicket | Cabecera de tickets | 1 fila por ticket |
| fact.VentaTicketDetalle | Items vendidos | 1 fila por linea |
| fact.VentaTicketDescuento | Descuentos aplicados | 1 fila por descuento |
| fact.VentaTicketMedioPago | Formas de pago | 1 fila por medio |

### Staging

| Tabla | Descripcion |
|-------|-------------|
| stg.IngestionBatch | Metadata de cada carga |
| stg.IngestionBatchRawJson | JSON completo recibido |
| stg.IngestionError | Errores y warnings |
| stg.ApiIngestaLog | Log de llamadas API |

## Seguridad

### Autenticacion

- API Key por franquicia (header `X-API-KEY`)
- Validacion de coincidencia franchise_code
- Expiracion configurable
- Log de uso

### Autorizacion

- Franquicias solo pueden enviar sus propios datos
- Dashboard acceso publico (sin auth en demo)
- Preparado para agregar JWT en produccion

### Datos Sensibles

- Connection strings en variables de entorno
- API Keys no en repositorio
- Passwords en .env (no commitear)

## Escalabilidad

### Horizontal

- Backend stateless (puede replicarse)
- Frontend estatico (CDN-ready)
- Base de datos: replica de lectura para dashboard

### Vertical

- SQL Server puede escalar recursos
- Indices optimizados para consultas frecuentes

### Volumenes Esperados

| Metrica | Estimacion |
|---------|------------|
| Franquicias | 33 |
| Tickets/dia/franquicia | 50-100 |
| Items/ticket | 3-8 |
| Batches/dia | 33 (1 por franquicia) |
| Crecimiento mensual | ~100K tickets |

## Monitoreo

### Health Checks

- `GET /api/health` - Estado general
- `GET /health` - Frontend (nginx)
- Docker healthcheck integrado

### Logs

- Serilog en backend (archivo + consola)
- Nginx access logs en frontend
- Nivel configurable por ambiente

### Metricas Sugeridas (Fase 2)

- Tiempo de respuesta API
- Tasa de errores de ingesta
- Franquicias sin sincronizar
- Volumen de datos procesados

## Decisiones Tecnicas

### ¿Por que Dapper vs Entity Framework?

- Control total sobre queries SQL
- Mejor rendimiento en consultas complejas
- Scripts SQL manuales (requerimiento)
- Menor overhead

### ¿Por que React + Vite vs Next.js?

- Dashboard SPA puro
- No requiere SSR
- Build mas simple
- Menor complejidad

### ¿Por que SQL Server?

- Requerimiento del cliente
- Compatibilidad con infraestructura existente
- Vistas materializadas para dashboard
- Stored procedures para idempotencia

## Evolucion Futura

### Corto Plazo

- Cache para vistas de dashboard
- Rate limiting en API
- JWT para acceso dashboard

### Mediano Plazo

- Ingesta via archivos (Google Drive watcher)
- Notificaciones por email
- Export a Excel/PDF

### Largo Plazo

- Microservicios (separar ingesta de dashboard)
- Event sourcing
- Real-time updates (WebSocket)
