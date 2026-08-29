import { Users, DollarSign, Ticket, TrendingUp } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { DataTable } from '@/components/dashboard/DataTable'
import { SalesBarChart } from '@/components/charts/SalesBarChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { useVentasPorMozo } from '@/hooks/useDashboard'
import { formatCurrency, formatNumber } from '@/lib/utils'
import type { VentasPorMozo } from '@/types/dashboard'

export function VentasMozo() {
  const { data, loading, error, refetch } = useVentasPorMozo()

  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  // Calcular totales
  const totals = data?.reduce(
    (acc, m) => ({
      ventaNeta: acc.ventaNeta + (m.venta_neta || 0),
      tickets: acc.tickets + (m.cantidad_tickets || 0),
      cubiertos: acc.cubiertos + (m.total_cubiertos || 0),
    }),
    { ventaNeta: 0, tickets: 0, cubiertos: 0 }
  ) || { ventaNeta: 0, tickets: 0, cubiertos: 0 }

  const ticketPromedio = totals.tickets > 0 ? totals.ventaNeta / totals.tickets : 0

  // Top mozos para grafico
  const topMozos = [...(data || [])]
    .sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0))
    .slice(0, 10)
    .map(m => ({
      name: m.mozo_nombre,
      value: m.venta_neta || 0,
    }))

  const columns = [
    {
      key: 'mozo_nombre',
      header: 'Mozo',
      render: (item: VentasPorMozo) => (
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-cabrera-burgundy/10 flex items-center justify-center">
            <span className="text-cabrera-burgundy text-sm font-medium">
              {(item.mozo_nombre || '').charAt(0)}
            </span>
          </div>
          <span className="font-medium">{item.mozo_nombre}</span>
        </div>
      ),
    },
    { key: 'franquicia_nombre', header: 'Franquicia' },
    {
      key: 'cantidad_tickets',
      header: 'Tickets',
      align: 'right' as const,
      render: (item: VentasPorMozo) => formatNumber(item.cantidad_tickets || 0),
    },
    {
      key: 'total_cubiertos',
      header: 'Cubiertos',
      align: 'right' as const,
      render: (item: VentasPorMozo) => formatNumber(item.total_cubiertos || 0),
    },
    {
      key: 'venta_neta',
      header: 'Venta Neta',
      align: 'right' as const,
      render: (item: VentasPorMozo) => formatCurrency(item.venta_neta || 0, 'USD'),
    },
    {
      key: 'ticket_promedio',
      header: 'Ticket Prom.',
      align: 'right' as const,
      render: (item: VentasPorMozo) => formatCurrency(item.ticket_promedio || 0, 'USD'),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Ventas por Mozo</h1>
        <p className="text-muted-foreground">Ranking y rendimiento del personal de servicio</p>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard
          title="Total Mozos"
          value={data?.length || 0}
          icon={Users}
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
          title="Total Tickets"
          value={totals.tickets}
          icon={Ticket}
          iconColor="bg-purple-100 text-purple-600"
        />
        <KpiCard
          title="Ticket Promedio"
          value={ticketPromedio}
          format="currency"
          icon={TrendingUp}
          iconColor="bg-amber-100 text-amber-600"
        />
      </div>

      {/* Chart */}
      <SalesBarChart
        title="Top 10 Mozos por Venta"
        data={topMozos}
        horizontal={true}
      />

      {/* Table */}
      <DataTable
        title="Detalle por Mozo"
        data={data || []}
        columns={columns}
      />
    </div>
  )
}
