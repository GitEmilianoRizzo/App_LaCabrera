import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatCurrency(value: number, currency: string = 'USD', decimals: number = 0): string {
  // Para monedas latinoamericanas usar formato local
  const latinCurrencies = ['ARS', 'CLP', 'COP', 'MXN', 'PEN', 'PYG']

  if (latinCurrencies.includes(currency)) {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: currency,
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    }).format(value)
  }

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency,
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value)
}

export function formatNumber(value: number, decimals: number = 0): string {
  return new Intl.NumberFormat('es-ES', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value)
}

export function formatPercent(value: number): string {
  return new Intl.NumberFormat('es-ES', {
    style: 'percent',
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value / 100)
}

export function formatDate(date: string | Date, format: 'short' | 'long' | 'time' = 'short'): string {
  const d = typeof date === 'string' ? new Date(date) : date

  if (format === 'short') {
    return d.toLocaleDateString('es-ES', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })
  }

  if (format === 'long') {
    return d.toLocaleDateString('es-ES', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    })
  }

  return d.toLocaleString('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function getStatusColor(status: string): string {
  switch (status?.toUpperCase()) {
    case 'ACTIVE':
    case 'PROCESSED':
    case 'OK':
      return 'text-green-600 bg-green-50'
    case 'WARNING':
    case 'ACCEPTED_WITH_WARNINGS':
      return 'text-yellow-600 bg-yellow-50'
    case 'ERROR':
    case 'FAILED':
    case 'REJECTED':
    case 'CRITICAL':
      return 'text-red-600 bg-red-50'
    case 'PENDING':
    case 'RECEIVED':
    case 'VALIDATING':
      return 'text-blue-600 bg-blue-50'
    case 'INACTIVE':
    case 'NEVER_SYNCED':
      return 'text-gray-600 bg-gray-50'
    default:
      return 'text-gray-600 bg-gray-50'
  }
}

export function getAlertIcon(level: string): string {
  switch (level?.toUpperCase()) {
    case 'OK':
      return '✓'
    case 'WARNING':
      return '⚠'
    case 'CRITICAL':
      return '✕'
    case 'NEVER_SYNCED':
      return '○'
    default:
      return '?'
  }
}

export function calculateVariation(current: number, previous: number): number {
  if (previous === 0) return current > 0 ? 100 : 0
  return ((current - previous) / previous) * 100
}

// Para graficos - generar colores consistentes
export const CHART_COLORS = [
  '#C4A35A', // Gold (primary)
  '#722F37', // Burgundy
  '#2D2D2D', // Charcoal
  '#4A90A4', // Teal
  '#8B5E3C', // Brown
  '#5C6BC0', // Indigo
  '#26A69A', // Cyan
  '#EF5350', // Red
  '#AB47BC', // Purple
  '#66BB6A', // Green
]

export function getChartColor(index: number): string {
  return CHART_COLORS[index % CHART_COLORS.length]
}
