// Lógica del comparativo por día de la semana (Ventas > Por Franquicia).
// Filas = períodos, columnas = Lunes..Domingo. Las fechas se manejan como 'YYYY-MM-DD' en hora local.

export type TipoPeriodo = 'semana' | 'mes' | 'personalizado'
export type MetricaComparativo = 'tickets' | 'local' | 'usd' | 'cubiertos'
export type Agregacion = 'promedio' | 'total'

export interface Periodo {
  desde: string
  hasta: string
  etiqueta: string
}

export interface DiaVenta {
  fecha: string
  tickets: number
  cubiertos: number
  local: number
  usd: number | null
}

export interface Celda {
  valor: number | null
  // Días de ese día de semana dentro del período (hasta la fecha de corte) y cuántos tienen dato
  diasPeriodo: number
  diasConDato: number
}

export interface FilaComparativo {
  periodo: Periodo
  dias: Celda[] // 7 celdas, Lunes = 0
  total: Celda
  // El período termina después de la fecha de corte (en curso)
  parcial: boolean
}

export const DIAS_SEMANA = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom']
const MESES = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic']

export function parseFecha(s: string): Date {
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y, m - 1, d)
}

export function formatFecha(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function sumarDias(d: Date, n: number): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n)
}

// Lunes = 0 ... Domingo = 6
export function indiceDiaSemana(fecha: string): number {
  return (parseFecha(fecha).getDay() + 6) % 7
}

const ddmm = (d: Date) => `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`

/** Los últimos N períodos (semanas lunes-domingo o meses calendario) que terminan en el que contiene `corte`, del más viejo al más nuevo. */
export function armarPeriodos(tipo: Exclude<TipoPeriodo, 'personalizado'>, cantidad: number, corte: string): Periodo[] {
  const ref = parseFecha(corte)
  const periodos: Periodo[] = []
  for (let i = cantidad - 1; i >= 0; i--) {
    if (tipo === 'semana') {
      const lunes = sumarDias(ref, -indiceDiaSemana(corte) - 7 * i)
      const domingo = sumarDias(lunes, 6)
      periodos.push({ desde: formatFecha(lunes), hasta: formatFecha(domingo), etiqueta: `${ddmm(lunes)} – ${ddmm(domingo)}` })
    } else {
      const primero = new Date(ref.getFullYear(), ref.getMonth() - i, 1)
      const ultimo = new Date(primero.getFullYear(), primero.getMonth() + 1, 0)
      periodos.push({ desde: formatFecha(primero), hasta: formatFecha(ultimo), etiqueta: `${MESES[primero.getMonth()]} ${primero.getFullYear()}` })
    }
  }
  return periodos
}

export function etiquetaRango(desde: string, hasta: string): string {
  const a = parseFecha(desde)
  const b = parseFecha(hasta)
  return a.getFullYear() === b.getFullYear() ? `${ddmm(a)} – ${ddmm(b)}/${b.getFullYear()}` : `${ddmm(a)}/${a.getFullYear()} – ${ddmm(b)}/${b.getFullYear()}`
}

function valorMetrica(d: DiaVenta, metrica: MetricaComparativo): number | null {
  switch (metrica) {
    case 'tickets': return d.tickets
    case 'cubiertos': return d.cubiertos
    case 'local': return d.local
    case 'usd': return d.usd
  }
}

/**
 * Arma las filas del comparativo. Un día cuenta como "con dato" si tiene tickets (y, en USD, cotización).
 * Promedio = suma / días con dato, para que un día sin carga no baje el promedio; la celda informa cuántos faltan.
 * Los días posteriores a `corte` no cuentan (período en curso).
 */
export function armarComparativo(
  periodos: Periodo[],
  datos: DiaVenta[],
  metrica: MetricaComparativo,
  agregacion: Agregacion,
  corte: string,
): FilaComparativo[] {
  const porFecha = new Map(datos.map(d => [d.fecha, d]))

  return periodos.map(periodo => {
    const suma = Array(7).fill(0)
    const conDato = Array(7).fill(0)
    const enPeriodo = Array(7).fill(0)
    const hasta = periodo.hasta < corte ? periodo.hasta : corte

    for (let d = parseFecha(periodo.desde); formatFecha(d) <= hasta; d = sumarDias(d, 1)) {
      const fecha = formatFecha(d)
      const i = (d.getDay() + 6) % 7
      enPeriodo[i]++
      const dia = porFecha.get(fecha)
      if (!dia || dia.tickets <= 0) continue
      const v = valorMetrica(dia, metrica)
      if (v == null) continue
      suma[i] += v
      conDato[i]++
    }

    const celda = (s: number, n: number, total: number): Celda => ({
      valor: n === 0 ? null : agregacion === 'promedio' ? s / n : s,
      diasPeriodo: total,
      diasConDato: n,
    })

    const sumaTotal = suma.reduce((a, b) => a + b, 0)
    const conDatoTotal = conDato.reduce((a, b) => a + b, 0)
    const enPeriodoTotal = enPeriodo.reduce((a, b) => a + b, 0)

    return {
      periodo,
      dias: suma.map((s, i) => celda(s, conDato[i], enPeriodo[i])),
      total: celda(sumaTotal, conDatoTotal, enPeriodoTotal),
      parcial: periodo.hasta > corte,
    }
  })
}

/** Variación porcentual de `actual` contra `base`; null si no se puede calcular. */
export function variacion(actual: number | null, base: number | null): number | null {
  if (actual == null || base == null || base === 0) return null
  return ((actual - base) / Math.abs(base)) * 100
}
