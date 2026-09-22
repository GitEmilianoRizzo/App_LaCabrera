import { useState, useMemo } from 'react'
import { DollarSign, Ticket, Users, TrendingUp, Info, Store } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { SalesLineChart, MetricOption } from '@/components/charts/SalesLineChart'
import { HorizontalBarChart } from '@/components/charts/HorizontalBarChart'
import { ClockChart } from '@/components/charts/ClockChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { FilterBar, FilterValues } from '@/components/dashboard/FilterBar'
import { DetailModal, useDetailModal } from '@/components/dashboard/DetailModal'
import { EnhancedDataTable, ColumnDef } from '@/components/dashboard/EnhancedDataTable'
import { FranquiciaDetailModal } from '@/components/dashboard/FranquiciaDetailModal'
import { ExportExcelModal } from '@/components/dashboard/ExportExcelModal'
import {
  useHomeDashboard,
  useVentasResumen,
  useVentasPorTipoPlato,
  useVentasPorProducto,
  useVentasPorHora,
} from '@/hooks/useDashboard'
import { formatCurrency, formatNumber } from '@/lib/utils'

// Métricas disponibles para el gráfico de tendencias
const TREND_METRICS: MetricOption[] = [
  { id: 'venta_neta', label: 'Venta Neta (USD)', format: 'currency' },
  { id: 'tickets', label: 'Tickets', format: 'number' },
  { id: 'ticket_promedio', label: 'Ticket Prom.', format: 'currency' },
  { id: 'tickets_dia', label: 'Tickets/Día', format: 'number' },
  { id: 'cubiertos', label: 'Cubiertos', format: 'number' },
  { id: 'cub_ticket', label: 'Cub/Ticket', format: 'number' },
  { id: 'cub_dia', label: 'Cub/Día', format: 'number' },
  { id: 'mesas_ocupadas', label: 'Mesas Ocup.', format: 'number' },
  { id: 'porcentaje_ocupacion', label: '% Ocup.', format: 'percent' },
]

// Tipo para los datos de franquicia en el modal
interface FranquiciaModalData {
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_nombre?: string
  pais?: string
  ciudad?: string
  moneda_codigo?: string
  venta_bruta: number
  venta_neta: number
  venta_neta_local: number
  total_tickets: number
  total_cubiertos: number
  ticket_promedio: number
  venta_mes_actual: number
  tickets_mes_actual: number
  venta_mes_anterior: number
  tickets_mes_anterior: number
  venta_acum_anio_actual: number
  tickets_acum_anio_actual: number
}

export function DashboardHome() {
  // Métrica seleccionada para el gráfico de tendencias
  const [selectedMetric, setSelectedMetric] = useState<string>('venta_neta')
  // Franquicia seleccionada en el gráfico de barras (para filtro en cascada)
  const [selectedFranquiciaChart, setSelectedFranquiciaChart] = useState<string | null>(null)
  // Tipo de plato seleccionado para filtrar productos
  const [selectedTipoPlato, setSelectedTipoPlato] = useState<string | null>(null)
  // Estado para el modal de detalle de franquicia
  const [selectedFranquicia, setSelectedFranquicia] = useState<FranquiciaModalData | null>(null)
  // v1.3: Hora seleccionada para filtrado bidireccional (ClockChart)
  const [selectedHour, setSelectedHour] = useState<number | null>(null)
  // Estado para el modal de exportación Excel
  const [exportModalOpen, setExportModalOpen] = useState(false)

  // Filtros - por defecto día 1 al último día del mes actual
  const getDefaultDates = () => {
    const today = new Date()
    const year = today.getFullYear()
    const month = today.getMonth()
    const firstDay = new Date(year, month, 1)
    const lastDay = new Date(year, month + 1, 0)
    return {
      fechaDesde: firstDay.toISOString().split('T')[0],
      fechaHasta: lastDay.toISOString().split('T')[0],
    }
  }
  const defaultDates = getDefaultDates()

  const [filters, setFilters] = useState<FilterValues>({
    fechaDesde: defaultDates.fechaDesde,
    fechaHasta: defaultDates.fechaHasta,
    pais: '',
    franquiciaId: null,
  })

  // Convertir filtros para API (debe estar antes de los hooks que lo usan)
  const apiFilters = {
    fechaDesde: filters.fechaDesde,
    fechaHasta: filters.fechaHasta,
    franquiciaId: filters.franquiciaId || undefined,
  }

  // Hooks de datos - ahora dashboard usa los mismos filtros
  const { data: dashboardData, loading, error, refetch } = useHomeDashboard(apiFilters)
  const { data: ventasResumen } = useVentasResumen(apiFilters)
  const { data: ventasPorTipo } = useVentasPorTipoPlato(apiFilters)
  const { data: ventasPorProducto } = useVentasPorProducto(apiFilters)
  // v1.3: Datos para ClockChart
  const { data: ventasPorHora } = useVentasPorHora(apiFilters)
  const { modalState, closeModal } = useDetailModal()

  // Calcular dias en el rango
  const diasRango = useMemo(() => {
    return Math.ceil(
      (new Date(filters.fechaHasta).getTime() - new Date(filters.fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
    ) + 1
  }, [filters.fechaDesde, filters.fechaHasta])

  // Filtrar datos del home dashboard (para lista de franquicias)
  const filteredData = useMemo(() => {
    if (!dashboardData) return []
    return dashboardData.filter(f => {
      if (filters.pais && f.pais !== filters.pais) return false
      if (filters.franquiciaId && f.franquicia_id !== filters.franquiciaId) return false
      return true
    })
  }, [dashboardData, filters])

  // Datos para la tabla: usar directamente dashboardData (ya viene filtrado del backend)
  const tableData = useMemo(() => {
    if (!dashboardData) return []

    // Obtener venta de Palermo para calcular % vs Palermo
    const palermo = dashboardData.find(f => f.franquicia_codigo === 'LC_PALERMO')
    const ventaPalermoRef = palermo?.venta_neta || 0

    // Filtrar por país si es necesario (filtro local adicional)
    let data = dashboardData
    if (filters.pais) {
      data = data.filter(f => f.pais === filters.pais)
    }

    return data.map(f => {
      const venta_bruta = f.venta_bruta || 0  // Ya viene en USD (con impuestos)
      const venta_neta = f.venta_neta || 0  // Ya viene en USD
      const venta_neta_local = f.venta_neta_local || 0
      const total_tickets = f.total_tickets || 0
      const total_cubiertos = f.total_cubiertos || 0
      const dias_con_tasa = f.dias_con_tasa || 0
      const dias_sin_tasa = f.dias_sin_tasa || 0
      const calidad = f.calidad_conversion || (dias_sin_tasa === 0 ? 'OK' : dias_sin_tasa < dias_con_tasa ? 'PARCIAL' : 'SIN_TASA')
      // % vs Palermo: (venta franquicia / venta palermo) * 100
      const pct_vs_palermo = ventaPalermoRef > 0 ? (venta_neta / ventaPalermoRef) * 100 : 0
      return {
        franquicia_id: f.franquicia_id,
        franquicia_codigo: f.franquicia_codigo,
        franquicia_nombre: f.franquicia_nombre,
        grupo_economico_nombre: f.grupo_economico_nombre,
        pais: f.pais,
        ciudad: f.ciudad,
        moneda_codigo: f.moneda_codigo,
        venta_bruta,  // USD - Con impuestos
        venta_neta,  // USD - Período seleccionado
        venta_neta_local,
        total_tickets,
        total_cubiertos,
        ticket_promedio: total_tickets > 0 ? venta_bruta / total_tickets : 0,  // Usar venta bruta (con impuestos)
        tickets_por_dia: diasRango > 0 ? total_tickets / diasRango : 0,
        cubiertos_por_ticket: total_tickets > 0 ? total_cubiertos / total_tickets : 0,
        cubiertos_por_dia: diasRango > 0 ? total_cubiertos / diasRango : 0,
        pct_vs_palermo,
        calidad_conversion: calidad,
        dias_con_tasa,
        dias_sin_tasa,
        // Nuevos campos por período
        venta_mes_actual: f.venta_mes_actual || 0,
        tickets_mes_actual: f.tickets_mes_actual || 0,
        cubiertos_mes_actual: f.cubiertos_mes_actual || 0,
        venta_mes_anterior: f.venta_mes_anterior || 0,
        tickets_mes_anterior: f.tickets_mes_anterior || 0,
        cubiertos_mes_anterior: f.cubiertos_mes_anterior || 0,
        venta_acum_anio_previo: f.venta_acum_anio_previo || 0,
        tickets_acum_anio_previo: f.tickets_acum_anio_previo || 0,
        cubiertos_acum_anio_previo: f.cubiertos_acum_anio_previo || 0,
        venta_acum_anio_actual: f.venta_acum_anio_actual || 0,
        tickets_acum_anio_actual: f.tickets_acum_anio_actual || 0,
        cubiertos_acum_anio_actual: f.cubiertos_acum_anio_actual || 0,
      }
    })
  }, [dashboardData, filters.pais, diasRango])

  // Definición de columnas para la tabla
  type TableRow = typeof tableData[number]
  const tableColumns: ColumnDef<TableRow>[] = useMemo(() => [
    {
      id: 'franquicia_nombre',
      header: 'Franquicia',
      accessorKey: 'franquicia_nombre',
      sortable: true,
      cell: (row) => (
        <span className="font-medium text-gray-900 dark:text-white">{row.franquicia_nombre}</span>
      ),
    },
    {
      id: 'grupo',
      header: 'Grupo',
      accessorKey: 'grupo_economico_nombre',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground">{row.grupo_economico_nombre || '-'}</span>
      ),
    },
    {
      id: 'pais',
      header: 'País',
      accessorKey: 'pais',
      sortable: true,
      defaultVisible: false,
      cell: (row) => row.pais || '-',
    },
    {
      id: 'ciudad',
      header: 'Ciudad',
      accessorKey: 'ciudad',
      sortable: true,
      defaultVisible: false,
      cell: (row) => row.ciudad || '-',
    },
    {
      id: 'venta_neta',
      header: `Venta (Periodo)`,
      accessorKey: 'venta_neta',
      align: 'right',
      sortable: true,
      cell: (row) => (
        <span className="font-medium text-green-600 dark:text-green-400" title={`Período: ${filters.fechaDesde} a ${filters.fechaHasta}`}>
          {formatCurrency(row.venta_neta, 'USD')}
        </span>
      ),
    },
    {
      id: 'venta_mes_anterior',
      header: 'Venta Mes Ant.',
      accessorKey: 'venta_mes_anterior',
      align: 'right',
      sortable: true,
      cell: (row) => (
        <span className="text-muted-foreground" title="Mes anterior completo">
          {formatCurrency(row.venta_mes_anterior, 'USD')}
        </span>
      ),
    },
    {
      id: 'venta_acum_anio_previo',
      header: 'Venta (YTD Ant)',
      accessorKey: 'venta_acum_anio_previo',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground" title="Acumulado año anterior hasta mismo mes/día">
          {formatCurrency(row.venta_acum_anio_previo, 'USD')}
        </span>
      ),
    },
    {
      id: 'venta_acum_anio_actual',
      header: 'Venta (YTD)',
      accessorKey: 'venta_acum_anio_actual',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="font-medium text-emerald-600 dark:text-emerald-400" title="Acumulado año actual">
          {formatCurrency(row.venta_acum_anio_actual, 'USD')}
        </span>
      ),
    },
    {
      id: 'venta_neta_local',
      header: 'Venta Local',
      accessorKey: 'venta_neta_local',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground text-sm">
          {formatCurrency(row.venta_neta_local, row.moneda_codigo || 'USD')}
        </span>
      ),
    },
    {
      id: 'total_tickets',
      header: 'Tickets (Periodo)',
      accessorKey: 'total_tickets',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span title={`Período: ${filters.fechaDesde} a ${filters.fechaHasta}`}>
          {formatNumber(row.total_tickets)}
        </span>
      ),
    },
    {
      id: 'tickets_mes_anterior',
      header: 'Tickets Mes Ant.',
      accessorKey: 'tickets_mes_anterior',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground" title="Mes anterior completo">
          {formatNumber(row.tickets_mes_anterior)}
        </span>
      ),
    },
    {
      id: 'tickets_acum_anio_actual',
      header: 'Tickets (YTD)',
      accessorKey: 'tickets_acum_anio_actual',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span title="Acumulado año actual">
          {formatNumber(row.tickets_acum_anio_actual)}
        </span>
      ),
    },
    {
      id: 'ticket_promedio',
      header: 'Ticket Prom.',
      accessorKey: 'ticket_promedio',
      align: 'right',
      sortable: true,
      cell: (row) => formatCurrency(row.ticket_promedio, 'USD'),
    },
    {
      id: 'pct_vs_palermo',
      header: '% vs Palermo',
      accessorKey: 'pct_vs_palermo',
      align: 'right',
      sortable: true,
      cell: (row) => {
        const pct = row.pct_vs_palermo
        const isPalermo = row.franquicia_codigo === 'LC_PALERMO'
        if (isPalermo) {
          return <span className="font-bold text-amber-600 dark:text-amber-400">100% (Ref)</span>
        }
        const colorClass = pct >= 100
          ? 'text-green-600 dark:text-green-400'
          : pct >= 80
          ? 'text-amber-600 dark:text-amber-400'
          : 'text-red-600 dark:text-red-400'
        return (
          <span className={`font-medium ${colorClass}`} title={`Comparado con Palermo Central`}>
            {formatNumber(pct, 1)}%
          </span>
        )
      },
    },
    {
      id: 'calidad_conversion',
      header: 'TC',
      accessorKey: 'calidad_conversion',
      align: 'center',
      sortable: true,
      defaultVisible: false,
      cell: (row) => {
        const calidad = row.calidad_conversion
        const colorClass = calidad === 'OK'
          ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
          : calidad === 'PARCIAL'
          ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
          : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
        return (
          <span className={`px-2 py-0.5 text-xs font-medium rounded ${colorClass}`} title={`Días con tasa: ${row.dias_con_tasa}, sin tasa: ${row.dias_sin_tasa}`}>
            {calidad}
          </span>
        )
      },
    },
    {
      id: 'tickets_por_dia',
      header: 'Tickets/Día',
      accessorKey: 'tickets_por_dia',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground">{formatNumber(row.tickets_por_dia, 1)}</span>
      ),
    },
    {
      id: 'total_cubiertos',
      header: 'Cubiertos (Periodo)',
      accessorKey: 'total_cubiertos',
      align: 'right',
      sortable: true,
      cell: (row) => (
        <span title={`Período: ${filters.fechaDesde} a ${filters.fechaHasta}`}>
          {formatNumber(row.total_cubiertos)}
        </span>
      ),
    },
    {
      id: 'cubiertos_mes_anterior',
      header: 'Cub. Mes Ant.',
      accessorKey: 'cubiertos_mes_anterior',
      align: 'right',
      sortable: true,
      cell: (row) => (
        <span className="text-muted-foreground" title="Mes anterior completo">
          {formatNumber(row.cubiertos_mes_anterior)}
        </span>
      ),
    },
    {
      id: 'cubiertos_acum_anio_actual',
      header: 'Cub. (YTD)',
      accessorKey: 'cubiertos_acum_anio_actual',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span title="Acumulado año actual">
          {formatNumber(row.cubiertos_acum_anio_actual)}
        </span>
      ),
    },
    {
      id: 'cubiertos_por_ticket',
      header: 'Cub/Ticket',
      accessorKey: 'cubiertos_por_ticket',
      align: 'right',
      sortable: true,
      defaultVisible: false,
      cell: (row) => (
        <span className="text-muted-foreground">{formatNumber(row.cubiertos_por_ticket, 1)}</span>
      ),
    },
    {
      id: 'cubiertos_por_dia',
      header: 'Cub/Día',
      accessorKey: 'cubiertos_por_dia',
      align: 'right',
      sortable: true,
      cell: (row) => (
        <span className="text-muted-foreground">{formatNumber(row.cubiertos_por_dia, 1)}</span>
      ),
    },
  ], [filters.fechaDesde, filters.fechaHasta])

  // Lista de franquicias para el filtro
  const franquiciasParaFiltro = useMemo(() => {
    return dashboardData?.map(f => ({
      id: f.franquicia_id,
      nombre: f.franquicia_nombre,
      pais: f.pais || '',
    })) || []
  }, [dashboardData])

  // Calcular totales usando datos del dashboard (ya filtrados por fecha en el backend)
  const totals = useMemo(() => {
    if (!dashboardData) return { ventaBruta: 0, ventaNeta: 0, tickets: 0, cubiertos: 0 }

    return dashboardData.reduce(
      (acc, f) => ({
        ventaBruta: acc.ventaBruta + (f.venta_bruta || 0),
        ventaNeta: acc.ventaNeta + (f.venta_neta || 0),
        tickets: acc.tickets + (f.total_tickets || 0),
        cubiertos: acc.cubiertos + (f.total_cubiertos || 0),
      }),
      { ventaBruta: 0, ventaNeta: 0, tickets: 0, cubiertos: 0 }
    )
  }, [dashboardData])

  // Obtener datos de Palermo (Central) para comparación
  const palermoData = useMemo(() => {
    if (!dashboardData) return null
    // Buscar franquicia Palermo por código LC_PALERMO
    return dashboardData.find(f => f.franquicia_codigo === 'LC_PALERMO') || null
  }, [dashboardData])

  const ventaPalermo = palermoData?.venta_neta || 0

  // Footer de totales simplificado (los totales completos están en los KPIs)
  const tableFooter = null // Deshabilitado - los totales ya se muestran en los KPI cards

  // Preparar datos para grafico de lineas (basado en métrica seleccionada)
  const chartData = useMemo(() => {
    if (!ventasResumen) return []

    // Función para obtener el valor de métrica del item de resumen
    const getMetricValue = (item: typeof ventasResumen[number], metricId: string): number => {
      // Para ventas usar USD si está disponible
      const ventaUsd = item.venta_neta_usd ?? item.venta_neta ?? 0
      const tickets = item.cantidad_tickets || 0
      const cubiertos = item.total_cubiertos || 0

      switch (metricId) {
        case 'venta_neta':
          return ventaUsd
        case 'tickets':
          return tickets
        case 'ticket_promedio':
          return item.ticket_promedio || (tickets > 0 ? ventaUsd / tickets : 0)
        case 'tickets_dia':
          return tickets
        case 'cubiertos':
          return cubiertos
        case 'cub_ticket':
          return tickets > 0 ? cubiertos / tickets : 0
        case 'cub_dia':
          return cubiertos
        case 'mesas_ocupadas':
          return (item as unknown as Record<string, number>).mesas_ocupadas || 0
        case 'porcentaje_ocupacion':
          return (item as unknown as Record<string, number>).porcentaje_ocupacion || 0
        default:
          return 0
      }
    }

    const result = ventasResumen.reduce((acc, item) => {
      if (!item.fecha_negocio) return acc
      const fechaStr = item.fecha_negocio.split('T')[0]
      const metricValue = getMetricValue(item, selectedMetric)

      const existing = acc.find(a => a.fecha === fechaStr)
      if (existing) {
        existing[item.franquicia_codigo] = metricValue
      } else {
        acc.push({
          fecha: fechaStr,
          [item.franquicia_codigo]: metricValue,
        })
      }
      return acc
    }, [] as { fecha: string; [key: string]: string | number }[])

    // Ordenar por fecha ascendente
    return result.sort((a, b) => a.fecha.localeCompare(b.fecha))
  }, [ventasResumen, selectedMetric])

  // Preparar datos para gráfico de ventas por franquicia
  const ventasPorFranquiciaData = useMemo(() => {
    if (!dashboardData) return []

    return dashboardData
      .map(f => ({
        name: f.franquicia_nombre,
        value: f.venta_neta || 0,
        code: f.franquicia_codigo,
      }))
      .sort((a, b) => b.value - a.value)
  }, [dashboardData])

  // Preparar datos para gráfico de tipos de plato (barras horizontales)
  // Agrupar por tipo de plato sumando ventas de todas las franquicias
  // Filtrar por franquicia si hay una seleccionada
  const tiposPlatoData = useMemo(() => {
    if (!ventasPorTipo) return []

    // Filtrar por franquicia si hay selección (usando franquicia_nombre)
    let data = ventasPorTipo
    if (selectedFranquiciaChart) {
      data = ventasPorTipo.filter(t => t.franquicia_nombre === selectedFranquiciaChart)
    }

    const grouped = data.reduce((acc, t) => {
      const name = t.tipo_plato_nombre
      if (!acc[name]) {
        acc[name] = { name, value: 0 }
      }
      acc[name].value += t.venta_neta || 0
      return acc
    }, {} as Record<string, { name: string; value: number }>)

    return Object.values(grouped).sort((a, b) => b.value - a.value)
  }, [ventasPorTipo, selectedFranquiciaChart])

  // Preparar datos para Top 10 Productos (filtrados por franquicia y/o tipo de plato)
  const top10ProductosData = useMemo(() => {
    if (!ventasPorProducto) return []

    let productos = ventasPorProducto

    // Filtrar por franquicia si hay selección (usando franquicia_nombre)
    if (selectedFranquiciaChart) {
      productos = productos.filter(p => p.franquicia_nombre === selectedFranquiciaChart)
    }

    // Filtrar por tipo de plato seleccionado
    if (selectedTipoPlato) {
      productos = productos.filter(p => p.categoria === selectedTipoPlato)
    }

    // Calcular total para porcentajes
    const total = productos.reduce((sum, p) => sum + (p.venta_neta || 0), 0)

    // Ordenar por venta y tomar top 10
    return productos
      .sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0))
      .slice(0, 10)
      .map(p => ({
        name: p.producto_nombre,
        value: p.venta_neta || 0,
        percent: total > 0 ? ((p.venta_neta || 0) / total) * 100 : 0,
      }))
  }, [ventasPorProducto, selectedTipoPlato, selectedFranquiciaChart])

  // v1.3: Preparar datos para ClockChart (agregados por hora)
  const clockChartData = useMemo(() => {
    if (!ventasPorHora) return []

    // Filtrar por franquicia si hay selección
    let data = ventasPorHora
    if (selectedFranquiciaChart) {
      data = data.filter(h => h.franquicia_nombre === selectedFranquiciaChart)
    }

    // Agrupar por hora (sumando todas las franquicias si no hay filtro)
    const byHour = data.reduce((acc, item) => {
      const hora = item.hora
      if (!acc[hora]) {
        acc[hora] = { hora, cubiertos: 0, tickets: 0, venta: 0 }
      }
      acc[hora].cubiertos += item.cubiertos || 0
      acc[hora].tickets += item.tickets || 0
      acc[hora].venta += item.venta_neta || 0
      return acc
    }, {} as Record<number, { hora: number; cubiertos: number; tickets: number; venta: number }>)

    return Object.values(byHour)
  }, [ventasPorHora, selectedFranquiciaChart])

  // Handler para selección de franquicia en el gráfico
  const handleFranquiciaChartClick = (item: { name: string } | null) => {
    const newValue = item?.name || null
    setSelectedFranquiciaChart(newValue)
    // Resetear tipo de plato y hora cuando cambia la franquicia
    if (newValue !== selectedFranquiciaChart) {
      setSelectedTipoPlato(null)
      setSelectedHour(null)
    }
  }

  // Handler para selección de tipo de plato
  const handleTipoPlatoClick = (item: { name: string } | null) => {
    setSelectedTipoPlato(item?.name || null)
  }

  // v1.3: Handler para selección de hora en ClockChart
  const handleHourClick = (hour: number | null) => {
    setSelectedHour(hour)
  }

  // Handler para click en fila de franquicia
  const handleFranquiciaClick = (row: TableRow) => {
    setSelectedFranquicia({
      franquicia_id: row.franquicia_id,
      franquicia_codigo: row.franquicia_codigo,
      franquicia_nombre: row.franquicia_nombre,
      grupo_economico_nombre: row.grupo_economico_nombre,
      pais: row.pais,
      ciudad: row.ciudad,
      moneda_codigo: row.moneda_codigo,
      venta_neta: row.venta_neta,
      venta_neta_local: row.venta_neta_local,
      total_tickets: row.total_tickets,
      total_cubiertos: row.total_cubiertos,
      ticket_promedio: row.ticket_promedio,
      venta_mes_actual: row.venta_mes_actual,
      tickets_mes_actual: row.tickets_mes_actual,
      venta_mes_anterior: row.venta_mes_anterior,
      tickets_mes_anterior: row.tickets_mes_anterior,
      venta_acum_anio_actual: row.venta_acum_anio_actual,
      tickets_acum_anio_actual: row.tickets_acum_anio_actual,
    })
  }

  // Early returns DESPUÉS de todos los hooks
  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Dashboard General</h1>
        <p className="text-muted-foreground">Vista consolidada de ventas por franquicia</p>
      </div>

      {/* Filtros Globales */}
      <FilterBar
        franquicias={franquiciasParaFiltro}
        onFilterChange={setFilters}
        initialFilters={filters}
        onExportClick={() => setExportModalOpen(true)}
      />

      {/* Indicador de Moneda USD */}
      <div className="flex items-center gap-2 px-4 py-2 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
        <DollarSign className="w-4 h-4 text-green-600 dark:text-green-400" />
        <span className="text-sm font-medium text-green-700 dark:text-green-300">
          Consolidado en USD
        </span>
        <span className="text-sm text-green-600 dark:text-green-400">
          — Valores convertidos usando tipo de cambio de cada fecha de transacción
        </span>
        <a
          href="/admin/tipos-cambio"
          className="ml-auto text-xs text-green-600 dark:text-green-400 hover:underline flex items-center gap-1"
        >
          <Info className="w-3 h-3" />
          Ver tipos de cambio
        </a>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <KpiCard
          title="Venta Neta (Periodo)"
          value={totals.ventaNeta}
          format="currency"
          icon={DollarSign}
          iconColor="bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400"
        />
        <KpiCard
          title="Tickets (Periodo)"
          value={totals.tickets}
          icon={Ticket}
          iconColor="bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400"
        />
        <KpiCard
          title="Cubiertos (Periodo)"
          value={totals.cubiertos}
          icon={Users}
          iconColor="bg-purple-100 dark:bg-purple-900/30 text-purple-600 dark:text-purple-400"
        />
        <KpiCard
          title="Ticket Promedio"
          value={totals.tickets > 0 ? totals.ventaBruta / totals.tickets : 0}
          format="currency"
          icon={TrendingUp}
          iconColor="bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400"
        />
        <KpiCard
          title="Palermo (Referencia)"
          value={ventaPalermo}
          format="currency"
          icon={Store}
          iconColor="bg-white/30 text-white"
          subtitle={ventaPalermo > 0 ? 'Central de comparación' : 'Sin datos disponibles'}
          className="bg-gradient-to-br from-emerald-500 to-emerald-700 text-white border-emerald-600 [&_p]:text-white [&_.text-muted-foreground]:text-emerald-100"
        />
      </div>

      {/* Tabla Resumen por Franquicia con selector de columnas, búsqueda y ordenamiento */}
      <EnhancedDataTable
        title="Resumen por Franquicia"
        subtitle="Click en una fila para ver detalle | Arrastra columnas para reordenar"
        data={tableData}
        columns={tableColumns}
        vistaId="dashboard-resumen-franquicia"
        searchPlaceholder="Buscar franquicia, país, ciudad..."
        footerRow={tableFooter}
        defaultSortColumn="venta_neta"
        defaultSortDirection="desc"
        onRowClick={handleFranquiciaClick}
        stickyFirstColumn={true}
      />

      {/* Indicador de filtros activos - más visible */}
      {(selectedFranquiciaChart || selectedTipoPlato || selectedHour !== null) && (
        <div className="flex items-center gap-4 flex-wrap p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
          <span className="text-base font-medium text-blue-800 dark:text-blue-200">Filtros activos:</span>
          {selectedFranquiciaChart && (
            <button
              onClick={() => {
                setSelectedFranquiciaChart(null)
                setSelectedTipoPlato(null)
                setSelectedHour(null)
              }}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-200 dark:bg-blue-800 text-blue-800 dark:text-blue-100 rounded-lg text-base font-medium hover:bg-blue-300 dark:hover:bg-blue-700"
            >
              Franquicia: {selectedFranquiciaChart}
              <span className="text-lg font-bold">×</span>
            </button>
          )}
          {selectedTipoPlato && (
            <button
              onClick={() => setSelectedTipoPlato(null)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-purple-200 dark:bg-purple-800 text-purple-800 dark:text-purple-100 rounded-lg text-base font-medium hover:bg-purple-300 dark:hover:bg-purple-700"
            >
              Tipo: {selectedTipoPlato}
              <span className="text-lg font-bold">×</span>
            </button>
          )}
          {selectedHour !== null && (
            <button
              onClick={() => setSelectedHour(null)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-amber-200 dark:bg-amber-800 text-amber-800 dark:text-amber-100 rounded-lg text-base font-medium hover:bg-amber-300 dark:hover:bg-amber-700"
            >
              Hora: {selectedHour}:00
              <span className="text-lg font-bold">×</span>
            </button>
          )}
          <button
            onClick={() => {
              setSelectedFranquiciaChart(null)
              setSelectedTipoPlato(null)
              setSelectedHour(null)
            }}
            className="text-base font-medium text-blue-600 dark:text-blue-400 hover:text-blue-800 dark:hover:text-blue-200 underline ml-auto"
          >
            Limpiar todo
          </button>
        </div>
      )}

      {/* Gráficos en cascada: Franquicia → Tipo de Plato → Productos */}
      {/* Primera fila: Franquicia y Tipo de Plato */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        <HorizontalBarChart
          title="Ventas por Franquicia"
          subtitle="Toque una barra para filtrar los otros gráficos"
          data={ventasPorFranquiciaData}
          onBarClick={handleFranquiciaChartClick}
          selectedItem={selectedFranquiciaChart}
          maxBars={12}
        />
        <HorizontalBarChart
          title={selectedFranquiciaChart ? `Tipo de Plato - ${selectedFranquiciaChart}` : "Ventas por Tipo de Plato"}
          subtitle={selectedFranquiciaChart ? "Toque para filtrar productos" : "Toque una barra para filtrar productos"}
          data={tiposPlatoData}
          onBarClick={handleTipoPlatoClick}
          selectedItem={selectedTipoPlato}
          maxBars={10}
        />
      </div>

      {/* Segunda fila: Top Productos */}
      <HorizontalBarChart
        title={
          selectedTipoPlato
            ? `Top 10 Productos - ${selectedTipoPlato}`
            : selectedFranquiciaChart
              ? `Top 10 Productos - ${selectedFranquiciaChart}`
              : "Top 10 Productos"
        }
        subtitle={
          selectedTipoPlato || selectedFranquiciaChart || selectedHour !== null
            ? "Filtrado por selección activa"
            : "Todos los productos de todas las franquicias"
        }
        data={top10ProductosData}
        showPercent={true}
        maxBars={10}
      />

      {/* Tercera fila: ClockChart (Consumo por Hora) - Dos relojes AM/PM */}
      <ClockChart
        title={selectedFranquiciaChart ? `Consumo por Hora - ${selectedFranquiciaChart}` : "Consumo por Hora"}
        subtitle="Toque un segmento para filtrar por hora"
        data={clockChartData}
        selectedHour={selectedHour}
        onHourClick={handleHourClick}
        metric="cubiertos"
      />

      {/* Gráfico de Tendencia - Al final del dashboard */}
      <SalesLineChart
        title="Tendencia (Últimos 30 días)"
        data={chartData.slice(-30)}
        dataKeys={
          filteredData.map(f => ({
            key: f.franquicia_codigo,
            name: f.franquicia_nombre,
          }))
        }
        metrics={TREND_METRICS}
        selectedMetric={selectedMetric}
        onMetricChange={setSelectedMetric}
      />

      {/* Modal de Detalle */}
      <DetailModal
        isOpen={modalState.isOpen}
        onClose={closeModal}
        title={modalState.title}
        subtitle={modalState.subtitle}
        data={modalState.data}
        currency={modalState.currency}
      />

      {/* Modal de Detalle de Franquicia */}
      <FranquiciaDetailModal
        isOpen={selectedFranquicia !== null}
        onClose={() => setSelectedFranquicia(null)}
        franquicia={selectedFranquicia}
        fechaDesde={filters.fechaDesde}
        fechaHasta={filters.fechaHasta}
      />

      {/* Modal de Exportación a Excel */}
      <ExportExcelModal
        open={exportModalOpen}
        onOpenChange={setExportModalOpen}
        filters={filters}
        franquicias={franquiciasParaFiltro}
      />
    </div>
  )
}
