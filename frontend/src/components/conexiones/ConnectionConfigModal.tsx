import { useState, useEffect } from 'react'
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Save,
  Loader2,
  Globe,
  FileJson,
  Key,
  Clock,
  AlertTriangle,
  CheckCircle2,
  Eye,
  EyeOff,
  RefreshCw,
  Info,
} from 'lucide-react'
import type { NodoConfiguracion } from '@/types/conexiones'

interface ConnectionConfigModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  connectionName: string
  connectorType: string
  currentConfig?: Partial<NodoConfiguracion>
  onSave: (config: NodoConfiguracion) => Promise<void>
  onTestConnection?: () => Promise<{ success: boolean; message: string }>
}

type ConnectionType = 'API_REST' | 'CLOUD_FILE'
type AuthType = 'TOKEN' | 'OAUTH2' | 'BASIC' | 'API_KEY'
type CloudProvider = 'GOOGLE_DRIVE' | 'ONEDRIVE' | 'DROPBOX' | 'S3'
type ScheduleType = 'CRON' | 'INTERVAL'

export function ConnectionConfigModal({
  open,
  onOpenChange,
  connectionName,
  connectorType,
  currentConfig,
  onSave,
  onTestConnection,
}: ConnectionConfigModalProps) {
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null)
  const [showSecrets, setShowSecrets] = useState<Record<string, boolean>>({})
  const [activeTab, setActiveTab] = useState<'connection' | 'extraction' | 'schedule'>('connection')

  // State for form
  const [connectionType, setConnectionType] = useState<ConnectionType>(
    currentConfig?.connection_type || 'API_REST'
  )

  // API Config - support both old (connection) and new (api_config) structure
  const [apiBaseUrl, setApiBaseUrl] = useState(
    currentConfig?.api_config?.base_url || currentConfig?.connection?.base_url || ''
  )
  const [apiAuthType, setApiAuthType] = useState<AuthType>(
    currentConfig?.api_config?.auth_type || 'TOKEN'
  )
  const [apiToken, setApiToken] = useState(
    currentConfig?.api_config?.api_token || currentConfig?.connection?.api_token || ''
  )
  const [tokenHeader, setTokenHeader] = useState(
    currentConfig?.api_config?.token_header || 'Api-Token'
  )
  const [responseFormat, setResponseFormat] = useState<'JSON' | 'XML'>(
    currentConfig?.api_config?.response_format || 'JSON'
  )
  const [timeoutSeconds, setTimeoutSeconds] = useState(
    currentConfig?.api_config?.timeout_seconds || currentConfig?.connection?.timeout_seconds || 30
  )

  // Cloud File Config
  const [cloudProvider, setCloudProvider] = useState<CloudProvider>(
    currentConfig?.cloud_file_config?.provider || 'GOOGLE_DRIVE'
  )
  const [folderId, setFolderId] = useState(currentConfig?.cloud_file_config?.google_drive_config?.folder_id || '')
  const [filePattern, setFilePattern] = useState(currentConfig?.cloud_file_config?.file_name_pattern || 'LC_{franchise_code}_{business_date}_*.json')

  // Extraction Config - support both old and new structure
  const [exportEndpoint, setExportEndpoint] = useState(
    currentConfig?.extraction_config?.export_endpoint || '/api/export/'
  )
  const [exportFilter, setExportFilter] = useState(
    currentConfig?.extraction_config?.export_filter || currentConfig?.export_filter || 'Invoices'
  )
  const [includeProcessed, setIncludeProcessed] = useState<boolean>(
    currentConfig?.extraction_config?.include_processed ?? currentConfig?.include_processed ?? false
  )
  const [markProcessed, setMarkProcessed] = useState<boolean>(
    currentConfig?.extraction_config?.mark_processed_after_accept ?? currentConfig?.mark_processed_after_accept ?? true
  )
  const [processedEndpoint, setProcessedEndpoint] = useState(
    currentConfig?.extraction_config?.processed_endpoint || '/api/doc/processed'
  )

  // Sync Config
  const [scheduleType, setScheduleType] = useState<ScheduleType>(currentConfig?.sync_config?.schedule_type || 'CRON')
  const [cronExpression, setCronExpression] = useState(currentConfig?.sync_config?.cron_expression || '0 3 * * *')
  const [intervalMinutes, setIntervalMinutes] = useState(currentConfig?.sync_config?.interval_minutes || 60)
  const [businessDayStartHour, setBusinessDayStartHour] = useState(currentConfig?.sync_config?.business_day_start_hour || 6)
  const [autoRetry, setAutoRetry] = useState<boolean>(currentConfig?.sync_config?.auto_retry_on_error ?? true)

  // Sync state when currentConfig changes (modal opens with new data)
  useEffect(() => {
    if (open && currentConfig) {
      setApiBaseUrl(currentConfig.api_config?.base_url || currentConfig.connection?.base_url || '')
      setApiToken(currentConfig.api_config?.api_token || currentConfig.connection?.api_token || '')
      setTimeoutSeconds(currentConfig.api_config?.timeout_seconds || currentConfig.connection?.timeout_seconds || 30)
      setExportFilter(currentConfig.extraction_config?.export_filter || currentConfig.export_filter || 'Invoices')
      setIncludeProcessed(currentConfig.extraction_config?.include_processed ?? currentConfig.include_processed ?? false)
      setMarkProcessed(currentConfig.extraction_config?.mark_processed_after_accept ?? currentConfig.mark_processed_after_accept ?? true)
    }
  }, [open, currentConfig])

  // Reset test result when config changes
  useEffect(() => {
    setTestResult(null)
  }, [connectionType, apiBaseUrl, apiToken, cloudProvider, folderId])

  const toggleSecret = (key: string) => {
    setShowSecrets(prev => ({ ...prev, [key]: !prev[key] }))
  }

  const handleTest = async () => {
    if (!onTestConnection) return
    setTesting(true)
    setTestResult(null)
    try {
      const result = await onTestConnection()
      setTestResult(result)
    } catch (error) {
      setTestResult({ success: false, message: error instanceof Error ? error.message : 'Error desconocido' })
    } finally {
      setTesting(false)
    }
  }

  const handleSave = async () => {
    setSaving(true)
    try {
      const config: NodoConfiguracion = {
        connection_type: connectionType,
        api_config: connectionType === 'API_REST' ? {
          base_url: apiBaseUrl,
          auth_type: apiAuthType,
          api_token: apiToken,
          token_header: tokenHeader,
          response_format: responseFormat,
          timeout_seconds: timeoutSeconds,
          retry: { max_attempts: 3, backoff_seconds: [5, 15, 30] },
        } : undefined,
        cloud_file_config: connectionType === 'CLOUD_FILE' ? {
          provider: cloudProvider,
          google_drive_config: cloudProvider === 'GOOGLE_DRIVE' ? { folder_id: folderId } : undefined,
          onedrive_config: cloudProvider === 'ONEDRIVE' ? { folder_path: folderId, client_id: '', client_secret: '', tenant_id: '' } : undefined,
          file_name_pattern: filePattern,
          after_processing: 'MOVE',
        } : undefined,
        extraction_config: {
          export_endpoint: exportEndpoint,
          export_filter: exportFilter,
          include_processed: includeProcessed,
          mark_processed_after_accept: markProcessed,
          processed_endpoint: processedEndpoint,
        },
        sync_config: {
          schedule_type: scheduleType,
          cron_expression: scheduleType === 'CRON' ? cronExpression : undefined,
          interval_minutes: scheduleType === 'INTERVAL' ? intervalMinutes : undefined,
          business_day_start_hour: businessDayStartHour,
          business_day_end_hour: businessDayStartHour, // Same hour next day
          auto_retry_on_error: autoRetry,
          max_retries_per_day: 3,
          alert_on_failure: true,
        },
      }
      await onSave(config)
      onOpenChange(false)
    } finally {
      setSaving(false)
    }
  }

  const tabs = [
    { id: 'connection', label: 'Conexion', icon: connectionType === 'API_REST' ? Globe : FileJson },
    { id: 'extraction', label: 'Extraccion', icon: FileJson },
    { id: 'schedule', label: 'Programacion', icon: Clock },
  ]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Key className="w-5 h-5" />
            Configuracion: {connectionName}
          </DialogTitle>
          <DialogDescription>
            Conector: <Badge variant="outline">{connectorType}</Badge>
          </DialogDescription>
        </DialogHeader>

        {/* Tabs */}
        <div className="flex gap-1 border-b">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as typeof activeTab)}
              className={`flex items-center gap-2 px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
                activeTab === tab.id
                  ? 'border-cabrera-burgundy text-cabrera-burgundy'
                  : 'border-transparent text-muted-foreground hover:text-foreground'
              }`}
            >
              <tab.icon className="w-4 h-4" />
              {tab.label}
            </button>
          ))}
        </div>

        <div className="py-4 space-y-6">
          {/* TAB: CONEXION */}
          {activeTab === 'connection' && (
            <>
              {/* Connection Type Selector */}
              <div className="space-y-3">
                <label className="text-sm font-medium">Tipo de Conexion</label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setConnectionType('API_REST')}
                    className={`p-4 rounded-lg border-2 text-left transition-all ${
                      connectionType === 'API_REST'
                        ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                        : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <Globe className={`w-6 h-6 ${connectionType === 'API_REST' ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                      <div>
                        <p className="font-medium">API REST</p>
                        <p className="text-xs text-muted-foreground">Consultar datos via HTTP</p>
                      </div>
                    </div>
                  </button>

                  <button
                    type="button"
                    onClick={() => setConnectionType('CLOUD_FILE')}
                    className={`p-4 rounded-lg border-2 text-left transition-all ${
                      connectionType === 'CLOUD_FILE'
                        ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                        : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <FileJson className={`w-6 h-6 ${connectionType === 'CLOUD_FILE' ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                      <div>
                        <p className="font-medium">Archivo en Nube</p>
                        <p className="text-xs text-muted-foreground">Leer JSON de Drive/OneDrive</p>
                      </div>
                    </div>
                  </button>
                </div>
              </div>

              {/* API REST Config */}
              {connectionType === 'API_REST' && (
                <div className="space-y-4 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
                  <h4 className="font-medium flex items-center gap-2">
                    <Globe className="w-4 h-4" />
                    Configuracion API REST
                  </h4>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="col-span-2 space-y-2">
                      <label className="text-sm font-medium">URL Base *</label>
                      <input
                        type="url"
                        value={apiBaseUrl}
                        onChange={(e) => setApiBaseUrl(e.target.value)}
                        placeholder="https://pos.servidor.com:8984"
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                      <p className="text-xs text-muted-foreground">
                        URL del servidor POS sin trailing slash
                      </p>
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Tipo de Autenticacion</label>
                      <Select value={apiAuthType} onValueChange={(v) => setApiAuthType(v as AuthType)}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="TOKEN">Token en Header</SelectItem>
                          <SelectItem value="API_KEY">API Key en Query</SelectItem>
                          <SelectItem value="BASIC">Basic Auth</SelectItem>
                          <SelectItem value="OAUTH2">OAuth 2.0</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Nombre del Header</label>
                      <input
                        type="text"
                        value={tokenHeader}
                        onChange={(e) => setTokenHeader(e.target.value)}
                        placeholder="Api-Token"
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="col-span-2 space-y-2">
                      <label className="text-sm font-medium">Token / API Key *</label>
                      <div className="relative">
                        <input
                          type={showSecrets['token'] ? 'text' : 'password'}
                          value={apiToken}
                          onChange={(e) => setApiToken(e.target.value)}
                          placeholder="gtSUwbHbxwg3hRXhZ01Kictq"
                          className="w-full px-3 py-2 pr-10 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                        <button
                          type="button"
                          onClick={() => toggleSecret('token')}
                          className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-gray-400 hover:text-gray-600"
                        >
                          {showSecrets['token'] ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                        </button>
                      </div>
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Formato Respuesta</label>
                      <Select value={responseFormat} onValueChange={(v) => setResponseFormat(v as 'JSON' | 'XML')}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="JSON">JSON</SelectItem>
                          <SelectItem value="XML">XML</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Timeout (segundos)</label>
                      <input
                        type="number"
                        value={timeoutSeconds}
                        onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
                        min={5}
                        max={300}
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>
                  </div>

                  {/* Test Connection Button */}
                  {onTestConnection && (
                    <div className="pt-4 border-t flex items-center gap-4">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={handleTest}
                        disabled={testing || !apiBaseUrl || !apiToken}
                      >
                        <RefreshCw className={`w-4 h-4 mr-2 ${testing ? 'animate-spin' : ''}`} />
                        {testing ? 'Testeando...' : 'Probar Conexion'}
                      </Button>

                      {testResult && (
                        <div className={`flex items-center gap-2 text-sm ${testResult.success ? 'text-green-600' : 'text-red-600'}`}>
                          {testResult.success ? (
                            <CheckCircle2 className="w-4 h-4" />
                          ) : (
                            <AlertTriangle className="w-4 h-4" />
                          )}
                          {testResult.message}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}

              {/* Cloud File Config */}
              {connectionType === 'CLOUD_FILE' && (
                <div className="space-y-4 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
                  <h4 className="font-medium flex items-center gap-2">
                    <FileJson className="w-4 h-4" />
                    Configuracion Archivo en Nube
                  </h4>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="col-span-2 space-y-2">
                      <label className="text-sm font-medium">Proveedor de Nube</label>
                      <div className="grid grid-cols-4 gap-2">
                        {[
                          { id: 'GOOGLE_DRIVE', label: 'Google Drive' },
                          { id: 'ONEDRIVE', label: 'OneDrive' },
                          { id: 'DROPBOX', label: 'Dropbox' },
                          { id: 'S3', label: 'Amazon S3' },
                        ].map(provider => (
                          <button
                            key={provider.id}
                            type="button"
                            onClick={() => setCloudProvider(provider.id as CloudProvider)}
                            className={`p-3 rounded-lg border text-sm transition-all ${
                              cloudProvider === provider.id
                                ? 'border-cabrera-burgundy bg-cabrera-burgundy/10 text-cabrera-burgundy'
                                : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                            }`}
                          >
                            {provider.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="col-span-2 space-y-2">
                      <label className="text-sm font-medium">
                        {cloudProvider === 'GOOGLE_DRIVE' ? 'ID de Carpeta' : cloudProvider === 'S3' ? 'Bucket/Prefix' : 'Ruta de Carpeta'}
                      </label>
                      <input
                        type="text"
                        value={folderId}
                        onChange={(e) => setFolderId(e.target.value)}
                        placeholder={
                          cloudProvider === 'GOOGLE_DRIVE' ? '1abc123def456...'
                          : cloudProvider === 'S3' ? 'my-bucket/ventas/marbella'
                          : '/Shared/LA_CABRERA/Ventas'
                        }
                        className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="col-span-2 space-y-2">
                      <label className="text-sm font-medium">Patron de Nombre de Archivo</label>
                      <input
                        type="text"
                        value={filePattern}
                        onChange={(e) => setFilePattern(e.target.value)}
                        placeholder="LC_{franchise_code}_{business_date}_*.json"
                        className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                      <p className="text-xs text-muted-foreground">
                        Variables: {'{franchise_code}'}, {'{business_date}'}, * para comodin
                      </p>
                    </div>
                  </div>

                  <div className="p-3 bg-blue-50 dark:bg-blue-900/20 rounded-lg border border-blue-200 dark:border-blue-800">
                    <p className="text-sm text-blue-800 dark:text-blue-300 flex items-start gap-2">
                      <Info className="w-4 h-4 mt-0.5 flex-shrink-0" />
                      <span>
                        Las credenciales de acceso a la nube se configuran de forma segura en el servidor.
                        Contacte al administrador para vincular la cuenta.
                      </span>
                    </p>
                  </div>
                </div>
              )}
            </>
          )}

          {/* TAB: EXTRACCION */}
          {activeTab === 'extraction' && (
            <div className="space-y-4">
              <div className="p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg space-y-4">
                <h4 className="font-medium">Configuracion de Extraccion</h4>

                {connectionType === 'API_REST' && (
                  <>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <label className="text-sm font-medium">Endpoint de Exportacion</label>
                        <input
                          type="text"
                          value={exportEndpoint}
                          onChange={(e) => setExportEndpoint(e.target.value)}
                          placeholder="/api/export/"
                          className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-medium">Filtro de Exportacion</label>
                        <Select value={exportFilter} onValueChange={setExportFilter}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Invoices">Facturas (Invoices)</SelectItem>
                            <SelectItem value="DeliveryNotes">Albaranes</SelectItem>
                            <SelectItem value="SalesOrders">Pedidos</SelectItem>
                            <SelectItem value="Invoices,DeliveryNotes">Facturas + Albaranes</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    </div>

                    <div className="space-y-3">
                      <label className="flex items-center gap-3 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={includeProcessed}
                          onChange={(e) => setIncludeProcessed(e.target.checked)}
                          className="w-4 h-4 rounded border-gray-300 text-cabrera-burgundy focus:ring-cabrera-burgundy"
                        />
                        <span className="text-sm">Incluir documentos ya procesados</span>
                      </label>

                      <label className="flex items-center gap-3 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={markProcessed}
                          onChange={(e) => setMarkProcessed(e.target.checked)}
                          className="w-4 h-4 rounded border-gray-300 text-cabrera-burgundy focus:ring-cabrera-burgundy"
                        />
                        <span className="text-sm">Marcar como procesados despues de aceptar</span>
                      </label>
                    </div>

                    {markProcessed && (
                      <div className="space-y-2">
                        <label className="text-sm font-medium">Endpoint para Marcar Procesados</label>
                        <input
                          type="text"
                          value={processedEndpoint}
                          onChange={(e) => setProcessedEndpoint(e.target.value)}
                          placeholder="/api/doc/processed"
                          className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>
                    )}
                  </>
                )}

                {connectionType === 'CLOUD_FILE' && (
                  <div className="p-3 bg-amber-50 dark:bg-amber-900/20 rounded-lg border border-amber-200 dark:border-amber-800">
                    <p className="text-sm text-amber-800 dark:text-amber-300">
                      En modo archivo, la extraccion se realiza leyendo los archivos JSON de la carpeta configurada.
                      El sistema buscara archivos que coincidan con el patron de nombre.
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB: PROGRAMACION */}
          {activeTab === 'schedule' && (
            <div className="space-y-4">
              <div className="p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg space-y-4">
                <h4 className="font-medium flex items-center gap-2">
                  <Clock className="w-4 h-4" />
                  Programacion de Sincronizacion
                </h4>

                <div className="space-y-2">
                  <label className="text-sm font-medium">Tipo de Programacion</label>
                  <div className="grid grid-cols-2 gap-3">
                    <button
                      type="button"
                      onClick={() => setScheduleType('CRON')}
                      className={`p-3 rounded-lg border text-left transition-all ${
                        scheduleType === 'CRON'
                          ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                          : 'border-gray-200 dark:border-gray-700'
                      }`}
                    >
                      <p className="font-medium">Expresion CRON</p>
                      <p className="text-xs text-muted-foreground">Horarios especificos</p>
                    </button>
                    <button
                      type="button"
                      onClick={() => setScheduleType('INTERVAL')}
                      className={`p-3 rounded-lg border text-left transition-all ${
                        scheduleType === 'INTERVAL'
                          ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                          : 'border-gray-200 dark:border-gray-700'
                      }`}
                    >
                      <p className="font-medium">Intervalo</p>
                      <p className="text-xs text-muted-foreground">Cada X minutos</p>
                    </button>
                  </div>
                </div>

                {scheduleType === 'CRON' ? (
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Expresion CRON</label>
                    <input
                      type="text"
                      value={cronExpression}
                      onChange={(e) => setCronExpression(e.target.value)}
                      placeholder="0 3 * * *"
                      className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                    <div className="flex flex-wrap gap-2 text-xs">
                      <button type="button" onClick={() => setCronExpression('0 3 * * *')} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        3:00 AM diario
                      </button>
                      <button type="button" onClick={() => setCronExpression('0 */6 * * *')} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        Cada 6 horas
                      </button>
                      <button type="button" onClick={() => setCronExpression('0 6,14,22 * * *')} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        6am, 2pm, 10pm
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Intervalo (minutos)</label>
                    <input
                      type="number"
                      value={intervalMinutes}
                      onChange={(e) => setIntervalMinutes(Number(e.target.value))}
                      min={15}
                      max={1440}
                      className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                    <div className="flex flex-wrap gap-2 text-xs">
                      <button type="button" onClick={() => setIntervalMinutes(30)} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        30 min
                      </button>
                      <button type="button" onClick={() => setIntervalMinutes(60)} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        1 hora
                      </button>
                      <button type="button" onClick={() => setIntervalMinutes(360)} className="px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded hover:bg-gray-200">
                        6 horas
                      </button>
                    </div>
                  </div>
                )}

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Inicio Dia de Negocio (hora)</label>
                    <input
                      type="number"
                      value={businessDayStartHour}
                      onChange={(e) => setBusinessDayStartHour(Number(e.target.value))}
                      min={0}
                      max={23}
                      className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                    <p className="text-xs text-muted-foreground">
                      Hora en que comienza el dia de negocio (ej: 6 = 6am)
                    </p>
                  </div>
                </div>

                <div className="space-y-3 pt-4 border-t">
                  <label className="flex items-center gap-3 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={autoRetry}
                      onChange={(e) => setAutoRetry(e.target.checked)}
                      className="w-4 h-4 rounded border-gray-300 text-cabrera-burgundy focus:ring-cabrera-burgundy"
                    />
                    <span className="text-sm">Reintentar automaticamente en caso de error</span>
                  </label>
                </div>
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button
            onClick={handleSave}
            disabled={saving || (connectionType === 'API_REST' && (!apiBaseUrl.trim() || !apiToken.trim()))}
            className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
          >
            {saving ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Guardando...
              </>
            ) : (
              <>
                <Save className="w-4 h-4 mr-2" />
                Guardar Configuracion
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
