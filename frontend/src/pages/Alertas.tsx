import { useState, useMemo } from 'react'
import { AlertTriangle, Calendar, DollarSign, Ticket, Users, X } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useHomeDashboard, useEstadoIntegracion } from '@/hooks/useDashboard'
import { dashboardApi } from '@/services/api'
import { formatCurrency, formatNumber } from '@/lib/utils'
import { LoadingPage } from '@/components/dashboard/LoadingState'

type MetricType = 'venta' | 'cubiertos' | 'tickets'
type FiltroConexion = 'todas' | 'activas' | 'sin'

interface DayData {
  fecha: string
  // Venta en USD; null si ese día la moneda no tiene cotización (no se suma moneda local como dólares)
  venta_neta: number | null
  total_tickets: number
  total_cubiertos: number
}

interface FranquiciaDayData {
  franquicia_id: number
  franquicia_nombre: string
  franquicia_codigo: string
  days: Record<string, DayData>
}

const PAISES: Record<string, string> = {
  spain: 'España', espana: 'España', 'españa': 'España',
  brazil: 'Brasil', brasil: 'Brasil',
  usa: 'EE.UU.', 'united states': 'EE.UU.', 'estados unidos': 'EE.UU.',
  mexico: 'México', 'méxico': 'México',
  peru: 'Perú', 'perú': 'Perú',
  philippines: 'Filipinas', filipinas: 'Filipinas',
}

function nombrePais(pais: string | null): string {
  const p = pais?.trim()
  if (!p) return 'Sin país'
  return PAISES[p.toLowerCase()] ?? p
}

export function Alertas() {
  // Filtros de fecha - por defecto mes actual
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

  const [selectedYear, setSelectedYear] = useState(new Date().getFullYear())
  const [selectedMonth, setSelectedMonth] = useState(new Date().getMonth())
  const [metric, setMetric] = useState<MetricType>('venta')
  const [dailyData, setDailyData] = useState<FranquiciaDayData[]>([])
  const [loading, setLoading] = useState(false)
  const [filtroConexion, setFiltroConexion] = useState<FiltroConexion>('todas')
  const [filtroPais, setFiltroPais] = useState('')
  const [filtroFranquicia, setFiltroFranquicia] = useState('')
  const [filtroGrupo, setFiltroGrupo] = useState('')
  const [filtroContacto, setFiltroContacto] = useState('')

  // Conexión activa = tiene un conector configurado (mismo criterio que Estado de Integración)
  const { data: estadoIntegracion } = useEstadoIntegracion()
  const conectadas = useMemo(() => new Set(
    (estadoIntegracion ?? [])
      .filter(e => (e.estado_integracion ?? 'PENDING').toUpperCase() !== 'PENDING')
      .map(e => e.franquicia_id)
  ), [estadoIntegracion])

  // País, grupo y contacto de cada franquicia (mismos datos que Estado de Integración).
  // El país se normaliza: en la base hay variantes ("Spain"/"Espana", "Brazil"/"Brasil").
  const infoFranquicia = useMemo(() => new Map(
    (estadoIntegracion ?? []).map(e => [e.franquicia_id, {
      pais: nombrePais(e.pais),
      grupo: e.grupo_economico_nombre?.trim() || 'Sin grupo',
      contacto: e.contacto_nombre?.trim() || e.contacto_email?.trim() || 'Sin contacto',
    }])
  ), [estadoIntegracion])

  const datosDe = (franquiciaId: number) =>
    infoFranquicia.get(franquiciaId) ?? { pais: 'Sin país', grupo: 'Sin grupo', contacto: 'Sin contacto' }

  // Filtros de país, franquicia, grupo y contacto (vacío = todos)
  const pasaFiltros = (f: FranquiciaDayData) => {
    const d = datosDe(f.franquicia_id)
    return (!filtroPais || d.pais === filtroPais)
      && (!filtroFranquicia || String(f.franquicia_id) === filtroFranquicia)
      && (!filtroGrupo || d.grupo === filtroGrupo)
      && (!filtroContacto || d.contacto === filtroContacto)
  }

  const hayFiltros = !!(filtroPais || filtroFranquicia || filtroGrupo || filtroContacto)
  const limpiarFiltros = () => {
    setFiltroPais('')
    setFiltroFranquicia('')
    setFiltroGrupo('')
    setFiltroContacto('')
  }

  // Get franchises from dashboard
  const { data: dashboardData, loading: loadingFranquicias } = useHomeDashboard({
    fechaDesde: defaultDates.fechaDesde,
    fechaHasta: defaultDates.fechaHasta,
  })

  // Months and years
  const months = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
  ]
  const currentYear = new Date().getFullYear()
  const availableYears = [currentYear, currentYear - 1, currentYear - 2]

  // Calculate days in selected month
  const daysInMonth = useMemo(() => {
    const lastDay = new Date(selectedYear, selectedMonth + 1, 0).getDate()
    return Array.from({ length: lastDay }, (_, i) => i + 1)
  }, [selectedYear, selectedMonth])

  // Load daily data for the selected period
  const loadDailyData = async () => {
    if (!dashboardData || dashboardData.length === 0) return

    setLoading(true)
    try {
      const fechaDesde = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-01`
      const lastDay = new Date(selectedYear, selectedMonth + 1, 0).getDate()
      const fechaHasta = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`

      // Fetch daily data from API
      const resumen = await dashboardApi.getVentasResumen({ fechaDesde, fechaHasta })

      // Group by franchise
      const byFranquicia: Record<number, FranquiciaDayData> = {}

      for (const franquicia of dashboardData) {
        byFranquicia[franquicia.franquicia_id] = {
          franquicia_id: franquicia.franquicia_id,
          franquicia_nombre: franquicia.franquicia_nombre,
          franquicia_codigo: franquicia.franquicia_codigo,
          days: {}
        }
      }

      // Populate with daily data
      for (const item of resumen) {
        const franquiciaId = item.franquicia_id
        if (!byFranquicia[franquiciaId]) continue

        const fecha = item.fecha_negocio?.split('T')[0]
        if (!fecha) continue

        byFranquicia[franquiciaId].days[fecha] = {
          fecha,
          venta_neta: item.venta_neta_usd ?? null,
          total_tickets: item.cantidad_tickets || 0,
          total_cubiertos: item.total_cubiertos || 0,
        }
      }

      setDailyData(Object.values(byFranquicia))
    } catch (error) {
      console.error('Error loading daily data:', error)
    } finally {
      setLoading(false)
    }
  }

  // Load data when period changes
  const handlePeriodChange = () => {
    loadDailyData()
  }

  // Get cell value based on metric
  const getCellValue = (dayData: DayData | undefined): number => {
    if (!dayData) return 0
    switch (metric) {
      case 'venta': return dayData.venta_neta ?? 0
      case 'tickets': return dayData.total_tickets
      case 'cubiertos': return dayData.total_cubiertos
    }
  }

  // Format cell value
  const formatCellValue = (value: number): string => {
    if (value === 0) return '-'
    switch (metric) {
      case 'venta': return `$${Math.round(value).toLocaleString()}`
      case 'tickets': return formatNumber(value)
      case 'cubiertos': return formatNumber(value)
    }
  }

  // Get cell class based on value (red for gaps/zeros)
  const getCellClass = (dayData: DayData | undefined, dayNumber: number): string => {
    const today = new Date()
    const cellDate = new Date(selectedYear, selectedMonth, dayNumber)

    // Don't highlight future dates
    if (cellDate > today) {
      return 'bg-gray-100 dark:bg-gray-800 text-gray-400'
    }

    // Día con ventas pero sin cotización: no hay dólares para mostrar, pero tampoco es un hueco
    if (metric === 'venta' && dayData && dayData.venta_neta === null && dayData.total_tickets > 0) {
      return 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 font-medium'
    }

    const value = getCellValue(dayData)
    if (value === 0 || !dayData) {
      return 'bg-red-200 dark:bg-red-900/50 text-red-700 dark:text-red-300 font-medium'
    }

    return 'bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300'
  }

  const fechaDelDia = (day: number) =>
    `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`

  // Franquicias visibles: filtradas por conexión y ordenadas de más a menos días con datos
  const filas = useMemo(() => {
    const today = new Date()
    // Un día está cargado si tiene ventas o tickets, sin importar la métrica que se esté viendo
    const diasConDatos = (f: FranquiciaDayData) => daysInMonth.filter(day => {
      const d = f.days[fechaDelDia(day)]
      return new Date(selectedYear, selectedMonth, day) <= today && ((d?.total_tickets ?? 0) > 0 || (d?.venta_neta ?? 0) > 0)
    }).length
    const total = (f: FranquiciaDayData) => daysInMonth.reduce((sum, day) => sum + getCellValue(f.days[fechaDelDia(day)]), 0)

    return dailyData
      .filter(pasaFiltros)
      .filter(f => filtroConexion === 'todas'
        || (filtroConexion === 'activas' ? conectadas.has(f.franquicia_id) : !conectadas.has(f.franquicia_id)))
      .map(f => ({ franquicia: f, dias: diasConDatos(f), total: total(f) }))
      .sort((a, b) => b.dias - a.dias || b.total - a.total
        || a.franquicia.franquicia_nombre.localeCompare(b.franquicia.franquicia_nombre))
      .map(x => x.franquicia)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dailyData, filtroConexion, conectadas, daysInMonth, selectedYear, selectedMonth, metric,
      infoFranquicia, filtroPais, filtroFranquicia, filtroGrupo, filtroContacto])

  // Los contadores de conexión respetan los demás filtros
  const filtradas = dailyData.filter(pasaFiltros)
  const cantidadPorFiltro = {
    todas: filtradas.length,
    activas: filtradas.filter(f => conectadas.has(f.franquicia_id)).length,
    sin: filtradas.filter(f => !conectadas.has(f.franquicia_id)).length,
  }

  // Opciones de cada filtro: solo las combinaciones posibles con los otros filtros elegidos
  const opciones = (excluir: 'pais' | 'franquicia' | 'grupo' | 'contacto') => {
    const base = dailyData.filter(f => {
      const d = datosDe(f.franquicia_id)
      return (excluir === 'pais' || !filtroPais || d.pais === filtroPais)
        && (excluir === 'franquicia' || !filtroFranquicia || String(f.franquicia_id) === filtroFranquicia)
        && (excluir === 'grupo' || !filtroGrupo || d.grupo === filtroGrupo)
        && (excluir === 'contacto' || !filtroContacto || d.contacto === filtroContacto)
    })
    if (excluir === 'franquicia') {
      return base
        .map(f => ({ valor: String(f.franquicia_id), etiqueta: f.franquicia_nombre }))
        .sort((a, b) => a.etiqueta.localeCompare(b.etiqueta))
    }
    return [...new Set(base.map(f => datosDe(f.franquicia_id)[excluir]))]
      .sort((a, b) => a.localeCompare(b))
      .map(v => ({ valor: v, etiqueta: v }))
  }

  const filtros = [
    { etiqueta: 'País', valor: filtroPais, set: setFiltroPais, opciones: opciones('pais') },
    { etiqueta: 'Franquicia', valor: filtroFranquicia, set: setFiltroFranquicia, opciones: opciones('franquicia') },
    { etiqueta: 'Grupo', valor: filtroGrupo, set: setFiltroGrupo, opciones: opciones('grupo') },
    { etiqueta: 'Contacto', valor: filtroContacto, set: setFiltroContacto, opciones: opciones('contacto') },
  ]

  // Días con ventas que no se pueden pasar a USD por falta de cotización
  const diasSinCotizacion = useMemo(() => filas.reduce((n, f) =>
    n + Object.values(f.days).filter(d => d.venta_neta === null && d.total_tickets > 0).length, 0), [filas])

  // Count total gaps
  const totalGaps = useMemo(() => {
    const today = new Date()
    let gaps = 0
    for (const franquicia of filas) {
      for (const day of daysInMonth) {
        const cellDate = new Date(selectedYear, selectedMonth, day)
        if (cellDate > today) continue

        const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
        const dayData = franquicia.days[dateStr]
        const sinCotizacion = metric === 'venta' && dayData?.venta_neta === null && (dayData?.total_tickets ?? 0) > 0
        if (!sinCotizacion && (!dayData || getCellValue(dayData) === 0)) {
          gaps++
        }
      }
    }
    return gaps
  }, [filas, daysInMonth, selectedYear, selectedMonth, metric])

  if (loadingFranquicias) {
    return <LoadingPage />
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <AlertTriangle className="h-6 w-6 text-amber-500" />
          Alertas - Detección de Huecos
        </h1>
        <p className="text-muted-foreground">
          Visualización de datos faltantes por franquicia y día
        </p>
      </div>

      {/* Filtro por conexión */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">Ver:</span>
        <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
          {([
            ['todas', 'Todas'],
            ['activas', 'Con conexión activa'],
            ['sin', 'Sin conexión activa'],
          ] as const).map(([valor, etiqueta], i) => (
            <button
              key={valor}
              onClick={() => setFiltroConexion(valor)}
              className={`px-3 py-1.5 text-sm transition-colors ${i > 0 ? 'border-l dark:border-gray-600' : ''} ${
                filtroConexion === valor
                  ? 'bg-cabrera-burgundy text-white'
                  : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
              }`}
            >
              {etiqueta}
              {dailyData.length > 0 && <span className="ml-1 opacity-75">({cantidadPorFiltro[valor]})</span>}
            </button>
          ))}
        </div>
      </div>

      {/* Filtros por país, franquicia, grupo y contacto */}
      <div className="flex flex-wrap items-center gap-3">
        {filtros.map(f => (
          <label key={f.etiqueta} className="flex items-center gap-2 text-sm">
            <span className="font-medium">{f.etiqueta}:</span>
            <select
              value={f.valor}
              onChange={e => f.set(e.target.value)}
              className={`px-2 py-1.5 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white max-w-[220px] ${
                f.valor ? 'border-cabrera-burgundy ring-1 ring-cabrera-burgundy/40' : ''
              }`}
            >
              <option value="">Todos</option>
              {f.opciones.map(o => (
                <option key={o.valor} value={o.valor}>{o.etiqueta}</option>
              ))}
            </select>
          </label>
        ))}
        {hayFiltros && (
          <button
            onClick={limpiarFiltros}
            className="flex items-center gap-1 text-sm text-cabrera-burgundy hover:underline"
          >
            <X className="h-3 w-3" /> Limpiar filtros
          </button>
        )}
      </div>

      {/* Controls */}
      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center gap-4">
            {/* Period selector */}
            <div className="flex items-center gap-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <span className="text-sm font-medium">Período:</span>
              <select
                value={selectedYear}
                onChange={(e) => setSelectedYear(Number(e.target.value))}
                className="px-2 py-1 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {availableYears.map(year => (
                  <option key={year} value={year}>{year}</option>
                ))}
              </select>
              <select
                value={selectedMonth}
                onChange={(e) => setSelectedMonth(Number(e.target.value))}
                className="px-2 py-1 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {months.map((month, idx) => (
                  <option key={idx} value={idx}>{month}</option>
                ))}
              </select>
            </div>

            {/* Metric selector */}
            <div className="flex items-center gap-2 border-l pl-4 dark:border-gray-600">
              <span className="text-sm font-medium">Métrica:</span>
              <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
                <button
                  onClick={() => setMetric('venta')}
                  className={`px-3 py-1.5 text-sm flex items-center gap-1 transition-colors ${
                    metric === 'venta'
                      ? 'bg-cabrera-burgundy text-white'
                      : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                  }`}
                >
                  <DollarSign className="h-3 w-3" />
                  Venta USD
                </button>
                <button
                  onClick={() => setMetric('tickets')}
                  className={`px-3 py-1.5 text-sm flex items-center gap-1 border-l dark:border-gray-600 transition-colors ${
                    metric === 'tickets'
                      ? 'bg-cabrera-burgundy text-white'
                      : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                  }`}
                >
                  <Ticket className="h-3 w-3" />
                  Tickets
                </button>
                <button
                  onClick={() => setMetric('cubiertos')}
                  className={`px-3 py-1.5 text-sm flex items-center gap-1 border-l dark:border-gray-600 transition-colors ${
                    metric === 'cubiertos'
                      ? 'bg-cabrera-burgundy text-white'
                      : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                  }`}
                >
                  <Users className="h-3 w-3" />
                  Cubiertos
                </button>
              </div>
            </div>

            {/* Load button */}
            <Button
              onClick={handlePeriodChange}
              disabled={loading}
              className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
            >
              {loading ? 'Cargando...' : 'Cargar Datos'}
            </Button>

            {/* Días sin cotización */}
            {dailyData.length > 0 && metric === 'venta' && diasSinCotizacion > 0 && (
              <div className="flex items-center gap-2 px-3 py-1.5 bg-amber-100 dark:bg-amber-900/30 rounded-lg">
                <span className="text-sm font-medium text-amber-700 dark:text-amber-300">
                  {diasSinCotizacion} días con ventas sin cotización (s/TC, no suman en USD)
                </span>
              </div>
            )}

            {/* Gap counter */}
            {dailyData.length > 0 && (
              <div className="ml-auto flex items-center gap-2 px-3 py-1.5 bg-red-100 dark:bg-red-900/30 rounded-lg">
                <AlertTriangle className="h-4 w-4 text-red-600 dark:text-red-400" />
                <span className="text-sm font-medium text-red-700 dark:text-red-300">
                  {totalGaps} huecos detectados
                </span>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Data Grid */}
      {dailyData.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">
              Datos por Día - {months[selectedMonth]} {selectedYear}
            </CardTitle>
          </CardHeader>
          <CardContent className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead className="sticky top-0 bg-gray-100 dark:bg-gray-800 z-10">
                <tr>
                  <th className="px-2 py-2 text-left font-semibold sticky left-0 bg-gray-100 dark:bg-gray-800 min-w-[150px]">
                    Franquicia
                  </th>
                  {daysInMonth.map(day => (
                    <th key={day} className="px-1 py-2 text-center font-semibold min-w-[60px]">
                      {day}
                    </th>
                  ))}
                  <th className="px-2 py-2 text-right font-semibold min-w-[80px]">
                    Total
                  </th>
                </tr>
              </thead>
              <tbody>
                {filas.map(franquicia => {
                  // Calculate row total
                  let rowTotal = 0
                  for (const day of daysInMonth) {
                    const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                    rowTotal += getCellValue(franquicia.days[dateStr])
                  }

                  return (
                    <tr key={franquicia.franquicia_id} className="border-t dark:border-gray-700">
                      <td className="px-2 py-2 font-medium sticky left-0 bg-white dark:bg-gray-900 z-5">
                        {franquicia.franquicia_nombre}
                      </td>
                      {daysInMonth.map(day => {
                        const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                        const dayData = franquicia.days[dateStr]
                        const cellClass = getCellClass(dayData, day)

                        return (
                          <td
                            key={day}
                            className={`px-1 py-2 text-center ${cellClass}`}
                            title={`${franquicia.franquicia_nombre} - ${dateStr}`}
                          >
                            {metric === 'venta' && dayData && dayData.venta_neta === null && dayData.total_tickets > 0
                              ? 's/TC'
                              : formatCellValue(getCellValue(dayData))}
                          </td>
                        )
                      })}
                      <td className="px-2 py-2 text-right font-semibold bg-gray-50 dark:bg-gray-800">
                        {metric === 'venta'
                          ? formatCurrency(rowTotal, 'USD')
                          : formatNumber(rowTotal)
                        }
                      </td>
                    </tr>
                  )
                })}
              </tbody>
              <tfoot className="bg-gray-100 dark:bg-gray-800 font-semibold">
                <tr>
                  <td className="px-2 py-2 sticky left-0 bg-gray-100 dark:bg-gray-800">
                    TOTAL
                  </td>
                  {daysInMonth.map(day => {
                    let colTotal = 0
                    for (const franquicia of filas) {
                      const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                      colTotal += getCellValue(franquicia.days[dateStr])
                    }
                    return (
                      <td key={day} className="px-1 py-2 text-center">
                        {formatCellValue(colTotal)}
                      </td>
                    )
                  })}
                  <td className="px-2 py-2 text-right">
                    {metric === 'venta'
                      ? formatCurrency(
                          filas.reduce((sum, f) => {
                            let total = 0
                            for (const day of daysInMonth) {
                              const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                              total += getCellValue(f.days[dateStr])
                            }
                            return sum + total
                          }, 0),
                          'USD'
                        )
                      : formatNumber(
                          filas.reduce((sum, f) => {
                            let total = 0
                            for (const day of daysInMonth) {
                              const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                              total += getCellValue(f.days[dateStr])
                            }
                            return sum + total
                          }, 0)
                        )
                    }
                  </td>
                </tr>
              </tfoot>
            </table>

            {/* Legend */}
            <div className="mt-4 flex items-center gap-6 text-xs text-muted-foreground">
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 bg-red-200 dark:bg-red-900/50 rounded" />
                <span>Sin datos (Hueco)</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded" />
                <span>Con datos</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 bg-gray-100 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded" />
                <span>Fecha futura</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 bg-amber-100 dark:bg-amber-900/30 rounded" />
                <span>s/TC: con ventas pero sin cotización de la moneda (no suma en USD)</span>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="p-12 text-center">
            <AlertTriangle className="h-12 w-12 mx-auto text-amber-500 mb-4" />
            <p className="text-lg font-medium text-gray-900 dark:text-white mb-2">
              Seleccione un período y haga clic en "Cargar Datos"
            </p>
            <p className="text-muted-foreground">
              La grilla mostrará los datos diarios de cada franquicia con alertas en rojo para días sin información.
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
