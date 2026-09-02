import { useState } from 'react'
import { Calendar, Filter, X, FileSpreadsheet } from 'lucide-react'
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

export function FilterBar({ onFilterChange, franquicias, initialFilters, onExportClick }: FilterBarProps) {
  const today = new Date()
  const firstDayOfMonth = new Date(today.getFullYear(), today.getMonth(), 1)

  const [filters, setFilters] = useState<FilterValues>({
    fechaDesde: initialFilters?.fechaDesde || firstDayOfMonth.toISOString().split('T')[0],
    fechaHasta: initialFilters?.fechaHasta || today.toISOString().split('T')[0],
    pais: initialFilters?.pais || '',
    franquiciaId: initialFilters?.franquiciaId ?? null,
  })

  const paises = [...new Set(franquicias.map(f => f.pais).filter(Boolean))]

  const handleChange = (field: keyof FilterValues, value: string | number | null) => {
    const newFilters = { ...filters, [field]: value }
    setFilters(newFilters)
    onFilterChange(newFilters)
  }

  const clearFilters = () => {
    const defaultFilters: FilterValues = {
      fechaDesde: firstDayOfMonth.toISOString().split('T')[0],
      fechaHasta: today.toISOString().split('T')[0],
      pais: '',
      franquiciaId: null,
    }
    setFilters(defaultFilters)
    onFilterChange(defaultFilters)
  }

  const hasActiveFilters = filters.pais !== '' || filters.franquiciaId !== null

  // Calcular dias en el rango
  const diasRango = Math.ceil(
    (new Date(filters.fechaHasta).getTime() - new Date(filters.fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
  ) + 1

  return (
    <Card className="mb-6">
      <CardContent className="p-4">
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-muted-foreground" />
            <span className="text-sm font-medium">Filtros:</span>
          </div>

          {/* Fecha Desde */}
          <div className="flex items-center gap-2">
            <Calendar className="w-4 h-4 text-muted-foreground" />
            <input
              type="date"
              value={filters.fechaDesde}
              onChange={(e) => handleChange('fechaDesde', e.target.value)}
              className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
            />
            <span className="text-muted-foreground">a</span>
            <input
              type="date"
              value={filters.fechaHasta}
              onChange={(e) => handleChange('fechaHasta', e.target.value)}
              className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
            />
            <span className="text-xs text-muted-foreground">({diasRango} días)</span>
          </div>

          {/* Pais */}
          <select
            value={filters.pais}
            onChange={(e) => handleChange('pais', e.target.value)}
            className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
          >
            <option value="">Todos los países</option>
            {paises.map(pais => (
              <option key={pais} value={pais}>{pais}</option>
            ))}
          </select>

          {/* Franquicia */}
          <select
            value={filters.franquiciaId ?? ''}
            onChange={(e) => handleChange('franquiciaId', e.target.value ? Number(e.target.value) : null)}
            className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
          >
            <option value="">Todas las franquicias</option>
            {franquicias
              .filter(f => !filters.pais || f.pais === filters.pais)
              .map(f => (
                <option key={f.id} value={f.id}>{f.nombre}</option>
              ))}
          </select>

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

          {/* Export button */}
          {onExportClick && (
            <Button
              variant="outline"
              size="sm"
              onClick={onExportClick}
              className="ml-auto text-green-700 border-green-300 hover:bg-green-50 hover:text-green-800 dark:text-green-400 dark:border-green-700 dark:hover:bg-green-900/30"
            >
              <FileSpreadsheet className="w-4 h-4 mr-1" />
              Exportar Excel
            </Button>
          )}
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
