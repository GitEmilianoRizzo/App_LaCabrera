import { useState, useCallback } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Upload,
  FileText,
  Loader2,
  CheckCircle2,
  XCircle,
  AlertCircle,
  X,
  Play,
  Database,
} from 'lucide-react'
import { conexionesApi } from '@/services/api'
import type { NodoConexion, ParseBatchResult, ParseFileResult, IngestBatchResult } from '@/types/conexiones'

interface TxtParserUploadModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  nodo: NodoConexion
  onComplete?: () => void
}

type ModalState = 'upload' | 'parsing' | 'preview' | 'ingesting' | 'complete'

// Supported file extensions
const ALLOWED_EXTENSIONS = ['.txt', '.html', '.htm', '.csv']

const isAllowedFile = (filename: string): boolean => {
  const lower = filename.toLowerCase()
  return ALLOWED_EXTENSIONS.some(ext => lower.endsWith(ext))
}

export function TxtParserUploadModal({ open, onOpenChange, nodo, onComplete }: TxtParserUploadModalProps) {
  const [state, setState] = useState<ModalState>('upload')
  const [files, setFiles] = useState<File[]>([])
  const [parseResult, setParseResult] = useState<ParseBatchResult | null>(null)
  const [ingestResult, setIngestResult] = useState<IngestBatchResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isDragging, setIsDragging] = useState(false)

  const handleReset = useCallback(() => {
    setState('upload')
    setFiles([])
    setParseResult(null)
    setIngestResult(null)
    setError(null)
  }, [])

  const handleClose = useCallback(() => {
    handleReset()
    onOpenChange(false)
    if (state === 'complete' && onComplete) {
      onComplete()
    }
  }, [handleReset, onOpenChange, state, onComplete])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)
    const droppedFiles = Array.from(e.dataTransfer.files).filter(
      file => isAllowedFile(file.name)
    )
    setFiles(prev => [...prev, ...droppedFiles])
  }, [])

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const selectedFiles = Array.from(e.target.files).filter(
        file => isAllowedFile(file.name)
      )
      setFiles(prev => [...prev, ...selectedFiles])
    }
  }, [])

  const removeFile = useCallback((index: number) => {
    setFiles(prev => prev.filter((_, i) => i !== index))
  }, [])

  const handleParse = useCallback(async () => {
    if (files.length === 0) return

    setState('parsing')
    setError(null)

    try {
      const result = await conexionesApi.parseTxtFiles(nodo.nodo_conexion_id, files)
      setParseResult(result)
      setState('preview')
    } catch (err) {
      console.error('Parse error:', err)
      setError(err instanceof Error ? err.message : 'Error al procesar archivos')
      setState('upload')
    }
  }, [files, nodo.nodo_conexion_id])

  const handleIngest = useCallback(async () => {
    if (!parseResult) return

    const successfulFiles = parseResult.results.filter(r => r.success)
    if (successfulFiles.length === 0) return

    setState('ingesting')
    setError(null)

    try {
      const filesToIngest = successfulFiles.map(r => ({
        filename: r.filename,
        data: r.data,
      }))

      const result = await conexionesApi.ingestParsedFiles(nodo.nodo_conexion_id, filesToIngest)
      setIngestResult(result)
      setState('complete')
    } catch (err) {
      console.error('Ingest error:', err)
      setError(err instanceof Error ? err.message : 'Error al ingestar datos')
      setState('preview')
    }
  }, [parseResult, nodo.nodo_conexion_id])

  const formatCurrency = (amount: number, currency: string) => {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: currency || 'USD',
      minimumFractionDigits: 2,
    }).format(amount)
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Subir Archivos - {nodo.nombre}
          </DialogTitle>
          <DialogDescription>
            Suba archivos (HTML, CSV, TXT) para parsear e ingestar datos de ventas
          </DialogDescription>
        </DialogHeader>

        {/* Error message */}
        {error && (
          <div className="bg-red-50 text-red-800 p-3 rounded-lg flex items-center gap-2">
            <AlertCircle className="h-5 w-5 flex-shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* State: Upload */}
        {state === 'upload' && (
          <div className="space-y-4">
            {/* Drop zone */}
            <div
              className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors ${
                isDragging
                  ? 'border-blue-500 bg-blue-50'
                  : 'border-gray-300 hover:border-gray-400'
              }`}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={handleDrop}
            >
              <Upload className="h-12 w-12 mx-auto text-gray-400 mb-4" />
              <p className="text-gray-600 mb-2">
                Arrastre archivos aqui o
              </p>
              <label className="cursor-pointer">
                <span className="text-blue-600 hover:text-blue-800 font-medium">
                  seleccione archivos
                </span>
                <input
                  type="file"
                  className="hidden"
                  accept=".txt,.html,.htm,.csv"
                  multiple
                  onChange={handleFileSelect}
                />
              </label>
              <p className="text-xs text-gray-400 mt-2">
                Formatos soportados: HTML, CSV, TXT
              </p>
            </div>

            {/* File list */}
            {files.length > 0 && (
              <div className="space-y-2">
                <h4 className="font-medium text-sm text-gray-700">
                  Archivos seleccionados ({files.length})
                </h4>
                <div className="max-h-40 overflow-y-auto space-y-1">
                  {files.map((file, index) => (
                    <div
                      key={`${file.name}-${index}`}
                      className="flex items-center justify-between p-2 bg-gray-50 rounded"
                    >
                      <div className="flex items-center gap-2">
                        <FileText className="h-4 w-4 text-gray-500" />
                        <span className="text-sm truncate max-w-[300px]">{file.name}</span>
                        <span className="text-xs text-gray-400">
                          ({(file.size / 1024).toFixed(1)} KB)
                        </span>
                      </div>
                      <button
                        onClick={() => removeFile(index)}
                        className="text-gray-400 hover:text-red-500"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* State: Parsing */}
        {state === 'parsing' && (
          <div className="py-12 text-center">
            <Loader2 className="h-12 w-12 mx-auto text-blue-500 animate-spin mb-4" />
            <p className="text-gray-600">Procesando archivos...</p>
            <p className="text-sm text-gray-400">{files.length} archivo(s)</p>
          </div>
        )}

        {/* State: Preview */}
        {state === 'preview' && parseResult && (
          <div className="space-y-4">
            {/* Summary */}
            <div className="flex gap-4 p-4 bg-gray-50 rounded-lg">
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold">{parseResult.total_files}</div>
                <div className="text-xs text-gray-500">Total</div>
              </div>
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold text-green-600">{parseResult.successful}</div>
                <div className="text-xs text-gray-500">Exitosos</div>
              </div>
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold text-red-600">{parseResult.failed}</div>
                <div className="text-xs text-gray-500">Fallidos</div>
              </div>
            </div>

            {/* Results list */}
            <div className="max-h-60 overflow-y-auto space-y-2">
              {parseResult.results.map((result, index) => (
                <ParseResultRow key={index} result={result} formatCurrency={formatCurrency} />
              ))}
            </div>

            {parseResult.successful === 0 && (
              <div className="bg-yellow-50 text-yellow-800 p-3 rounded-lg flex items-center gap-2">
                <AlertCircle className="h-5 w-5 flex-shrink-0" />
                <span>No hay archivos exitosos para ingestar</span>
              </div>
            )}
          </div>
        )}

        {/* State: Ingesting */}
        {state === 'ingesting' && (
          <div className="py-12 text-center">
            <Loader2 className="h-12 w-12 mx-auto text-green-500 animate-spin mb-4" />
            <p className="text-gray-600">Ingresando datos a la base...</p>
            <p className="text-sm text-gray-400">
              {parseResult?.successful} archivo(s)
            </p>
          </div>
        )}

        {/* State: Complete */}
        {state === 'complete' && ingestResult && (
          <div className="space-y-4">
            <div className="text-center py-4">
              {ingestResult.failed === 0 ? (
                <CheckCircle2 className="h-16 w-16 mx-auto text-green-500 mb-4" />
              ) : (
                <AlertCircle className="h-16 w-16 mx-auto text-yellow-500 mb-4" />
              )}
              <h3 className="text-lg font-semibold">
                {ingestResult.failed === 0 ? 'Ingesta completada' : 'Ingesta con errores'}
              </h3>
            </div>

            {/* Summary */}
            <div className="flex gap-4 p-4 bg-gray-50 rounded-lg">
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold">{ingestResult.total_files}</div>
                <div className="text-xs text-gray-500">Total</div>
              </div>
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold text-green-600">{ingestResult.successful}</div>
                <div className="text-xs text-gray-500">Exitosos</div>
              </div>
              <div className="flex-1 text-center">
                <div className="text-2xl font-bold text-red-600">{ingestResult.failed}</div>
                <div className="text-xs text-gray-500">Fallidos</div>
              </div>
            </div>

            {/* Results */}
            <div className="max-h-48 overflow-y-auto space-y-2">
              {ingestResult.results.map((result, index) => (
                <div
                  key={index}
                  className={`flex items-center justify-between p-3 rounded-lg ${
                    result.success ? 'bg-green-50' : 'bg-red-50'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    {result.success ? (
                      <CheckCircle2 className="h-5 w-5 text-green-500" />
                    ) : (
                      <XCircle className="h-5 w-5 text-red-500" />
                    )}
                    <span className="font-medium truncate max-w-[200px]">{result.filename}</span>
                  </div>
                  <div className="text-right">
                    {result.success ? (
                      <div className="text-sm">
                        <Badge variant="outline" className="text-green-700">
                          {result.tickets_processed} tickets
                        </Badge>
                      </div>
                    ) : (
                      <span className="text-sm text-red-600 truncate max-w-[200px]">
                        {result.error}
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        <DialogFooter>
          {state === 'upload' && (
            <>
              <Button variant="outline" onClick={handleClose}>
                Cancelar
              </Button>
              <Button
                onClick={handleParse}
                disabled={files.length === 0}
                className="gap-2"
              >
                <Play className="h-4 w-4" />
                Procesar ({files.length})
              </Button>
            </>
          )}

          {state === 'preview' && (
            <>
              <Button variant="outline" onClick={handleReset}>
                Volver
              </Button>
              <Button
                onClick={handleIngest}
                disabled={parseResult?.successful === 0}
                className="gap-2"
              >
                <Database className="h-4 w-4" />
                Confirmar Ingesta ({parseResult?.successful})
              </Button>
            </>
          )}

          {state === 'complete' && (
            <>
              <Button variant="outline" onClick={handleReset}>
                Subir mas archivos
              </Button>
              <Button onClick={handleClose}>
                Cerrar
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// Subcomponent for parse result row
function ParseResultRow({
  result,
  formatCurrency,
}: {
  result: ParseFileResult
  formatCurrency: (amount: number, currency: string) => string
}) {
  return (
    <div
      className={`p-3 rounded-lg ${
        result.success ? 'bg-green-50' : 'bg-red-50'
      }`}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {result.success ? (
            <CheckCircle2 className="h-5 w-5 text-green-500" />
          ) : (
            <XCircle className="h-5 w-5 text-red-500" />
          )}
          <span className="font-medium truncate max-w-[200px]">{result.filename}</span>
        </div>
        {result.success && result.preview && (
          <Badge variant="outline">
            {result.preview.ticket_count} tickets
          </Badge>
        )}
      </div>

      {result.success && result.preview && (
        <div className="mt-2 pl-7 grid grid-cols-3 gap-2 text-sm text-gray-600">
          <div>
            <span className="text-gray-400">Franquicia: </span>
            {result.preview.franchise_code}
          </div>
          <div>
            <span className="text-gray-400">Fecha: </span>
            {result.preview.business_date}
          </div>
          <div>
            <span className="text-gray-400">Ventas: </span>
            {formatCurrency(result.preview.net_sales_amount, result.preview.currency)}
          </div>
        </div>
      )}

      {!result.success && result.error && (
        <div className="mt-1 pl-7 text-sm text-red-600">
          {result.error}
        </div>
      )}
    </div>
  )
}

export default TxtParserUploadModal
