import { useState, useEffect, useRef, useMemo } from 'react'
import { Calendar, Filter, X, FileSpreadsheet, ChevronDown, ChevronUp, Search, Store } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

interface FilterBarProps {
  onFilterChange: (filters: FilterValues) => void
  franquicias: { id: number; nombre: string; pais: string }[]
  initialFilters?: Partial<FilterValues>
  onExportClick?: () => void
}

export interface FilterValues {
  fechaDesde: string
  fechaHasta: string
  pais: string
  franquiciaId: number | null
}

// Helper functions for date calculations
function getLastDayOfMonth(year: number, month: number): number {
  return new Date(year, month + 1, 0).getDate()
}

// API format: yyyy-mm-dd (for backend/storage)
function formatDateApi(year: number, month: number, day: number): string {
  return `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

// Display format: dd-mm-yyyy (for user input)
function formatDateDisplay(year: number, month: number, day: number): string {
  return `${String(day).padStart(2, '0')}-${String(month + 1).padStart(2, '0')}-${year}`
}

// Convert API format (yyyy-mm-dd) to display format (dd-mm-yyyy)
function apiToDisplay(apiDate: string): string {
  const match = apiDate.match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (!match) return apiDate
  return `${match[3]}-${match[2]}-${match[1]}`
}

// Convert display format (dd-mm-yyyy) to API format (yyyy-mm-dd)
function displayToApi(displayDate: string): string {
  const match = displayDate.match(/^(\d{2})-(\d{2})-(\d{4})$/)
  if (!match) return displayDate
  return `${match[3]}-${match[2]}-${match[1]}`
}

// Parse display format (dd-mm-yyyy)
function parseDateDisplay(dateStr: string): { year: number; month: number; day: number } | null {
  const match = dateStr.match(/^(\d{2})-(\d{2})-(\d{4})$/)
  if (!match) return null
  return {
    day: parseInt(match[1]),
    month: parseInt(match[2]) - 1,
    year: parseInt(match[3])
  }
}

function isValidDisplayDate(dateStr: string): boolean {
  const parsed = parseDateDisplay(dateStr)
  if (!parsed) return false
  const date = new Date(parsed.year, parsed.month, parsed.day)
  return date.getFullYear() === parsed.year &&
         date.getMonth() === parsed.month &&
         date.getDate() === parsed.day
}

export function FilterBar({ onFilterChange, franquicias, initialFilters, onExportClick }: FilterBarProps) {
  const today = new Date()
  const currentYear = today.getFullYear()
  const currentMonth = today.getMonth()

  // Default: first to last day of current month
  const lastDayOfCurrentMonth = getLastDayOfMonth(currentYear, currentMonth)
  const defaultDesdeApi = formatDateApi(currentYear, currentMonth, 1)
  const defaultHastaApi = formatDateApi(currentYear, currentMonth, lastDayOfCurrentMonth)
  const defaultDesdeDisplay = formatDateDisplay(currentYear, currentMonth, 1)
  const defaultHastaDisplay = formatDateDisplay(currentYear, currentMonth, lastDayOfCurrentMonth)

  // Applied filters (what's actually used for queries) - stored in API format (yyyy-mm-dd)
  const [appliedFilters, setAppliedFilters] = useState<FilterValues>({
    fechaDesde: initialFilters?.fechaDesde || defaultDesdeApi,
    fechaHasta: initialFilters?.fechaHasta || defaultHastaApi,
    pais: initialFilters?.pais || '',
    franquiciaId: initialFilters?.franquiciaId ?? null,
  })

  // Local state for editing (display format dd-mm-yyyy)
  const [localDesde, setLocalDesde] = useState(initialFilters?.fechaDesde ? apiToDisplay(initialFilters.fechaDesde) : defaultDesdeDisplay)
  const [localHasta, setLocalHasta] = useState(initialFilters?.fechaHasta ? apiToDisplay(initialFilters.fechaHasta) : defaultHastaDisplay)
  const [localPais, setLocalPais] = useState(appliedFilters.pais)
  const [localFranquiciaId, setLocalFranquiciaId] = useState<number | null>(appliedFilters.franquiciaId)

  // Year/Month quick selectors
  const [selectedYear, setSelectedYear] = useState(currentYear)
  const [selectedMonth, setSelectedMonth] = useState(currentMonth)

  // Mobile: show/hide filters
  const [showFilters, setShowFilters] = useState(false)

  // Track if there are pending changes
  const [hasChanges, setHasChanges] = useState(false)

  // Searchable franchise dropdown
  const [franquiciaDropdownOpen, setFranquiciaDropdownOpen] = useState(false)
  const [franquiciaSearch, setFranquiciaSearch] = useState('')
  const franquiciaDropdownRef = useRef<HTMLDivElement>(null)

  const paises = [...new Set(franquicias.map(f => f.pais).filter(Boolean))]

  // Filtered franchises based on search and country
  const filteredFranquicias = useMemo(() => {
    let list = franquicias
    // Filter by country if selected
    if (localPais) {
      list = list.filter(f => f.pais === localPais)
    }
    // Filter by search term
    if (franquiciaSearch.trim()) {
      const search = franquiciaSearch.toLowerCase()
      list = list.filter(f =>
        f.nombre.toLowerCase().includes(search) ||
        f.pais.toLowerCase().includes(search)
      )
    }
    return list
  }, [franquicias, localPais, franquiciaSearch])

  // Selected franchise display name
  const selectedFranquiciaDisplay = useMemo(() => {
    if (!localFranquiciaId) return null
    const f = franquicias.find(fr => fr.id === localFranquiciaId)
    return f ? `${f.nombre} (${f.pais})` : null
  }, [franquicias, localFranquiciaId])

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

  // Years available (current year and 2 years back)
  const availableYears = [currentYear, currentYear - 1, currentYear - 2]

  const months = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
  ]

  // Check for changes whenever local values change
  useEffect(() => {
    const changed =
      displayToApi(localDesde) !== appliedFilters.fechaDesde ||
      displayToApi(localHasta) !== appliedFilters.fechaHasta ||
      localPais !== appliedFilters.pais ||
      localFranquiciaId !== appliedFilters.franquiciaId
    setHasChanges(changed)
  }, [localDesde, localHasta, localPais, localFranquiciaId, appliedFilters])

  // Sync year/month selectors with localDesde when it changes
  useEffect(() => {
    const parsed = parseDateDisplay(localDesde)
    if (parsed) {
      setSelectedYear(parsed.year)
      setSelectedMonth(parsed.month)
    }
  }, [localDesde])

  // Handle year/month selection - auto-fill dates (display format dd-mm-yyyy)
  const handleYearMonthChange = (year: number, month: number) => {
    setSelectedYear(year)
    setSelectedMonth(month)
    const lastDay = getLastDayOfMonth(year, month)
    setLocalDesde(formatDateDisplay(year, month, 1))
    setLocalHasta(formatDateDisplay(year, month, lastDay))
  }

  // Apply filters
  const applyFilters = () => {
    // Validate dates before applying (display format dd-mm-yyyy)
    if (!isValidDisplayDate(localDesde) || !isValidDisplayDate(localHasta)) {
      alert('Por favor ingrese fechas válidas en formato DD-MM-YYYY')
      return
    }

    // Convert display format to API format for storage
    const newFilters: FilterValues = {
      fechaDesde: displayToApi(localDesde),
      fechaHasta: displayToApi(localHasta),
      pais: localPais,
      franquiciaId: localFranquiciaId,
    }
    setAppliedFilters(newFilters)
    onFilterChange(newFilters)
    setHasChanges(false)

    // Close filters on mobile after applying
    if (window.innerWidth < 768) {
      setShowFilters(false)
    }
  }

  // Clear filters
  const clearFilters = () => {
    setLocalDesde(defaultDesdeDisplay)
    setLocalHasta(defaultHastaDisplay)
    setLocalPais('')
    setLocalFranquiciaId(null)
    setSelectedYear(currentYear)
    setSelectedMonth(currentMonth)

    const defaultFilters: FilterValues = {
      fechaDesde: defaultDesdeApi,
      fechaHasta: defaultHastaApi,
      pais: '',
      franquiciaId: null,
    }
    setAppliedFilters(defaultFilters)
    onFilterChange(defaultFilters)
    setHasChanges(false)
  }

  const hasActiveFilters = appliedFilters.pais !== '' || appliedFilters.franquiciaId !== null

  // Calculate days in range
  const diasRango = Math.ceil(
    (new Date(appliedFilters.fechaHasta).getTime() - new Date(appliedFilters.fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
  ) + 1

  return (
    <Card className="mb-6">
      <CardContent className="p-4">
        {/* Mobile toggle button */}
        <div className="md:hidden flex items-center justify-between mb-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setShowFilters(!showFilters)}
            className="flex items-center gap-2"
          >
            <Filter className="w-4 h-4" />
            Filtros
            {showFilters ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </Button>
          <span className="text-xs text-muted-foreground">
            {apiToDisplay(appliedFilters.fechaDesde)} - {apiToDisplay(appliedFilters.fechaHasta)} ({diasRango} días)
          </span>
        </div>

        {/* Filter content - hidden on mobile unless showFilters is true */}
        <div className={`${showFilters ? 'block' : 'hidden'} md:block`}>
          <div className="flex flex-wrap items-center gap-4">
            {/* Desktop: Filter icon and label */}
            <div className="hidden md:flex items-center gap-2">
              <Filter className="w-4 h-4 text-muted-foreground" />
              <span className="text-sm font-medium">Filtros:</span>
            </div>

            {/* Year/Month quick selector */}
            <div className="flex items-center gap-2 border-r pr-4 dark:border-gray-600">
              <span className="text-xs text-muted-foreground">Rápido:</span>
              <select
                value={selectedYear}
                onChange={(e) => handleYearMonthChange(Number(e.target.value), selectedMonth)}
                className="px-2 py-1 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {availableYears.map(year => (
                  <option key={year} value={year}>{year}</option>
                ))}
              </select>
              <select
                value={selectedMonth}
                onChange={(e) => handleYearMonthChange(selectedYear, Number(e.target.value))}
                className="px-2 py-1 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {months.map((month, idx) => (
                  <option key={idx} value={idx}>{month}</option>
                ))}
              </select>
            </div>

            {/* Manual date inputs */}
            <div className="flex items-center gap-2">
              <span className="text-xs text-muted-foreground">Desde:</span>
              <div className="relative flex items-center">
                <input
                  type="text"
                  value={localDesde}
                  onChange={(e) => setLocalDesde(e.target.value)}
                  placeholder="DD-MM-YYYY"
                  className={`px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white w-32 ${
                    !isValidDisplayDate(localDesde) && localDesde.length === 10 ? 'border-red-500' : ''
                  }`}
                />
                <button
                  type="button"
                  onClick={() => {
                    const input = document.getElementById('date-picker-desde') as HTMLInputElement
                    input?.showPicker()
                  }}
                  className="absolute right-1 p-1 text-muted-foreground hover:text-foreground"
                  title="Abrir calendario"
                >
                  <Calendar className="w-4 h-4" />
                </button>
                <input
                  id="date-picker-desde"
                  type="date"
                  value={displayToApi(localDesde)}
                  onChange={(e) => setLocalDesde(apiToDisplay(e.target.value))}
                  className="absolute opacity-0 w-0 h-0"
                  tabIndex={-1}
                />
              </div>

              <span className="text-muted-foreground">a</span>

              <span className="text-xs text-muted-foreground">Hasta:</span>
              <div className="relative flex items-center">
                <input
                  type="text"
                  value={localHasta}
                  onChange={(e) => setLocalHasta(e.target.value)}
                  placeholder="DD-MM-YYYY"
                  className={`px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white w-32 ${
                    !isValidDisplayDate(localHasta) && localHasta.length === 10 ? 'border-red-500' : ''
                  }`}
                />
                <button
                  type="button"
                  onClick={() => {
                    const input = document.getElementById('date-picker-hasta') as HTMLInputElement
                    input?.showPicker()
                  }}
                  className="absolute right-1 p-1 text-muted-foreground hover:text-foreground"
                  title="Abrir calendario"
                >
                  <Calendar className="w-4 h-4" />
                </button>
                <input
                  id="date-picker-hasta"
                  type="date"
                  value={displayToApi(localHasta)}
                  onChange={(e) => setLocalHasta(apiToDisplay(e.target.value))}
                  className="absolute opacity-0 w-0 h-0"
                  tabIndex={-1}
                />
              </div>
            </div>

            {/* Pais */}
            <select
              value={localPais}
              onChange={(e) => setLocalPais(e.target.value)}
              className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
            >
              <option value="">Todos los países</option>
              {paises.map(pais => (
                <option key={pais} value={pais}>{pais}</option>
              ))}
            </select>

            {/* Franquicia - Searchable Dropdown */}
            <div className="relative" ref={franquiciaDropdownRef}>
              {/* Trigger button */}
              <button
                type="button"
                onClick={() => setFranquiciaDropdownOpen(!franquiciaDropdownOpen)}
                className="flex items-center justify-between gap-2 px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 min-w-[200px] text-left"
              >
                <div className="flex items-center gap-2">
                  <Store className="h-3.5 w-3.5 text-muted-foreground" />
                  <span className={selectedFranquiciaDisplay ? 'text-gray-900 dark:text-white' : 'text-muted-foreground'}>
                    {selectedFranquiciaDisplay || 'Todas las franquicias'}
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  {localFranquiciaId && (
                    <span
                      onClick={(e) => {
                        e.stopPropagation()
                        setLocalFranquiciaId(null)
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
                <div className="absolute z-50 mt-1 w-full min-w-[280px] bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg">
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
                    {/* "All" option */}
                    <button
                      type="button"
                      onClick={() => {
                        setLocalFranquiciaId(null)
                        setFranquiciaDropdownOpen(false)
                        setFranquiciaSearch('')
                      }}
                      className={`w-full px-3 py-2 text-sm text-left hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center justify-between ${
                        !localFranquiciaId
                          ? 'bg-cabrera-burgundy/10 text-cabrera-burgundy font-medium'
                          : 'text-gray-900 dark:text-white'
                      }`}
                    >
                      <span>Todas las franquicias</span>
                    </button>

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
                            setLocalFranquiciaId(f.id)
                            setFranquiciaDropdownOpen(false)
                            setFranquiciaSearch('')
                          }}
                          className={`w-full px-3 py-2 text-sm text-left hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center justify-between ${
                            localFranquiciaId === f.id
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

            {/* Apply button - highlighted when there are changes */}
            <Button
              variant={hasChanges ? "default" : "outline"}
              size="sm"
              onClick={applyFilters}
              className={hasChanges
                ? "bg-cabrera-burgundy hover:bg-cabrera-burgundy/90 text-white animate-pulse"
                : ""}
            >
              <Search className="w-4 h-4 mr-1" />
              Aplicar Filtros
            </Button>

            {/* Clear button */}
            {hasActiveFilters && (
              <Button
                variant="ghost"
                size="sm"
                onClick={clearFilters}
                className="text-muted-foreground hover:text-foreground"
              >
                <X className="w-4 h-4 mr-1" />
                Limpiar
              </Button>
            )}

            {/* Days indicator - desktop only */}
            <span className="hidden md:inline text-xs text-muted-foreground">
              ({diasRango} días)
            </span>

            {/* Export button - hidden on mobile */}
            {onExportClick && (
              <Button
                variant="outline"
                size="sm"
                onClick={onExportClick}
                className="hidden md:flex ml-auto text-green-700 border-green-300 hover:bg-green-50 hover:text-green-800 dark:text-green-400 dark:border-green-700 dark:hover:bg-green-900/30"
              >
                <FileSpreadsheet className="w-4 h-4 mr-1" />
                Exportar Excel
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

export function useDiasRango(fechaDesde: string, fechaHasta: string): number {
  return Math.ceil(
    (new Date(fechaHasta).getTime() - new Date(fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
  ) + 1
}
