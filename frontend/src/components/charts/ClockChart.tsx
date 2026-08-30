import { useMemo } from 'react'
import { cn } from '@/lib/utils'

interface HourData {
  hora: number // 0-23
  cubiertos: number
  tickets?: number
  venta?: number
}

interface ClockChartProps {
  title: string
  subtitle?: string
  data: HourData[]
  selectedHour?: number | null
  onHourClick?: (hour: number | null) => void
  metric?: 'cubiertos' | 'tickets' | 'venta'
}

// Interpolar color de verde a rojo
function getHeatColor(value: number, min: number, max: number): string {
  if (max === min || value === 0) return 'transparent'

  const normalized = (value - min) / (max - min)
  // Verde (120) -> Amarillo (60) -> Rojo (0)
  const hue = 120 - (normalized * 120)
  const saturation = 65 + (normalized * 15)
  const lightness = 45 + (normalized * 10)

  return `hsl(${hue}, ${saturation}%, ${lightness}%)`
}

// Formatear valor para tooltip
function formatValue(value: number, metric: string): string {
  if (metric === 'venta') {
    return `$${value.toLocaleString()}`
  }
  return value.toLocaleString()
}

// Componente de un reloj individual (12 horas)
function SingleClock({
  period,
  hourlyData,
  minValue,
  maxValue,
  totalValue,
  selectedHour,
  onHourClick,
  metric,
}: {
  period: 'AM' | 'PM'
  hourlyData: (HourData & { value: number })[]
  minValue: number
  maxValue: number
  totalValue: number
  selectedHour?: number | null
  onHourClick?: (hour: number | null) => void
  metric: string
}) {
  // Tamaño duplicado
  const size = 340
  const center = size / 2
  const outerRadius = 130
  const innerRadius = 55
  const segmentGap = 2 // grados de separación

  // Calcular path para un segmento del reloj (12 segmentos de 30 grados cada uno)
  const getSegmentPath = (hourIndex: number, inner: number, outer: number) => {
    // Cada hora ocupa 30 grados (360/12)
    // El reloj empieza en 12 (arriba), que es -90 grados en coordenadas SVG
    // Hora 12/0 -> arriba (0 grados desde las 12)
    // Hora 1 -> 30 grados, Hora 2 -> 60 grados, etc.
    const degreesPerHour = 30
    const startAngle = (hourIndex * degreesPerHour) + (segmentGap / 2) - 90
    const endAngle = ((hourIndex + 1) * degreesPerHour) - (segmentGap / 2) - 90

    const toRad = (deg: number) => (deg * Math.PI) / 180

    const startRad = toRad(startAngle)
    const endRad = toRad(endAngle)

    const x1 = center + inner * Math.cos(startRad)
    const y1 = center + inner * Math.sin(startRad)
    const x2 = center + outer * Math.cos(startRad)
    const y2 = center + outer * Math.sin(startRad)
    const x3 = center + outer * Math.cos(endRad)
    const y3 = center + outer * Math.sin(endRad)
    const x4 = center + inner * Math.cos(endRad)
    const y4 = center + inner * Math.sin(endRad)

    return `M ${x1} ${y1} L ${x2} ${y2} A ${outer} ${outer} 0 0 1 ${x3} ${y3} L ${x4} ${y4} A ${inner} ${inner} 0 0 0 ${x1} ${y1}`
  }

  // Posición de las etiquetas de hora (12, 3, 6, 9)
  const getLabelPosition = (hourLabel: number, radius: number) => {
    // 12 -> arriba (0 grados), 3 -> derecha (90), 6 -> abajo (180), 9 -> izquierda (270)
    const hourToIndex: Record<number, number> = { 12: 0, 3: 3, 6: 6, 9: 9 }
    const index = hourToIndex[hourLabel] || 0
    const angle = ((index * 30) - 90) * Math.PI / 180
    return {
      x: center + radius * Math.cos(angle),
      y: center + radius * Math.sin(angle),
    }
  }

  const handleSegmentClick = (hour: number) => {
    if (onHourClick) {
      onHourClick(selectedHour === hour ? null : hour)
    }
  }

  // Mapear índice de segmento (0-11) a hora real
  // Para AM: índice 0 = 12AM (hora 0), índice 1 = 1AM, ..., índice 11 = 11AM
  // Para PM: índice 0 = 12PM (hora 12), índice 1 = 1PM (hora 13), ..., índice 11 = 11PM (hora 23)
  const getHourFromIndex = (index: number): number => {
    if (period === 'AM') {
      return index === 0 ? 0 : index // 12AM = 0, 1AM = 1, ..., 11AM = 11
    } else {
      return index === 0 ? 12 : index + 12 // 12PM = 12, 1PM = 13, ..., 11PM = 23
    }
  }

  // Obtener índice de display (0-11) donde 0 = posición de las 12
  const getDisplayIndex = (hour24: number): number => {
    if (period === 'AM') {
      return hour24 === 0 ? 0 : hour24 // 0 (12AM) -> 0, 1-11 -> 1-11
    } else {
      return hour24 === 12 ? 0 : hour24 - 12 // 12 (12PM) -> 0, 13-23 -> 1-11
    }
  }

  return (
    <div className="flex flex-col items-center">
      <div className="text-2xl font-bold text-gray-800 dark:text-gray-100 mb-4 tracking-wide">
        {period === 'AM' ? 'MAÑANA (AM)' : 'TARDE/NOCHE (PM)'}
      </div>
      <svg width={size} height={size} className="overflow-visible">
        {/* Círculo exterior decorativo */}
        <circle
          cx={center}
          cy={center}
          r={outerRadius + 8}
          fill="none"
          stroke="currentColor"
          strokeWidth="3"
          className="text-gray-300 dark:text-gray-600"
        />
        <circle
          cx={center}
          cy={center}
          r={outerRadius + 4}
          fill="none"
          stroke="currentColor"
          strokeWidth="1"
          className="text-gray-200 dark:text-gray-700"
        />

        {/* Segmentos de 12 horas */}
        {Array.from({ length: 12 }).map((_, displayIndex) => {
          const hour24 = getHourFromIndex(displayIndex)
          const hourData = hourlyData.find(h => h.hora === hour24)
          const value = hourData?.value || 0
          const hasValue = value > 0
          const color = hasValue ? getHeatColor(value, minValue, maxValue) : 'transparent'
          const isSelected = selectedHour === hour24

          return (
            <g key={`segment-${hour24}`}>
              {/* Fondo del segmento */}
              <path
                d={getSegmentPath(displayIndex, innerRadius, outerRadius)}
                fill="currentColor"
                className="text-gray-100 dark:text-gray-800"
              />
              {/* Segmento con color de calor */}
              <path
                d={getSegmentPath(displayIndex, innerRadius, outerRadius)}
                fill={color}
                stroke={isSelected ? '#3b82f6' : hasValue ? 'rgba(255,255,255,0.4)' : 'currentColor'}
                strokeWidth={isSelected ? 3 : 1}
                className={cn(
                  "transition-all duration-200",
                  !hasValue && "text-gray-200 dark:text-gray-700",
                  onHourClick && hasValue && "cursor-pointer hover:opacity-80"
                )}
                onClick={() => hasValue && handleSegmentClick(hour24)}
              />
              {/* Tooltip */}
              {hasValue && (
                <title>
                  {displayIndex === 0 ? 12 : displayIndex}:00 {period}: {formatValue(value, metric)} {metric}
                </title>
              )}
            </g>
          )
        })}

        {/* Centro del reloj */}
        <circle
          cx={center}
          cy={center}
          r={innerRadius - 3}
          fill="currentColor"
          className="text-white dark:text-gray-900"
        />
        <circle
          cx={center}
          cy={center}
          r={innerRadius - 3}
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          className="text-gray-300 dark:text-gray-600"
        />

        {/* Valor total en el centro */}
        <text
          x={center}
          y={center - 5}
          textAnchor="middle"
          dominantBaseline="middle"
          className="fill-gray-900 dark:fill-white font-bold text-xl"
        >
          {totalValue.toLocaleString()}
        </text>
        <text
          x={center}
          y={center + 18}
          textAnchor="middle"
          dominantBaseline="middle"
          className="fill-gray-500 dark:fill-gray-400 text-sm"
        >
          {metric}
        </text>

        {/* Marcas de hora principales: 12, 3, 6, 9 */}
        {[12, 3, 6, 9].map(hourLabel => {
          const pos = getLabelPosition(hourLabel, outerRadius + 25)
          return (
            <text
              key={`label-${hourLabel}`}
              x={pos.x}
              y={pos.y}
              textAnchor="middle"
              dominantBaseline="middle"
              className="fill-gray-700 dark:fill-gray-300 font-bold text-lg"
            >
              {hourLabel}
            </text>
          )
        })}

        {/* Marcas pequeñas para otras horas */}
        {[1, 2, 4, 5, 7, 8, 10, 11].map(hourLabel => {
          const displayIndex = hourLabel === 0 ? 0 : hourLabel
          const angle = ((displayIndex * 30) - 90) * Math.PI / 180
          const x1 = center + (outerRadius + 4) * Math.cos(angle)
          const y1 = center + (outerRadius + 4) * Math.sin(angle)
          const x2 = center + (outerRadius + 12) * Math.cos(angle)
          const y2 = center + (outerRadius + 12) * Math.sin(angle)
          return (
            <line
              key={`tick-${hourLabel}`}
              x1={x1}
              y1={y1}
              x2={x2}
              y2={y2}
              stroke="currentColor"
              strokeWidth="2"
              className="text-gray-400 dark:text-gray-500"
            />
          )
        })}

        {/* Punto central decorativo */}
        <circle
          cx={center}
          cy={center}
          r={3}
          fill="currentColor"
          className="text-gray-600 dark:text-gray-400"
        />
      </svg>
    </div>
  )
}

export function ClockChart({
  title,
  subtitle,
  data,
  selectedHour,
  onHourClick,
  metric = 'cubiertos'
}: ClockChartProps) {
  // Preparar datos de 24 horas
  const hourlyData = useMemo(() => {
    const hours: (HourData & { value: number })[] = []

    for (let h = 0; h < 24; h++) {
      const found = data.find(d => d.hora === h)
      const value = found
        ? (metric === 'cubiertos' ? found.cubiertos : metric === 'tickets' ? (found.tickets || 0) : (found.venta || 0))
        : 0
      hours.push({
        hora: h,
        cubiertos: found?.cubiertos || 0,
        tickets: found?.tickets || 0,
        venta: found?.venta || 0,
        value
      })
    }

    return hours
  }, [data, metric])

  // Calcular min/max para colores (solo valores > 0)
  const { minValue, maxValue } = useMemo(() => {
    const values = hourlyData.map(h => h.value).filter(v => v > 0)
    return {
      minValue: values.length > 0 ? Math.min(...values) : 0,
      maxValue: values.length > 0 ? Math.max(...values) : 0,
    }
  }, [hourlyData])

  // Totales por período
  const { totalAM, totalPM } = useMemo(() => {
    const am = hourlyData.filter(h => h.hora < 12).reduce((sum, h) => sum + h.value, 0)
    const pm = hourlyData.filter(h => h.hora >= 12).reduce((sum, h) => sum + h.value, 0)
    return { totalAM: am, totalPM: pm }
  }, [hourlyData])

  return (
    <div className="bg-white dark:bg-gray-900 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
      <div className="mb-6">
        <h3 className="font-semibold text-lg text-gray-900 dark:text-white">{title}</h3>
        {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
      </div>

      {/* Grid de dos columnas para los relojes */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* Cuadrante izquierdo - AM */}
        <div className="flex flex-col items-center justify-center p-4 bg-gray-50 dark:bg-gray-800/50 rounded-xl">
          <SingleClock
            period="AM"
            hourlyData={hourlyData}
            minValue={minValue}
            maxValue={maxValue}
            totalValue={totalAM}
            selectedHour={selectedHour}
            onHourClick={onHourClick}
            metric={metric}
          />
        </div>

        {/* Cuadrante derecho - PM */}
        <div className="flex flex-col items-center justify-center p-4 bg-gray-50 dark:bg-gray-800/50 rounded-xl">
          <SingleClock
            period="PM"
            hourlyData={hourlyData}
            minValue={minValue}
            maxValue={maxValue}
            totalValue={totalPM}
            selectedHour={selectedHour}
            onHourClick={onHourClick}
            metric={metric}
          />
        </div>
      </div>

      {/* Leyenda de colores */}
      <div className="mt-6 flex items-center justify-center gap-3">
        <span className="text-sm text-muted-foreground">Menor actividad</span>
        <div className="flex h-4 rounded overflow-hidden">
          {[0, 0.25, 0.5, 0.75, 1].map((v, i) => (
            <div
              key={i}
              className="w-8 h-full"
              style={{ backgroundColor: v === 0 ? '#d1d5db' : getHeatColor(v * 100, 0, 100) }}
            />
          ))}
        </div>
        <span className="text-sm text-muted-foreground">Mayor actividad</span>
      </div>

      {/* Hora seleccionada */}
      {selectedHour !== null && selectedHour !== undefined && (
        <div className="mt-4 text-center">
          <span className="inline-flex items-center gap-2 px-4 py-2 bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded-full text-base font-medium">
            Filtrado: {selectedHour === 0 ? '12' : selectedHour > 12 ? selectedHour - 12 : selectedHour}:00 {selectedHour < 12 ? 'AM' : 'PM'}
            <button
              onClick={() => onHourClick?.(null)}
              className="hover:text-blue-900 dark:hover:text-blue-100 text-lg"
            >
              ×
            </button>
          </span>
        </div>
      )}
    </div>
  )
}
