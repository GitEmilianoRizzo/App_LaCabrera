# LA CABRERA - Frontend Dashboard

Dashboard de gestion para franquicias de LA CABRERA.

## Stack Tecnologico

- React 18 + TypeScript
- Vite (build tool)
- Tailwind CSS
- shadcn/ui (componentes)
- Recharts (graficos)
- React Router (navegacion)
- Axios (HTTP client)

## Requisitos

- Node.js 18+
- npm o yarn
- Backend API corriendo en `http://localhost:5000`

## Instalacion

```bash
cd frontend

# Instalar dependencias
npm install

# Iniciar en desarrollo
npm run dev
```

El dashboard estara disponible en: http://localhost:3000

## Estructura del Proyecto

```
frontend/
├── public/
│   └── favicon.svg
├── src/
│   ├── components/
│   │   ├── charts/           # Componentes de graficos (Recharts)
│   │   │   ├── SalesBarChart.tsx
│   │   │   ├── SalesDonutChart.tsx
│   │   │   └── SalesLineChart.tsx
│   │   ├── dashboard/        # Componentes de dashboard
│   │   │   ├── DataTable.tsx
│   │   │   ├── ErrorState.tsx
│   │   │   ├── KpiCard.tsx
│   │   │   └── LoadingState.tsx
│   │   ├── layout/           # Layout principal
│   │   │   ├── Header.tsx
│   │   │   ├── MainLayout.tsx
│   │   │   └── Sidebar.tsx
│   │   └── ui/               # Componentes base (shadcn)
│   │       ├── badge.tsx
│   │       ├── card.tsx
│   │       └── skeleton.tsx
│   ├── hooks/
│   │   └── useDashboard.ts   # Custom hooks para data fetching
│   ├── lib/
│   │   └── utils.ts          # Utilidades y formatters
│   ├── pages/
│   │   ├── DashboardHome.tsx
│   │   ├── EstadoIntegracion.tsx
│   │   ├── OcupacionMesas.tsx
│   │   ├── VentasFranquicia.tsx
│   │   ├── VentasMozo.tsx
│   │   └── VentasProducto.tsx
│   ├── services/
│   │   └── api.ts            # Cliente API
│   ├── types/
│   │   └── dashboard.ts      # Tipos TypeScript
│   ├── App.tsx
│   ├── index.css
│   └── main.tsx
├── index.html
├── package.json
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts
```

## Pantallas

| Ruta | Pantalla | Descripcion |
|------|----------|-------------|
| `/` | Dashboard Home | Vista consolidada con KPIs principales |
| `/ventas/franquicia` | Por Franquicia | Comparativa entre franquicias |
| `/ventas/producto` | Por Producto | Ranking de productos vendidos |
| `/ventas/mozo` | Por Mozo | Rendimiento del personal |
| `/operaciones/mesas` | Mesas | Ocupacion y rotacion |
| `/integracion/estado` | Integracion | Estado de sincronizacion |

## Configuracion

### Variables de Entorno

Crear archivo `.env` en la raiz del frontend:

```env
VITE_API_URL=http://localhost:5000/api/v1
```

### Proxy de Desarrollo

El `vite.config.ts` esta configurado para proxy automatico:

```typescript
proxy: {
  '/api': {
    target: 'http://localhost:5000',
    changeOrigin: true,
  },
}
```

## Conexion con Backend

Los tipos TypeScript estan alineados con los DTOs del backend:

| Frontend Type | Backend DTO |
|---------------|-------------|
| `HomeDashboard` | `HomeDashboardDto` |
| `VentasResumenDiario` | `VentasResumenDiarioDto` |
| `VentasPorFranquicia` | `VentasPorFranquiciaDto` |
| `VentasPorProducto` | `VentasPorProductoDto` |
| `VentasPorMozo` | `VentasPorMozoDto` |
| `EstadoIntegracion` | `EstadoIntegracionDto` |

## Comandos

```bash
# Desarrollo
npm run dev

# Build produccion
npm run build

# Preview build
npm run preview

# Lint
npm run lint
```

## Build para Produccion

```bash
npm run build
```

Los archivos estaticos se generan en `dist/` y pueden servirse con cualquier servidor web.

## Docker

```bash
# Desde la raiz del proyecto
docker build -t lacabrera-frontend -f frontend/Dockerfile .

docker run -p 3000:80 lacabrera-frontend
```

## Estilo y Colores

Colores de marca LA CABRERA:

| Color | Hex | Uso |
|-------|-----|-----|
| Gold | `#C4A35A` | Acentos, hover |
| Burgundy | `#722F37` | Secundario |
| Charcoal | `#2D2D2D` | Sidebar, textos |
| Cream | `#F5F1E8` | Fondos alternos |

## Responsive

El dashboard esta optimizado para:
- Desktop: 1920px+
- Laptop: 1366px+
- Tablet: 768px+ (parcial)

Las tablas y graficos se adaptan automaticamente.

## Notas de Desarrollo

1. Los datos se cargan via hooks personalizados (`useDashboard.ts`)
2. Los estados de loading y error estan manejados globalmente
3. Los formatters de moneda soportan PYG y USD
4. Los graficos usan la paleta de colores definida en `utils.ts`
