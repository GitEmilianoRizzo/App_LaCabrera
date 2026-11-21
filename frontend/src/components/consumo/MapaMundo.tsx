import { useEffect, useMemo, useRef, useState } from 'react'
import { geoNaturalEarth1, geoPath, type GeoPermissibleObjects } from 'd3-geo'
import { zoom as d3zoom, zoomIdentity, type ZoomBehavior, type ZoomTransform } from 'd3-zoom'
import { select } from 'd3-selection'
import 'd3-transition'
import { feature } from 'topojson-client'
import type { Topology, GeometryCollection } from 'topojson-specification'
import type { Feature, Geometry } from 'geojson'
import worldUrl from 'world-atlas/countries-50m.json?url'
import { Minus, Plus, RotateCcw } from 'lucide-react'
import { colorSecuencial, familiaDe, iconoProducto, nombreCorto, type Zona } from '@/lib/consumo'

export interface DatoZona {
  zona: Zona
  valor: number
  texto: string
  lider: { producto: string; rubro: string } | null
  detalle: string[]
}

export interface DatoPais {
  iso: string
  nombre: string
  valor: number
  texto: string
  lider: { producto: string; rubro: string } | null
}

interface Props {
  zonas: DatoZona[]
  paises: DatoPais[]
  maxPais: number
  maxZona: number
  modo: 'intensidad' | 'lider'
  oscuro: boolean
  overlay?: React.ReactNode
  /** Zonas por las que se puede saltar con un click. */
  enfocar?: { key: string; seq: number } | null
}

type Pais = Feature<Geometry, { name: string }> & { id?: string }

let mundoCache: Promise<Pais[]> | null = null
function cargarMundo(): Promise<Pais[]> {
  mundoCache ??= fetch(worldUrl)
    .then(r => r.json())
    .then((topo: Topology<{ countries: GeometryCollection<{ name: string }> }>) => {
      const fc = feature(topo, topo.objects.countries) as unknown as { features: Pais[] }
      return fc.features.filter(f => f.id !== '010') // sin Antártida
    })
  return mundoCache
}

const ZOOM_ICONOS = 2.6
const ZOOM_ETIQUETAS = 1.6

export function MapaMundo({ zonas, paises, maxPais, maxZona, modo, oscuro, overlay, enfocar }: Props) {
  const contRef = useRef<HTMLDivElement>(null)
  const svgRef = useRef<SVGSVGElement>(null)
  const zoomRef = useRef<ZoomBehavior<SVGSVGElement, unknown> | null>(null)
  const [mundo, setMundo] = useState<Pais[] | null>(null)
  const [ancho, setAncho] = useState(900)
  const [t, setT] = useState<ZoomTransform>(zoomIdentity)
  const [hover, setHover] = useState<{ x: number; y: number; titulo: string; lineas: string[] } | null>(null)

  const alto = Math.max(320, Math.min(620, Math.round(ancho * 0.52)))

  useEffect(() => { cargarMundo().then(setMundo).catch(() => setMundo([])) }, [])

  useEffect(() => {
    const el = contRef.current
    if (!el) return
    const ro = new ResizeObserver(([e]) => setAncho(Math.max(320, Math.round(e.contentRect.width))))
    ro.observe(el)
    return () => ro.disconnect()
  }, [])

  const proyeccion = useMemo(() => {
    const p = geoNaturalEarth1()
    if (mundo?.length) p.fitExtent([[8, 8], [ancho - 8, alto - 8]], { type: 'FeatureCollection', features: mundo } as GeoPermissibleObjects)
    return p
  }, [mundo, ancho, alto])
  const path = useMemo(() => geoPath(proyeccion), [proyeccion])
  const paths = useMemo(() => (mundo ?? []).map(f => ({ id: String(f.id ?? ''), nombre: f.properties?.name ?? '', d: path(f) ?? '' })), [mundo, path])

  useEffect(() => {
    const svg = svgRef.current
    if (!svg) return
    const z = d3zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 40])
      .translateExtent([[-ancho * 0.25, -alto * 0.25], [ancho * 1.25, alto * 1.25]])
      .on('zoom', e => setT(e.transform))
    zoomRef.current = z
    select(svg).call(z)
    return () => { select(svg).on('.zoom', null) }
  }, [ancho, alto])

  const irA = (puntos: [number, number][], kMin = 4) => {
    const svg = svgRef.current, z = zoomRef.current
    if (!svg || !z || !puntos.length) return
    const xy = puntos.map(c => proyeccion(c)!).filter(Boolean)
    const xs = xy.map(p => p[0]), ys = xy.map(p => p[1])
    const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)]
    const k = Math.min(30, Math.max(kMin, 0.5 / Math.max((x1 - x0 + 1) / ancho, (y1 - y0 + 1) / alto)))
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2
    select(svg).transition().duration(750).call(z.transform, zoomIdentity.translate(ancho / 2 - k * cx, alto / 2 - k * cy).scale(k))
  }
  const zoomPor = (f: number) => { const svg = svgRef.current, z = zoomRef.current; if (svg && z) select(svg).transition().duration(300).call(z.scaleBy, f) }
  const reset = () => { const svg = svgRef.current, z = zoomRef.current; if (svg && z) select(svg).transition().duration(600).call(z.transform, zoomIdentity) }

  useEffect(() => {
    if (!enfocar) return
    const pts = zonas.filter(z => z.zona.key === enfocar.key || z.zona.pais.nombre === enfocar.key).flatMap(z => (z.zona.coord ? [z.zona.coord] : []))
    if (enfocar.key === '*') reset()
    else irA(pts, pts.length > 1 ? 3 : 7)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enfocar?.seq])

  const porIso = useMemo(() => new Map(paises.map(p => [p.iso, p])), [paises])
  const tierra = oscuro ? '#383835' : '#e7e6e1'
  const borde = oscuro ? '#1a1a19' : '#fcfcfb'
  const k = t.k

  const colorPais = (p: DatoPais | undefined) => {
    if (!p) return tierra
    if (modo === 'lider') {
      if (!p.lider) return tierra
      const f = familiaDe(p.lider.rubro)
      return oscuro ? f.oscuro : f.claro
    }
    if (p.valor <= 0 || maxPais <= 0) return tierra
    return colorSecuencial(0.08 + 0.92 * Math.sqrt(p.valor / maxPais), oscuro)
  }
  const colorZona = (z: DatoZona) => {
    if (modo === 'lider') return z.lider ? (oscuro ? familiaDe(z.lider.rubro).oscuro : familiaDe(z.lider.rubro).claro) : tierra
    if (z.valor <= 0 || maxZona <= 0) return tierra
    return colorSecuencial(0.08 + 0.92 * Math.sqrt(z.valor / maxZona), oscuro)
  }

  const mover = (e: React.MouseEvent, titulo: string, lineas: string[]) => {
    const r = contRef.current!.getBoundingClientRect()
    setHover({ x: e.clientX - r.left, y: e.clientY - r.top, titulo, lineas })
  }

  // Zonas en la misma ciudad proyectada se apilan apenas para que no se pisen los íconos.
  const zonasConXY = zonas.filter(z => z.zona.coord).map(z => ({ ...z, xy: proyeccion(z.zona.coord!)! }))

  return (
    <div ref={contRef} className="relative w-full select-none" style={{ height: alto }}>
      {!mundo && <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">Cargando mapa…</div>}
      <svg ref={svgRef} width={ancho} height={alto} className="block rounded-lg bg-sky-50/40 dark:bg-[#1a1a19] cursor-grab active:cursor-grabbing" role="img" aria-label="Mapa del mundo con el consumo por zona">
        <g transform={t.toString()}>
          {paths.map(p => {
            const dato = porIso.get(p.id)
            return (
              <path
                key={p.id + p.nombre}
                d={p.d}
                fill={colorPais(dato)}
                stroke={borde}
                strokeWidth={0.6}
                vectorEffect="non-scaling-stroke"
                className={dato ? 'cursor-pointer transition-[fill] duration-300' : undefined}
                onMouseMove={dato ? e => mover(e, dato.nombre, [dato.texto, ...(dato.lider ? [`Más vendido: ${nombreCorto(dato.lider.producto)}`] : [])]) : undefined}
                onMouseLeave={() => setHover(null)}
                onClick={dato ? () => {
                  const pts = zonas.filter(z => z.zona.pais.iso === p.id && z.zona.coord).map(z => z.zona.coord!)
                  irA(pts, pts.length > 1 ? 3 : 7)
                } : undefined}
              />
            )
          })}

          {zonasConXY.map(z => {
            const [x, y] = z.xy
            const muestraIcono = k >= ZOOM_ICONOS && z.lider
            return (
              <g key={z.zona.key} transform={`translate(${x},${y}) scale(${1 / k})`}
                 onMouseMove={e => mover(e, z.zona.key, [z.texto, ...z.detalle])} onMouseLeave={() => setHover(null)}
                 className="cursor-pointer" onClick={() => irA([z.zona.coord!], Math.max(k * 1.8, 7))}>
                {/* Área de hover más grande que la marca */}
                <circle r={muestraIcono ? 26 : 12} fill="transparent" />
                {muestraIcono ? (
                  <>
                    <circle r={21} fill={colorZona(z)} stroke={borde} strokeWidth={3} className="transition-[fill] duration-300" />
                    <circle r={15} fill={oscuro ? '#1a1a19' : '#ffffff'} />
                    <text textAnchor="middle" dominantBaseline="central" fontSize={18}>{iconoProducto(z.lider!.producto, z.lider!.rubro)}</text>
                    <g transform="translate(0,30)">
                      {(() => {
                        const linea2 = `${nombreCorto(z.lider!.producto)} · ${z.texto}`
                        const w = Math.max(z.zona.ciudad.length * 7.4, linea2.length * 6.3) + 14
                        return (
                          <>
                            <rect x={-w / 2} y={0} width={w} height={34} rx={6} fill={oscuro ? '#1a1a19' : '#ffffff'} fillOpacity={0.92}
                                  stroke={oscuro ? '#383835' : '#e7e6e1'} />
                            <text y={14} textAnchor="middle" fontSize={12} fontWeight={700} fill={oscuro ? '#f0efec' : '#0b0b0b'}>{z.zona.ciudad}</text>
                            <text y={28} textAnchor="middle" fontSize={11} fill={oscuro ? '#c3c2b7' : '#52514e'}>{linea2}</text>
                          </>
                        )
                      })()}
                    </g>
                  </>
                ) : (
                  <>
                    <circle r={6} fill={colorZona(z)} stroke={oscuro ? '#f0efec' : '#2d2d2d'} strokeWidth={1.5} className="transition-[fill] duration-300" />
                    {k >= ZOOM_ETIQUETAS && (
                      <text x={9} y={4} fontSize={11} fontWeight={600} fill={oscuro ? '#f0efec' : '#0b0b0b'} stroke={borde} strokeWidth={3} paintOrder="stroke">{z.zona.ciudad}</text>
                    )}
                  </>
                )}
              </g>
            )
          })}
        </g>
      </svg>

      {overlay}

      <div className="absolute right-2 top-2 flex flex-col gap-1">
        {[{ ic: Plus, f: () => zoomPor(1.8), t: 'Acercar' }, { ic: Minus, f: () => zoomPor(1 / 1.8), t: 'Alejar' }, { ic: RotateCcw, f: reset, t: 'Ver el mundo' }].map(b => (
          <button key={b.t} onClick={b.f} title={b.t} aria-label={b.t}
                  className="h-8 w-8 rounded-md border bg-white/90 dark:bg-gray-800/90 dark:border-gray-600 flex items-center justify-center hover:bg-white dark:hover:bg-gray-700 shadow-sm">
            <b.ic className="h-4 w-4" />
          </button>
        ))}
      </div>
      {k < ZOOM_ICONOS && (
        <div className="absolute left-2 bottom-2 text-[11px] text-muted-foreground bg-white/80 dark:bg-gray-900/80 rounded px-2 py-1">
          Hacé zoom o click en un país para ver el producto más consumido de cada zona
        </div>
      )}

      {hover && (
        <div className="pointer-events-none absolute z-10 rounded-md border bg-white dark:bg-gray-800 dark:border-gray-600 px-3 py-2 text-xs shadow-lg"
             style={{ left: Math.min(hover.x + 14, ancho - 230), top: Math.max(4, hover.y - 10), maxWidth: 220 }}>
          <p className="font-semibold text-sm mb-0.5">{hover.titulo}</p>
          {hover.lineas.map((l, i) => <p key={i} className="text-muted-foreground">{l}</p>)}
        </div>
      )}
    </div>
  )
}
