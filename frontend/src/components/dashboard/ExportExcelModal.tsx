import { useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import {
  FileSpreadsheet,
  Download,
  Loader2,
  Calendar,
  Globe,
  Building2,
  AlertCircle,
  CheckCircle2,
} from 'lucide-react'
import { dashboardApi } from '@/services/api'

interface ExportExcelModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  filters: {
    fechaDesde: string
    fechaHasta: string
    pais: string
    franquiciaId: number | null
  }
  franquicias: { id: number; nombre: string; pais: string }[]
}

export function ExportExcelModal({ open, onOpenChange, filters, franquicias }: ExportExcelModalProps) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  // Get filtered franchise name
  const selectedFranquicia = filters.franquiciaId
    ? franquicias.find(f => f.id === filters.franquiciaId)?.nombre
    : null

  // Calculate days in range
  const diasRango = Math.ceil(
    (new Date(filters.fechaHasta).getTime() - new Date(filters.fechaDesde).getTime()) / (1000 * 60 * 60 * 24)
  ) + 1

  // Format date for display
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr + 'T00:00:00')
    return date.toLocaleDateString('es-ES', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    })
  }

  const handleExport = async () => {
    setLoading(true)
    setError(null)
    setSuccess(false)

    try {
      // Call the export API
      const blob = await dashboardApi.exportTransaccionesExcel({
        fechaDesde: filters.fechaDesde,
        fechaHasta: filters.fechaHasta,
        pais: filters.pais || undefined,
        franquiciaId: filters.franquiciaId || undefined,
      })

      // Create download link
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url

      // Generate filename with date range
      const filename = `transacciones_${filters.fechaDesde}_${filters.fechaHasta}.xlsx`
      link.download = filename

      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      window.URL.revokeObjectURL(url)

      setSuccess(true)

      // Close modal after short delay
      setTimeout(() => {
        onOpenChange(false)
        setSuccess(false)
      }, 1500)

    } catch (err) {
      console.error('Export error:', err)
      setError(err instanceof Error ? err.message : 'Error al exportar datos')
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
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileSpreadsheet className="h-5 w-5 text-green-600" />
            Exportar a Excel
          </DialogTitle>
          <DialogDescription>
            Se exportará el detalle de transacciones con los filtros seleccionados
          </DialogDescription>
        </DialogHeader>

        {/* Summary of what will be exported */}
        <div className="space-y-4 py-4">
          {/* Period */}
          <div className="flex items-start gap-3 p-3 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
            <Calendar className="h-5 w-5 text-blue-600 dark:text-blue-400 mt-0.5" />
            <div>
              <div className="font-medium text-blue-900 dark:text-blue-100">Período</div>
              <div className="text-sm text-blue-700 dark:text-blue-300">
                {formatDate(filters.fechaDesde)} — {formatDate(filters.fechaHasta)}
              </div>
              <div className="text-xs text-blue-600 dark:text-blue-400 mt-1">
                {diasRango} días
              </div>
            </div>
          </div>

          {/* Country filter */}
          <div className="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Globe className="h-5 w-5 text-gray-600 dark:text-gray-400 mt-0.5" />
            <div>
              <div className="font-medium text-gray-900 dark:text-gray-100">País</div>
              <div className="text-sm text-gray-700 dark:text-gray-300">
                {filters.pais || 'Todos los países'}
              </div>
            </div>
          </div>

          {/* Franchise filter */}
          <div className="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Building2 className="h-5 w-5 text-gray-600 dark:text-gray-400 mt-0.5" />
            <div>
              <div className="font-medium text-gray-900 dark:text-gray-100">Franquicia</div>
              <div className="text-sm text-gray-700 dark:text-gray-300">
                {selectedFranquicia || 'Todas las franquicias'}
              </div>
            </div>
          </div>

          {/* What's included */}
          <div className="p-3 bg-amber-50 dark:bg-amber-900/20 rounded-lg">
            <div className="text-sm text-amber-800 dark:text-amber-200">
              <strong>El Excel incluirá:</strong>
              <ul className="mt-1 ml-4 list-disc text-amber-700 dark:text-amber-300">
                <li>Detalle de todas las transacciones</li>
                <li>Montos en moneda de origen</li>
                <li>Montos convertidos a USD</li>
                <li>Tipo de cambio utilizado</li>
              </ul>
            </div>
          </div>

          {/* Error message */}
          {error && (
            <div className="flex items-center gap-2 p-3 bg-red-50 dark:bg-red-900/20 text-red-800 dark:text-red-200 rounded-lg">
              <AlertCircle className="h-5 w-5 flex-shrink-0" />
              <span className="text-sm">{error}</span>
            </div>
          )}

          {/* Success message */}
          {success && (
            <div className="flex items-center gap-2 p-3 bg-green-50 dark:bg-green-900/20 text-green-800 dark:text-green-200 rounded-lg">
              <CheckCircle2 className="h-5 w-5 flex-shrink-0" />
              <span className="text-sm">Archivo descargado exitosamente</span>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={loading}>
            Cancelar
          </Button>
          <Button
            onClick={handleExport}
            disabled={loading || success}
            className="gap-2 bg-green-600 hover:bg-green-700"
          >
            {loading ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Exportando...
              </>
            ) : success ? (
              <>
                <CheckCircle2 className="h-4 w-4" />
                Descargado
              </>
            ) : (
              <>
                <Download className="h-4 w-4" />
                Descargar Excel
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default ExportExcelModal
