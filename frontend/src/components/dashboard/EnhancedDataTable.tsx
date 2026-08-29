import { useState, useEffect, useMemo, useCallback } from 'react'
import { Search, Settings2, ArrowUpDown, ArrowUp, ArrowDown, GripVertical } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { preferenciasApi } from '@/services/api'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragEndEvent,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  horizontalListSortingStrategy,
  useSortable,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'

export interface ColumnDef<T> {
  id: string
  header: string
  accessorKey?: keyof T
  cell?: (row: T) => React.ReactNode
  align?: 'left' | 'center' | 'right'
  sortable?: boolean
  defaultVisible?: boolean
  className?: string
}

interface EnhancedDataTableProps<T> {
  title: string
  subtitle?: string
  data: T[]
  columns: ColumnDef<T>[]
  vistaId: string
  searchPlaceholder?: string
  footerRow?: React.ReactNode
  defaultSortColumn?: string
  defaultSortDirection?: 'asc' | 'desc'
  onRowClick?: (row: T) => void
}

type SortDirection = 'asc' | 'desc' | null

// Componente para cabecera de columna arrastrable
interface SortableHeaderProps {
  column: ColumnDef<unknown>
  sortColumn: string | null
  sortDirection: SortDirection
  onSort: (columnId: string) => void
  renderSortIcon: (columnId: string) => React.ReactNode
}

function SortableHeader({ column, sortColumn, sortDirection, onSort, renderSortIcon }: SortableHeaderProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: column.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  return (
    <th
      ref={setNodeRef}
      style={style}
      className={`p-3 font-medium text-gray-900 dark:text-gray-100 whitespace-nowrap ${
        column.align === 'right' ? 'text-right' :
        column.align === 'center' ? 'text-center' : 'text-left'
      } ${isDragging ? 'bg-blue-50 dark:bg-blue-900/20' : ''}`}
    >
      <div className={`flex items-center gap-1 ${
        column.align === 'right' ? 'justify-end' :
        column.align === 'center' ? 'justify-center' : 'justify-start'
      }`}>
        <span
          {...attributes}
          {...listeners}
          className="cursor-grab active:cursor-grabbing p-1 hover:bg-gray-200 dark:hover:bg-gray-700 rounded"
          title="Arrastrar para reordenar"
        >
          <GripVertical className="h-3 w-3 text-gray-400" />
        </span>
        <span
          className={`${column.sortable ? 'cursor-pointer select-none hover:text-blue-600 dark:hover:text-blue-400' : ''}`}
          onClick={() => column.sortable && onSort(column.id)}
        >
          {column.header}
        </span>
        {renderSortIcon(column.id)}
      </div>
    </th>
  )
}

export function EnhancedDataTable<T extends Record<string, unknown>>({
  title,
  subtitle,
  data,
  columns,
  vistaId,
  searchPlaceholder = 'Buscar...',
  footerRow,
  defaultSortColumn,
  defaultSortDirection = 'desc',
  onRowClick,
}: EnhancedDataTableProps<T>) {
  // Estado para columnas visibles (array de IDs en el orden actual)
  const [columnOrder, setColumnOrder] = useState<string[]>(() =>
    columns.filter(c => c.defaultVisible !== false).map(c => c.id)
  )
  const [searchTerm, setSearchTerm] = useState('')
  const [sortColumn, setSortColumn] = useState<string | null>(defaultSortColumn || null)
  const [sortDirection, setSortDirection] = useState<SortDirection>(defaultSortColumn ? defaultSortDirection : null)
  const [prefsLoaded, setPrefsLoaded] = useState(false)

  // Sensores para drag & drop
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  )

  // Cargar preferencias (columnas visibles y orden)
  useEffect(() => {
    const loadPreferences = async () => {
      try {
        const prefs = await preferenciasApi.getColumnPreferences(vistaId)
        if (prefs && prefs.columnas_visibles && prefs.columnas_visibles.length > 0) {
          // Validar que las columnas existan
          const validColumns = prefs.columnas_visibles.filter(id =>
            columns.some(c => c.id === id)
          )
          if (validColumns.length > 0) {
            setColumnOrder(validColumns)
          }
        }
      } catch (e) {
        console.error('Error cargando preferencias:', e)
      } finally {
        setPrefsLoaded(true)
      }
    }
    loadPreferences()
  }, [vistaId, columns])

  // Guardar preferencias
  const savePreferences = useCallback(async (newColumnOrder: string[]) => {
    try {
      await preferenciasApi.saveColumnPreferences(vistaId, newColumnOrder)
    } catch (error) {
      console.error('Error guardando preferencias:', error)
    }
  }, [vistaId])

  // Toggle visibilidad de columna
  const toggleColumn = (columnId: string) => {
    setColumnOrder(prev => {
      const newOrder = prev.includes(columnId)
        ? prev.filter(id => id !== columnId)
        : [...prev, columnId]
      savePreferences(newOrder)
      return newOrder
    })
  }

  // Manejar fin de drag
  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event

    if (over && active.id !== over.id) {
      setColumnOrder((items) => {
        const oldIndex = items.indexOf(active.id as string)
        const newIndex = items.indexOf(over.id as string)
        const newOrder = arrayMove(items, oldIndex, newIndex)
        savePreferences(newOrder)
        return newOrder
      })
    }
  }

  // Filtrar por búsqueda
  const filteredData = useMemo(() => {
    if (!searchTerm.trim()) return data
    const term = searchTerm.toLowerCase().trim()
    return data.filter(row => {
      return columns.some(col => {
        const value = col.accessorKey ? row[col.accessorKey] : null
        if (value == null) return false
        return String(value).toLowerCase().includes(term)
      })
    })
  }, [data, searchTerm, columns])

  // Ordenar
  const sortedData = useMemo(() => {
    if (!sortColumn || !sortDirection) return filteredData
    const column = columns.find(c => c.id === sortColumn)
    if (!column || !column.accessorKey) return filteredData

    return [...filteredData].sort((a, b) => {
      const aVal = a[column.accessorKey!]
      const bVal = b[column.accessorKey!]

      if (aVal == null && bVal == null) return 0
      if (aVal == null) return sortDirection === 'asc' ? 1 : -1
      if (bVal == null) return sortDirection === 'asc' ? -1 : 1

      if (typeof aVal === 'number' && typeof bVal === 'number') {
        return sortDirection === 'asc' ? aVal - bVal : bVal - aVal
      }

      const strA = String(aVal).toLowerCase()
      const strB = String(bVal).toLowerCase()
      if (strA < strB) return sortDirection === 'asc' ? -1 : 1
      if (strA > strB) return sortDirection === 'asc' ? 1 : -1
      return 0
    })
  }, [filteredData, sortColumn, sortDirection, columns])

  const handleSort = (columnId: string) => {
    const column = columns.find(c => c.id === columnId)
    if (!column?.sortable) return

    if (sortColumn !== columnId) {
      setSortColumn(columnId)
      setSortDirection('asc')
    } else if (sortDirection === 'asc') {
      setSortDirection('desc')
    } else {
      setSortColumn(null)
      setSortDirection(null)
    }
  }

  const renderSortIcon = (columnId: string) => {
    const column = columns.find(c => c.id === columnId)
    if (!column?.sortable) return null
    if (sortColumn !== columnId) {
      return <ArrowUpDown className="ml-1 h-3 w-3 opacity-50" />
    }
    return sortDirection === 'asc'
      ? <ArrowUp className="ml-1 h-3 w-3 text-blue-500" />
      : <ArrowDown className="ml-1 h-3 w-3 text-blue-500" />
  }

  // Columnas ordenadas según columnOrder
  const displayColumns = useMemo(() => {
    return columnOrder
      .map(id => columns.find(c => c.id === id))
      .filter((c): c is ColumnDef<T> => c !== undefined)
  }, [columnOrder, columns])

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between flex-wrap gap-4">
          <div>
            <CardTitle className="text-base font-semibold">{title}</CardTitle>
            {subtitle && (
              <p className="text-xs text-muted-foreground">{subtitle}</p>
            )}
          </div>

          <div className="flex items-center gap-2">
            {/* Buscador */}
            <div className="relative">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                type="text"
                placeholder={searchPlaceholder}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-8 h-9 w-[180px] md:w-[220px]"
              />
            </div>

            {/* Selector de columnas */}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm" className="h-9">
                  <Settings2 className="h-4 w-4 mr-2" />
                  Columnas
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-[220px] max-h-[400px] overflow-y-auto">
                <DropdownMenuLabel>Columnas visibles</DropdownMenuLabel>
                <DropdownMenuSeparator />
                {columns.map((column) => (
                  <DropdownMenuCheckboxItem
                    key={column.id}
                    checked={columnOrder.includes(column.id)}
                    onCheckedChange={() => toggleColumn(column.id)}
                  >
                    {column.header}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </CardHeader>

      <CardContent>
        <div className="overflow-x-auto">
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
          >
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-gray-50 dark:bg-gray-800">
                  <SortableContext
                    items={columnOrder}
                    strategy={horizontalListSortingStrategy}
                  >
                    {displayColumns.map((column) => (
                      <SortableHeader
                        key={column.id}
                        column={column as ColumnDef<unknown>}
                        sortColumn={sortColumn}
                        sortDirection={sortDirection}
                        onSort={handleSort}
                        renderSortIcon={renderSortIcon}
                      />
                    ))}
                  </SortableContext>
                </tr>
              </thead>
              <tbody>
                {sortedData.length === 0 ? (
                  <tr>
                    <td
                      colSpan={displayColumns.length}
                      className="p-8 text-center text-muted-foreground"
                    >
                      {searchTerm ? 'No se encontraron resultados' : 'No hay datos disponibles'}
                    </td>
                  </tr>
                ) : (
                  sortedData.map((row, rowIndex) => (
                    <tr
                      key={rowIndex}
                      className={`border-b border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 ${
                        onRowClick ? 'cursor-pointer' : ''
                      }`}
                      onClick={() => onRowClick?.(row)}
                    >
                      {displayColumns.map((column) => (
                        <td
                          key={column.id}
                          className={`p-3 ${column.className || ''} ${
                            column.align === 'right' ? 'text-right' :
                            column.align === 'center' ? 'text-center' : 'text-left'
                          }`}
                        >
                          {column.cell
                            ? column.cell(row)
                            : column.accessorKey
                            ? String(row[column.accessorKey] ?? '-')
                            : '-'}
                        </td>
                      ))}
                    </tr>
                  ))
                )}
              </tbody>
              {footerRow && (
                <tfoot>
                  {footerRow}
                </tfoot>
              )}
            </table>
          </DndContext>
        </div>

        <div className="flex items-center justify-between mt-4 text-xs text-muted-foreground">
          <span>
            Mostrando {sortedData.length} de {data.length} filas
            {columnOrder.length < columns.length && ` | ${columnOrder.length}/${columns.length} columnas`}
          </span>
          <div className="flex items-center gap-2">
            <span className="text-gray-400">
              <GripVertical className="h-3 w-3 inline" /> Arrastra columnas para reordenar
            </span>
            {searchTerm && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setSearchTerm('')}
                className="h-7 text-xs"
              >
                Limpiar búsqueda
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
