import { useState, useMemo, useRef, useEffect } from 'react'
import { DollarSign, Ticket, Users, TrendingUp, Store, Banknote, AlertCircle, Search, ChevronDown, X, Calendar } from 'lucide-react'
import { KpiCard } from '@/components/dashboard/KpiCard'
import { SalesLineChart, MetricOption } from '@/components/charts/SalesLineChart'
import { HorizontalBarChart } from '@/components/charts/HorizontalBarChart'
import { ClockChart } from '@/components/charts/ClockChart'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  useHomeDashboard,
  useVentasResumen,
  useVentasPorTipoPlato,
  useVentasPorProducto,
  useVentasPorHora,
} from '@/hooks/useDashboard'
import { transaccionesApi, dashboardApiV2 } from '@/services/api'
import type { Transaccion, VentasPorProductoConPeso } from '@/types/dashboard'

// Tipo de moneda para visualización
type CurrencyMode = 'USD' | 'LOCAL'

// Tipo de métrica para grilla de datos por día
type DayGridMetric = 'venta' | 'tickets' | 'cubiertos'

// Métricas disponibles para el gráfico de tendencias
const TREND_METRICS: MetricOption[] = [
  { id: 'venta_neta', label: 'Venta Neta', format: 'currency' },
  { id: 'tickets', label: 'Tickets', format: 'number' },
  { id: 'ticket_promedio', label: 'Ticket Prom.', format: 'currency' },
  { id: 'cubiertos', label: 'Cubiertos', format: 'number' },
  { id: 'cub_ticket', label: 'Cub/Ticket', format: 'number' },
]

export function VentasFranquicia() {
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

  // Estados
  const [fechaDesde, setFechaDesde] = useState(defaultDates.fechaDesde)
  const [fechaHasta, setFechaHasta] = useState(defaultDates.fechaHasta)
  const [selectedFranquiciaId, setSelectedFranquiciaId] = useState<number | null>(null)
  const [currencyMode, setCurrencyMode] = useState<CurrencyMode>('USD')
  const [selectedMetric, setSelectedMetric] = useState<string>('venta_neta')
  const [selectedTipoPlato, setSelectedTipoPlato] = useState<string | null>(null)
  const [selectedHour, setSelectedHour] = useState<number | null>(null)
  const [dayGridMetric, setDayGridMetric] = useState<DayGridMetric>('venta')

  // Modal de productos por día
  const [selectedDayForProducts, setSelectedDayForProducts] = useState<string | null>(null)
  const [dayProductsData, setDayProductsData] = useState<VentasPorProductoConPeso[]>([])
  const [loadingDayProducts, setLoadingDayProducts] = useState(false)

  // Searchable franchise dropdown
  const [franquiciaDropdownOpen, setFranquiciaDropdownOpen] = useState(false)
  const [franquiciaSearch, setFranquiciaSearch] = useState('')
  const franquiciaDropdownRef = useRef<HTMLDivElement>(null)

  // Transactions grid
  const [transacciones, setTransacciones] = useState<Transaccion[]>([])
  const [loadingTransacciones, setLoadingTransacciones] = useState(false)

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (franquiciaDropdownRef.current && !franquiciaDropdownRef.current.contains(event.target as Node)) {
        setFranquiciaDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  // Cargar productos cuando se selecciona un día
  useEffect(() => {
    const fetchDayProducts = async () => {
      if (!selectedDayForProducts || !selectedFranquiciaId) {
        setDayProductsData([])
        return
      }
      setLoadingDayProducts(true)
      try {
        const data = await dashboardApiV2.getVentasPorProductoConPeso({
          franquiciaId: selectedFranquiciaId,
          fechaDesde: selectedDayForProducts,
          fechaHasta: selectedDayForProducts,
        })
        // Filtrar por la franquicia seleccionada
        const filtered = data.filter(p => p.franquicia_id === selectedFranquiciaId)
        setDayProductsData(filtered)
      } catch (err) {
        console.error('Error loading day products:', err)
        setDayProductsData([])
      } finally {
        setLoadingDayProducts(false)
      }
    }
    fetchDayProducts()
  }, [selectedDayForProducts, selectedFranquiciaId])

  // Fetch transactions when franchise is selected
  useEffect(() => {
    const fetchTransacciones = async () => {
      if (!selectedFranquiciaId) {
        setTransacciones([])
        return
      }
      setLoadingTransacciones(true)
      try {
        const data = await transaccionesApi.getTransaccionesByFranquicia(
          selectedFranquiciaId,
          fechaDesde,
          fechaHasta
        )
        setTransacciones(data)
      } catch (err) {
        console.error('Error loading transactions:', err)
        setTransacciones([])
      } finally {
        setLoadingTransacciones(false)
      }
    }
    fetchTransacciones()
  }, [selectedFranquiciaId, fechaDesde, fechaHasta])

  // Year/Month quick selectors
  const currentYear = new Date().getFullYear()
  const currentMonth = new Date().getMonth()
  const [selectedYear, setSelectedYear] = useState(currentYear)
  const [selectedMonth, setSelectedMonth] = useState(currentMonth)

  const months = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
  ]
  const availableYears = [currentYear, currentYear - 1, currentYear - 2]

  // Handle year/month selection
  const handleYearMonthChange = (year: number, month: number) => {
    setSelectedYear(year)
    setSelectedMonth(month)
    const lastDay = new Date(year, month + 1, 0).getDate()
    setFechaDesde(`${year}-${String(month + 1).padStart(2, '0')}-01`)
    setFechaHasta(`${year}-${String(month + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`)
  }

  // API filters - solo con franquicia seleccionada
  const apiFilters = selectedFranquiciaId ? {
    fechaDesde,
    fechaHasta,
    franquiciaId: selectedFranquiciaId,
  } : {
    fechaDesde,
    fechaHasta,
  }

  // Hooks de datos
  const { data: dashboardData, loading, error, refetch } = useHomeDashboard(apiFilters)
  const { data: ventasResumen } = useVentasResumen(selectedFranquiciaId ? apiFilters : undefined)
  const { data: ventasPorTipo } = useVentasPorTipoPlato(selectedFranquiciaId ? apiFilters : undefined)
  const { data: ventasPorProducto } = useVentasPorProducto(selectedFranquiciaId ? apiFilters : undefined)
  const { data: ventasPorHora } = useVentasPorHora(selectedFranquiciaId ? apiFilters : undefined)

  // Lista de franquicias para selector
  const { data: allFranquicias } = useHomeDashboard({
    fechaDesde: defaultDates.fechaDesde,
    fechaHasta: defaultDates.fechaHasta,
  })

  const franquiciasList = useMemo(() => {
    return allFranquicias?.map(f => ({
      id: f.franquicia_id,
      nombre: f.franquicia_nombre,
      codigo: f.franquicia_codigo,
      pais: f.pais || '',
      moneda: f.moneda_codigo || 'USD',
    })) || []
  }, [allFranquicias])

  // Filtered franchises based on search
  const filteredFranquicias = useMemo(() => {
    if (!franquiciaSearch.trim()) return franquiciasList
    const search = franquiciaSearch.toLowerCase()
    return franquiciasList.filter(f =>
      f.nombre.toLowerCase().includes(search) ||
      f.codigo.toLowerCase().includes(search) ||
      f.pais.toLowerCase().includes(search)
    )
  }, [franquiciasList, franquiciaSearch])

  // Selected franchise display name
  const selectedFranquiciaDisplay = useMemo(() => {
    const f = franquiciasList.find(fr => fr.id === selectedFranquiciaId)
    return f ? `${f.nombre} (${f.pais})` : null
  }, [franquiciasList, selectedFranquiciaId])

  // Datos de la franquicia seleccionada
  const selectedFranquicia = useMemo(() => {
    if (!selectedFranquiciaId || !dashboardData) return null
    return dashboardData.find(f => f.franquicia_id === selectedFranquiciaId) || null
  }, [selectedFranquiciaId, dashboardData])

  // Moneda a usar según modo
  const displayCurrency = currencyMode === 'LOCAL' && selectedFranquicia?.moneda_codigo
    ? selectedFranquicia.moneda_codigo
    : 'USD'

  // Calcular días en el rango
  const diasRango = useMemo(() => {
    return Math.ceil(
      (new Date(fechaHasta).getTime() - new Date(fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
    ) + 1
  }, [fechaDesde, fechaHasta])

  // Calcular días en el mes seleccionado para la grilla
  const daysInMonth = useMemo(() => {
    const lastDay = new Date(selectedYear, selectedMonth + 1, 0).getDate()
    return Array.from({ length: lastDay }, (_, i) => i + 1)
  }, [selectedYear, selectedMonth])

  // Datos por día para la grilla (similar a Alertas)
  const dayGridData = useMemo(() => {
    if (!ventasResumen || !selectedFranquiciaId) return {}

    const filtered = ventasResumen.filter(v => v.franquicia_id === selectedFranquiciaId)
    const byDay: Record<string, { venta: number; tickets: number; cubiertos: number }> = {}

    for (const item of filtered) {
      const fecha = item.fecha_negocio?.split('T')[0]
      if (!fecha) continue

      const ventaValue = currencyMode === 'LOCAL'
        ? (item.venta_neta || 0)
        : (item.venta_neta_usd ?? item.venta_neta ?? 0)

      byDay[fecha] = {
        venta: ventaValue,
        tickets: item.cantidad_tickets || 0,
        cubiertos: item.total_cubiertos || 0,
      }
    }

    return byDay
  }, [ventasResumen, selectedFranquiciaId, currencyMode])

  // Totales de la franquicia seleccionada
  const totals = useMemo(() => {
    if (!selectedFranquicia) return { ventaNeta: 0, ventaLocal: 0, tickets: 0, cubiertos: 0 }
    return {
      ventaNeta: selectedFranquicia.venta_neta || 0,
      ventaLocal: selectedFranquicia.venta_neta_local || 0,
      tickets: selectedFranquicia.total_tickets || 0,
      cubiertos: selectedFranquicia.total_cubiertos || 0,
    }
  }, [selectedFranquicia])

  // Valor de venta según modo de moneda
  const displayVenta = currencyMode === 'LOCAL' ? totals.ventaLocal : totals.ventaNeta
  const displayTicketPromedio = totals.tickets > 0
    ? (currencyMode === 'LOCAL' ? totals.ventaLocal : totals.ventaNeta) / totals.tickets
    : 0

  // Preparar datos para gráfico de líneas
  const chartData = useMemo(() => {
    if (!ventasResumen || !selectedFranquiciaId) return []

    const filtered = ventasResumen.filter(v => v.franquicia_id === selectedFranquiciaId)

    const getMetricValue = (item: typeof filtered[number], metricId: string): number => {
      const ventaUsd = item.venta_neta_usd ?? item.venta_neta ?? 0
      const ventaLocal = item.venta_neta ?? 0
      const venta = currencyMode === 'LOCAL' ? ventaLocal : ventaUsd
      const tickets = item.cantidad_tickets || 0
      const cubiertos = item.total_cubiertos || 0

      switch (metricId) {
        case 'venta_neta': return venta
        case 'tickets': return tickets
        case 'ticket_promedio': return tickets > 0 ? venta / tickets : 0
        case 'cubiertos': return cubiertos
        case 'cub_ticket': return tickets > 0 ? cubiertos / tickets : 0
        default: return 0
      }
    }

    return filtered
      .map(item => ({
        fecha: item.fecha_negocio?.split('T')[0] || '',
        valor: getMetricValue(item, selectedMetric),
      }))
      .filter(item => item.fecha)
      .sort((a, b) => a.fecha.localeCompare(b.fecha))
  }, [ventasResumen, selectedFranquiciaId, selectedMetric, currencyMode])

  // Preparar datos para tipos de plato
  const tiposPlatoData = useMemo(() => {
    if (!ventasPorTipo || !selectedFranquicia) return []

    // Filtrar por nombre de franquicia
    const filtered = ventasPorTipo.filter(t => t.franquicia_nombre === selectedFranquicia.franquicia_nombre)

    const grouped = filtered.reduce((acc, t) => {
      const name = t.tipo_plato_nombre
      if (!acc[name]) {
        acc[name] = { name, value: 0 }
      }
      // Usar venta local o USD según modo
      const venta = currencyMode === 'LOCAL' ? (t.venta_neta || 0) : (t.venta_neta || 0)
      acc[name].value += venta
      return acc
    }, {} as Record<string, { name: string; value: number }>)

    return Object.values(grouped).sort((a, b) => b.value - a.value)
  }, [ventasPorTipo, selectedFranquicia, currencyMode])

  // Top 10 productos
  const top10ProductosData = useMemo(() => {
    if (!ventasPorProducto || !selectedFranquicia) return []

    let productos = ventasPorProducto.filter(p => p.franquicia_nombre === selectedFranquicia.franquicia_nombre)

    if (selectedTipoPlato) {
      productos = productos.filter(p => p.categoria === selectedTipoPlato)
    }

    const total = productos.reduce((sum, p) => sum + (p.venta_neta || 0), 0)

    return productos
      .sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0))
      .slice(0, 10)
      .map(p => ({
        name: p.producto_nombre,
        value: p.venta_neta || 0,
        percent: total > 0 ? ((p.venta_neta || 0) / total) * 100 : 0,
      }))
  }, [ventasPorProducto, selectedFranquiciaId, selectedTipoPlato])

  // Datos para ClockChart
  const clockChartData = useMemo(() => {
    if (!ventasPorHora || !selectedFranquiciaId) return []

    const filtered = ventasPorHora.filter(h => h.franquicia_id === selectedFranquiciaId)

    const byHour = filtered.reduce((acc, item) => {
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
  }, [ventasPorHora, selectedFranquiciaId])

  // Handlers
  const handleTipoPlatoClick = (item: { name: string } | null) => {
    setSelectedTipoPlato(item?.name || null)
  }

  const handleHourClick = (hour: number | null) => {
    setSelectedHour(hour)
  }

  if (loading && selectedFranquiciaId) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <Store className="h-6 w-6" />
          Dashboard por Franquicia
        </h1>
        <p className="text-muted-foreground">
          Vista detallada de una franquicia individual
        </p>
      </div>

      {/* Filtros y Selector de Franquicia */}
      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center gap-4">
            {/* Selector de Franquicia - OBLIGATORIO con buscador */}
            <div className="flex items-center gap-2">
              <Store className="h-4 w-4 text-cabrera-burgundy" />
              <span className="text-sm font-semibold text-cabrera-burgundy">Franquicia:</span>
              <div className="relative" ref={franquiciaDropdownRef}>
                {/* Trigger button */}
                <button
                  type="button"
                  onClick={() => setFranquiciaDropdownOpen(!franquiciaDropdownOpen)}
                  className={`flex items-center justify-between gap-2 px-3 py-1.5 text-sm border-2 rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 min-w-[280px] text-left ${
                    !selectedFranquiciaId
                      ? 'border-red-400 bg-red-50 dark:bg-red-900/20 text-gray-700 dark:text-gray-200'
                      : 'border-cabrera-burgundy bg-white dark:bg-gray-800 text-gray-900 dark:text-white'
                  }`}
                >
                  <span className={selectedFranquiciaDisplay ? '' : 'text-gray-500 dark:text-gray-400'}>
                    {selectedFranquiciaDisplay || '-- Seleccionar Franquicia --'}
                  </span>
                  <div className="flex items-center gap-1">
                    {selectedFranquiciaId && (
                      <span
                        onClick={(e) => {
                          e.stopPropagation()
                          setSelectedFranquiciaId(null)
                          setFranquiciaSearch('')
                        }}
                        className="p-0.5 hover:bg-gray-200 dark:hover:bg-gray-600 rounded"
                      >
                        <X className="h-3 w-3 text-gray-500" />
                      </span>
                    )}
                    <ChevronDown className={`h-4 w-4 text-gray-500 transition-transform ${franquiciaDropdownOpen ? 'rotate-180' : ''}`} />
                  </div>
                </button>

                {/* Dropdown panel */}
                {franquiciaDropdownOpen && (
                  <div className="absolute z-50 mt-1 w-full min-w-[320px] bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg">
                    {/* Search input */}
                    <div className="p-2 border-b border-gray-200 dark:border-gray-600">
                      <div className="relative">
                        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                        <input
                          type="text"
                          placeholder="Buscar franquicia..."
                          value={franquiciaSearch}
                          onChange={(e) => setFranquiciaSearch(e.target.value)}
                          className="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50"
                          autoFocus
                        />
                      </div>
                    </div>

                    {/* Options list */}
                    <div className="max-h-60 overflow-y-auto">
                      {filteredFranquicias.length === 0 ? (
                        <div className="p-3 text-sm text-gray-500 dark:text-gray-400 text-center">
                          No se encontraron franquicias
                        </div>
                      ) : (
                        filteredFranquicias.map(f => (
                          <button
                            key={f.id}
                            type="button"
                            onClick={() => {
                              setSelectedFranquiciaId(f.id)
                              setFranquiciaDropdownOpen(false)
                              setFranquiciaSearch('')
                            }}
                            className={`w-full px-3 py-2 text-sm text-left hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center justify-between ${
                              selectedFranquiciaId === f.id
                                ? 'bg-cabrera-burgundy/10 text-cabrera-burgundy font-medium'
                                : 'text-gray-900 dark:text-white'
                            }`}
                          >
                            <span>{f.nombre}</span>
                            <span className="text-xs text-gray-500 dark:text-gray-400">{f.pais}</span>
                          </button>
                        ))
                      )}
                    </div>
                  </div>
                )}
              </div>
              {!selectedFranquiciaId && (
                <span className="text-xs text-red-500 flex items-center gap-1">
                  <AlertCircle className="h-3 w-3" />
                  Requerido
                </span>
              )}
            </div>

            {/* Separador */}
            <div className="h-8 w-px bg-gray-300 dark:bg-gray-600" />

            {/* Período */}
            <div className="flex items-center gap-2">
              <span className="text-xs text-muted-foreground">Período:</span>
              <select
                value={selectedYear}
                onChange={(e) => handleYearMonthChange(Number(e.target.value), selectedMonth)}
                className="px-2 py-1 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {availableYears.map(year => (
                  <option key={year} value={year}>{year}</option>
                ))}
              </select>
              <select
                value={selectedMonth}
                onChange={(e) => handleYearMonthChange(selectedYear, Number(e.target.value))}
                className="px-2 py-1 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {months.map((month, idx) => (
                  <option key={idx} value={idx}>{month}</option>
                ))}
              </select>
              <span className="text-xs text-muted-foreground">
                ({diasRango} días)
              </span>
            </div>

            {/* Separador */}
            <div className="h-8 w-px bg-gray-300 dark:bg-gray-600" />

            {/* Selector de Moneda */}
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium">Moneda:</span>
              <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
                <button
                  onClick={() => setCurrencyMode('USD')}
                  className={`px-3 py-1.5 text-sm flex items-center gap-1 transition-colors ${
                    currencyMode === 'USD'
                      ? 'bg-green-600 text-white'
                      : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                  }`}
                >
                  <DollarSign className="h-3 w-3" />
                  USD
                </button>
                <button
                  onClick={() => setCurrencyMode('LOCAL')}
                  disabled={!selectedFranquicia}
                  className={`px-3 py-1.5 text-sm flex items-center gap-1 border-l dark:border-gray-600 transition-colors ${
                    currencyMode === 'LOCAL'
                      ? 'bg-emerald-600 text-white'
                      : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                  } ${!selectedFranquicia ? 'opacity-50 cursor-not-allowed' : ''}`}
                >
                  <Banknote className="h-3 w-3" />
                  {selectedFranquicia?.moneda_codigo || 'Local'}
                </button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Mensaje si no hay franquicia seleccionada */}
      {!selectedFranquiciaId ? (
        <Card>
          <CardContent className="p-12 text-center">
            <Store className="h-16 w-16 mx-auto text-gray-300 dark:text-gray-600 mb-4" />
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white mb-2">
              Seleccione una Franquicia
            </h2>
            <p className="text-muted-foreground max-w-md mx-auto">
              Para ver el dashboard detallado, debe seleccionar una franquicia del listado superior.
              Podrá ver KPIs, gráficos de tendencia, consumo por hora y más.
            </p>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Indicador de Moneda */}
          <div className={`flex items-center gap-2 px-4 py-2 rounded-lg border ${
            currencyMode === 'USD'
              ? 'bg-green-50 dark:bg-green-900/20 border-green-200 dark:border-green-800'
              : 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-800'
          }`}>
            {currencyMode === 'USD' ? (
              <DollarSign className="w-4 h-4 text-green-600 dark:text-green-400" />
            ) : (
              <Banknote className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
            )}
            <span className={`text-sm font-medium ${
              currencyMode === 'USD'
                ? 'text-green-700 dark:text-green-300'
                : 'text-emerald-700 dark:text-emerald-300'
            }`}>
              {currencyMode === 'USD' ? 'Valores en USD' : `Valores en ${displayCurrency} (Moneda Original)`}
            </span>
            {currencyMode === 'USD' && (
              <span className="text-sm text-green-600 dark:text-green-400">
                — Convertido usando tipo de cambio de cada fecha
              </span>
            )}
            {currencyMode === 'LOCAL' && (
              <span className="text-sm text-emerald-600 dark:text-emerald-400">
                — Sin conversión de tipo de cambio
              </span>
            )}
          </div>

          {/* KPIs */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <KpiCard
              title={`Venta Neta (${displayCurrency})`}
              value={displayVenta}
              format="currency"
              currency={displayCurrency}
              icon={currencyMode === 'USD' ? DollarSign : Banknote}
              iconColor={currencyMode === 'USD'
                ? "bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400"
                : "bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400"
              }
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
              title={`Ticket Promedio (${displayCurrency})`}
              value={displayTicketPromedio}
              format="currency"
              currency={displayCurrency}
              icon={TrendingUp}
              iconColor="bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400"
            />
          </div>

          {/* Grilla de Totales por Día */}
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <Ticket className="h-4 w-4" />
                Totales por Día ({transacciones.length} tickets) — {currencyMode === 'USD' ? 'USD' : displayCurrency}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {loadingTransacciones ? (
                <div className="flex items-center justify-center py-8">
                  <div className="animate-spin rounded-full h-6 w-6 border-2 border-cabrera-burgundy border-t-transparent" />
                </div>
              ) : transacciones.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground text-sm">
                  No hay tickets en el período seleccionado
                </div>
              ) : (() => {
                // Agrupar transacciones por fecha - usar valores según currencyMode
                const porDia = transacciones.reduce((acc, t) => {
                  const fechaKey = t.fecha_negocio.split('T')[0]
                  if (!acc[fechaKey]) {
                    acc[fechaKey] = {
                      fecha: fechaKey,
                      moneda: currencyMode === 'USD' ? 'USD' : t.moneda_codigo,
                      tickets: 0,
                      bruto: 0,
                      descuento: 0,
                      neto: 0,
                      impuesto: 0,
                      propina: 0,
                      total: 0,
                    }
                  }
                  acc[fechaKey].tickets++
                  // Usar valores USD o Local según el modo seleccionado
                  if (currencyMode === 'USD') {
                    acc[fechaKey].bruto += t.importe_bruto_usd
                    acc[fechaKey].descuento += t.importe_descuento_usd
                    acc[fechaKey].neto += t.importe_neto_usd
                    acc[fechaKey].impuesto += t.importe_impuesto_usd
                    acc[fechaKey].propina += t.importe_propina_usd
                    acc[fechaKey].total += t.importe_total_pagado_usd
                  } else {
                    acc[fechaKey].bruto += t.importe_bruto
                    acc[fechaKey].descuento += t.importe_descuento
                    acc[fechaKey].neto += t.importe_neto
                    acc[fechaKey].impuesto += t.importe_impuesto
                    acc[fechaKey].propina += t.importe_propina
                    acc[fechaKey].total += t.importe_total_pagado
                  }
                  return acc
                }, {} as Record<string, { fecha: string; moneda: string; tickets: number; bruto: number; descuento: number; neto: number; impuesto: number; propina: number; total: number }>)

                const diasOrdenados = Object.values(porDia).sort((a, b) => b.fecha.localeCompare(a.fecha))
                const fmt = (val: number) => val.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

                // Calcular gran total
                const granTotal = diasOrdenados.reduce((acc, d) => ({
                  tickets: acc.tickets + d.tickets,
                  bruto: acc.bruto + d.bruto,
                  descuento: acc.descuento + d.descuento,
                  neto: acc.neto + d.neto,
                  impuesto: acc.impuesto + d.impuesto,
                  propina: acc.propina + d.propina,
                  total: acc.total + d.total,
                }), { tickets: 0, bruto: 0, descuento: 0, neto: 0, impuesto: 0, propina: 0, total: 0 })

                return (
                  <div className="overflow-x-auto max-h-[400px] overflow-y-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-gray-50 dark:bg-gray-800 sticky top-0">
                        <tr>
                          <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-gray-300">Fecha</th>
                          <th className="px-3 py-2 text-center font-medium text-gray-700 dark:text-gray-300">Tickets</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300" title="Ticket Promedio = Neto (s/IVA) / Tickets">Ticket Prom.</th>
                          <th className="px-3 py-2 text-center font-medium text-gray-700 dark:text-gray-300">Moneda</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300" title="Neto sin IVA">Neto (s/IVA)</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300" title="IVA / Impuestos">IVA</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300" title="Bruto = Neto + IVA">Bruto</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300">Descuento</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300">Propina</th>
                          <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300" title="Total = Bruto - Descuento + Propina">Total Pagado</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                        {diasOrdenados.map((d) => {
                          const fechaObj = new Date(d.fecha + 'T12:00:00')
                          const fechaDisplay = fechaObj.toLocaleDateString('es-ES', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' })
                          return (
                            <tr key={d.fecha} className="hover:bg-gray-50 dark:hover:bg-gray-800/50">
                              <td className="px-3 py-2 text-gray-900 dark:text-white font-medium">{fechaDisplay}</td>
                              <td className="px-3 py-2 text-center text-gray-700 dark:text-gray-300">{d.tickets}</td>
                              <td className="px-3 py-2 text-right text-amber-600 dark:text-amber-400 font-medium">
                                {d.tickets > 0 ? fmt(d.neto / d.tickets) : '-'}
                              </td>
                              <td className="px-3 py-2 text-center">
                                <span className="px-1.5 py-0.5 text-xs rounded bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">
                                  {d.moneda}
                                </span>
                              </td>
                              <td className="px-3 py-2 text-right text-gray-900 dark:text-white">{fmt(d.neto)}</td>
                              <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">{fmt(d.impuesto)}</td>
                              <td className="px-3 py-2 text-right font-medium text-gray-900 dark:text-white">{fmt(d.bruto)}</td>
                              <td className="px-3 py-2 text-right text-red-600 dark:text-red-400">
                                {d.descuento > 0 ? `-${fmt(d.descuento)}` : '-'}
                              </td>
                              <td className="px-3 py-2 text-right text-green-600 dark:text-green-400">
                                {d.propina > 0 ? fmt(d.propina) : '-'}
                              </td>
                              <td className="px-3 py-2 text-right font-bold text-gray-900 dark:text-white">{fmt(d.total)}</td>
                            </tr>
                          )
                        })}
                      </tbody>
                      <tfoot className="bg-cabrera-burgundy/10 dark:bg-cabrera-burgundy/20 border-t-2 border-cabrera-burgundy">
                        <tr className="font-bold">
                          <td className="px-3 py-2 text-cabrera-burgundy">TOTAL</td>
                          <td className="px-3 py-2 text-center text-cabrera-burgundy">{granTotal.tickets}</td>
                          <td className="px-3 py-2 text-right text-amber-700 dark:text-amber-400">
                            {granTotal.tickets > 0 ? fmt(granTotal.neto / granTotal.tickets) : '-'}
                          </td>
                          <td className="px-3 py-2"></td>
                          <td className="px-3 py-2 text-right text-cabrera-burgundy">{fmt(granTotal.neto)}</td>
                          <td className="px-3 py-2 text-right text-gray-700 dark:text-gray-300">{fmt(granTotal.impuesto)}</td>
                          <td className="px-3 py-2 text-right text-cabrera-burgundy">{fmt(granTotal.bruto)}</td>
                          <td className="px-3 py-2 text-right text-red-700 dark:text-red-400">
                            {granTotal.descuento > 0 ? `-${fmt(granTotal.descuento)}` : '-'}
                          </td>
                          <td className="px-3 py-2 text-right text-green-700 dark:text-green-400">
                            {granTotal.propina > 0 ? fmt(granTotal.propina) : '-'}
                          </td>
                          <td className="px-3 py-2 text-right text-cabrera-burgundy text-base">{fmt(granTotal.total)}</td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                )
              })()}
            </CardContent>
          </Card>

          {/* Grilla de Datos por Día (similar a Alertas) */}
          <Card>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-semibold flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  Datos por Día — {months[selectedMonth]} {selectedYear}
                </CardTitle>
                {/* Selector de métrica */}
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">Métrica:</span>
                  <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
                    <button
                      onClick={() => setDayGridMetric('venta')}
                      className={`px-2 py-1 text-xs flex items-center gap-1 transition-colors ${
                        dayGridMetric === 'venta'
                          ? 'bg-cabrera-burgundy text-white'
                          : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                      }`}
                    >
                      <DollarSign className="h-3 w-3" />
                      {currencyMode === 'USD' ? 'USD' : displayCurrency}
                    </button>
                    <button
                      onClick={() => setDayGridMetric('tickets')}
                      className={`px-2 py-1 text-xs flex items-center gap-1 border-l dark:border-gray-600 transition-colors ${
                        dayGridMetric === 'tickets'
                          ? 'bg-cabrera-burgundy text-white'
                          : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                      }`}
                    >
                      <Ticket className="h-3 w-3" />
                      Tickets
                    </button>
                    <button
                      onClick={() => setDayGridMetric('cubiertos')}
                      className={`px-2 py-1 text-xs flex items-center gap-1 border-l dark:border-gray-600 transition-colors ${
                        dayGridMetric === 'cubiertos'
                          ? 'bg-cabrera-burgundy text-white'
                          : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
                      }`}
                    >
                      <Users className="h-3 w-3" />
                      Cubiertos
                    </button>
                  </div>
                </div>
              </div>
            </CardHeader>
            <CardContent className="overflow-x-auto p-4">
              {(() => {
                const today = new Date()
                const formatValue = (val: number): string => {
                  if (val === 0) return '-'
                  if (dayGridMetric === 'venta') return `$${Math.round(val).toLocaleString()}`
                  return val.toLocaleString()
                }

                const getCellClass = (dateStr: string, day: number): string => {
                  const cellDate = new Date(selectedYear, selectedMonth, day)
                  // No destacar fechas futuras
                  if (cellDate > today) {
                    return 'bg-gray-100 dark:bg-gray-800 text-gray-400'
                  }
                  const data = dayGridData[dateStr]
                  const value = data ? data[dayGridMetric] : 0
                  if (value === 0 || !data) {
                    return 'bg-red-200 dark:bg-red-900/50 text-red-700 dark:text-red-300 font-medium'
                  }
                  return 'bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300'
                }

                // Calcular total del mes
                let monthTotal = 0
                for (const day of daysInMonth) {
                  const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                  const data = dayGridData[dateStr]
                  if (data) {
                    monthTotal += data[dayGridMetric]
                  }
                }

                // Contar huecos
                let gaps = 0
                for (const day of daysInMonth) {
                  const cellDate = new Date(selectedYear, selectedMonth, day)
                  if (cellDate > today) continue
                  const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                  const data = dayGridData[dateStr]
                  if (!data || data[dayGridMetric] === 0) gaps++
                }

                return (
                  <>
                    <table className="w-full text-xs">
                      <thead className="bg-gray-100 dark:bg-gray-800">
                        <tr>
                          {daysInMonth.map(day => (
                            <th key={day} className="px-1 py-2 text-center font-semibold min-w-[50px]">
                              {day}
                            </th>
                          ))}
                          <th className="px-2 py-2 text-right font-semibold min-w-[80px] bg-gray-200 dark:bg-gray-700">
                            Total
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          {daysInMonth.map(day => {
                            const dateStr = `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
                            const data = dayGridData[dateStr]
                            const value = data ? data[dayGridMetric] : 0
                            const cellClass = getCellClass(dateStr, day)
                            const cellDate = new Date(selectedYear, selectedMonth, day)
                            const isFuture = cellDate > today
                            const hasData = data && value > 0

                            return (
                              <td
                                key={day}
                                className={`px-1 py-2 text-center ${cellClass} ${!isFuture ? 'cursor-pointer hover:ring-2 hover:ring-cabrera-burgundy hover:ring-inset' : ''}`}
                                title={`${dateStr}: ${formatValue(value)} - Click para ver productos`}
                                onClick={() => {
                                  if (!isFuture) {
                                    setSelectedDayForProducts(dateStr)
                                  }
                                }}
                              >
                                {formatValue(value)}
                              </td>
                            )
                          })}
                          <td className="px-2 py-2 text-right font-bold bg-gray-200 dark:bg-gray-700 text-gray-900 dark:text-white">
                            {dayGridMetric === 'venta'
                              ? `$${Math.round(monthTotal).toLocaleString()}`
                              : monthTotal.toLocaleString()
                            }
                          </td>
                        </tr>
                      </tbody>
                    </table>

                    {/* Leyenda y contador de huecos */}
                    <div className="mt-4 flex items-center justify-between flex-wrap gap-4">
                      <div className="flex items-center gap-6 text-xs text-muted-foreground">
                        <div className="flex items-center gap-2">
                          <div className="w-4 h-4 bg-red-200 dark:bg-red-900/50 rounded" />
                          <span>Sin datos</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <div className="w-4 h-4 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded" />
                          <span>Con datos</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <div className="w-4 h-4 bg-gray-100 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded" />
                          <span>Futuro</span>
                        </div>
                        <div className="border-l border-gray-300 dark:border-gray-600 pl-6 flex items-center gap-2 text-cabrera-burgundy">
                          <Search className="h-3 w-3" />
                          <span>Click en día para ver productos</span>
                        </div>
                      </div>
                      {gaps > 0 && (
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-red-100 dark:bg-red-900/30 rounded-lg">
                          <AlertCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
                          <span className="text-sm font-medium text-red-700 dark:text-red-300">
                            {gaps} día(s) sin datos
                          </span>
                        </div>
                      )}
                    </div>
                  </>
                )
              })()}
            </CardContent>
          </Card>

          {/* Gráfico de Tendencia */}
          {chartData.length > 0 && (
            <SalesLineChart
              title="Tendencia Diaria"
              data={chartData.map(d => ({ fecha: d.fecha, [selectedFranquicia?.franquicia_codigo || 'valor']: d.valor }))}
              dataKeys={[{
                key: selectedFranquicia?.franquicia_codigo || 'valor',
                name: selectedFranquicia?.franquicia_nombre || 'Valor',
              }]}
              metrics={TREND_METRICS}
              selectedMetric={selectedMetric}
              onMetricChange={setSelectedMetric}
            />
          )}

          {/* ClockChart */}
          {clockChartData.length > 0 && (
            <ClockChart
              title="Consumo por Hora"
              subtitle="Distribución horaria del período"
              data={clockChartData}
              selectedHour={selectedHour}
              onHourClick={handleHourClick}
              metric="cubiertos"
            />
          )}

          {/* Gráficos de barras */}
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
            <HorizontalBarChart
              title="Ventas por Tipo de Plato"
              subtitle="Toque para filtrar productos"
              data={tiposPlatoData}
              onBarClick={handleTipoPlatoClick}
              selectedItem={selectedTipoPlato}
              maxBars={10}
            />
            <HorizontalBarChart
              title={selectedTipoPlato ? `Top 10 Productos - ${selectedTipoPlato}` : "Top 10 Productos"}
              subtitle={selectedTipoPlato ? "Filtrado por tipo de plato" : "Todos los productos"}
              data={top10ProductosData}
              showPercent={true}
              maxBars={10}
            />
          </div>

          {/* Filtros activos */}
          {(selectedTipoPlato || selectedHour !== null) && (
            <div className="flex items-center gap-4 flex-wrap p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
              <span className="text-sm font-medium text-blue-800 dark:text-blue-200">Filtros activos:</span>
              {selectedTipoPlato && (
                <button
                  onClick={() => setSelectedTipoPlato(null)}
                  className="inline-flex items-center gap-2 px-3 py-1 bg-purple-200 dark:bg-purple-800 text-purple-800 dark:text-purple-100 rounded-lg text-sm hover:bg-purple-300 dark:hover:bg-purple-700"
                >
                  Tipo: {selectedTipoPlato}
                  <span className="font-bold">×</span>
                </button>
              )}
              {selectedHour !== null && (
                <button
                  onClick={() => setSelectedHour(null)}
                  className="inline-flex items-center gap-2 px-3 py-1 bg-amber-200 dark:bg-amber-800 text-amber-800 dark:text-amber-100 rounded-lg text-sm hover:bg-amber-300 dark:hover:bg-amber-700"
                >
                  Hora: {selectedHour}:00
                  <span className="font-bold">×</span>
                </button>
              )}
              <button
                onClick={() => { setSelectedTipoPlato(null); setSelectedHour(null) }}
                className="text-sm text-blue-600 dark:text-blue-400 hover:underline ml-auto"
              >
                Limpiar todo
              </button>
            </div>
          )}
        </>
      )}

      {/* Modal de Productos por Día */}
      {selectedDayForProducts && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={() => setSelectedDayForProducts(null)}>
          <div
            className="bg-white dark:bg-gray-900 rounded-lg shadow-xl max-w-5xl w-full mx-4 max-h-[85vh] overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header del Modal */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700 bg-cabrera-burgundy text-white">
              <div>
                <h2 className="text-lg font-semibold flex items-center gap-2">
                  Productos del Día
                  <span className={`px-2 py-0.5 text-xs rounded ${currencyMode === 'USD' ? 'bg-green-500' : 'bg-emerald-500'}`}>
                    {currencyMode === 'USD' ? 'USD' : displayCurrency}
                  </span>
                </h2>
                <p className="text-sm text-white/80">
                  {selectedFranquicia?.franquicia_nombre} — {new Date(selectedDayForProducts + 'T12:00:00').toLocaleDateString('es-ES', { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' })}
                </p>
              </div>
              <button
                onClick={() => setSelectedDayForProducts(null)}
                className="p-2 hover:bg-white/20 rounded-full transition-colors"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {/* Contenido del Modal */}
            <div className="p-6 overflow-y-auto max-h-[calc(85vh-120px)]">
              {loadingDayProducts ? (
                <div className="flex items-center justify-center py-12">
                  <div className="animate-spin rounded-full h-8 w-8 border-2 border-cabrera-burgundy border-t-transparent" />
                </div>
              ) : dayProductsData.length === 0 ? (
                <div className="text-center py-12 text-muted-foreground">
                  No hay productos vendidos en este día
                </div>
              ) : (() => {
                // Obtener los datos del resumen del día (son los mismos que muestra la grilla)
                const dayResumen = ventasResumen?.find(
                  v => v.franquicia_id === selectedFranquiciaId &&
                       v.fecha_negocio?.split('T')[0] === selectedDayForProducts
                )
                const tipoCambio = dayResumen?.tipo_cambio || 1

                // Valor total del resumen - usar directamente del resumen para que coincida con la grilla
                const totalNetoResumen = currencyMode === 'USD'
                  ? (dayResumen?.venta_neta_usd ?? dayResumen?.venta_neta ?? 0)
                  : (dayResumen?.venta_neta ?? 0)

                // Sumar venta_neta de productos para calcular el factor de conversión
                const sumProductosLocal = dayProductsData.reduce((sum, p) => sum + (p.venta_neta ?? 0), 0)

                // Factor de conversión: si el total de productos en local es diferente al total del resumen,
                // usamos el factor para mantener consistencia
                const factorConversion = sumProductosLocal > 0 && currencyMode === 'USD' && tipoCambio > 1
                  ? totalNetoResumen / sumProductosLocal
                  : 1

                // Función para obtener el valor según el modo de moneda
                const getVenta = (p: VentasPorProductoConPeso): number => {
                  if (currencyMode === 'USD') {
                    // Si tenemos venta_neta_usd válido, usarlo
                    if (p.venta_neta_usd != null && p.venta_neta_usd > 0) {
                      return p.venta_neta_usd
                    }
                    // Convertir usando el factor calculado para mantener consistencia con el total
                    return (p.venta_neta ?? 0) * factorConversion
                  }
                  return p.venta_neta ?? 0
                }

                // Agrupar por categoría
                const byCategory = dayProductsData.reduce((acc, p) => {
                  const cat = p.categoria || 'Sin Categoría'
                  if (!acc[cat]) acc[cat] = []
                  acc[cat].push(p)
                  return acc
                }, {} as Record<string, VentasPorProductoConPeso[]>)

                // Calcular totales
                const totalCantidad = dayProductsData.reduce((sum, p) => sum + p.cantidad_vendida, 0)
                // Usar el total del resumen para el KPI principal (consistente con la grilla)
                const totalNeto = totalNetoResumen

                const fmt = (val: number) => val.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

                return (
                  <>
                    {/* Resumen */}
                    <div className="grid grid-cols-4 gap-4 mb-6">
                      <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-4 text-center">
                        <p className="text-sm text-muted-foreground">Productos</p>
                        <p className="text-2xl font-bold text-gray-900 dark:text-white">{dayProductsData.length}</p>
                      </div>
                      <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-4 text-center">
                        <p className="text-sm text-muted-foreground">Cantidad Total</p>
                        <p className="text-2xl font-bold text-gray-900 dark:text-white">{totalCantidad.toLocaleString()}</p>
                      </div>
                      <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-4 text-center">
                        <p className="text-sm text-muted-foreground">Venta Neta {currencyMode === 'USD' ? 'USD' : displayCurrency}</p>
                        <p className="text-2xl font-bold text-cabrera-burgundy">${fmt(totalNeto)}</p>
                      </div>
                      {currencyMode === 'USD' && tipoCambio > 1 && (
                        <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4 text-center">
                          <p className="text-sm text-blue-600 dark:text-blue-400">Tipo Cambio</p>
                          <p className="text-2xl font-bold text-blue-700 dark:text-blue-300">{tipoCambio.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                        </div>
                      )}
                    </div>

                    {/* Tabla de productos */}
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead className="bg-gray-100 dark:bg-gray-800 sticky top-0">
                          <tr>
                            <th className="px-3 py-2 text-left font-semibold text-gray-700 dark:text-gray-300">Categoría</th>
                            <th className="px-3 py-2 text-left font-semibold text-gray-700 dark:text-gray-300">Familia</th>
                            <th className="px-3 py-2 text-left font-semibold text-gray-700 dark:text-gray-300">Producto</th>
                            <th className="px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Cantidad</th>
                            <th className="px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Neto {currencyMode === 'USD' ? 'USD' : displayCurrency}</th>
                            <th className="px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Unitario</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                          {Object.entries(byCategory).sort((a, b) => a[0].localeCompare(b[0])).map(([cat, products]) => (
                            products.sort((a, b) => getVenta(b) - getVenta(a)).map((p, idx) => {
                              const ventaValue = getVenta(p)
                              return (
                                <tr key={`${p.producto_codigo}-${idx}`} className="hover:bg-gray-50 dark:hover:bg-gray-800/50">
                                  <td className="px-3 py-2 text-gray-600 dark:text-gray-400">
                                    {idx === 0 ? cat : ''}
                                  </td>
                                  <td className="px-3 py-2 text-gray-600 dark:text-gray-400">-</td>
                                  <td className="px-3 py-2 text-gray-900 dark:text-white font-medium">{p.producto_nombre}</td>
                                  <td className="px-3 py-2 text-right text-gray-700 dark:text-gray-300">{p.cantidad_vendida}</td>
                                  <td className="px-3 py-2 text-right text-gray-900 dark:text-white font-medium">${fmt(ventaValue)}</td>
                                  <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">
                                    ${p.cantidad_vendida > 0 ? fmt(ventaValue / p.cantidad_vendida) : '0.00'}
                                  </td>
                                </tr>
                              )
                            })
                          ))}
                        </tbody>
                        <tfoot className="bg-cabrera-burgundy/10 dark:bg-cabrera-burgundy/20 border-t-2 border-cabrera-burgundy">
                          <tr className="font-bold">
                            <td colSpan={3} className="px-3 py-2 text-cabrera-burgundy">TOTAL</td>
                            <td className="px-3 py-2 text-right text-cabrera-burgundy">{totalCantidad.toLocaleString()}</td>
                            <td className="px-3 py-2 text-right text-cabrera-burgundy">${fmt(totalNeto)}</td>
                            <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">
                              ${totalCantidad > 0 ? fmt(totalNeto / totalCantidad) : '0.00'}
                            </td>
                          </tr>
                        </tfoot>
                      </table>
                    </div>
                  </>
                )
              })()}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
