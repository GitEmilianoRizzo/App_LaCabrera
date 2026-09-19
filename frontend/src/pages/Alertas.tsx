import { useState, useMemo } from 'react'
import { AlertTriangle, Calendar, DollarSign, Ticket, Users } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useHomeDashboard } from '@/hooks/useDashboard'
import { dashboardApi } from '@/services/api'
import { formatCurrency, formatNumber } from '@/lib/utils'
import { LoadingPage } from '@/components/dashboard/LoadingState'

type MetricType = 'venta' | 'cubiertos' | 'tickets'

interface DayData {
  fecha: string
  venta_neta: number
  total_tickets: number
  total_cubiertos: number
}

interface FranquiciaDayData {
  franquicia_id: number
  franquicia_nombre: string
  franquicia_codigo: string
  days: Record<string, DayData>
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
          venta_neta: item.venta_neta_usd || item.venta_neta || 0,
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
      case 'venta': return dayData.venta_neta
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

    const value = getCellValue(dayData)
    if (value === 0 || !dayData) {
      return 'bg-red-200 dark:bg-red-900/50 text-red-700 dark:text-red-300 font-medium'
    }

    return 'bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300'
  }

  // Count total gaps
  const totalGaps = useMemo(() => {
    const today = new Date()
    let gaps = 0
    for (const franquicia of dailyData) {
      for (const day of daysInMonth) {
        const cellDate = new Date(selectedYear, selectedMonth, day)
        if (cellDate > today) continue

        const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
        const dayData = franquicia.days[dateStr]
        if (!dayData || getCellValue(dayData) === 0) {
          gaps++
        }
      }
    }
    return gaps
  }, [dailyData, daysInMonth, selectedYear, selectedMonth, metric])

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
                {dailyData.map(franquicia => {
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
                            {formatCellValue(getCellValue(dayData))}
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
                    for (const franquicia of dailyData) {
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
                          dailyData.reduce((sum, f) => {
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
                          dailyData.reduce((sum, f) => {
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
