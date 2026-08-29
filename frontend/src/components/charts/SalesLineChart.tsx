import { useState, useMemo } from 'react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatNumber } from '@/lib/utils'
import { CHART_COLORS } from '@/lib/utils'
import { ChevronDown, Check } from 'lucide-react'

interface DataPoint {
  fecha: string
  [key: string]: string | number
}

export interface MetricOption {
  id: string
  label: string
  format: 'currency' | 'number' | 'percent'
}

interface SalesLineChartProps {
  title: string
  data: DataPoint[]
  dataKeys: { key: string; name: string; color?: string }[]
  currency?: string
  className?: string
  metrics?: MetricOption[]
  selectedMetric?: string
  onMetricChange?: (metricId: string) => void
  enableFranchiseSelector?: boolean
  maxDefaultSelected?: number
}

const DEFAULT_METRICS: MetricOption[] = [
  { id: 'venta_neta', label: 'Venta Neta', format: 'currency' },
]

export function SalesLineChart({
  title,
  data,
  dataKeys,
  currency = 'USD',
  className,
  metrics = DEFAULT_METRICS,
  selectedMetric,
  onMetricChange,
  enableFranchiseSelector = true,
  maxDefaultSelected = 5,
}: SalesLineChartProps) {
  const [internalMetric, setInternalMetric] = useState(metrics[0]?.id || 'venta_neta')
  const [selectorOpen, setSelectorOpen] = useState(false)

  // Por defecto solo mostrar las primeras N franquicias si hay muchas
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(() => {
    const initialKeys = new Set<string>()
    dataKeys.slice(0, maxDefaultSelected).forEach(dk => initialKeys.add(dk.key))
    return initialKeys
  })

  const currentMetricId = selectedMetric ?? internalMetric
  const currentMetric = metrics.find(m => m.id === currentMetricId) || metrics[0]

  const handleMetricChange = (metricId: string) => {
    if (onMetricChange) {
      onMetricChange(metricId)
    } else {
      setInternalMetric(metricId)
    }
  }

  const toggleKey = (key: string) => {
    setSelectedKeys(prev => {
      const newSet = new Set(prev)
      if (newSet.has(key)) {
        newSet.delete(key)
      } else {
        newSet.add(key)
      }
      return newSet
    })
  }

  const selectAll = () => {
    setSelectedKeys(new Set(dataKeys.map(dk => dk.key)))
  }

  const deselectAll = () => {
    setSelectedKeys(new Set())
  }

  // Filtrar dataKeys según selección
  const visibleDataKeys = useMemo(() => {
    if (!enableFranchiseSelector || selectedKeys.size === 0) {
      return dataKeys
    }
    return dataKeys.filter(dk => selectedKeys.has(dk.key))
  }, [dataKeys, selectedKeys, enableFranchiseSelector])

  const formatValue = (value: number): string => {
    if (!currentMetric) return value.toString()

    switch (currentMetric.format) {
      case 'currency':
        return formatCurrency(value, currency)
      case 'percent':
        return `${formatNumber(value, 1)}%`
      case 'number':
      default:
        return formatNumber(value, 1)
    }
  }

  const formatYAxis = (value: number): string => {
    if (currentMetric?.format === 'percent') {
      return `${value}%`
    }
    if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`
    if (value >= 1000) return `${(value / 1000).toFixed(0)}K`
    return value.toString()
  }

  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between flex-wrap gap-2">
          <CardTitle className="text-base font-semibold">{title}</CardTitle>
          <div className="flex items-center gap-2">
            {/* Selector de franquicias */}
            {enableFranchiseSelector && dataKeys.length > 1 && (
              <div className="relative">
                <button
                  onClick={() => setSelectorOpen(!selectorOpen)}
                  className="flex items-center gap-2 px-3 py-1.5 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white hover:bg-gray-50 dark:hover:bg-gray-700"
                >
                  <span>Franquicias ({selectedKeys.size}/{dataKeys.length})</span>
                  <ChevronDown className={`w-4 h-4 transition-transform ${selectorOpen ? 'rotate-180' : ''}`} />
                </button>
                {selectorOpen && (
                  <>
                    <div
                      className="fixed inset-0 z-40"
                      onClick={() => setSelectorOpen(false)}
                    />
                    <div className="absolute right-0 top-full mt-1 z-50 w-64 max-h-80 overflow-auto bg-white dark:bg-gray-800 border dark:border-gray-600 rounded-lg shadow-lg">
                      <div className="sticky top-0 bg-white dark:bg-gray-800 border-b dark:border-gray-600 p-2 flex gap-2">
                        <button
                          onClick={selectAll}
                          className="flex-1 text-xs px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200 dark:hover:bg-gray-600"
                        >
                          Todas
                        </button>
                        <button
                          onClick={deselectAll}
                          className="flex-1 text-xs px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200 dark:hover:bg-gray-600"
                        >
                          Ninguna
                        </button>
                      </div>
                      <div className="p-2 space-y-1">
                        {dataKeys.map((dk, index) => (
                          <label
                            key={dk.key}
                            className="flex items-center gap-2 p-1.5 rounded cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700"
                          >
                            <div
                              className={`w-4 h-4 rounded border flex items-center justify-center ${
                                selectedKeys.has(dk.key)
                                  ? 'bg-cabrera-burgundy border-cabrera-burgundy'
                                  : 'border-gray-300 dark:border-gray-500'
                              }`}
                              onClick={() => toggleKey(dk.key)}
                            >
                              {selectedKeys.has(dk.key) && <Check className="w-3 h-3 text-white" />}
                            </div>
                            <span
                              className="w-3 h-3 rounded-full"
                              style={{ backgroundColor: dk.color || CHART_COLORS[index] }}
                            />
                            <span className="text-sm truncate flex-1">{dk.name}</span>
                          </label>
                        ))}
                      </div>
                    </div>
                  </>
                )}
              </div>
            )}
            {/* Selector de métrica */}
            {metrics.length > 1 && (
              <select
                value={currentMetricId}
                onChange={(e) => handleMetricChange(e.target.value)}
                className="px-3 py-1.5 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
              >
                {metrics.map((metric) => (
                  <option key={metric.id} value={metric.id}>
                    {metric.label}
                  </option>
                ))}
              </select>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="chart-container">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis
                dataKey="fecha"
                tick={{ fontSize: 12 }}
                tickLine={false}
                axisLine={{ stroke: '#e5e7eb' }}
              />
              <YAxis
                tick={{ fontSize: 12 }}
                tickLine={false}
                axisLine={{ stroke: '#e5e7eb' }}
                tickFormatter={formatYAxis}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'var(--tooltip-bg, white)',
                  border: '1px solid var(--tooltip-border, #e5e7eb)',
                  borderRadius: '8px',
                  boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                  color: 'var(--tooltip-text, #1f2937)',
                }}
                formatter={(value: number) => [formatValue(value), '']}
              />
              <Legend />
              {visibleDataKeys.map((dk) => {
                const originalIndex = dataKeys.findIndex(d => d.key === dk.key)
                return (
                  <Line
                    key={dk.key}
                    type="monotone"
                    dataKey={dk.key}
                    name={dk.name}
                    stroke={dk.color || CHART_COLORS[originalIndex]}
                    strokeWidth={2}
                    dot={{ r: 3 }}
                    activeDot={{ r: 5 }}
                  />
                )
              })}
            </LineChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  )
}
