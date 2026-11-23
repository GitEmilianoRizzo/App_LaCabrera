import { Fragment, useEffect, useState } from 'react'
import { AlertOctagon, AlertTriangle, CheckCircle2, ChevronDown, ChevronRight, CircleDashed, RefreshCw } from 'lucide-react'
import { consumoApi } from '@/services/api'
import type { ControlFranquicia, ControlMapaConsumo, EstadoRegla } from '@/types/consumo'

const nf = (d = 0) => new Intl.NumberFormat('es-AR', { maximumFractionDigits: d, minimumFractionDigits: d })
const pct = (x: number | null) => (x == null ? '—' : `${nf(x < 100 && x > 99 ? 1 : 0).format(x)} %`)

const ESTADO: Record<EstadoRegla, { etiqueta: string; icono: typeof CheckCircle2; clase: string }> = {
  ERROR: { etiqueta: 'Error', icono: AlertOctagon, clase: 'text-red-700 dark:text-red-400 bg-red-50 dark:bg-red-950/40 border-red-200 dark:border-red-900' },
  ATENCION: { etiqueta: 'Atención', icono: AlertTriangle, clase: 'text-amber-800 dark:text-amber-300 bg-amber-50 dark:bg-amber-950/40 border-amber-200 dark:border-amber-900' },
  OK: { etiqueta: 'OK', icono: CheckCircle2, clase: 'text-green-800 dark:text-green-400 bg-green-50 dark:bg-green-950/40 border-green-200 dark:border-green-900' },
  SIN_DATOS: { etiqueta: 'Sin datos', icono: CircleDashed, clase: 'text-muted-foreground bg-muted/40 border-transparent' },
}

const CHEQUEO: Record<string, string> = {
  UBICACION: 'Ubicación', COTIZACION: 'Cotización', CLASIFICACION: 'Clasificación', NO_PRODUCTO: 'No producto', DETALLE: 'Detalle', CUBIERTOS: 'Cubiertos',
}

function Chip({ estado }: { estado: EstadoRegla }) {
  const e = ESTADO[estado]
  const Icono = e.icono
  return (
    <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap ${e.clase}`}>
      <Icono className="h-3.5 w-3.5" aria-hidden />{e.etiqueta}
    </span>
  )
}

/**
 * Regla del sistema: toda franquicia que entra tiene que acomodarse al Mapa de Consumo (ubicación, cotización,
 * productos clasificados, detalle y cubiertos). El control lo corre el backend todos los días (ControlMapaConsumoJob);
 * acá se ve en vivo, con los nombres de producto que faltan clasificar.
 */
export function ControlRegla() {
  const [data, setData] = useState<ControlMapaConsumo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(false)
  const [abiertos, setAbiertos] = useState<Set<number>>(new Set())
  const [verTodas, setVerTodas] = useState(false)

  const cargar = () => {
    setCargando(true); setError(null)
    consumoApi.getControlMapa(30)
      .then(setData)
      .catch(e => setError(e instanceof Error ? e.message : 'No se pudo correr el control'))
      .finally(() => setCargando(false))
  }
  useEffect(cargar, [])

  if (error) return <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
  if (!data) return <p className="text-sm text-muted-foreground">{cargando ? 'Corriendo el control…' : ''}</p>

  const fuera = data.franquicias.filter(f => f.estado === 'ERROR' || f.estado === 'ATENCION')
  const visibles = verTodas ? data.franquicias : data.franquicias.filter(f => f.estado !== 'SIN_DATOS')
  const toggle = (id: number) => setAbiertos(s => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        {(['ERROR', 'ATENCION', 'OK', 'SIN_DATOS'] as EstadoRegla[]).map(e => (
          <span key={e} className="inline-flex items-center gap-1.5">
            <Chip estado={e} />
            <span className="tabular-nums font-semibold">{data.resumen[e === 'SIN_DATOS' ? 'sin_datos' : (e.toLowerCase() as 'ok' | 'atencion' | 'error')]}</span>
          </span>
        ))}
        <span className="text-xs text-muted-foreground ml-auto">
          Últimos {data.dias} días con ventas de cada franquicia ·{' '}
          {data.ultimo_control_automatico ? `control automático: ${new Date(data.ultimo_control_automatico).toLocaleString('es-AR')}` : 'el control automático corre todos los días a las 11:30'}
        </span>
        <button onClick={cargar} disabled={cargando} className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded border dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50">
          <RefreshCw className={`h-3.5 w-3.5 ${cargando ? 'animate-spin' : ''}`} />Volver a controlar
        </button>
      </div>

      {fuera.length === 0 && (
        <p className="flex items-center gap-2 text-sm font-medium text-green-700 dark:text-green-500">
          <CheckCircle2 className="h-4 w-4" />Todas las franquicias con ventas cumplen la regla del mapa.
        </p>
      )}

      <div className="overflow-x-auto rounded-lg border dark:border-gray-700">
        <table className="w-full text-[13px]">
          <thead className="bg-muted/50">
            <tr>
              <th className="text-left px-3 py-2 font-semibold">Franquicia</th>
              <th className="text-left px-2 py-2 font-semibold">Estado</th>
              <th className="text-right px-2 py-2 font-semibold" title="Parte de la venta de las líneas que el clasificador reconoce">Clasificado</th>
              <th className="text-right px-2 py-2 font-semibold" title="Días con tickets pero sin líneas de producto">Días sin detalle</th>
              <th className="text-right px-2 py-2 font-semibold">Tickets con cubiertos</th>
              <th className="text-left px-2 py-2 font-semibold">Qué falta</th>
            </tr>
          </thead>
          <tbody>
            {visibles.map(f => <FilaControl key={f.franquicia_id} f={f} abierto={abiertos.has(f.franquicia_id)} onToggle={() => toggle(f.franquicia_id)} />)}
          </tbody>
        </table>
      </div>
      <div className="flex items-center gap-3 text-xs text-muted-foreground">
        <button onClick={() => setVerTodas(v => !v)} className="underline hover:text-foreground">
          {verTodas ? 'Ocultar las franquicias sin ventas' : `Ver también las ${data.resumen.sin_datos} franquicias sin ventas (se controla que tengan ubicación)`}
        </button>
      </div>
      <p className="text-xs text-muted-foreground">
        Regla: toda franquicia nueva tiene que tener ubicación en el mapa (sin ella no se puede activar su conexión), cotización de su moneda,
        al menos el 90 % de la venta clasificada en productos, detalle de productos y cubiertos. Los nombres sin clasificar se agregan
        al clasificador (reglas del POS → producto canónico).
      </p>
    </div>
  )
}

function FilaControl({ f, abierto, onToggle }: { f: ControlFranquicia; abierto: boolean; onToggle: () => void }) {
  const problemas = f.chequeos.filter(c => c.estado !== 'OK')
  const abrible = f.sin_clasificar.length > 0 || problemas.length > 0
  return (
    <Fragment>
      <tr className="border-t dark:border-gray-700 align-top">
        <td className="px-3 py-1.5">
          <button onClick={abrible ? onToggle : undefined} className={`flex items-start gap-1 text-left ${abrible ? 'hover:text-cabrera-burgundy dark:hover:text-cabrera-gold' : 'cursor-default'}`}>
            {abrible ? (abierto ? <ChevronDown className="h-3.5 w-3.5 mt-0.5 shrink-0" /> : <ChevronRight className="h-3.5 w-3.5 mt-0.5 shrink-0" />) : <span className="w-3.5 shrink-0" />}
            <span>
              <span className="font-medium">{f.nombre}</span>
              <span className="block text-xs text-muted-foreground">{f.zona ?? 'sin zona'} · {f.sistema_origen ?? 'sin POS'} · {f.moneda}{f.nodo_activo ? '' : ' · conexión no activa'}</span>
            </span>
          </button>
        </td>
        <td className="px-2 py-1.5"><Chip estado={f.estado} /></td>
        <td className={`px-2 py-1.5 text-right tabular-nums ${f.pct_clasificado != null && f.pct_clasificado < 90 ? 'text-red-700 dark:text-red-400 font-semibold' : ''}`}>{pct(f.pct_clasificado)}</td>
        <td className="px-2 py-1.5 text-right tabular-nums">{f.dias_con_ventas ? pct(f.pct_dias_sin_detalle) : '—'}</td>
        <td className="px-2 py-1.5 text-right tabular-nums">{pct(f.pct_tickets_con_cubiertos)}</td>
        <td className="px-2 py-1.5 text-xs">
          {problemas.length ? problemas.map(c => (
            <span key={c.codigo} className="block"><span className="font-semibold">{CHEQUEO[c.codigo] ?? c.codigo}:</span> {c.mensaje}</span>
          )) : <span className="text-muted-foreground">{f.dias_con_ventas ? 'Cumple la regla' : 'Ubicación OK; todavía sin ventas'}</span>}
        </td>
      </tr>
      {abierto && f.sin_clasificar.length > 0 && (
        <tr className="bg-muted/30">
          <td colSpan={6} className="px-8 py-2">
            <p className="text-xs font-semibold mb-1">
              Nombres del POS sin clasificar ({f.nombres_sin_clasificar}{f.nombres_sin_clasificar > f.sin_clasificar.length ? `, los ${f.sin_clasificar.length} de más venta` : ''}) · importes en {f.moneda} sin impuestos
            </p>
            <table className="text-xs">
              <tbody>
                {f.sin_clasificar.map(n => (
                  <tr key={`${n.nombre}|${n.categoria}|${n.familia}`}>
                    <td className="pr-4 py-0.5 font-mono">{n.nombre || '(vacío)'}</td>
                    <td className="pr-4 py-0.5 text-muted-foreground">{[n.categoria, n.familia].filter(Boolean).join(' / ') || '—'}</td>
                    <td className="pr-4 py-0.5 text-right tabular-nums">{nf(0).format(n.cantidad)} u.</td>
                    <td className="py-0.5 text-right tabular-nums">{nf(0).format(n.importe)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </td>
        </tr>
      )}
    </Fragment>
  )
}
