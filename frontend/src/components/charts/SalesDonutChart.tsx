import {
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  Tooltip,
  Legend,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatPercent, CHART_COLORS } from '@/lib/utils'

interface DataPoint {
  name: string
  value: number
  percent?: number
}

interface SalesDonutChartProps {
  title: string
  data: DataPoint[]
  currency?: string
  showPercent?: boolean
  className?: string
}

export function SalesDonutChart({
  title,
  data,
  currency = 'USD',
  showPercent = true,
  className,
}: SalesDonutChartProps) {
  const total = data.reduce((sum, item) => sum + item.value, 0)

  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="chart-container">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={data}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={100}
                paddingAngle={2}
                dataKey="value"
                label={({ name, percent }) =>
                  showPercent ? `${name} ${formatPercent(percent * 100)}` : name
                }
                labelLine={{ stroke: '#9ca3af', strokeWidth: 1 }}
              >
                {data.map((_, index) => (
                  <Cell key={`cell-${index}`} fill={CHART_COLORS[index % CHART_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip
                contentStyle={{
                  backgroundColor: 'white',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                  boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                }}
                formatter={(value: number) => [
                  formatCurrency(value, currency),
                  `${((value / total) * 100).toFixed(1)}%`,
                ]}
              />
              <Legend
                verticalAlign="bottom"
                height={36}
                formatter={(value) => <span className="text-sm">{value}</span>}
              />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  )
}
