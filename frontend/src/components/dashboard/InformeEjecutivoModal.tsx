import { useMemo, useState } from 'react'
import { format, parseISO, startOfMonth, endOfMonth, subMonths, differenceInCalendarDays } from 'date-fns'
import { es } from 'date-fns/locale'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { FileText, Download, Loader2, AlertCircle, CheckCircle2, ArrowRight } from 'lucide-react'
import { dashboardApi } from '@/services/api'
import { cn } from '@/lib/utils'

type Modo = 'mes' | 'rango'

interface PeriodoState {
  modo: Modo
  mes: string // yyyy-MM
  desde: string // yyyy-MM-dd
  hasta: string // yyyy-MM-dd
}

interface InformeEjecutivoModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const ISO = 'yyyy-MM-dd'

function periodoDeMes(mes: Date): PeriodoState {
  return {
    modo: 'mes',
    mes: format(mes, 'yyyy-MM'),
    desde: format(startOfMonth(mes), ISO),
    hasta: format(endOfMonth(mes), ISO),
  }
}

/** Rango efectivo (desde/hasta) según el modo elegido */
function rango(p: PeriodoState): { desde: string; hasta: string } | null {
  if (p.modo === 'mes') {
    if (!p.mes) return null
    const d = parseISO(`${p.mes}-01`)
    return { desde: format(startOfMonth(d), ISO), hasta: format(endOfMonth(d), ISO) }
  }
  if (!p.desde || !p.hasta) return null
  return { desde: p.desde, hasta: p.hasta }
}

function describir(p: PeriodoState): string {
  const r = rango(p)
  if (!r) return '—'
  if (p.modo === 'mes') {
    const txt = format(parseISO(r.desde), "MMMM yyyy", { locale: es })
    return txt.charAt(0).toUpperCase() + txt.slice(1)
  }
  return `${format(parseISO(r.desde), 'dd/MM/yyyy')} al ${format(parseISO(r.hasta), 'dd/MM/yyyy')}`
}

function PeriodoPicker({
  titulo,
  ayuda,
  value,
  onChange,
  disabled,
  acento,
}: {
  titulo: string
  ayuda: string
  value: PeriodoState
  onChange: (p: PeriodoState) => void
  disabled: boolean
  acento: 'base' | 'comp'
}) {
  const r = rango(value)
  const dias = r ? differenceInCalendarDays(parseISO(r.hasta), parseISO(r.desde)) + 1 : null

  return (
    <div
      className={cn(
        'rounded-lg border p-4 space-y-3',
        acento === 'comp'
          ? 'border-lime-400 bg-lime-50/60 dark:border-lime-700 dark:bg-lime-900/10'
          : 'border-slate-200 bg-slate-50 dark:border-slate-700 dark:bg-slate-800/40'
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div>
          <div className="font-semibold text-gray-900 dark:text-gray-100">{titulo}</div>
          <div className="text-xs text-muted-foreground">{ayuda}</div>
        </div>
        <div className="inline-flex rounded-md border bg-background p-0.5 text-xs">
          {(['mes', 'rango'] as Modo[]).map((m) => (
            <button
              key={m}
              type="button"
              disabled={disabled}
              onClick={() => onChange({ ...value, ...(r ?? {}), modo: m })}
              className={cn(
                'rounded px-2 py-1 transition-colors',
                value.modo === m ? 'bg-slate-900 text-white dark:bg-slate-100 dark:text-slate-900' : 'text-muted-foreground hover:text-foreground'
              )}
            >
              {m === 'mes' ? 'Mes' : 'Rango'}
            </button>
          ))}
        </div>
      </div>

      {value.modo === 'mes' ? (
        <Input
          type="month"
          value={value.mes}
          disabled={disabled}
          onChange={(e) => onChange({ ...value, mes: e.target.value })}
        />
      ) : (
        <div className="grid grid-cols-2 gap-2">
          <label className="text-xs text-muted-foreground space-y-1">
            <span>Desde</span>
            <Input type="date" value={value.desde} max={value.hasta || undefined} disabled={disabled}
              onChange={(e) => onChange({ ...value, desde: e.target.value })} />
          </label>
          <label className="text-xs text-muted-foreground space-y-1">
            <span>Hasta</span>
            <Input type="date" value={value.hasta} min={value.desde || undefined} disabled={disabled}
              onChange={(e) => onChange({ ...value, hasta: e.target.value })} />
          </label>
        </div>
      )}

      <div className="text-xs text-muted-foreground">
        {describir(value)}
        {dias !== null && dias > 0 && <span> · {dias} días</span>}
      </div>
    </div>
  )
}

export function InformeEjecutivoModal({ open, onOpenChange }: InformeEjecutivoModalProps) {
  // Por defecto: último mes cerrado vs el mes anterior
  const [periodoBase, setPeriodoBase] = useState<PeriodoState>(() => periodoDeMes(subMonths(new Date(), 2)))
  const [periodoComp, setPeriodoComp] = useState<PeriodoState>(() => periodoDeMes(subMonths(new Date(), 1)))
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const base = rango(periodoBase)
  const comp = rango(periodoComp)

  const validacion = useMemo(() => {
    if (!base || !comp) return 'Completá ambos períodos'
    if (base.desde > base.hasta) return 'En el período inicial, "desde" es posterior a "hasta"'
    if (comp.desde > comp.hasta) return 'En el período final, "desde" es posterior a "hasta"'
    if (base.desde === comp.desde && base.hasta === comp.hasta) return 'Los dos períodos son iguales'
    return null
  }, [base, comp])

  const aplicarAtajo = (atajo: 'mes-anterior' | 'anio-anterior') => {
    // Toma como período final el mes donde arranca el período final actual
    const finalMes = comp ? parseISO(comp.desde) : subMonths(new Date(), 1)
    setPeriodoComp(periodoDeMes(finalMes))
    setPeriodoBase(periodoDeMes(subMonths(finalMes, atajo === 'mes-anterior' ? 1 : 12)))
    setError(null)
  }

  const handleGenerar = async () => {
    if (!base || !comp || validacion) return
    setLoading(true)
    setError(null)
    setSuccess(false)

    try {
      const { blob, filename } = await dashboardApi.generarInformeEjecutivoPdf({
        baseDesde: base.desde,
        baseHasta: base.hasta,
        compDesde: comp.desde,
        compHasta: comp.hasta,
      })

      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      window.URL.revokeObjectURL(url)

      setSuccess(true)
      setTimeout(() => {
        onOpenChange(false)
        setSuccess(false)
      }, 1500)
    } catch (err) {
      console.error('Informe ejecutivo error:', err)
      setError(err instanceof Error ? err.message : 'Error al generar el informe')
    } finally {
      setLoading(false)
    }
  }

  const handleClose = () => {
    if (!loading) {
      setError(null)
      setSuccess(false)
      onOpenChange(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-lime-600" />
            Generar Informe Ejecutivo
          </DialogTitle>
          <DialogDescription>
            Elegí los dos períodos a comparar. El informe muestra el período final con su variación contra el período inicial.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="text-muted-foreground">Atajos:</span>
            <Button type="button" variant="outline" size="sm" className="h-7 text-xs" disabled={loading}
              onClick={() => aplicarAtajo('mes-anterior')}>
              vs mes anterior
            </Button>
            <Button type="button" variant="outline" size="sm" className="h-7 text-xs" disabled={loading}
              onClick={() => aplicarAtajo('anio-anterior')}>
              vs mismo mes del año anterior
            </Button>
          </div>

          <div className="grid gap-3 md:grid-cols-[1fr_auto_1fr] md:items-center">
            <PeriodoPicker
              titulo="Período inicial"
              ayuda="Base de comparación"
              value={periodoBase}
              onChange={(p) => { setPeriodoBase(p); setError(null) }}
              disabled={loading}
              acento="base"
            />
            <ArrowRight className="hidden md:block h-5 w-5 text-muted-foreground mx-auto" />
            <PeriodoPicker
              titulo="Período final"
              ayuda="Período a analizar"
              value={periodoComp}
              onChange={(p) => { setPeriodoComp(p); setError(null) }}
              disabled={loading}
              acento="comp"
            />
          </div>

          {!validacion && (
            <div className="rounded-lg bg-slate-900 px-4 py-3 text-sm text-slate-100 dark:bg-slate-800">
              Se generará <span className="font-semibold text-lime-400">{describir(periodoComp)}</span> vs{' '}
              <span className="font-semibold text-lime-400">{describir(periodoBase)}</span>
              <div className="mt-1 text-xs text-slate-400">
                PDF de 5 páginas: resumen y puntos clave, indicadores por local, franquicias vs Palermo, comparativo por grupo y ventas en moneda local.
              </div>
            </div>
          )}

          {validacion && (base || comp) && (
            <div className="flex items-center gap-2 text-sm text-amber-700 dark:text-amber-300">
              <AlertCircle className="h-4 w-4 flex-shrink-0" />
              {validacion}
            </div>
          )}

          {loading && (
            <div className="flex items-center gap-2 p-3 bg-slate-50 dark:bg-slate-800 rounded-lg text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Procesando el informe… puede tardar unos segundos.
            </div>
          )}

          {error && (
            <div className="flex items-center gap-2 p-3 bg-red-50 dark:bg-red-900/20 text-red-800 dark:text-red-200 rounded-lg">
              <AlertCircle className="h-5 w-5 flex-shrink-0" />
              <span className="text-sm">{error}</span>
            </div>
          )}

          {success && (
            <div className="flex items-center gap-2 p-3 bg-green-50 dark:bg-green-900/20 text-green-800 dark:text-green-200 rounded-lg">
              <CheckCircle2 className="h-5 w-5 flex-shrink-0" />
              <span className="text-sm">Informe descargado</span>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={loading}>
            Cancelar
          </Button>
          <Button
            onClick={handleGenerar}
            disabled={loading || success || !!validacion}
            className="gap-2 bg-slate-900 text-white hover:bg-slate-800 dark:bg-lime-500 dark:text-slate-900 dark:hover:bg-lime-400"
          >
            {loading ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Generando...
              </>
            ) : success ? (
              <>
                <CheckCircle2 className="h-4 w-4" />
                Descargado
              </>
            ) : (
              <>
                <Download className="h-4 w-4" />
                Generar PDF
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default InformeEjecutivoModal
