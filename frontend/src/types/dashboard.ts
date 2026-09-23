// Types aligned with backend DTOs (snake_case from API)

export interface HomeDashboard {
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_codigo: string | null
  grupo_economico_nombre: string | null
  pais: string | null
  ciudad: string | null
  moneda_codigo: string | null

  // Integracion
  ultima_sincronizacion: string | null
  estado_integracion: string | null
  dias_sin_sincronizar: number | null
  alerta_sincronizacion: boolean

  // Totales (en el rango de fechas seleccionado) - en USD
  venta_bruta: number
  venta_neta: number
  venta_neta_local: number
  total_cubiertos: number
  total_tickets: number
  total_propinas: number
  venta_por_cubierto: number | null
  ticket_promedio: number | null

  // Indicadores de calidad de conversión
  dias_con_tasa: number
  dias_sin_tasa: number
  calidad_conversion: string

  // Mes actual
  venta_mes_actual: number
  tickets_mes_actual: number
  cubiertos_mes_actual: number

  // Mes anterior
  venta_mes_anterior: number
  tickets_mes_anterior: number
  cubiertos_mes_anterior: number

  // Propinas por mes
  propinas_mes_actual: number
  propinas_mes_anterior: number

  // Acumulado año previo (YTD del año anterior)
  venta_acum_anio_previo: number
  tickets_acum_anio_previo: number
  cubiertos_acum_anio_previo: number

  // Acumulado año actual (YTD)
  venta_acum_anio_actual: number
  tickets_acum_anio_actual: number
  cubiertos_acum_anio_actual: number
}

export interface VentasResumenDiario {
  fecha_negocio: string
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_nombre: string | null
  pais: string | null
  moneda_codigo: string | null
  cantidad_tickets: number
  total_cubiertos: number | null
  venta_bruta: number
  total_descuentos: number
  venta_neta: number
  venta_neta_usd: number | null
  tipo_cambio: number | null
  ticket_promedio: number | null
  venta_por_cubierto: number | null
}

export interface VentasPorFranquicia {
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_nombre: string | null
  pais: string | null
  ciudad: string | null
  moneda_codigo: string | null
  ultima_sincronizacion: string | null
  estado_integracion: string | null
  venta_neta_ytd: number
  cubiertos_ytd: number
  tickets_ytd: number
  venta_neta_mtd: number
  cubiertos_mtd: number
  tickets_mtd: number
  venta_neta_total: number
  primera_venta: string | null
  ultima_venta: string | null
}

export interface VentasPorProducto {
  producto_codigo: string
  producto_nombre: string
  categoria: string | null
  familia: string | null
  franquicia_nombre: string
  cantidad_vendida: number
  venta_bruta: number
  venta_neta: number
  cantidad_tickets: number
}

export interface VentasPorMozo {
  mozo_id: number
  mozo_nombre: string
  franquicia_nombre: string
  cantidad_tickets: number
  total_cubiertos: number | null
  venta_neta: number
  total_propinas: number | null
  ticket_promedio: number | null
}

export interface VentasPorTipoPlato {
  tipo_plato_codigo: string
  tipo_plato_nombre: string
  franquicia_nombre: string | null
  cantidad_vendida: number
  venta_neta: number
  cantidad_tickets: number
  porcentaje_venta: number | null
}

export interface OcupacionMesas {
  franquicia_id: number
  franquicia_nombre: string
  fecha_negocio: string
  mesas_totales: number | null
  mesas_utilizadas: number
  rotacion_mesas: number
  porcentaje_ocupacion: number | null
  tiempo_promedio_minutos: number | null
  total_tickets: number
  total_cubiertos: number | null
}

export interface EstadoIntegracion {
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_nombre: string | null
  pais: string | null
  ciudad: string | null
  estado_integracion: string | null
  sistema_origen: string | null
  contacto_nombre: string | null
  contacto_email: string | null
  contacto_telefono: string | null
  ultima_sincronizacion: string | null
  dias_sin_sincronizar: number | null
  alerta_sincronizacion: boolean
  ultimo_batch_id: string | null
  ultimo_batch_estado: string | null
  batches_ultimo_mes: number
  batches_con_error_ultimo_mes: number
}

export interface DashboardFilters {
  fechaDesde?: string
  fechaHasta?: string
  franquiciaId?: number
  grupoEconomicoId?: number
  pais?: string
  // v1.2: Currency and tax mode
  currencyMode?: 'LOCAL' | 'USD'
  taxMode?: 'NETO' | 'NETO_SIN_IMP'
  mealPeriod?: string
  anio?: number
  mes?: number
  // v1.3: Bidirectional filtering for ClockChart
  productoId?: number
  hora?: number
}

// v1.2 Types - Consolidated USD views and tax separation

export interface VentasConsolidadas {
  fecha_negocio: string
  franquicia_id: number
  franquicia_nombre: string
  pais: string | null
  moneda_local: string | null
  cantidad_tickets: number
  total_cubiertos: number | null
  // Local currency
  venta_bruta_local: number
  venta_neta_local: number
  venta_neta_sin_imp_local: number | null
  impuesto_local: number | null
  // USD amounts
  tipo_cambio: number | null
  venta_bruta_usd: number | null
  venta_neta_usd: number | null
  venta_neta_sin_imp_usd: number | null
  // Quality indicators
  calidad_impuesto: string | null
  calidad_tipo_cambio: string | null
  // Metrics
  ticket_promedio: number | null
  venta_por_cubierto: number | null
}

export interface VentasPorProductoConPeso {
  franquicia_id: number
  franquicia_nombre: string
  producto_codigo: string
  producto_nombre: string
  categoria: string | null
  cantidad_vendida: number
  venta_neta: number
  venta_neta_usd: number | null
  pct_sobre_total_franquicia: number | null
  pct_sobre_categoria: number | null
  pct_categoria_sobre_total: number | null
  ranking_franquicia: number | null
  ranking_categoria: number | null
}

export interface VentasPorMealPeriod {
  franquicia_id: number
  franquicia_nombre: string
  meal_period: string
  meal_period_origen: string | null
  cantidad_tickets: number
  total_cubiertos: number | null
  venta_neta: number
  venta_neta_usd: number | null
  venta_neta_sin_imp: number | null
  ticket_promedio: number | null
  pct_sobre_total: number | null
}

export interface DiaRanking {
  fecha_negocio: string
  franquicia_id: number
  franquicia_nombre: string
  dia_semana: string | null
  venta_neta: number
  venta_neta_usd: number | null
  cantidad_tickets: number
  total_cubiertos: number | null
  ranking_mejor: number
  ranking_peor: number
}

export interface VentasDelDia {
  franquicia_id: number
  franquicia_nombre: string
  fecha_hoy: string
  venta_hoy: number
  venta_hoy_usd: number | null
  tickets_hoy: number
  cubiertos_hoy: number | null
  venta_mismo_dia_semana_pasada: number | null
  var_vs_semana_pasada_pct: number | null
  promedio_mensual: number | null
  var_vs_promedio_pct: number | null
}

export interface ComparativoMensual {
  franquicia_id: number
  franquicia_nombre: string
  anio: number
  mes: number
  venta_neta: number
  venta_neta_usd: number | null
  cantidad_tickets: number
  total_cubiertos: number | null
  venta_mes_anterior: number | null
  var_vs_mes_anterior_pct: number | null
  venta_mismo_mes_anio_anterior: number | null
  var_vs_anio_anterior_pct: number | null
}

// Transacciones / Tickets
export interface Transaccion {
  ticket_id: number
  numero_ticket: string
  fecha_negocio: string
  fecha_apertura: string
  fecha_cierre: string | null
  estado: string
  periodo_comida: string | null
  numero_mesa: string | null
  area_mesa: string | null
  nombre_mozo: string | null
  cantidad_cubiertos: number | null
  moneda_codigo: string
  importe_bruto: number
  importe_descuento: number
  importe_neto: number
  importe_impuesto: number
  importe_propina: number
  importe_total_pagado: number
  tiempo_consumo_minutos: number | null
  cantidad_items: number
  // USD conversion
  tipo_cambio: number
  importe_bruto_usd: number
  importe_descuento_usd: number
  importe_neto_usd: number
  importe_impuesto_usd: number
  importe_propina_usd: number
  importe_total_pagado_usd: number
}

export interface TransaccionDetalle {
  detalle_id: number
  ticket_id: number
  codigo_producto: string
  nombre_producto: string
  categoria: string | null
  familia: string | null
  cantidad: number
  precio_unitario: number
  importe_bruto: number
  importe_descuento: number
  importe_neto: number
  esta_anulado: boolean
  notas: string | null
}

// v1.3: Hourly consumption (ClockChart)
export interface VentasPorHora {
  hora: number
  franquicia_id: number | null
  franquicia_nombre: string | null
  cubiertos: number
  tickets: number
  venta_neta: number
  venta_neta_usd: number | null
}

// Agregados para graficos
export interface ChartDataPoint {
  name: string
  value: number
  [key: string]: string | number
}

export interface TrendData {
  fecha: string
  valor: number
  franquicia?: string
}
