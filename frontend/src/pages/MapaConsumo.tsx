import { useCallback, useEffect, useMemo, useState } from 'react'
import { Calendar, Globe2, Pause, Play, RefreshCw, SkipBack } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { MapaMundo, type DatoPais, type DatoZona } from '@/components/consumo/MapaMundo'
import { MatrizConsumo } from '@/components/consumo/MatrizConsumo'
import { InformeConsumo } from '@/components/consumo/InformeConsumo'
import { CuadreAlertas } from '@/components/consumo/CuadreAlertas'
import { consumoApi } from '@/services/api'
import type { ConsumoProductosResponse } from '@/types/consumo'
import {
  FAMILIAS, METRICAS, NC, acumular, armarInforme, crearModelo, familiaDe, formatoMetrica, gradienteSecuencial, lider,
  nombreCorto, productosDelFiltro, valorMetrica, type AcumZona, type FiltroProducto, type Metrica, type Modelo,
} from '@/lib/consumo'

type ModoColor = 'intensidad' | 'lider'
type Ventana = 'dia' | '7d' | 'acum'

const VENTANAS: { key: Ventana; etiqueta: string }[] = [
  { key: 'dia', etiqueta: 'Día' },
  { key: '7d', etiqueta: 'Últimos 7 días' },
  { key: 'acum', etiqueta: 'Acumulado' },
]
const VELOCIDADES = [{ ms: 1200, t: '1×' }, { ms: 600, t: '2×' }, { ms: 250, t: '4×' }]

function rangoVentana(ventana: Ventana, f: number): [number, number] {
  return ventana === 'dia' ? [f, f] : ventana === '7d' ? [f - 6, f] : [0, f]
}

function useModoOscuro() {
  const [oscuro, setOscuro] = useState(() => document.documentElement.classList.contains('dark'))
  useEffect(() => {
    const mo = new MutationObserver(() => setOscuro(document.documentElement.classList.contains('dark')))
    mo.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
    return () => mo.disconnect()
  }, [])
  return oscuro
}

const fechaLarga = (iso: string) =>
  new Date(`${iso}T12:00:00`).toLocaleDateString('es-AR', { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' })
const fechaCorta = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' })

/** Agrega zonas al nivel país para pintar el mapa. */
function porPais(m: Modelo, acum: AcumZona[], prods: number[], metrica: Metrica): DatoPais[] {
  const grupos = new Map<string, number[]>()
  m.zonas.forEach((z, i) => { if (z.pais.iso) grupos.set(z.pais.iso, [...(grupos.get(z.pais.iso) ?? []), i]) })
  return [...grupos].map(([iso, zs]) => {
    const a: AcumZona = { cubiertos: 0, tickets: 0, ventaUsd: 0, ventaArs: 0, dias: new Set(), franquicias: new Set(), prod: new Float64Array(m.nProd * NC) }
    for (const i of zs) {
      const b = acum[i]
      a.cubiertos += b.cubiertos; a.tickets += b.tickets; a.ventaUsd += b.ventaUsd; a.ventaArs += b.ventaArs
      for (let k = 0; k < a.prod.length; k++) a.prod[k] += b.prod[k]
    }
    const valor = valorMetrica(a, prods, metrica)
    const l = lider(a, prods, metrica, m.data.productos.map(p => p.rubro))
    return {
      iso, nombre: m.zonas[zs[0]].pais.nombre, valor,
      texto: `${METRICAS.find(x => x.key === metrica)!.etiqueta}: ${formatoMetrica(valor, metrica)}`,
      lider: l !== null ? m.data.productos[l] : null,
    }
  })
}

export function MapaConsumo() {
  const oscuro = useModoOscuro()
  const [data, setData] = useState<ConsumoProductosResponse | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')

  const [filtro, setFiltro] = useState<FiltroProducto>('todos')
  const [metrica, setMetrica] = useState<Metrica>('usd')
  const [modoColor, setModoColor] = useState<ModoColor>('intensidad')
  const [ventana, setVentana] = useState<Ventana>('dia')
  const [frame, setFrame] = useState<number | null>(null) // null = rango completo
  const [jugando, setJugando] = useState(false)
  const [velocidad, setVelocidad] = useState(VELOCIDADES[1].ms)
  const [enfocar, setEnfocar] = useState<{ key: string; seq: number } | null>(null)

  const cargar = useCallback(async (d?: string, h?: string) => {
    setCargando(true); setError(null); setJugando(false); setFrame(null)
    try {
      const r = await consumoApi.getConsumoProductos({ fechaDesde: d || undefined, fechaHasta: h || undefined })
      setData(r); setDesde(r.desde); setHasta(r.hasta)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cargar el consumo por producto')
    } finally {
      setCargando(false)
    }
  }, [])

  useEffect(() => { cargar() }, [cargar])

  const modelo = useMemo(() => (data ? crearModelo(data) : null), [data])
  const n = data?.fechas.length ?? 0
  const prods = useMemo(() => (modelo ? productosDelFiltro(modelo, filtro) : []), [modelo, filtro])

  // Play: avanza un día por tick; al llegar al final se detiene.
  useEffect(() => {
    if (!jugando || !n) return
    const id = window.setInterval(() => {
      setFrame(f => {
        const sig = f === null ? 0 : f + 1
        if (sig >= n - 1) { setJugando(false); return n - 1 }
        return sig
      })
    }, velocidad)
    return () => window.clearInterval(id)
  }, [jugando, velocidad, n])

  const [i0, i1] = frame === null ? [0, n - 1] : rangoVentana(ventana, frame)
  const acum = useMemo(() => (modelo ? acumular(modelo, i0, i1) : []), [modelo, i0, i1])
  const acumTotal = useMemo(() => (modelo ? acumular(modelo, 0, n - 1) : []), [modelo, n])

  // Escala fija durante el play: el máximo de todos los cuadros, así los colores se comparan entre días.
  const maximos = useMemo(() => {
    if (!modelo || !n) return { pais: 0, zona: 0 }
    const cuadros = frame === null ? [[0, n - 1]] : ventana === 'acum' ? [[0, n - 1]] : Array.from({ length: n }, (_, f) => rangoVentana(ventana, f))
    let pais = 0, zona = 0
    for (const [a, b] of cuadros) {
      const ac = acumular(modelo, a, b)
      for (const z of ac) zona = Math.max(zona, valorMetrica(z, prods, metrica))
      for (const p of porPais(modelo, ac, prods, metrica)) pais = Math.max(pais, p.valor)
    }
    return { pais, zona }
  }, [modelo, n, prods, metrica, ventana, frame === null]) // eslint-disable-line react-hooks/exhaustive-deps

  const etiquetaMetrica = METRICAS.find(x => x.key === metrica)!.etiqueta
  const datosZonas: DatoZona[] = useMemo(() => {
    if (!modelo) return []
    const rubrosProd = modelo.data.productos.map(p => p.rubro)
    return modelo.zonas.map((z, i) => {
      const a = acum[i]
      const valor = valorMetrica(a, prods, metrica)
      const l = lider(a, prods, metrica, rubrosProd)
      return {
        zona: z, valor, texto: formatoMetrica(valor, metrica),
        lider: l !== null ? modelo.data.productos[l] : null,
        detalle: [
          `${etiquetaMetrica}: ${formatoMetrica(valor, metrica)}`,
          ...(l !== null ? [`Más vendido: ${nombreCorto(modelo.data.productos[l].producto)}`] : []),
          `${a.franquicias.size} ${a.franquicias.size === 1 ? 'local' : 'locales'} · ${a.dias.size} días con datos · ${Math.round(a.cubiertos).toLocaleString('es-AR')} cubiertos`,
        ],
      }
    })
  }, [modelo, acum, prods, metrica, etiquetaMetrica])
  const datosPaises = useMemo(() => (modelo ? porPais(modelo, acum, prods, metrica) : []), [modelo, acum, prods, metrica])

  const informe = useMemo(() => (modelo ? armarInforme(modelo, acumTotal) : null), [modelo, acumTotal])
  const avisos = useMemo(() => {
    if (!data) return []
    const a: string[] = []
    const ex = data.franquicias.filter(f => f.sin_nombres)
    if (ex.length) a.push(`${ex.map(f => f.nombre || f.codigo).join(', ')}: el POS informa códigos en lugar de nombres de producto; su venta está incluida en los totales, en "Sin clasificar".`)
    const c = data.cobertura
    const pctDe = (x: number) => `${(c.usd_total ? (x / c.usd_total) * 100 : 0).toLocaleString('es-AR', { maximumFractionDigits: 1 })} %`
    if (c.usd_total > 0) a.push(`De la venta total (USD ${Math.round(c.usd_total).toLocaleString('es-AR')}): clasificada ${pctDe(c.usd_clasificado)}, sin clasificar ${pctDe(c.usd_sin_clasificar)}, no producto (propinas, ajustes) ${pctDe(c.usd_no_producto)}, ajuste a tickets ${pctDe(c.usd_ajuste)}.`)
    return a
  }, [data])

  const rubros = useMemo(() => {
    if (!data) return []
    const m = new Map<string, number[]>()
    data.productos.forEach((p, i) => { if (p.rubro !== 'SIN CLASIFICAR') m.set(p.rubro, [...(m.get(p.rubro) ?? []), i]) })
    return [...m].sort((a, b) => a[0].localeCompare(b[0], 'es'))
  }, [data])
  const paisesActivos = useMemo(() => [...new Set(datosZonas.filter(z => z.zona.coord).map(z => z.zona.pais.nombre))], [datosZonas])
  const familiasPresentes = useMemo(() => {
    const ks = new Set(datosPaises.flatMap(p => (p.lider ? [familiaDe(p.lider.rubro).key] : [])))
    datosZonas.forEach(z => z.lider && ks.add(familiaDe(z.lider.rubro).key))
    return FAMILIAS.filter(f => ks.has(f.key))
  }, [datosPaises, datosZonas])

  if (cargando && !data) return <LoadingPage />
  if (error && !data) return <ErrorBanner message={error} onRetry={() => cargar(desde, hasta)} />
  if (!data || !modelo) return null

  const periodo = `${fechaCorta(data.desde)} al ${fechaCorta(data.hasta)}`
  const etiquetaVentana = frame === null ? `Rango completo · ${periodo}`
    : ventana === 'dia' ? fechaLarga(data.fechas[frame])
    : ventana === '7d' ? `7 días al ${fechaCorta(data.fechas[frame])}`
    : `Acumulado al ${fechaCorta(data.fechas[frame])}`
  const filtroTexto = filtro === 'todos' ? 'Todos los productos'
    : filtro.startsWith('r:') ? filtro.slice(2).charAt(0) + filtro.slice(3).toLowerCase()
    : data.productos[Number(filtro.slice(2))]?.producto

  const sel = 'px-2 py-1 text-sm border rounded-md focus:outline-none focus:ring-2 focus:ring-cabrera-burgundy/50 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white'
  const seg = (activo: boolean) => `px-3 py-1 text-sm ${activo ? 'bg-cabrera-burgundy text-white' : 'hover:bg-gray-100 dark:hover:bg-gray-700'}`

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <Globe2 className="h-6 w-6 text-cabrera-gold" /> Mapa de Consumo
        </h1>
        <p className="text-muted-foreground">Qué se come y se toma en cada zona de La Cabrera: productos unificados entre franquicias, en cantidades, USD y ARS.</p>
      </div>

      {/* Filtros: una sola fila arriba de todo */}
      <Card>
        <CardContent className="p-4 flex flex-wrap items-center gap-x-5 gap-y-3">
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-muted-foreground" />
            <span className="text-sm font-medium">Período:</span>
            <input type="date" className={sel} value={desde} onChange={e => setDesde(e.target.value)} max={hasta || undefined} />
            <span className="text-sm text-muted-foreground">a</span>
            <input type="date" className={sel} value={hasta} onChange={e => setHasta(e.target.value)} min={desde || undefined} />
            <Button size="sm" variant="outline" onClick={() => cargar(desde, hasta)} disabled={cargando} className="gap-1">
              <RefreshCw className={`h-3.5 w-3.5 ${cargando ? 'animate-spin' : ''}`} /> Aplicar
            </Button>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <span className="font-medium">Producto:</span>
            <select className={`${sel} max-w-[260px]`} value={filtro} onChange={e => setFiltro(e.target.value)}>
              <option value="todos">Todos los productos</option>
              <optgroup label="Rubros">
                {rubros.map(([r]) => <option key={r} value={`r:${r}`}>{r.charAt(0) + r.slice(1).toLowerCase()}</option>)}
              </optgroup>
              {rubros.map(([r, ps]) => (
                <optgroup key={r} label={r.charAt(0) + r.slice(1).toLowerCase()}>
                  {ps.map(i => <option key={i} value={`p:${i}`}>{data.productos[i].producto}</option>)}
                </optgroup>
              ))}
            </select>
          </label>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium">Medir en:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
              {METRICAS.map(m => <button key={m.key} className={seg(metrica === m.key)} onClick={() => setMetrica(m.key)}>{m.etiqueta}</button>)}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium">Colorear por:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
              <button className={seg(modoColor === 'intensidad')} onClick={() => setModoColor('intensidad')}>Intensidad</button>
              <button className={seg(modoColor === 'lider')} onClick={() => setModoColor('lider')}>Producto líder</button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Mapa */}
      <Card>
        <CardHeader className="pb-2">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <CardTitle className="text-lg">{filtroTexto} · {etiquetaMetrica}</CardTitle>
              <p className="text-sm text-muted-foreground">{etiquetaVentana}</p>
            </div>
            {modoColor === 'intensidad' ? (
              <div className="flex items-center gap-2 text-xs text-muted-foreground" aria-label="Escala de color">
                <span>Menos</span>
                <span className="h-2.5 w-40 rounded-full" style={{ background: gradienteSecuencial(oscuro) }} />
                <span>Más{maximos.pais > 0 ? ` (${formatoMetrica(maximos.pais, metrica)})` : ''}</span>
              </div>
            ) : (
              <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs max-w-xl justify-end" aria-label="Familias de producto">
                {familiasPresentes.map(f => (
                  <span key={f.key} className="flex items-center gap-1.5">
                    <span className="h-2.5 w-2.5 rounded-sm" style={{ background: oscuro ? f.oscuro : f.claro }} />{f.nombre}
                  </span>
                ))}
              </div>
            )}
          </div>
          <div className="flex flex-wrap gap-1.5 pt-2">
            <button onClick={() => setEnfocar({ key: '*', seq: Date.now() })} className="text-xs px-2 py-0.5 rounded-full border dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700">🌎 Mundo</button>
            {paisesActivos.map(p => (
              <button key={p} onClick={() => setEnfocar({ key: p, seq: Date.now() })} className="text-xs px-2 py-0.5 rounded-full border dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700">{p}</button>
            ))}
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <MapaMundo
            zonas={datosZonas}
            paises={datosPaises}
            maxPais={maximos.pais}
            maxZona={maximos.zona}
            modo={modoColor}
            oscuro={oscuro}
            enfocar={enfocar}
            overlay={frame !== null ? (
              <div className="pointer-events-none absolute left-3 top-3 rounded-lg bg-white/85 dark:bg-gray-900/85 px-3 py-1.5 shadow-sm">
                <p className="text-[11px] uppercase tracking-wide text-muted-foreground">{VENTANAS.find(v => v.key === ventana)!.etiqueta}</p>
                <p className="text-lg font-bold tabular-nums">{fechaLarga(data.fechas[frame])}</p>
              </div>
            ) : undefined}
          />

          {/* Play día a día */}
          <div className="flex flex-wrap items-center gap-3 rounded-lg border dark:border-gray-700 px-3 py-2">
            <Button size="sm" onClick={() => { if (frame === null || frame >= n - 1) setFrame(0); setJugando(j => !j) }} className="gap-1 bg-cabrera-burgundy hover:bg-cabrera-burgundy/90 text-white" aria-label={jugando ? 'Pausar' : 'Reproducir'}>
              {jugando ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />} {jugando ? 'Pausa' : 'Play'}
            </Button>
            <Button size="sm" variant="outline" onClick={() => { setJugando(false); setFrame(null) }} title="Volver al rango completo" className="gap-1">
              <SkipBack className="h-4 w-4" /> Rango completo
            </Button>
            <input type="range" min={0} max={Math.max(0, n - 1)} value={frame ?? n - 1}
                   onChange={e => { setJugando(false); setFrame(Number(e.target.value)) }}
                   className="flex-1 min-w-[160px] accent-cabrera-burgundy" aria-label="Día" />
            <span className="text-xs tabular-nums text-muted-foreground w-24 text-right">{frame === null ? `${n} días` : `${frame + 1} / ${n}`}</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
              {VENTANAS.map(v => <button key={v.key} className={seg(ventana === v.key)} onClick={() => setVentana(v.key)}>{v.etiqueta}</button>)}
            </div>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden" title="Velocidad">
              {VELOCIDADES.map(v => <button key={v.ms} className={seg(velocidad === v.ms)} onClick={() => setVelocidad(v.ms)}>{v.t}</button>)}
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            Durante el play la escala de colores queda fija (el máximo del rango), así un país que se oscurece de verdad vendió más.
            “Últimos 7 días” suaviza el efecto fin de semana.
          </p>
        </CardContent>
      </Card>

      {/* Matriz */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-lg">Matriz por país y producto · {etiquetaMetrica}</CardTitle>
          <p className="text-sm text-muted-foreground">{etiquetaVentana}{filtro !== 'todos' ? ' · la matriz muestra todos los productos' : ''}</p>
        </CardHeader>
        <CardContent>
          <MatrizConsumo modelo={modelo} acum={acum} metrica={metrica} oscuro={oscuro} />
        </CardContent>
      </Card>

      {/* Cuadre */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-lg">Cuadre con Alertas</CardTitle>
          <p className="text-sm text-muted-foreground">Rango completo ({periodo}): lo que muestra esta sección contra lo que muestra Alertas, por franquicia.</p>
        </CardHeader>
        <CardContent>
          <CuadreAlertas data={data} />
        </CardContent>
      </Card>

      {/* Informe */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-lg">Informe: qué nos dice el consumo por zona</CardTitle>
          <p className="text-sm text-muted-foreground">Calculado sobre el rango completo ({periodo}).</p>
        </CardHeader>
        <CardContent>
          {informe && <InformeConsumo informe={informe} avisos={avisos} periodo={periodo} />}
        </CardContent>
      </Card>
    </div>
  )
}
