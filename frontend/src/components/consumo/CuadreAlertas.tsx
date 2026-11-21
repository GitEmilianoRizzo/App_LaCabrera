import { useEffect, useMemo, useState } from 'react'
import { CheckCircle2, AlertTriangle } from 'lucide-react'
import { dashboardApi } from '@/services/api'
import type { ConsumoProductosResponse } from '@/types/consumo'

interface Totales { tickets: number; cubiertos: number; usd: number }
interface FilaCuadre { codigo: string; nombre: string; mapa: Totales & { usdMatriz: number; cantidad: number }; alertas: Totales | null }

const nf = (d = 0) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d, minimumFractionDigits: d })
const TOL_USD = 1 // centavos de redondeo diario

/**
 * Cruce del mapa contra Alertas (mismo endpoint que usa esa pantalla) para el rango cargado.
 * Por franquicia: tickets, cubiertos y venta USD; además la suma de la matriz (todos los rubros) contra la venta de los tickets.
 */
export function CuadreAlertas({ data }: { data: ConsumoProductosResponse }) {
  const [alertas, setAlertas] = useState<Map<string, Totales> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let vivo = true
    setAlertas(null); setError(null)
    dashboardApi.getVentasResumen({ fechaDesde: data.desde, fechaHasta: data.hasta })
      .then(rs => {
        const m = new Map<string, Totales>()
        for (const r of rs) {
          const t = m.get(r.franquicia_codigo) ?? { tickets: 0, cubiertos: 0, usd: 0 }
          t.tickets += r.cantidad_tickets || 0; t.cubiertos += r.total_cubiertos || 0; t.usd += r.venta_neta_usd || 0
          m.set(r.franquicia_codigo, t)
        }
        if (vivo) setAlertas(m)
      })
      .catch(e => vivo && setError(e instanceof Error ? e.message : 'No se pudo leer Alertas'))
    return () => { vivo = false }
  }, [data.desde, data.hasta])

  const filas: FilaCuadre[] = useMemo(() => {
    const mapa = data.franquicias.map(() => ({ tickets: 0, cubiertos: 0, usd: 0, usdMatriz: 0, cantidad: 0 }))
    for (const d of data.dias) { const t = mapa[d[1]]; t.tickets += d[2]; t.cubiertos += d[3]; t.usd += d[4] }
    for (const r of data.filas) { const t = mapa[r[1]]; t.usdMatriz += r[6]; t.cantidad += r[8] ?? 0 }
    return data.franquicias
      .map((f, i) => ({ codigo: f.codigo, nombre: f.nombre || f.codigo, mapa: mapa[i], alertas: alertas?.get(f.codigo) ?? (alertas ? { tickets: 0, cubiertos: 0, usd: 0 } : null) }))
      .sort((a, b) => b.mapa.usd - a.mapa.usd)
  }, [data, alertas])

  const ok = (f: FilaCuadre) => !!f.alertas && f.mapa.tickets === f.alertas.tickets && f.mapa.cubiertos === f.alertas.cubiertos
    && Math.abs(f.mapa.usd - f.alertas.usd) < TOL_USD && Math.abs(f.mapa.usdMatriz - f.mapa.usd) < TOL_USD
  const tot = filas.reduce((t, f) => ({
    tickets: t.tickets + f.mapa.tickets, cubiertos: t.cubiertos + f.mapa.cubiertos, usd: t.usd + f.mapa.usd, usdMatriz: t.usdMatriz + f.mapa.usdMatriz, cantidad: t.cantidad + f.mapa.cantidad,
    aTickets: t.aTickets + (f.alertas?.tickets ?? 0), aCub: t.aCub + (f.alertas?.cubiertos ?? 0), aUsd: t.aUsd + (f.alertas?.usd ?? 0),
  }), { tickets: 0, cubiertos: 0, usd: 0, usdMatriz: 0, cantidad: 0, aTickets: 0, aCub: 0, aUsd: 0 })
  const todoOk = alertas !== null && filas.every(ok)

  const par = (a: number, b: number | undefined, d = 0, tol = 0.5) => (
    <>
      <td className="px-2 py-1.5 text-right tabular-nums">{nf(d).format(a)}</td>
      <td className={`px-2 py-1.5 text-right tabular-nums ${b !== undefined && Math.abs(a - b) >= tol ? 'text-red-600 font-semibold' : 'text-muted-foreground'}`}>
        {b === undefined ? '…' : nf(d).format(b)}
      </td>
    </>
  )

  return (
    <div className="space-y-3">
      <div className={`flex items-center gap-2 text-sm font-medium ${todoOk ? 'text-green-700 dark:text-green-500' : 'text-amber-700 dark:text-amber-400'}`}>
        {todoOk ? <CheckCircle2 className="h-4 w-4" /> : <AlertTriangle className="h-4 w-4" />}
        {error ? `No se pudo cruzar con Alertas: ${error}`
          : alertas === null ? 'Cruzando con Alertas…'
          : todoOk ? 'Cuadra: tickets, cubiertos y venta USD son iguales a Alertas, y la matriz suma la venta de los tickets.'
          : 'Hay diferencias con Alertas (en rojo).'}
      </div>
      <div className="overflow-x-auto rounded-lg border dark:border-gray-700">
        <table className="w-full text-[13px]">
          <thead className="bg-muted/50">
            <tr>
              <th rowSpan={2} className="text-left px-3 py-2 font-semibold">Franquicia</th>
              <th colSpan={2} className="px-2 pt-2 font-semibold text-center">Tickets</th>
              <th colSpan={2} className="px-2 pt-2 font-semibold text-center">Cubiertos</th>
              <th colSpan={2} className="px-2 pt-2 font-semibold text-center">Venta USD</th>
              <th rowSpan={2} className="px-2 py-2 font-semibold text-right">Suma matriz USD</th>
              <th rowSpan={2} className="px-2 py-2 font-semibold text-right" title="SUM(Cantidad) de las líneas no anuladas de los tickets">Cantidad ítems POS</th>
              <th rowSpan={2} className="px-2 py-2" />
            </tr>
            <tr className="text-xs text-muted-foreground">
              {['Mapa', 'Alertas', 'Mapa', 'Alertas', 'Mapa', 'Alertas'].map((t, i) => <th key={i} className="px-2 pb-2 font-medium text-right">{t}</th>)}
            </tr>
          </thead>
          <tbody>
            {filas.map(f => (
              <tr key={f.codigo} className="border-t dark:border-gray-700">
                <td className="px-3 py-1.5 whitespace-nowrap">{f.nombre}</td>
                {par(f.mapa.tickets, f.alertas?.tickets)}
                {par(f.mapa.cubiertos, f.alertas?.cubiertos)}
                {par(f.mapa.usd, f.alertas?.usd, 0, TOL_USD)}
                <td className={`px-2 py-1.5 text-right tabular-nums ${Math.abs(f.mapa.usdMatriz - f.mapa.usd) >= TOL_USD ? 'text-red-600 font-semibold' : ''}`}>{nf(0).format(f.mapa.usdMatriz)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{nf(0).format(f.mapa.cantidad)}</td>
                <td className="px-2 py-1.5">{alertas && (ok(f) ? <CheckCircle2 className="h-4 w-4 text-green-600" aria-label="Cuadra" /> : <AlertTriangle className="h-4 w-4 text-red-600" aria-label="No cuadra" />)}</td>
              </tr>
            ))}
            <tr className="border-t-2 dark:border-gray-600 font-semibold">
              <td className="px-3 py-1.5">Total</td>
              {par(tot.tickets, alertas ? tot.aTickets : undefined)}
              {par(tot.cubiertos, alertas ? tot.aCub : undefined)}
              {par(tot.usd, alertas ? tot.aUsd : undefined, 0, TOL_USD * Math.max(1, filas.length))}
              <td className="px-2 py-1.5 text-right tabular-nums">{nf(0).format(tot.usdMatriz)}</td>
              <td className="px-2 py-1.5 text-right tabular-nums">{nf(0).format(tot.cantidad)}</td>
              <td />
            </tr>
          </tbody>
        </table>
      </div>
      <p className="text-xs text-muted-foreground">
        “Alertas” se lee en vivo del mismo servicio que usa esa pantalla. La suma de la matriz incluye todos los rubros (también Sin clasificar,
        No producto y Ajuste a tickets), por eso cierra con la venta. La cantidad de ítems es la suma de cantidades de las líneas del POS y es
        el total de la matriz medida en “Cantidad”.
      </p>
    </div>
  )
}
