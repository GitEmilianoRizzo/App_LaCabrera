import { ShoppingBag, DollarSign, Package } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { DataTable } from '@/components/dashboard/DataTable'
import { SalesBarChart } from '@/components/charts/SalesBarChart'
import { SalesDonutChart } from '@/components/charts/SalesDonutChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { useVentasPorProducto, useVentasPorTipoPlato } from '@/hooks/useDashboard'
import { formatCurrency, formatNumber } from '@/lib/utils'
import type { VentasPorProducto } from '@/types/dashboard'

export function VentasProducto() {
  const { data: productos, loading, error, refetch } = useVentasPorProducto()
  const { data: tiposPlato } = useVentasPorTipoPlato()

  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  // Calcular totales
  const totals = productos?.reduce(
    (acc, p) => ({
      ventaNeta: acc.ventaNeta + (p.venta_neta || 0),
      cantidad: acc.cantidad + (p.cantidad_vendida || 0),
    }),
    { ventaNeta: 0, cantidad: 0 }
  ) || { ventaNeta: 0, cantidad: 0 }

  // Top 10 productos para grafico
  const topProductos = [...(productos || [])]
    .sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0))
    .slice(0, 10)
    .map(p => ({
      name: (p.producto_nombre || '').length > 20 ? (p.producto_nombre || '').slice(0, 20) + '...' : (p.producto_nombre || ''),
      value: p.venta_neta || 0,
    }))

  // Datos para donut por tipo
  const donutData = tiposPlato?.map(t => ({
    name: t.tipo_plato_nombre,
    value: t.venta_neta,
  })) || []

  const columns = [
    {
      key: 'producto_nombre',
      header: 'Producto',
      render: (item: VentasPorProducto) => (
        <div>
          <p className="font-medium">{item.producto_nombre}</p>
          <p className="text-xs text-muted-foreground">{item.producto_codigo}</p>
        </div>
      ),
    },
    { key: 'categoria', header: 'Categoria' },
    { key: 'franquicia_nombre', header: 'Franquicia' },
    {
      key: 'cantidad_vendida',
      header: 'Cantidad',
      align: 'right' as const,
      render: (item: VentasPorProducto) => formatNumber(item.cantidad_vendida || 0),
    },
    {
      key: 'venta_neta',
      header: 'Venta Neta',
      align: 'right' as const,
      render: (item: VentasPorProducto) => formatCurrency(item.venta_neta || 0, 'USD'),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Ventas por Producto</h1>
        <p className="text-muted-foreground">Ranking y analisis de productos vendidos</p>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <KpiCard
          title="Productos Vendidos"
          value={productos?.length || 0}
          icon={Package}
          iconColor="bg-blue-100 text-blue-600"
        />
        <KpiCard
          title="Venta Total"
          value={totals.ventaNeta}
          format="currency"
          icon={DollarSign}
          iconColor="bg-green-100 text-green-600"
        />
        <KpiCard
          title="Unidades Vendidas"
          value={totals.cantidad}
          icon={ShoppingBag}
          iconColor="bg-purple-100 text-purple-600"
        />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <SalesBarChart
          title="Top 10 Productos"
          data={topProductos}
          horizontal={true}
          className="lg:col-span-2"
        />
        <SalesDonutChart
          title="Ventas por Categoria"
          data={donutData}
        />
      </div>

      {/* Table */}
      <DataTable
        title="Detalle de Productos"
        data={productos || []}
        columns={columns}
        maxRows={20}
      />
    </div>
  )
}
