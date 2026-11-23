// Respuesta de GET /dashboard/consumo-productos (arrays compactos con índices)

export interface ConsumoFranquicia {
  id: number
  codigo: string
  nombre: string
  pais: string | null
  ciudad: string | null
  moneda: string
  /** El POS manda códigos en vez de nombres: su venta queda en Sin clasificar. */
  sin_nombres: boolean
  motivo: string | null
  /** Ubicación en el mapa (dim.Franquicia): regla del sistema para toda franquicia nueva */
  pais_iso?: string | null
  zona_mapa?: string | null
  latitud?: number | null
  longitud?: number | null
}

export interface ConsumoProducto {
  rubro: string
  producto: string
}

export interface ConsumoProductosResponse {
  desde: string
  hasta: string
  fecha_maxima_datos: string | null
  fechas: string[]
  franquicias: ConsumoFranquicia[]
  productos: ConsumoProducto[]
  /** [fecha, franquicia, tickets, cubiertos, venta USD, venta ARS] */
  dias: number[][]
  /** [fecha, franquicia, producto, unidades cobradas, unidades equivalentes, kg, USD, ARS, cantidad POS] */
  filas: number[][]
  cobertura: {
    /** = venta de los tickets = clasificado + sin clasificar + no producto + ajuste */
    usd_total: number
    usd_clasificado: number
    usd_sin_clasificar: number
    usd_no_producto: number
    usd_ajuste: number
  }
}

// Respuesta de GET /dashboard/consumo-productos/control (regla del Mapa de Consumo)
export type EstadoRegla = 'OK' | 'ATENCION' | 'ERROR' | 'SIN_DATOS'

export interface ControlChequeo { codigo: string; estado: EstadoRegla; mensaje: string }
export interface ControlNombre { nombre: string; categoria: string | null; familia: string | null; importe: number; cantidad: number }

export interface ControlFranquicia {
  franquicia_id: number
  codigo: string
  nombre: string
  pais: string | null
  zona: string | null
  moneda: string
  sistema_origen: string | null
  nodo_activo: boolean
  estado: EstadoRegla
  desde: string | null
  hasta: string | null
  dias_con_ventas: number
  pct_clasificado: number | null
  pct_sin_clasificar: number | null
  pct_no_producto: number | null
  pct_dias_sin_detalle: number | null
  pct_tickets_con_cubiertos: number | null
  chequeos: ControlChequeo[]
  sin_clasificar: ControlNombre[]
  nombres_sin_clasificar: number
}

export interface ControlMapaConsumo {
  fecha_control: string
  dias: number
  ultimo_control_automatico: string | null
  resumen: { ok: number; atencion: number; error: number; sin_datos: number }
  franquicias: ControlFranquicia[]
}
