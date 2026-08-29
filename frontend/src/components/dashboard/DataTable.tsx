import { useState, useMemo } from 'react'
import { ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

interface Column<T> {
  key: keyof T | string
  header: string
  align?: 'left' | 'center' | 'right'
  render?: (item: T) => React.ReactNode
  className?: string
  sortable?: boolean
  // For sorting, use this key to access the value (defaults to 'key')
  sortKey?: keyof T
}

type SortDirection = 'asc' | 'desc' | null

interface DataTableProps<T> {
  title?: string
  data: T[]
  columns: Column<T>[]
  className?: string
  maxRows?: number
  defaultSortColumn?: string
  defaultSortDirection?: 'asc' | 'desc'
}

export function DataTable<T extends object>({
  title,
  data,
  columns,
  className,
  maxRows,
  defaultSortColumn,
  defaultSortDirection = 'asc',
}: DataTableProps<T>) {
  const [sortColumn, setSortColumn] = useState<string | null>(defaultSortColumn || null)
  const [sortDirection, setSortDirection] = useState<SortDirection>(defaultSortColumn ? defaultSortDirection : null)

  // Sort data
  const sortedData = useMemo(() => {
    if (!sortColumn || !sortDirection) return data

    const column = columns.find(c => String(c.key) === sortColumn)
    if (!column) return data

    const sortKey = column.sortKey || column.key as keyof T

    return [...data].sort((a, b) => {
      const aVal = a[sortKey]
      const bVal = b[sortKey]

      if (aVal == null && bVal == null) return 0
      if (aVal == null) return sortDirection === 'asc' ? 1 : -1
      if (bVal == null) return sortDirection === 'asc' ? -1 : 1

      // Handle numbers
      if (typeof aVal === 'number' && typeof bVal === 'number') {
        return sortDirection === 'asc' ? aVal - bVal : bVal - aVal
      }

      // Handle dates (ISO strings)
      if (typeof aVal === 'string' && typeof bVal === 'string') {
        // Check if it looks like a date
        if (aVal.match(/^\d{4}-\d{2}-\d{2}/) && bVal.match(/^\d{4}-\d{2}-\d{2}/)) {
          const dateA = new Date(aVal).getTime()
          const dateB = new Date(bVal).getTime()
          return sortDirection === 'asc' ? dateA - dateB : dateB - dateA
        }
      }

      // Handle strings
      const strA = String(aVal).toLowerCase()
      const strB = String(bVal).toLowerCase()
      if (strA < strB) return sortDirection === 'asc' ? -1 : 1
      if (strA > strB) return sortDirection === 'asc' ? 1 : -1
      return 0
    })
  }, [data, sortColumn, sortDirection, columns])

  const displayData = maxRows ? sortedData.slice(0, maxRows) : sortedData

  const handleSort = (columnKey: string) => {
    const column = columns.find(c => String(c.key) === columnKey)
    if (!column?.sortable) return

    if (sortColumn !== columnKey) {
      setSortColumn(columnKey)
      setSortDirection('asc')
    } else if (sortDirection === 'asc') {
      setSortDirection('desc')
    } else {
      setSortColumn(null)
      setSortDirection(null)
    }
  }

  const renderSortIcon = (columnKey: string) => {
    const column = columns.find(c => String(c.key) === columnKey)
    if (!column?.sortable) return null

    if (sortColumn !== columnKey) {
      return <ArrowUpDown className="ml-1 h-3 w-3 opacity-50 inline" />
    }
    return sortDirection === 'asc'
      ? <ArrowUp className="ml-1 h-3 w-3 text-blue-500 inline" />
      : <ArrowDown className="ml-1 h-3 w-3 text-blue-500 inline" />
  }

  return (
    <Card className={className}>
      {title && (
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-semibold">{title}</CardTitle>
        </CardHeader>
      )}
      <CardContent className={cn(!title && 'pt-6')}>
        <div className="overflow-x-auto">
          <table className="data-table">
            <thead>
              <tr className="border-b">
                {columns.map((col) => (
                  <th
                    key={String(col.key)}
                    className={cn(
                      col.align === 'right' && 'text-right',
                      col.align === 'center' && 'text-center',
                      col.className,
                      col.sortable && 'cursor-pointer select-none hover:bg-gray-100 dark:hover:bg-gray-800'
                    )}
                    onClick={() => col.sortable && handleSort(String(col.key))}
                  >
                    <span className="inline-flex items-center">
                      {col.header}
                      {renderSortIcon(String(col.key))}
                    </span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {displayData.length === 0 ? (
                <tr>
                  <td colSpan={columns.length} className="text-center py-8 text-muted-foreground">
                    No hay datos disponibles
                  </td>
                </tr>
              ) : (
                displayData.map((item, idx) => (
                  <tr key={idx}>
                    {columns.map((col) => (
                      <td
                        key={String(col.key)}
                        className={cn(
                          col.align === 'right' && 'text-right',
                          col.align === 'center' && 'text-center',
                          col.className
                        )}
                      >
                        {col.render
                          ? col.render(item)
                          : String(item[col.key as keyof T] ?? '-')}
                      </td>
                    ))}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        {maxRows && data.length > maxRows && (
          <p className="text-sm text-muted-foreground mt-4 text-center">
            Mostrando {maxRows} de {data.length} registros
          </p>
        )}
      </CardContent>
    </Card>
  )
}
