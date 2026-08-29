import { useState } from 'react'
import { Store, DollarSign, Ticket, Users } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { DataTable } from '@/components/dashboard/DataTable'
import { SalesBarChart } from '@/components/charts/SalesBarChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { FilterBar, type FilterValues } from '@/components/dashboard/FilterBar'
import { useVentasPorFranquicia, useHomeDashboard } from '@/hooks/useDashboard'
import { formatCurrency, formatNumber } from '@/lib/utils'
import type { VentasPorFranquicia, DashboardFilters } from '@/types/dashboard'

export function VentasFranquicia() {
  // Fechas por defecto: ultimo mes con datos (junio-julio 2026)
  const defaultFilters: FilterValues = {
    fechaDesde: '2026-06-01',
    fechaHasta: '2026-07-31',
    pais: '',
    franquiciaId: null,
  }

  const [filters, setFilters] = useState<FilterValues>(defaultFilters)

  // Convertir filtros a formato de API
  const apiFilters: DashboardFilters = {
    fechaDesde: filters.fechaDesde,
    fechaHasta: filters.fechaHasta,
    franquiciaId: filters.franquiciaId || undefined,
  }

  const { data, loading, error, refetch } = useVentasPorFranquicia(apiFilters)
  const { data: homeData } = useHomeDashboard()

  // Obtener lista de franquicias para el FilterBar
  const franquicias = homeData?.map(f => ({
    id: f.franquicia_id,
    nombre: f.franquicia_nombre,
    pais: f.pais || '',
  })) || []

  const handleFilterChange = (newFilters: FilterValues) => {
    setFilters(newFilters)
  }

  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  // Calcular totales usando venta_neta_ytd (que ahora contiene los datos filtrados)
  const totals = data?.reduce(
    (acc, f) => ({
      ventaNeta: acc.ventaNeta + (f.venta_neta_ytd || 0),
      tickets: acc.tickets + (f.tickets_ytd || 0),
      cubiertos: acc.cubiertos + (f.cubiertos_ytd || 0),
    }),
    { ventaNeta: 0, tickets: 0, cubiertos: 0 }
  ) || { ventaNeta: 0, tickets: 0, cubiertos: 0 }

  // Preparar datos para grafico
  const barData = data?.map(f => ({
    name: f.franquicia_nombre,
    value: f.venta_neta_ytd || 0,
  })) || []

  const columns = [
    { key: 'franquicia_nombre', header: 'Franquicia' },
    { key: 'grupo_economico_nombre', header: 'Grupo' },
    { key: 'pais', header: 'Pais' },
    { key: 'ciudad', header: 'Ciudad' },
    {
      key: 'venta_neta_ytd',
      header: 'Venta Neta',
      align: 'right' as const,
      render: (item: VentasPorFranquicia) => formatCurrency(item.venta_neta_ytd || 0, item.moneda_codigo || 'USD'),
    },
    {
      key: 'tickets_ytd',
      header: 'Tickets',
      align: 'right' as const,
      render: (item: VentasPorFranquicia) => formatNumber(item.tickets_ytd || 0),
    },
    {
      key: 'cubiertos_ytd',
      header: 'Cubiertos',
      align: 'right' as const,
      render: (item: VentasPorFranquicia) => formatNumber(item.cubiertos_ytd || 0),
    },
    {
      key: 'ticket_promedio',
      header: 'Ticket Prom.',
      align: 'right' as const,
      render: (item: VentasPorFranquicia) => {
        const promedio = item.tickets_ytd > 0 ? item.venta_neta_ytd / item.tickets_ytd : 0
        return formatCurrency(promedio, item.moneda_codigo || 'USD')
      },
    },
  ]

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Ventas por Franquicia</h1>
        <p className="text-muted-foreground">Comparativa de rendimiento entre franquicias</p>
      </div>

      {/* Filter Bar */}
      <FilterBar
        onFilterChange={handleFilterChange}
        franquicias={franquicias}
        initialFilters={defaultFilters}
      />

      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard
          title="Total Franquicias"
          value={data?.length || 0}
          icon={Store}
          iconColor="bg-blue-100 text-blue-600"
        />
        <KpiCard
          title="Venta Neta Total"
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
          title="Total Cubiertos"
          value={totals.cubiertos}
          icon={Users}
          iconColor="bg-amber-100 text-amber-600"
        />
      </div>

      {/* Chart */}
      <SalesBarChart
        title="Venta Neta por Franquicia"
        data={barData}
        horizontal={true}
      />

      {/* Table */}
      <DataTable
        title="Detalle por Franquicia"
        data={data || []}
        columns={columns}
      />
    </div>
  )
}
