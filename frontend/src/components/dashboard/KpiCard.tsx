import { Card, CardContent } from '@/components/ui/card'
import { cn, formatCurrency, formatNumber } from '@/lib/utils'
import { TrendingUp, TrendingDown, Minus } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

interface KpiCardProps {
  title: string
  value: number
  format?: 'currency' | 'number' | 'percent'
  currency?: string
  decimals?: number
  trend?: number
  trendLabel?: string
  subtitle?: string
  icon?: LucideIcon
  iconColor?: string
  className?: string
}

export function KpiCard({
  title,
  value,
  format = 'number',
  currency = 'USD',
  decimals = 0,
  trend,
  trendLabel,
  subtitle,
  icon: Icon,
  iconColor = 'bg-primary/10 text-primary',
  className,
}: KpiCardProps) {
  const formattedValue = format === 'currency'
    ? formatCurrency(value, currency)
    : format === 'percent'
      ? `${formatNumber(value, 1)}%`
      : formatNumber(value, decimals)

  const TrendIcon = trend !== undefined
    ? trend > 0 ? TrendingUp : trend < 0 ? TrendingDown : Minus
    : null

  const trendColor = trend !== undefined
    ? trend > 0 ? 'text-green-600 dark:text-green-400' : trend < 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400'
    : ''

  return (
    <Card className={cn('kpi-card', className)}>
      <CardContent className="p-6">
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <p className="text-sm font-medium text-muted-foreground">{title}</p>
            <p className="mt-2 text-2xl font-bold tracking-tight">{formattedValue}</p>
            {subtitle && (
              <p className="mt-1 text-xs text-muted-foreground">{subtitle}</p>
            )}
            {trend !== undefined && (
              <div className={cn('flex items-center gap-1 mt-1 text-sm', trendColor)}>
                {TrendIcon && <TrendIcon className="w-4 h-4" />}
                <span className="font-medium">{Math.abs(trend).toFixed(1)}%</span>
                {trendLabel && <span className="text-muted-foreground">{trendLabel}</span>}
              </div>
            )}
          </div>
          {Icon && (
            <div className={cn('p-3 rounded-lg', iconColor)}>
              <Icon className="w-5 h-5" />
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
