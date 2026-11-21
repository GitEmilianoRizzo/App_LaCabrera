import { Lightbulb } from 'lucide-react'
import { iconoProducto, nombreCorto, type Informe } from '@/lib/consumo'

const nf = (d = 0) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d, minimumFractionDigits: d })
const pct = (x: number) => `${nf(0).format(x * 100)} %`

interface Props {
  informe: Informe
  avisos: string[]
  periodo: string
}

export function InformeConsumo({ informe, avisos, periodo }: Props) {
  if (!informe.filas.length) return <p className="text-sm text-muted-foreground">No hay ventas con productos en el rango elegido.</p>
  return (
    <div className="space-y-5">
      <div className="rounded-lg border-l-4 border-cabrera-gold bg-cabrera-cream/60 dark:bg-white/5 p-4">
        <div className="flex items-center gap-2 mb-2">
          <Lightbulb className="h-4 w-4 text-cabrera-gold" />
          <h3 className="font-semibold">Conclusiones · {periodo}</h3>
        </div>
        <ul className="space-y-1.5 text-sm list-disc pl-5">
          {informe.conclusiones.map((c, i) => <li key={i}>{c}</li>)}
        </ul>
      </div>

      <div className="overflow-x-auto rounded-lg border dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr>
              <th className="text-left px-3 py-2 font-semibold">Zona</th>
              <th className="text-right px-3 py-2 font-semibold">Gasto por cubierto</th>
              <th className="text-right px-3 py-2 font-semibold">Venta (USD)</th>
              <th className="text-right px-3 py-2 font-semibold" title="Parte de la venta asignada a un producto">Clasificado</th>
              <th className="text-right px-3 py-2 font-semibold" title="Sobre la venta clasificada">Carne vacuna</th>
              <th className="text-left px-3 py-2 font-semibold">Más vendido</th>
              <th className="text-left px-3 py-2 font-semibold">Lo distintivo (cada 100 cubiertos)</th>
            </tr>
          </thead>
          <tbody>
            {informe.filas.map(f => (
              <tr key={f.zona.key} className="border-t dark:border-gray-700 align-top">
                <td className="px-3 py-2">
                  <p className="font-medium">{f.zona.ciudad}</p>
                  <p className="text-xs text-muted-foreground">{f.zona.pais.nombre} · {f.franquicias} {f.franquicias === 1 ? 'local' : 'locales'} · {f.dias} días</p>
                </td>
                <td className="px-3 py-2 text-right tabular-nums font-semibold">{f.gastoPorCubierto > 0 ? `USD ${nf(0).format(f.gastoPorCubierto)}` : '—'}</td>
                <td className="px-3 py-2 text-right tabular-nums">{nf(0).format(f.ventaUsd)}</td>
                <td className={`px-3 py-2 text-right tabular-nums ${f.ventaUsd > 0 && f.ventaClasificada / f.ventaUsd < 0.9 ? 'text-amber-700 dark:text-amber-400 font-semibold' : ''}`}>{f.ventaUsd > 0 ? pct(f.ventaClasificada / f.ventaUsd) : '—'}</td>
                <td className="px-3 py-2 text-right tabular-nums">{pct(f.shareCarnes)}</td>
                <td className="px-3 py-2 whitespace-nowrap">
                  {f.lider ? <><span className="mr-1">{iconoProducto(f.lider.producto, f.lider.rubro)}</span>{nombreCorto(f.lider.producto)} <span className="text-muted-foreground">({pct(f.lider.share)})</span></> : '—'}
                </td>
                <td className="px-3 py-2">
                  {f.distintivos.length ? f.distintivos.map(d => (
                    <span key={d.producto} className="inline-block mr-3">
                      {nombreCorto(d.producto)} <span className="tabular-nums">{nf(1).format(d.por100)}</span>
                      <span className="text-muted-foreground"> (×{nf(1).format(d.indice)} la red)</span>
                    </span>
                  )) : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        {informe.top.map((t, i) => (
          <div key={t.producto} className="rounded-lg border dark:border-gray-700 p-3">
            <p className="text-xs text-muted-foreground">#{i + 1} de la red</p>
            <p className="font-semibold truncate" title={t.producto}>{iconoProducto(t.producto, t.rubro)} {nombreCorto(t.producto)}</p>
            <p className="text-2xl font-bold tabular-nums">{pct(t.share)}</p>
            <p className="text-xs text-muted-foreground">de la venta clasificada · USD {nf(0).format(t.usdPorUnidad)} por unidad</p>
          </div>
        ))}
      </div>

      <div className="text-xs text-muted-foreground space-y-1">
        <p>
          Venta neta sin impuestos ni servicio, convertida a USD con la cotización de cada día (misma definición que el Dashboard).
          Las unidades cuentan solo lo cobrado: media porción = 0,5 y empanadas por unidad. Lo sin cargo (menús, cortesías) no suma.
          “Lo distintivo” compara cada zona con el promedio de la red, cada 100 cubiertos. Los porcentajes de producto se calculan sobre la venta clasificada; la venta y el gasto por cubierto, sobre el total de los tickets (igual que Alertas).
        </p>
        {avisos.map((a, i) => <p key={i}>⚠ {a}</p>)}
      </div>
    </div>
  )
}
