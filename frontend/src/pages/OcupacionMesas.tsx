import { Table2, Clock, RotateCcw, Users } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { DataTable } from '@/components/dashboard/DataTable'
import { SalesBarChart } from '@/components/charts/SalesBarChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { useOcupacionMesas } from '@/hooks/useDashboard'
import { formatNumber } from '@/lib/utils'
import type { OcupacionMesas } from '@/types/dashboard'

export function OcupacionMesas() {
  const { data, loading, error, refetch } = useOcupacionMesas()

  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  // Calcular promedios
  const totalMesas = data?.length || 0
  const avgTiempo = (data?.reduce((sum, m) => sum + (m.tiempo_promedio_minutos || 0), 0) || 0) / (totalMesas || 1)
  const avgRotacion = (data?.reduce((sum, m) => sum + (m.rotacion_mesas || 0), 0) || 0) / (totalMesas || 1)
  const totalCubiertos = data?.reduce((sum, m) => sum + (m.total_cubiertos || 0), 0) || 0

  // Top mesas por rotacion
  const topMesas = [...(data || [])]
    .sort((a, b) => (b.rotacion_mesas || 0) - (a.rotacion_mesas || 0))
    .slice(0, 10)
    .map(m => ({
      name: `${m.franquicia_nombre} - ${m.fecha_negocio?.split('T')[0] || ''}`,
      value: m.rotacion_mesas || 0,
    }))

  const columns = [
    { key: 'franquicia_nombre', header: 'Franquicia' },
    {
      key: 'fecha_negocio',
      header: 'Fecha',
      render: (item: OcupacionMesas) => item.fecha_negocio?.split('T')[0] || '-',
    },
    {
      key: 'mesas_utilizadas',
      header: 'Mesas Usadas',
      align: 'right' as const,
      render: (item: OcupacionMesas) => formatNumber(item.mesas_utilizadas || 0),
    },
    {
      key: 'total_tickets',
      header: 'Servicios',
      align: 'right' as const,
      render: (item: OcupacionMesas) => formatNumber(item.total_tickets || 0),
    },
    {
      key: 'total_cubiertos',
      header: 'Cubiertos',
      align: 'right' as const,
      render: (item: OcupacionMesas) => formatNumber(item.total_cubiertos || 0),
    },
    {
      key: 'tiempo_promedio_minutos',
      header: 'Tiempo Prom.',
      align: 'right' as const,
      render: (item: OcupacionMesas) => `${formatNumber(item.tiempo_promedio_minutos || 0)} min`,
    },
    {
      key: 'rotacion_mesas',
      header: 'Rotacion',
      align: 'right' as const,
      render: (item: OcupacionMesas) => formatNumber(item.rotacion_mesas || 0, 1),
    },
    {
      key: 'porcentaje_ocupacion',
      header: '% Ocupacion',
      align: 'right' as const,
      render: (item: OcupacionMesas) => `${formatNumber(item.porcentaje_ocupacion || 0, 1)}%`,
    },
  ]

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Ocupacion de Mesas</h1>
        <p className="text-muted-foreground">Metricas de ocupacion, rotacion y tiempos</p>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard
          title="Total Registros"
          value={totalMesas}
          icon={Table2}
          iconColor="bg-blue-100 text-blue-600"
        />
        <KpiCard
          title="Tiempo Promedio"
          value={avgTiempo}
          decimals={0}
          icon={Clock}
          iconColor="bg-purple-100 text-purple-600"
        />
        <KpiCard
          title="Rotacion Promedio"
          value={avgRotacion}
          decimals={1}
          icon={RotateCcw}
          iconColor="bg-green-100 text-green-600"
        />
        <KpiCard
          title="Total Cubiertos"
          value={totalCubiertos}
          icon={Users}
          iconColor="bg-amber-100 text-amber-600"
        />
      </div>

      {/* Chart */}
      <SalesBarChart
        title="Top 10 por Rotacion Diaria"
        data={topMesas}
        horizontal={true}
      />

      {/* Table */}
      <DataTable
        title="Detalle de Ocupacion"
        data={data || []}
        columns={columns}
      />
    </div>
  )
}
