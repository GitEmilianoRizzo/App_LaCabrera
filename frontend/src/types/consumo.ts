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
