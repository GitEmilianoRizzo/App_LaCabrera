import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatNumber, CHART_COLORS } from '@/lib/utils'

interface DataPoint {
  name: string
  value: number
  percent?: number
  selected?: boolean
}

interface HorizontalBarChartProps {
  title: string
  subtitle?: string
  data: DataPoint[]
  currency?: string
  showPercent?: boolean
  className?: string
  onBarClick?: (item: DataPoint | null) => void
  selectedItem?: string | null
  valueFormat?: 'currency' | 'number'
  maxBars?: number
}

export function HorizontalBarChart({
  title,
  subtitle,
  data,
  currency = 'USD',
  showPercent = true,
  className,
  onBarClick,
  selectedItem,
  valueFormat = 'currency',
  maxBars = 10,
}: HorizontalBarChartProps) {
  const total = data.reduce((sum, item) => sum + item.value, 0)

  // Limitar cantidad de barras para mejor visualización
  const limitedData = data.slice(0, maxBars)

  const formattedData = limitedData.map((item, index) => ({
    ...item,
    percent: total > 0 ? (item.value / total) * 100 : 0,
    fill: CHART_COLORS[index % CHART_COLORS.length],
    isSelected: selectedItem === item.name,
  }))

  const formatValue = (value: number): string => {
    if (valueFormat === 'currency') {
      return formatCurrency(value, currency)
    }
    return formatNumber(value)
  }

  const handleClick = (data: DataPoint) => {
    if (onBarClick) {
      // Si ya está seleccionado, deseleccionar
      if (selectedItem === data.name) {
        onBarClick(null)
      } else {
        onBarClick(data)
      }
    }
  }

  // Calcular altura dinámica basada en cantidad de barras (mínimo 60px por barra)
  const chartHeight = Math.max(400, formattedData.length * 50)

  return (
    <Card className={className}>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-lg font-bold">{title}</CardTitle>
            {subtitle && <p className="text-sm text-muted-foreground mt-1">{subtitle}</p>}
          </div>
          {selectedItem && onBarClick && (
            <button
              onClick={() => onBarClick(null)}
              className="text-sm font-medium text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 underline px-3 py-1"
            >
              Limpiar filtro
            </button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        <div style={{ height: chartHeight }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={formattedData}
              layout="vertical"
              margin={{ top: 10, right: 40, left: 120, bottom: 10 }}
              barCategoryGap="20%"
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" horizontal={false} />
              <XAxis
                type="number"
                tick={{ fontSize: 14, fontWeight: 500 }}
                tickLine={false}
                axisLine={{ stroke: '#e5e7eb' }}
                tickFormatter={(value) => {
                  if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`
                  if (value >= 1000) return `${(value / 1000).toFixed(0)}K`
                  return value.toString()
                }}
              />
              <YAxis
                type="category"
                dataKey="name"
                tick={{ fontSize: 14, fontWeight: 500 }}
                tickLine={false}
                axisLine={{ stroke: '#e5e7eb' }}
                width={115}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'var(--tooltip-bg, white)',
                  border: '1px solid var(--tooltip-border, #e5e7eb)',
                  borderRadius: '8px',
                  boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                  color: 'var(--tooltip-text, #1f2937)',
                  fontSize: '14px',
                  padding: '12px 16px',
                }}
                formatter={(value: number) => {
                  return [formatValue(value), '']
                }}
                labelFormatter={(label) => <span className="font-bold text-base">{label}</span>}
              />
              <Bar
                dataKey="value"
                radius={[0, 6, 6, 0]}
                cursor={onBarClick ? 'pointer' : 'default'}
                onClick={(data) => handleClick(data)}
                barSize={32}
              >
                {formattedData.map((entry, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={entry.fill}
                    opacity={selectedItem && !entry.isSelected ? 0.3 : 1}
                    stroke={entry.isSelected ? '#1e40af' : 'none'}
                    strokeWidth={entry.isSelected ? 3 : 0}
                  />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
        {/* Leyenda debajo del gráfico - más grande y legible */}
        <div className="mt-6 flex flex-wrap gap-3 justify-center">
          {formattedData.slice(0, 6).map((item, index) => (
            <button
              key={item.name}
              onClick={() => handleClick(item)}
              className={`flex items-center gap-2 text-sm font-medium px-4 py-2 rounded-lg transition-all border ${
                selectedItem === item.name
                  ? 'bg-blue-100 dark:bg-blue-900/50 border-blue-400 dark:border-blue-600 ring-2 ring-blue-400'
                  : 'bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700 hover:bg-gray-100 dark:hover:bg-gray-700'
              } ${selectedItem && selectedItem !== item.name ? 'opacity-40' : ''}`}
            >
              <span
                className="w-4 h-4 rounded"
                style={{ backgroundColor: CHART_COLORS[index % CHART_COLORS.length] }}
              />
              <span className="truncate max-w-[140px]">{item.name}</span>
              {showPercent && (
                <span className="text-muted-foreground font-normal">
                  ({item.percent.toFixed(1)}%)
                </span>
              )}
            </button>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
