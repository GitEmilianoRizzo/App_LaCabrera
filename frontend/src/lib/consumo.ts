import type { ConsumoProductosResponse } from '@/types/consumo'

// ---------------------------------------------------------------------------------------------
// Zonas geográficas: una por ciudad (Ezeiza y Aeroparque → Buenos Aires).
// ---------------------------------------------------------------------------------------------

export interface PaisInfo { nombre: string; iso: string }

const PAISES: Record<string, PaisInfo> = {
  argentina: { nombre: 'Argentina', iso: '032' },
  brasil: { nombre: 'Brasil', iso: '076' }, brazil: { nombre: 'Brasil', iso: '076' },
  chile: { nombre: 'Chile', iso: '152' },
  colombia: { nombre: 'Colombia', iso: '170' },
  spain: { nombre: 'España', iso: '724' }, espana: { nombre: 'España', iso: '724' },
  usa: { nombre: 'EE.UU.', iso: '840' }, 'united states': { nombre: 'EE.UU.', iso: '840' }, 'estados unidos': { nombre: 'EE.UU.', iso: '840' },
  philippines: { nombre: 'Filipinas', iso: '608' }, filipinas: { nombre: 'Filipinas', iso: '608' },
  peru: { nombre: 'Perú', iso: '604' },
  mexico: { nombre: 'México', iso: '484' },
  uruguay: { nombre: 'Uruguay', iso: '858' },
  paraguay: { nombre: 'Paraguay', iso: '600' },
}

// [longitud, latitud]
const CIUDADES: Record<string, { nombre: string; coord: [number, number] }> = {
  'buenos aires': { nombre: 'Buenos Aires', coord: [-58.38, -34.6] },
  ezeiza: { nombre: 'Buenos Aires', coord: [-58.38, -34.6] },
  'porto alegre': { nombre: 'Porto Alegre', coord: [-51.23, -30.03] },
  santiago: { nombre: 'Santiago', coord: [-70.67, -33.45] },
  bogota: { nombre: 'Bogotá', coord: [-74.07, 4.71] },
  cartagena: { nombre: 'Cartagena', coord: [-75.51, 10.39] },
  marbella: { nombre: 'Marbella', coord: [-4.88, 36.51] },
  malaga: { nombre: 'Málaga', coord: [-4.42, 36.72] },
  miami: { nombre: 'Miami', coord: [-80.19, 25.76] },
  manila: { nombre: 'Manila', coord: [120.98, 14.6] },
  lima: { nombre: 'Lima', coord: [-77.04, -12.05] },
  'ciudad de mexico': { nombre: 'Ciudad de México', coord: [-99.13, 19.43] },
  montevideo: { nombre: 'Montevideo', coord: [-56.16, -34.9] },
  'punta del este': { nombre: 'Punta del Este', coord: [-54.95, -34.96] },
  asuncion: { nombre: 'Asunción', coord: [-57.58, -25.26] },
}

const sinAcentos = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim()

export interface Zona {
  key: string
  ciudad: string
  pais: PaisInfo
  coord: [number, number] | null
}

export function zonaDe(pais: string | null, ciudad: string | null): Zona {
  const p = PAISES[sinAcentos(pais ?? '')] ?? { nombre: pais?.trim() || 'Sin país', iso: '' }
  const c = CIUDADES[sinAcentos(ciudad ?? '')]
  const nombreCiudad = c?.nombre ?? (ciudad?.trim() || p.nombre)
  return { key: `${p.nombre} · ${nombreCiudad}`, ciudad: nombreCiudad, pais: p, coord: c?.coord ?? null }
}

// ---------------------------------------------------------------------------------------------
// Familias de producto (modo "producto líder") e íconos
// ---------------------------------------------------------------------------------------------

export interface Familia { key: string; nombre: string; claro: string; oscuro: string }

// Orden fijo: el color sigue a la familia, nunca al ranking.
export const FAMILIAS: Familia[] = [
  { key: 'carnes', nombre: 'Carnes vacunas', claro: '#e34948', oscuro: '#e66767' },
  { key: 'mar', nombre: 'Pescados y mariscos', claro: '#2a78d6', oscuro: '#3987e5' },
  { key: 'vino', nombre: 'Vinos', claro: '#6250d6', oscuro: '#9085e9' },
  { key: 'bar', nombre: 'Coctelería y cerveza', claro: '#eda100', oscuro: '#c98500' },
  { key: 'rapido', nombre: 'Sándwiches y hamburguesas', claro: '#eb6834', oscuro: '#d95926' },
  { key: 'otras', nombre: 'Aves, cerdo y cordero', claro: '#e87ba4', oscuro: '#d55181' },
  { key: 'entradas', nombre: 'Entradas, ensaladas y guarniciones', claro: '#008300', oscuro: '#0ca30c' },
  { key: 'resto', nombre: 'Postres, café y otros', claro: '#1baf7a', oscuro: '#199e70' },
]

export function familiaDe(rubro: string): Familia {
  const k =
    rubro === 'CARNES VACUNAS' || rubro === 'ACHURAS Y EMBUTIDOS' || rubro === 'MILANESAS' ? 'carnes'
    : rubro === 'PESCADOS Y MARISCOS' ? 'mar'
    : rubro === 'VINO' ? 'vino'
    : rubro === 'COCTELERÍA Y DESTILADOS' || rubro === 'CERVEZA' ? 'bar'
    : rubro === 'SÁNDWICHES Y HAMBURGUESAS' ? 'rapido'
    : rubro === 'POLLO' || rubro === 'CERDO' || rubro === 'CORDERO' ? 'otras'
    : rubro === 'ENTRADAS' || rubro === 'ENSALADAS' || rubro === 'GUARNICIONES' || rubro === 'PASTAS' ? 'entradas'
    : 'resto'
  return FAMILIAS.find(f => f.key === k)!
}

const ICONO_PRODUCTO: [RegExp, string][] = [
  [/Salmón|Otros pescados/, '🐟'], [/Pulpo/, '🐙'], [/Langostinos/, '🦐'],
  [/Sándwich|Lomito|Otros sándwiches|Choripán/, '🥪'], [/Hamburguesa/, '🍔'],
  [/Empanadas/, '🥟'], [/Provoleta|Burrata|quesos/, '🧀'], [/Jamón/, '🥓'],
  [/Papas fritas/, '🍟'], [/Puré|Otras papas/, '🥔'], [/Vegetales/, '🥦'], [/Arroz/, '🍚'],
  [/Helados/, '🍨'], [/Flan|Panqueques|Volcán|Otros postres/, '🍮'],
  [/Café|Desayuno/, '☕'], [/Té/, '🍵'], [/Agua/, '💧'], [/Limonada/, '🍋'], [/Gaseosas/, '🥤'],
  [/Espumante/, '🥂'], [/Vino/, '🍷'], [/Cerveza/, '🍺'], [/Cócteles|Gin|Aperol|Sangría|Fernet/, '🍸'], [/Destilados/, '🥃'],
  [/Pollo/, '🍗'], [/Cerdo/, '🐖'], [/Cordero/, '🐑'], [/Milanesa/, '🍖'],
  [/Ensaladas/, '🥗'], [/Pastas/, '🍝'], [/Panadería/, '🥐'], [/Menú infantil/, '🧒'],
]

export function iconoProducto(producto: string, rubro: string): string {
  for (const [re, ic] of ICONO_PRODUCTO) if (re.test(producto)) return ic
  if (rubro === 'CARNES VACUNAS' || rubro === 'ACHURAS Y EMBUTIDOS') return '🥩'
  return '🍽️'
}

/** Nombre corto para badges: "Ojo de bife / rib eye" → "Ojo de bife". */
export const nombreCorto = (producto: string) => producto.split(' / ')[0].replace(/ \(.*\)$/, '')

// ---------------------------------------------------------------------------------------------
// Escala secuencial: celeste (menos) → azul oscuro (más)
// ---------------------------------------------------------------------------------------------

const RAMPA_CLARO = ['#cde2fb', '#9ec5f4', '#6da7ec', '#3987e5', '#256abf', '#184f95', '#0d366b']
// En oscuro se mantiene el sentido pedido por la Central (celeste = menos); arranca un paso más oscuro para no encandilar.
const RAMPA_OSCURO = ['#b7d3f6', '#86b6ef', '#5598e7', '#3987e5', '#256abf', '#1c5cab', '#104281']

const hex = (h: string) => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16))

/** t en [0,1] → color de la rampa (interpolación lineal entre pasos). */
export function colorSecuencial(t: number, oscuro = false): string {
  const r = oscuro ? RAMPA_OSCURO : RAMPA_CLARO
  const x = Math.max(0, Math.min(1, t)) * (r.length - 1)
  const i = Math.min(r.length - 2, Math.floor(x))
  const a = hex(r[i]), b = hex(r[i + 1]), f = x - i
  return `rgb(${a.map((v, k) => Math.round(v + (b[k] - v) * f)).join(',')})`
}

export const gradienteSecuencial = (oscuro = false) => `linear-gradient(to right, ${(oscuro ? RAMPA_OSCURO : RAMPA_CLARO).join(',')})`

// ---------------------------------------------------------------------------------------------
// Modelo y agregación
// ---------------------------------------------------------------------------------------------

export type Metrica = 'cantidad' | 'unidades' | 'usd' | 'ars' | 'por100'
export const METRICAS: { key: Metrica; etiqueta: string; ayuda: string }[] = [
  { key: 'cantidad', etiqueta: 'Cantidad', ayuda: 'Cantidad tal cual la informa el POS (incluye lo sin cargo): cruza con el detalle de los tickets' },
  { key: 'unidades', etiqueta: 'Porciones', ayuda: 'Porciones cobradas: media porción = 0,5, empanadas por unidad; lo sin cargo no suma' },
  { key: 'usd', etiqueta: 'USD', ayuda: 'Venta neta sin impuestos ni servicio, en USD (misma que Dashboard y Alertas)' },
  { key: 'ars', etiqueta: 'ARS', ayuda: 'La venta en USD pasada a pesos con la cotización de cada día' },
  { key: 'por100', etiqueta: 'Cada 100 cubiertos', ayuda: 'Porciones cada 100 cubiertos' },
]

/** Rubros que no son producto: se muestran (para que los totales cierren) pero no compiten como "más vendido". */
export const RUBROS_CONTROL = ['SIN CLASIFICAR', 'NO PRODUCTO', 'AJUSTE A TICKETS']
const NO_LIDER = new Set([...RUBROS_CONTROL, 'SERVICIO DE MESA', 'CORTESÍAS (SIN CARGO)'])
export const puedeSerLider = (rubro: string) => !NO_LIDER.has(rubro)
export const esControl = (rubro: string) => RUBROS_CONTROL.includes(rubro)

/** 'todos' | 'r:<rubro>' | 'p:<índice de producto>' */
export type FiltroProducto = string

// columnas de cada producto en el acumulado de una zona
export const U = 0, EQ = 1, KG = 2, USD = 3, ARS = 4, CANT = 5
export const NC = 6

export interface Modelo {
  data: ConsumoProductosResponse
  zonas: Zona[]
  zonaDeFr: number[] // índice de franquicia → índice de zona (-1 excluida)
  filasPorFecha: number[][][]
  diasPorFecha: number[][][]
  nProd: number
}

export function crearModelo(data: ConsumoProductosResponse): Modelo {
  const zonas: Zona[] = []
  const idx = new Map<string, number>()
  const zonaDeFr = data.franquicias.map(f => {
    const z = zonaDe(f.pais, f.ciudad)
    if (!idx.has(z.key)) { idx.set(z.key, zonas.length); zonas.push(z) }
    return idx.get(z.key)!
  })
  const filasPorFecha: number[][][] = data.fechas.map(() => [])
  for (const r of data.filas) filasPorFecha[r[0]]?.push(r)
  const diasPorFecha: number[][][] = data.fechas.map(() => [])
  for (const d of data.dias) diasPorFecha[d[0]]?.push(d)
  return { data, zonas, zonaDeFr, filasPorFecha, diasPorFecha, nProd: data.productos.length }
}

export interface AcumZona {
  cubiertos: number
  tickets: number
  ventaUsd: number
  ventaArs: number
  dias: Set<number>
  franquicias: Set<number>
  prod: Float64Array // nProd × NC
}

/** Acumula las fechas [i0, i1] por zona (la clave es el índice de zona). */
export function acumular(m: Modelo, i0: number, i1: number): AcumZona[] {
  const out: AcumZona[] = m.zonas.map(() => ({
    cubiertos: 0, tickets: 0, ventaUsd: 0, ventaArs: 0, dias: new Set(), franquicias: new Set(), prod: new Float64Array(m.nProd * NC),
  }))
  for (let i = Math.max(0, i0); i <= Math.min(m.data.fechas.length - 1, i1); i++) {
    for (const d of m.diasPorFecha[i]) {
      const z = m.zonaDeFr[d[1]]
      if (z < 0) continue
      const a = out[z]
      a.tickets += d[2]; a.cubiertos += d[3]; a.ventaUsd += d[4]; a.ventaArs += d[5]
      a.dias.add(i); a.franquicias.add(d[1])
    }
    for (const r of m.filasPorFecha[i]) {
      const z = m.zonaDeFr[r[1]]
      if (z < 0) continue
      const p = out[z].prod, o = r[2] * NC
      p[o + U] += r[3]; p[o + EQ] += r[4]; p[o + KG] += r[5]; p[o + USD] += r[6]; p[o + ARS] += r[7]; p[o + CANT] += r[8] ?? 0
    }
  }
  return out
}

export function productosDelFiltro(m: Modelo, filtro: FiltroProducto): number[] {
  const ps = m.data.productos
  if (filtro === 'todos') return ps.map((_, i) => i)
  if (filtro.startsWith('r:')) return ps.flatMap((p, i) => (p.rubro === filtro.slice(2) ? [i] : []))
  if (filtro.startsWith('p:')) return [Number(filtro.slice(2))]
  return []
}

export function sumar(a: AcumZona, prods: number[], col: number): number {
  let s = 0
  for (const p of prods) s += a.prod[p * NC + col]
  return s
}

export function valorMetrica(a: AcumZona, prods: number[], metrica: Metrica): number {
  switch (metrica) {
    case 'cantidad': return sumar(a, prods, CANT)
    case 'unidades': return sumar(a, prods, EQ)
    case 'usd': return sumar(a, prods, USD)
    case 'ars': return sumar(a, prods, ARS)
    case 'por100': return a.cubiertos > 0 ? (sumar(a, prods, EQ) / a.cubiertos) * 100 : 0
  }
}

/** Producto con más venta (o unidades) dentro del filtro. */
export function lider(a: AcumZona, prods: number[], metrica: Metrica, rubros: string[]): number | null {
  const m_rubro = (p: number) => rubros[p]
  const col = metrica === 'cantidad' || metrica === 'unidades' || metrica === 'por100' ? EQ : metrica === 'ars' ? ARS : USD
  let best = -1, bv = 0
  for (const p of prods) {
    if (!puedeSerLider(m_rubro(p))) continue
    const v = a.prod[p * NC + col]
    if (v > bv) { bv = v; best = p }
  }
  return best >= 0 ? best : null
}

export function formatoMetrica(v: number, metrica: Metrica): string {
  const nf = (d: number) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d, minimumFractionDigits: 0 })
  if (metrica === 'usd') return `USD ${compacto(v)}`
  if (metrica === 'ars') return `$ ${compacto(v)}`
  if (metrica === 'por100') return nf(1).format(v)
  return compacto(v)
}

/** Cifra completa para tablas auditables: separador de miles, sin abreviar. */
export function formatoExacto(v: number, metrica: Metrica): string {
  const nf = (d: number) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d, minimumFractionDigits: 0 })
  if (metrica === 'usd') return nf(0).format(v)
  if (metrica === 'ars') return nf(0).format(v)
  if (metrica === 'por100') return nf(1).format(v)
  return nf(Number.isInteger(Math.round(v * 100) / 100) ? 0 : 1).format(v)
}

export function compacto(v: number): string {
  const a = Math.abs(v)
  const nf = (d: number) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d })
  if (a >= 1e9) return `${nf(1).format(v / 1e9)} mil M`
  if (a >= 1e6) return `${nf(1).format(v / 1e6)} M`
  if (a >= 1e4) return `${nf(0).format(v / 1e3)} mil`
  return nf(a < 10 ? 1 : 0).format(v)
}

// ---------------------------------------------------------------------------------------------
// Informe: conclusiones automáticas para el rango elegido
// ---------------------------------------------------------------------------------------------

export interface FilaInforme {
  zona: Zona
  dias: number
  franquicias: number
  cubiertos: number
  /** Venta de los tickets (igual que Alertas) */
  ventaUsd: number
  /** Parte de la venta asignada a un producto: base de los porcentajes */
  ventaClasificada: number
  gastoPorCubierto: number
  lider: { producto: string; rubro: string; share: number } | null
  distintivos: { producto: string; rubro: string; por100: number; indice: number }[]
  shareCarnes: number
}

export interface Informe {
  filas: FilaInforme[]
  ventaTotal: number
  ventaClasificada: number
  top: { producto: string; rubro: string; share: number; usdPorUnidad: number }[]
  shareCarnes: number
  conclusiones: string[]
}

const listaY = (xs: string[]) => (xs.length <= 1 ? xs.join('') : `${xs.slice(0, -1).join(', ')} y ${xs[xs.length - 1]}`)

export function armarInforme(m: Modelo, acum: AcumZona[]): Informe {
  const P = m.data.productos
  const todos = P.map((_, i) => i)
  const carnes = P.flatMap((p, i) => (p.rubro === 'CARNES VACUNAS' ? [i] : []))
  const total = { usd: new Float64Array(P.length), eq: new Float64Array(P.length), u: new Float64Array(P.length) }
  let cubTot = 0
  for (const a of acum) {
    cubTot += a.cubiertos
    for (let p = 0; p < P.length; p++) {
      total.usd[p] += a.prod[p * NC + USD]; total.eq[p] += a.prod[p * NC + EQ]; total.u[p] += a.prod[p * NC + U]
    }
  }
  const ventaTotal = acum.reduce((s, a) => s + a.ventaUsd, 0)
  // Los porcentajes son sobre la venta clasificada (sin "sin clasificar", "no producto" ni ajustes), para no diluirlos.
  const clasif = todos.filter(p => !esControl(P[p].rubro))
  const ventaClasificada = clasif.reduce((s, p) => s + total.usd[p], 0)
  const relevante = (p: number) => ventaClasificada > 0 && total.usd[p] / ventaClasificada >= 0.01 && puedeSerLider(P[p].rubro)

  const filas: FilaInforme[] = acum.map((a, z) => {
    const venta = sumar(a, clasif, USD)
    const l = lider(a, todos, 'usd', P.map(x => x.rubro))
    const distintivos = todos
      .filter(p => relevante(p) && cubTot > 0 && a.cubiertos > 0 && a.prod[p * NC + EQ] >= 20)
      .map(p => {
        const por100 = (a.prod[p * NC + EQ] / a.cubiertos) * 100
        const global = (total.eq[p] / cubTot) * 100
        return { producto: P[p].producto, rubro: P[p].rubro, por100, indice: global > 0 ? por100 / global : 0 }
      })
      .filter(x => x.indice > 1.2)
      .sort((x, y) => y.indice - x.indice)
      .slice(0, 2)
    return {
      zona: m.zonas[z], dias: a.dias.size, franquicias: a.franquicias.size, cubiertos: a.cubiertos, ventaUsd: a.ventaUsd, ventaClasificada: venta,
      gastoPorCubierto: a.cubiertos > 0 ? a.ventaUsd / a.cubiertos : 0,
      lider: l !== null && venta > 0 ? { producto: P[l].producto, rubro: P[l].rubro, share: a.prod[l * NC + USD] / venta } : null,
      distintivos,
      shareCarnes: venta > 0 ? sumar(a, carnes, USD) / venta : 0,
    }
  }).filter(f => f.ventaUsd > 0).sort((x, y) => y.gastoPorCubierto - x.gastoPorCubierto)

  const top = todos
    .filter(p => puedeSerLider(P[p].rubro))
    .sort((a, b) => total.usd[b] - total.usd[a]).slice(0, 5)
    .map(p => ({ producto: P[p].producto, rubro: P[p].rubro, share: ventaClasificada ? total.usd[p] / ventaClasificada : 0, usdPorUnidad: total.u[p] ? total.usd[p] / total.u[p] : 0 }))
  const shareCarnes = ventaClasificada ? carnes.reduce((s, p) => s + total.usd[p], 0) / ventaClasificada : 0

  const pct = (x: number) => `${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 0 }).format(x * 100)} %`
  const usd = (x: number) => `USD ${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 0 }).format(x)}`
  const conclusiones: string[] = []
  if (ventaClasificada > 0) {
    conclusiones.push(`La carne vacuna explica el ${pct(shareCarnes)} de la venta clasificada. ${top.slice(0, 3).map(t => `${nombreCorto(t.producto)} (${pct(t.share)})`).join(', ')} encabezan el ranking.`)
  }
  const conCub = filas.filter(f => f.cubiertos > 0 && f.gastoPorCubierto > 0)
  if (conCub.length >= 2) {
    const hi = conCub[0], lo = conCub[conCub.length - 1]
    conclusiones.push(`El gasto por cubierto va de ${usd(hi.gastoPorCubierto)} en ${hi.zona.ciudad} a ${usd(lo.gastoPorCubierto)} en ${lo.zona.ciudad}.`)
  }
  const lideres = new Map<string, string[]>()
  for (const f of filas) if (f.lider) lideres.set(nombreCorto(f.lider.producto), [...(lideres.get(nombreCorto(f.lider.producto)) ?? []), f.zona.ciudad])
  if (lideres.size > 1) {
    conclusiones.push(`Cada zona tiene su plato fuerte: ${[...lideres].map(([p, zs]) => `${p} en ${listaY(zs)}`).join('; ')}.`)
  }
  // Los tres contrastes más fuertes de la red (comidas y bebidas con volumen), para no llenar el informe.
  const fuertes = filas.flatMap(f => (f.distintivos[0] ? [{ f, d: f.distintivos[0] }] : []))
    .filter(x => x.d.indice >= 1.8).sort((a, b) => b.d.indice - a.d.indice).slice(0, 3)
  for (const { f, d } of fuertes) {
    conclusiones.push(`${f.zona.ciudad} pide ${nombreCorto(d.producto).toLowerCase()} ${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 1 }).format(d.indice)} veces más que el promedio de la red (${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 1 }).format(d.por100)} cada 100 cubiertos).`)
  }
  const pocos = filas.filter(f => f.dias > 0 && f.dias < 10)
  if (pocos.length) conclusiones.push(`Ojo: ${pocos.map(f => `${f.zona.ciudad} (${f.dias} días)`).join(', ')} tienen pocos días de datos en el rango; sus cifras son indicativas.`)

  return { filas, ventaTotal, ventaClasificada, top, shareCarnes, conclusiones }
}
