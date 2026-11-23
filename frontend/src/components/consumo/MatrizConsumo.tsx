import { Fragment, useMemo, useState } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { colorSecuencial, esControl, formatoExacto, valorMetrica, nombreCorto, type AcumZona, type Metrica, type Modelo } from '@/lib/consumo'

interface Props {
  modelo: Modelo
  acum: AcumZona[]
  metrica: Metrica
  oscuro: boolean
}

type Nivel = 'rubro' | 'producto'

const AYUDA_CONTROL: Record<string, string> = {
  'SIN CLASIFICAR': 'Ítems cuyo nombre no se reconoce (p. ej. POS que informan códigos): se muestran para que el total cierre',
  'NO PRODUCTO': 'Propinas, eventos, ajustes e instrucciones de cocina que vienen como ítems del ticket',
  'AJUSTE A TICKETS': 'Diferencia entre las líneas y el neto de los tickets: días sin detalle de productos y redondeos',
}

interface Columna { key: string; titulo: string; prods: number[]; control?: boolean }

// Une los acumulados de varias zonas (fila de país).
function unir(as: AcumZona[]): AcumZona {
  const prod = new Float64Array(as[0]?.prod.length ?? 0)
  const r: AcumZona = { cubiertos: 0, tickets: 0, ventaUsd: 0, ventaArs: 0, dias: new Set(), franquicias: new Set(), prod }
  for (const a of as) {
    r.cubiertos += a.cubiertos; r.tickets += a.tickets; r.ventaUsd += a.ventaUsd; r.ventaArs += a.ventaArs
    a.dias.forEach(d => r.dias.add(d)); a.franquicias.forEach(f => r.franquicias.add(f))
    for (let i = 0; i < prod.length; i++) prod[i] += a.prod[i]
  }
  return r
}

export function MatrizConsumo({ modelo, acum, metrica, oscuro }: Props) {
  const [nivel, setNivel] = useState<Nivel>('rubro')
  const [pctFila, setPctFila] = useState(false)
  const [abiertos, setAbiertos] = useState<Set<string>>(new Set())
  const P = modelo.data.productos

  const columnas: Columna[] = useMemo(() => {
    const total = unir(acum)
    // Peso para ordenar: si la métrica es por cubierto, se ordena por porciones; si las columnas no tienen USD, por cantidad.
    const peso = (prods: number[]) => valorMetrica(total, prods, metrica === 'por100' ? 'unidades' : metrica) || valorMetrica(total, prods, 'cantidad')
    const titulo = (r: string) => r.charAt(0) + r.slice(1).toLowerCase()
    const porRubro = (r: string) => P.flatMap((p, i) => (p.rubro === r ? [i] : []))
    // Rubros de control al final y siempre visibles: así la suma de columnas da el total (auditable).
    const control: Columna[] = ['SIN CLASIFICAR', 'NO PRODUCTO', 'AJUSTE A TICKETS']
      .map(r => ({ key: r, titulo: titulo(r), prods: porRubro(r), control: true }))
      .filter(c => c.prods.length > 0 && peso(c.prods) !== 0)
    const ordenar = (cs: Columna[]) => cs.filter(c => peso(c.prods) !== 0).sort((a, b) => peso(b.prods) - peso(a.prods))
    if (nivel === 'rubro') {
      const rubros = [...new Set(P.map(p => p.rubro))].filter(r => !esControl(r))
      return [...ordenar(rubros.map(r => ({ key: r, titulo: titulo(r), prods: porRubro(r) }))), ...control]
    }
    const prods = ordenar(P.flatMap((p, i) => (esControl(p.rubro) ? [] : [{ key: String(i), titulo: nombreCorto(p.producto), prods: [i] }])))
    const resto = prods.slice(20).flatMap(c => c.prods)
    return [...prods.slice(0, 20), ...(resto.length ? [{ key: 'resto', titulo: `Resto (${resto.length} productos)`, prods: resto }] : []), ...control]
  }, [P, acum, metrica, nivel])

  const filas = useMemo(() => {
    const porPais = new Map<string, number[]>()
    modelo.zonas.forEach((z, i) => { if (acum[i] && (acum[i].ventaUsd > 0 || acum[i].prod.some(v => v))) porPais.set(z.pais.nombre, [...(porPais.get(z.pais.nombre) ?? []), i]) })
    return [...porPais].map(([pais, zs]) => ({ pais, a: unir(zs.map(i => acum[i])), zonas: zs.map(i => ({ nombre: modelo.zonas[i].ciudad, a: acum[i] })) }))
      .sort((x, y) => y.a.ventaUsd - x.a.ventaUsd)
  }, [modelo, acum])

  const todosProds = columnas.flatMap(c => c.prods)
  const celda = (a: AcumZona, prods: number[]) => {
    const v = valorMetrica(a, prods, metrica)
    if (!pctFila || metrica === 'por100') return v
    const t = valorMetrica(a, todosProds, metrica)
    return t > 0 ? (v / t) * 100 : 0
  }
  const max = Math.max(1e-9, ...filas.flatMap(f => columnas.filter(c => !c.control).map(c => celda(f.a, c.prods))))
  const fmt = (v: number) => (pctFila && metrica !== 'por100' ? `${v.toLocaleString('es-AR', { maximumFractionDigits: 1 })} %` : formatoExacto(v, metrica))
  const fondo = (v: number) => (v > 0 ? colorSecuencial(Math.sqrt(v / max) * 0.85, oscuro) : 'transparent')
  const tinta = (v: number) => (v > 0 ? (Math.sqrt(v / max) * 0.85 > 0.5 ? '#ffffff' : '#0b0b0b') : undefined)

  const Fila = ({ nombre, a, nivelFila, onToggle, abierto }: { nombre: string; a: AcumZona; nivelFila: 0 | 1; onToggle?: () => void; abierto?: boolean }) => (
    <tr className={nivelFila === 0 ? 'border-t dark:border-gray-700' : 'text-[12px]'}>
      <th scope="row" className={`sticky left-0 z-[1] bg-card text-left font-medium px-3 py-1.5 whitespace-nowrap ${nivelFila ? 'pl-8 font-normal text-muted-foreground' : ''}`}>
        {onToggle ? (
          <button onClick={onToggle} className="flex items-center gap-1 hover:text-cabrera-burgundy dark:hover:text-cabrera-gold">
            {abierto ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}{nombre}
          </button>
        ) : nombre}
      </th>
      {columnas.map(c => {
        const v = celda(a, c.prods)
        return (
          <td key={c.key} className="px-2 py-1.5 text-right tabular-nums whitespace-nowrap border-l border-white dark:border-[#1a1a19]"
              style={c.control ? undefined : { background: fondo(v), color: tinta(v) }} title={`${nombre} · ${c.titulo}: ${fmt(v)}`}>
            {v !== 0 ? fmt(v) : '·'}
          </td>
        )
      })}
      <td className="sticky right-0 z-[1] bg-card px-3 py-1.5 text-right tabular-nums font-semibold whitespace-nowrap border-l-2 dark:border-gray-600 shadow-[-4px_0_6px_-4px_rgba(0,0,0,0.15)]">
        {pctFila && metrica !== 'por100' ? '100 %' : formatoExacto(valorMetrica(a, todosProds, metrica), metrica)}
      </td>
    </tr>
  )

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden">
          {(['rubro', 'producto'] as Nivel[]).map(n => (
            <button key={n} onClick={() => setNivel(n)}
                    className={`px-3 py-1 ${nivel === n ? 'bg-cabrera-burgundy text-white' : 'hover:bg-gray-100 dark:hover:bg-gray-700'}`}>
              {n === 'rubro' ? 'Rubros' : 'Productos (top 20)'}
            </button>
          ))}
        </div>
        {metrica !== 'por100' && (
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={pctFila} onChange={e => setPctFila(e.target.checked)} className="accent-cabrera-burgundy" />
            % del país (mix)
          </label>
        )}
        <span className="text-xs text-muted-foreground">Click en un país para abrir sus zonas.</span>
      </div>
      <div className="overflow-x-auto rounded-lg border dark:border-gray-700">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="bg-muted/50">
              <th className="sticky left-0 z-[1] bg-muted text-left px-3 py-2 font-semibold">País / zona</th>
              {columnas.map(c => <th key={c.key} title={c.control ? AYUDA_CONTROL[c.key] : undefined} className={`px-2 py-2 font-semibold text-right align-bottom min-w-[84px] leading-tight ${c.control ? 'italic text-muted-foreground' : ''}`}>{c.titulo}</th>)}
              <th className="sticky right-0 z-[1] bg-muted px-3 py-2 font-semibold text-right border-l-2 dark:border-gray-600">Total</th>
            </tr>
          </thead>
          <tbody>
            {filas.map(f => (
              <Fragment key={f.pais}>
                <Fila nombre={f.pais} a={f.a} nivelFila={0} abierto={abiertos.has(f.pais)}
                      onToggle={f.zonas.length > 1 || f.zonas[0]?.nombre !== f.pais ? () => setAbiertos(s => { const n = new Set(s); if (n.has(f.pais)) n.delete(f.pais); else n.add(f.pais); return n }) : undefined} />
                {abiertos.has(f.pais) && f.zonas.map(z => <Fila key={z.nombre} nombre={z.nombre} a={z.a} nivelFila={1} />)}
              </Fragment>
            ))}
            {filas.length > 1 && <Fila nombre="Total red" a={unir(filas.map(f => f.a))} nivelFila={0} />}
          </tbody>
        </table>
      </div>
    </div>
  )
}
