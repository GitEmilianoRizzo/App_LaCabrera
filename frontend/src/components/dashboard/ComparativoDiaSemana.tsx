import { useMemo, useState } from 'react'
import { CalendarRange, Plus, Trash2, Info } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useVentasResumen } from '@/hooks/useDashboard'
import {
  DIAS_SEMANA,
  armarComparativo,
  armarPeriodos,
  etiquetaRango,
  formatFecha,
  variacion,
  type Agregacion,
  type DiaVenta,
  type FilaComparativo,
  type MetricaComparativo,
  type Periodo,
  type TipoPeriodo,
} from '@/lib/comparativoDiaSemana'

interface Props {
  franquiciaId: number
  moneda: string
}

type Base = 'anterior' | 'primero'

const ayer = () => {
  const d = new Date()
  d.setDate(d.getDate() - 1)
  return formatFecha(d)
}

const btn = (activo: boolean) =>
  `px-2.5 py-1 text-xs transition-colors ${
    activo ? 'bg-cabrera-burgundy text-white' : 'bg-white dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700'
  }`

// Fondo según la variación: verde si sube, rojo si baja, más intenso cuanto mayor la diferencia
function claseVariacion(v: number | null): string {
  if (v == null) return ''
  const a = Math.abs(v)
  if (a < 5) return ''
  if (v > 0) return a >= 25 ? 'bg-green-200 dark:bg-green-900/60' : a >= 15 ? 'bg-green-100 dark:bg-green-900/40' : 'bg-green-50 dark:bg-green-900/20'
  return a >= 25 ? 'bg-red-200 dark:bg-red-900/60' : a >= 15 ? 'bg-red-100 dark:bg-red-900/40' : 'bg-red-50 dark:bg-red-900/20'
}

export function ComparativoDiaSemana({ franquiciaId, moneda }: Props) {
  const [tipo, setTipo] = useState<TipoPeriodo>('semana')
  const [cantidad, setCantidad] = useState(4)
  const [corte, setCorte] = useState(ayer)
  const [personalizados, setPersonalizados] = useState<{ desde: string; hasta: string }[]>(() => {
    const [a, b] = armarPeriodos('mes', 2, ayer())
    return [{ desde: a.desde, hasta: a.hasta }, { desde: b.desde, hasta: b.hasta }]
  })
  const [metrica, setMetrica] = useState<MetricaComparativo | 'todas'>('usd')
  const [agregacion, setAgregacion] = useState<Agregacion>('promedio')
  const [base, setBase] = useState<Base>('anterior')

  const periodos: Periodo[] = useMemo(() => {
    if (tipo !== 'personalizado') return armarPeriodos(tipo, cantidad, corte)
    return personalizados
      .filter(p => p.desde && p.hasta && p.desde <= p.hasta)
      .map(p => ({ ...p, etiqueta: etiquetaRango(p.desde, p.hasta) }))
  }, [tipo, cantidad, corte, personalizados])

  // En personalizado no hay fecha de corte: cuenta todo el rango elegido (hasta ayer)
  const corteEfectivo = tipo === 'personalizado' ? ayer() : corte

  const rango = useMemo(() => {
    if (periodos.length === 0) return null
    const desde = periodos.reduce((m, p) => (p.desde < m ? p.desde : m), periodos[0].desde)
    const hastaMax = periodos.reduce((m, p) => (p.hasta > m ? p.hasta : m), periodos[0].hasta)
    return { desde, hasta: hastaMax < corteEfectivo ? hastaMax : corteEfectivo }
  }, [periodos, corteEfectivo])

  const { data, loading, error } = useVentasResumen(
    rango ? { fechaDesde: rango.desde, fechaHasta: rango.hasta, franquiciaId } : { fechaDesde: corteEfectivo, fechaHasta: corteEfectivo, franquiciaId }
  )

  const dias: DiaVenta[] = useMemo(
    () =>
      (data ?? [])
        .filter(v => v.franquicia_id === franquiciaId && v.fecha_negocio)
        .map(v => ({
          fecha: v.fecha_negocio.split('T')[0],
          tickets: v.cantidad_tickets || 0,
          cubiertos: v.total_cubiertos || 0,
          local: v.venta_neta || 0,
          usd: v.venta_neta_usd ?? null,
        })),
    [data, franquiciaId]
  )

  const metricas: MetricaComparativo[] = metrica === 'todas' ? ['tickets', 'local', 'usd', 'cubiertos'] : [metrica]
  const titulo: Record<MetricaComparativo, string> = {
    tickets: 'Tickets',
    local: `Venta neta (${moneda})`,
    usd: 'Venta neta (USD)',
    cubiertos: 'Cubiertos',
  }

  const actualizarPersonalizado = (i: number, campo: 'desde' | 'hasta', valor: string) =>
    setPersonalizados(ps => ps.map((p, j) => (j === i ? { ...p, [campo]: valor } : p)))

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold flex items-center gap-2">
          <CalendarRange className="h-4 w-4" />
          Comparativo por día de la semana
        </CardTitle>
        <p className="text-xs text-muted-foreground">
          Cada fila es un período y cada columna un día de la semana: lunes contra lunes, sábado contra sábado.
        </p>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Controles */}
        <div className="flex flex-wrap items-center gap-x-6 gap-y-3 text-sm">
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">Períodos:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden divide-x dark:divide-gray-600">
              <button className={btn(tipo === 'semana')} onClick={() => setTipo('semana')}>Semanas</button>
              <button className={btn(tipo === 'mes')} onClick={() => setTipo('mes')}>Meses</button>
              <button className={btn(tipo === 'personalizado')} onClick={() => setTipo('personalizado')}>Personalizado</button>
            </div>
          </div>

          {tipo !== 'personalizado' && (
            <>
              <label className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Cantidad:</span>
                <select
                  value={cantidad}
                  onChange={e => setCantidad(Number(e.target.value))}
                  className="px-2 py-1 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
                >
                  {Array.from({ length: 12 }, (_, i) => i + 2).map(n => (
                    <option key={n} value={n}>{n}</option>
                  ))}
                </select>
              </label>
              <label className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Hasta:</span>
                <input
                  type="date"
                  value={corte}
                  max={ayer()}
                  onChange={e => e.target.value && setCorte(e.target.value)}
                  className="px-2 py-1 text-sm border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
                />
              </label>
            </>
          )}

          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">Métrica:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden divide-x dark:divide-gray-600">
              <button className={btn(metrica === 'tickets')} onClick={() => setMetrica('tickets')}>Tickets</button>
              <button className={btn(metrica === 'local')} onClick={() => setMetrica('local')}>{moneda}</button>
              <button className={btn(metrica === 'usd')} onClick={() => setMetrica('usd')}>USD</button>
              <button className={btn(metrica === 'cubiertos')} onClick={() => setMetrica('cubiertos')}>Cubiertos</button>
              <button className={btn(metrica === 'todas')} onClick={() => setMetrica('todas')}>Todas</button>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">Valor:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden divide-x dark:divide-gray-600">
              <button className={btn(agregacion === 'promedio')} onClick={() => setAgregacion('promedio')}>Promedio por día</button>
              <button className={btn(agregacion === 'total')} onClick={() => setAgregacion('total')}>Total</button>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">Variación vs:</span>
            <div className="flex rounded-lg border dark:border-gray-600 overflow-hidden divide-x dark:divide-gray-600">
              <button className={btn(base === 'anterior')} onClick={() => setBase('anterior')}>Período anterior</button>
              <button className={btn(base === 'primero')} onClick={() => setBase('primero')}>Primer período</button>
            </div>
          </div>
        </div>

        {tipo === 'personalizado' && (
          <div className="space-y-2">
            {personalizados.map((p, i) => (
              <div key={i} className="flex items-center gap-2 text-sm">
                <span className="w-16 text-xs text-muted-foreground">Período {i + 1}</span>
                <input
                  type="date"
                  value={p.desde}
                  onChange={e => actualizarPersonalizado(i, 'desde', e.target.value)}
                  className="px-2 py-1 border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
                />
                <span className="text-muted-foreground">a</span>
                <input
                  type="date"
                  value={p.hasta}
                  onChange={e => actualizarPersonalizado(i, 'hasta', e.target.value)}
                  className="px-2 py-1 border rounded-md bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white"
                />
                {p.desde && p.hasta && p.desde > p.hasta && <span className="text-xs text-red-600">"Desde" es posterior a "hasta"</span>}
                {personalizados.length > 1 && (
                  <button
                    onClick={() => setPersonalizados(ps => ps.filter((_, j) => j !== i))}
                    className="p-1 text-gray-500 hover:text-red-600"
                    title="Quitar período"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                )}
              </div>
            ))}
            {personalizados.length < 12 && (
              <button
                onClick={() => setPersonalizados(ps => [...ps, { desde: '', hasta: '' }])}
                className="flex items-center gap-1 text-xs text-cabrera-burgundy hover:underline"
              >
                <Plus className="h-3 w-3" /> Agregar período
              </button>
            )}
          </div>
        )}

        {/* Tablas */}
        {error ? (
          <div className="text-sm text-red-600">No se pudieron cargar las ventas: {error}</div>
        ) : loading && !data ? (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-6 w-6 border-2 border-cabrera-burgundy border-t-transparent" />
          </div>
        ) : periodos.length === 0 ? (
          <div className="text-sm text-muted-foreground">Elegí al menos un período válido.</div>
        ) : (
          metricas.map(m => (
            <TablaComparativo
              key={m}
              titulo={metricas.length > 1 ? titulo[m] : null}
              filas={armarComparativo(periodos, dias, m, agregacion, corteEfectivo)}
              metrica={m}
              agregacion={agregacion}
              base={base}
            />
          ))
        )}

        <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
          <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-green-100 dark:bg-green-900/40" /> sube</span>
          <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-red-100 dark:bg-red-900/40" /> baja</span>
          <span>Color más intenso a partir de ±15% y ±25%; menos de ±5% sin color.</span>
          <span className="flex items-center gap-1">
            <Info className="h-3 w-3" />
            "3/4 días": el período tiene 4 de ese día y solo 3 con datos. El promedio usa los días con datos; en USD no cuentan los días sin cotización.
          </span>
        </div>
      </CardContent>
    </Card>
  )
}

function TablaComparativo({
  titulo,
  filas,
  metrica,
  agregacion,
  base,
}: {
  titulo: string | null
  filas: FilaComparativo[]
  metrica: MetricaComparativo
  agregacion: Agregacion
  base: Base
}) {
  const esMonto = metrica === 'local' || metrica === 'usd'
  const fmt = (v: number | null) => {
    if (v == null) return '—'
    const decimales = !esMonto && agregacion === 'promedio' && v < 100 ? 1 : 0
    return v.toLocaleString('es-AR', { minimumFractionDigits: decimales, maximumFractionDigits: decimales })
  }

  const filaBase = (i: number): FilaComparativo | null => (i === 0 ? null : base === 'anterior' ? filas[i - 1] : filas[0])

  const renderCelda = (c: FilaComparativo['total'], cBase: FilaComparativo['total'] | null, key: string, extra = '') => {
    const v = cBase ? variacion(c.valor, cBase.valor) : null
    const faltan = c.diasPeriodo > 0 && c.diasConDato < c.diasPeriodo
    return (
      <td key={key} className={`px-2 py-1.5 text-right align-top ${claseVariacion(v)} ${extra}`}>
        <div className="font-medium text-gray-900 dark:text-white tabular-nums">{fmt(c.valor)}</div>
        {v != null && (
          <div className={`text-[11px] tabular-nums ${v >= 0 ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'}`}>
            {v >= 0 ? '▲' : '▼'} {Math.abs(v).toLocaleString('es-AR', { maximumFractionDigits: 1 })}%
          </div>
        )}
        {faltan && (
          <div className="text-[10px] text-amber-600 dark:text-amber-400" title="Días con datos / días del período">
            {c.diasConDato}/{c.diasPeriodo} días
          </div>
        )}
      </td>
    )
  }

  return (
    <div className="overflow-x-auto">
      {titulo && <div className="text-sm font-semibold mb-1 text-cabrera-burgundy">{titulo}</div>}
      <table className="w-full text-xs border-collapse">
        <thead className="bg-gray-100 dark:bg-gray-800">
          <tr>
            <th className="px-2 py-2 text-left font-semibold min-w-[130px]">Período</th>
            {DIAS_SEMANA.map(d => (
              <th key={d} className="px-2 py-2 text-right font-semibold min-w-[80px]">{d}</th>
            ))}
            <th className="px-2 py-2 text-right font-semibold min-w-[90px] bg-gray-200 dark:bg-gray-700">
              {agregacion === 'promedio' ? 'Prom. diario' : 'Total'}
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
          {filas.map((f, i) => {
            const fb = filaBase(i)
            return (
              <tr key={f.periodo.desde + f.periodo.hasta}>
                <td className="px-2 py-1.5 font-medium text-gray-900 dark:text-white whitespace-nowrap align-top">
                  {f.periodo.etiqueta}
                  {f.parcial && <div className="text-[10px] font-normal text-amber-600 dark:text-amber-400">en curso</div>}
                </td>
                {f.dias.map((c, j) => renderCelda(c, fb ? fb.dias[j] : null, String(j)))}
                {renderCelda(f.total, fb ? fb.total : null, 'total', 'border-l-2 border-gray-300 dark:border-gray-600')}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
