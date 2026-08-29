import { useState, useMemo, useEffect } from 'react'
import {
  RefreshCw,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Clock,
  DollarSign,
  TrendingUp,
  Activity,
  Plus,
  Pencil,
  Trash2,
  Save,
  X
} from 'lucide-react'
import { formatNumber } from '@/lib/utils'

// Types
interface ExchangeRate {
  codigo_moneda: string
  fecha: string
  fecha_cotizacion_origen: string
  unidades_por_usd: number
  tipo_tasa: string
  proveedor: string
  es_arrastrada: boolean
  es_override_manual: boolean
  observacion_override?: string
}

interface Currency {
  codigo_iso: string
  nombre: string
  simbolo?: string
  es_moneda_base: boolean
  activo: boolean
  proveedor_preferido_id?: number
  proveedor_preferido_codigo?: string
}

interface Provider {
  proveedor_id: number
  codigo: string
  nombre: string
  prioridad: number
  monedas_soportadas: string[]
  activo: boolean
}

interface IngestLog {
  fecha_ejecucion: string
  proveedor: string
  fecha_desde: string
  fecha_hasta: string
  estado: string
  monedas_obtenidas: number
  dias_obtenidos: number
  dias_arrastrados: number
  mensaje_error?: string
  monedas_faltantes?: string
  duracion_ms?: number
}

interface ProviderHealth {
  [provider: string]: boolean
}

interface RefreshResult {
  estado: string
  monedas_obtenidas: number
  dias_obtenidos: number
  dias_arrastrados: number
  monedas_faltantes?: string
  mensaje_error?: string
  duracion_ms?: number
}

const API_BASE = '/api/v1'

export function TiposCambio() {
  const [rates, setRates] = useState<ExchangeRate[]>([])
  const [currencies, setCurrencies] = useState<Currency[]>([])
  const [providers, setProviders] = useState<Provider[]>([])
  const [logs, setLogs] = useState<IngestLog[]>([])
  const [providerHealth, setProviderHealth] = useState<ProviderHealth>({})
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastRefreshResult, setLastRefreshResult] = useState<RefreshResult | null>(null)

  // Modal states
  const [showAddCurrency, setShowAddCurrency] = useState(false)
  const [editingCurrency, setEditingCurrency] = useState<Currency | null>(null)
  const [currencyForm, setCurrencyForm] = useState({
    codigo_iso: '',
    nombre: '',
    simbolo: '',
    proveedor_preferido_id: null as number | null
  })
  const [saving, setSaving] = useState(false)

  // Cargar datos iniciales
  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [ratesRes, currenciesRes, providersRes, logsRes, healthRes] = await Promise.all([
        fetch(`${API_BASE}/fx/rates?from=${getDateDaysAgo(30)}&to=${getToday()}`),
        fetch(`${API_BASE}/fx/currencies`),
        fetch(`${API_BASE}/fx/providers`),
        fetch(`${API_BASE}/fx/logs?count=20`),
        fetch(`${API_BASE}/fx/health`)
      ])

      if (!ratesRes.ok || !logsRes.ok || !healthRes.ok) {
        throw new Error('Error al cargar datos')
      }

      setRates(await ratesRes.json())
      setCurrencies(currenciesRes.ok ? await currenciesRes.json() : [])
      setProviders(providersRes.ok ? await providersRes.json() : [])
      setLogs(await logsRes.json())
      setProviderHealth(await healthRes.json())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error desconocido')
    } finally {
      setLoading(false)
    }
  }

  // Cargar al montar
  useEffect(() => {
    loadData()
  }, [])

  // Refrescar tasas desde proveedores
  const handleRefresh = async () => {
    setRefreshing(true)
    setLastRefreshResult(null)
    try {
      const res = await fetch(`${API_BASE}/fx/rates/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fecha_desde: getDateDaysAgo(7),
          fecha_hasta: getToday()
        })
      })

      const result = await res.json()
      setLastRefreshResult(result)

      // Recargar datos
      await loadData()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al refrescar')
    } finally {
      setRefreshing(false)
    }
  }

  // CRUD Monedas
  const handleAddCurrency = () => {
    setCurrencyForm({ codigo_iso: '', nombre: '', simbolo: '', proveedor_preferido_id: null })
    setShowAddCurrency(true)
    setEditingCurrency(null)
  }

  const handleEditCurrency = (currency: Currency) => {
    setCurrencyForm({
      codigo_iso: currency.codigo_iso,
      nombre: currency.nombre,
      simbolo: currency.simbolo || '',
      proveedor_preferido_id: currency.proveedor_preferido_id || null
    })
    setEditingCurrency(currency)
    setShowAddCurrency(true)
  }

  const handleSaveCurrency = async () => {
    setSaving(true)
    try {
      if (editingCurrency) {
        // Update
        const res = await fetch(`${API_BASE}/fx/currencies/${editingCurrency.codigo_iso}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            nombre: currencyForm.nombre,
            simbolo: currencyForm.simbolo || null,
            proveedor_preferido_id: currencyForm.proveedor_preferido_id
          })
        })
        if (!res.ok) {
          const err = await res.json()
          throw new Error(err.message || 'Error al actualizar')
        }
      } else {
        // Create
        const res = await fetch(`${API_BASE}/fx/currencies`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            codigo_iso: currencyForm.codigo_iso.toUpperCase(),
            nombre: currencyForm.nombre,
            simbolo: currencyForm.simbolo || null,
            activo: true,
            proveedor_preferido_id: currencyForm.proveedor_preferido_id
          })
        })
        if (!res.ok) {
          const err = await res.json()
          throw new Error(err.message || 'Error al crear')
        }
      }

      setShowAddCurrency(false)
      await loadData()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al guardar')
    } finally {
      setSaving(false)
    }
  }

  const handleDeleteCurrency = async (code: string) => {
    if (!confirm(`¿Eliminar moneda ${code} y todas sus tasas de cambio?`)) return

    try {
      const res = await fetch(`${API_BASE}/fx/currencies/${code}`, { method: 'DELETE' })
      if (!res.ok) {
        const err = await res.json()
        throw new Error(err.message || 'Error al eliminar')
      }
      await loadData()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al eliminar')
    }
  }

  // Obtener última tasa por moneda
  const latestRateByCode = useMemo(() => {
    const byMoneda: Record<string, ExchangeRate> = {}
    rates.forEach(r => {
      if (!byMoneda[r.codigo_moneda] || r.fecha > byMoneda[r.codigo_moneda].fecha) {
        byMoneda[r.codigo_moneda] = r
      }
    })
    return byMoneda
  }, [rates])

  // Estadísticas
  const stats = useMemo(() => {
    const monedas = new Set(rates.map(r => r.codigo_moneda))
    const dias = new Set(rates.map(r => r.fecha))
    const arrastradas = rates.filter(r => r.es_arrastrada).length
    const manuales = rates.filter(r => r.es_override_manual).length
    return {
      totalMonedas: monedas.size,
      totalDias: dias.size,
      totalArrastradas: arrastradas,
      totalManuales: manuales
    }
  }, [rates])

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <RefreshCw className="w-8 h-8 animate-spin text-cabrera-gold" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Tipos de Cambio</h1>
          <p className="text-muted-foreground">Gestión de tasas de cambio para consolidación USD</p>
        </div>
        <button
          onClick={handleRefresh}
          disabled={refreshing}
          className="flex items-center gap-2 px-4 py-2 bg-cabrera-gold text-cabrera-charcoal rounded-lg font-medium hover:bg-cabrera-gold/90 disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
          {refreshing ? 'Sincronizando...' : 'Sincronizar Ahora'}
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg text-red-700 dark:text-red-400 flex justify-between items-center">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Resultado de último refresh */}
      {lastRefreshResult && (
        <div className={`p-4 rounded-lg border ${
          lastRefreshResult.estado === 'OK'
            ? 'bg-green-50 dark:bg-green-900/20 border-green-200 dark:border-green-800'
            : 'bg-yellow-50 dark:bg-yellow-900/20 border-yellow-200 dark:border-yellow-800'
        }`}>
          <div className="flex items-center gap-2">
            {lastRefreshResult.estado === 'OK' ? (
              <CheckCircle2 className="w-5 h-5 text-green-600" />
            ) : (
              <AlertTriangle className="w-5 h-5 text-yellow-600" />
            )}
            <span className="font-medium">
              Sincronización {lastRefreshResult.estado}
            </span>
          </div>
          <p className="text-sm mt-1 text-muted-foreground">
            {lastRefreshResult.monedas_obtenidas} monedas, {lastRefreshResult.dias_obtenidos} días
            {lastRefreshResult.dias_arrastrados > 0 && ` (${lastRefreshResult.dias_arrastrados} arrastrados)`}
            {lastRefreshResult.duracion_ms && ` - ${lastRefreshResult.duracion_ms}ms`}
          </p>
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <StatCard icon={DollarSign} title="Monedas" value={stats.totalMonedas} color="blue" />
        <StatCard icon={Clock} title="Días con Tasa" value={stats.totalDias} color="green" />
        <StatCard icon={TrendingUp} title="Tasas Arrastradas" value={stats.totalArrastradas} subtitle="Fines de semana/feriados" color="amber" />
        <StatCard icon={Activity} title="Override Manual" value={stats.totalManuales} color="purple" />
      </div>

      {/* Estado de Proveedores */}
      <div className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4">
        <h2 className="text-lg font-semibold mb-4 text-gray-900 dark:text-white">
          Estado de Proveedores FX
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {Object.entries(providerHealth).map(([provider, healthy]) => {
            const providerInfo = providers.find(p => p.codigo === provider)
            return (
              <div
                key={provider}
                className={`flex items-center gap-3 p-3 rounded-lg ${
                  healthy ? 'bg-green-50 dark:bg-green-900/20' : 'bg-red-50 dark:bg-red-900/20'
                }`}
              >
                {healthy ? (
                  <CheckCircle2 className="w-5 h-5 text-green-600" />
                ) : (
                  <XCircle className="w-5 h-5 text-red-600" />
                )}
                <div className="flex-1">
                  <p className="font-medium text-gray-900 dark:text-white">{provider}</p>
                  <p className={`text-sm ${healthy ? 'text-green-600' : 'text-red-600'}`}>
                    {healthy ? 'Disponible' : 'No disponible'}
                  </p>
                  {providerInfo && (
                    <p className="text-xs text-muted-foreground">
                      Monedas: {providerInfo.monedas_soportadas.join(', ')}
                    </p>
                  )}
                </div>
                {providerInfo && (
                  <span className="text-xs bg-gray-200 dark:bg-gray-700 px-2 py-1 rounded">
                    Prioridad: {providerInfo.prioridad}
                  </span>
                )}
              </div>
            )
          })}
        </div>
      </div>

      {/* Monedas Configuradas */}
      <div className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <div className="p-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
          <div>
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
              Monedas y Tasas de Cambio
            </h2>
            <p className="text-sm text-muted-foreground">
              Configura el proveedor preferido para cada moneda
            </p>
          </div>
          <button
            onClick={handleAddCurrency}
            className="flex items-center gap-2 px-3 py-2 bg-cabrera-gold text-cabrera-charcoal rounded-lg font-medium hover:bg-cabrera-gold/90 text-sm"
          >
            <Plus className="w-4 h-4" />
            Agregar Moneda
          </button>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gray-50 dark:bg-gray-700">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Moneda</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Nombre</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Proveedor Preferido</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Última Tasa</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Fuente Actual</th>
                <th className="px-4 py-3 text-center text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
              {currencies.map(currency => {
                const lastRate = latestRateByCode[currency.codigo_iso]
                return (
                  <tr key={currency.codigo_iso} className="hover:bg-gray-50 dark:hover:bg-gray-700/50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-semibold text-gray-900 dark:text-white">
                          {currency.codigo_iso}
                        </span>
                        {currency.simbolo && (
                          <span className="text-gray-400">({currency.simbolo})</span>
                        )}
                        {currency.es_moneda_base && (
                          <span className="px-2 py-0.5 text-xs bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 rounded">
                            BASE
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                      {currency.nombre}
                    </td>
                    <td className="px-4 py-3">
                      {currency.proveedor_preferido_codigo ? (
                        <span className="px-2 py-1 text-sm bg-indigo-100 text-indigo-700 dark:bg-indigo-900/30 dark:text-indigo-400 rounded font-medium">
                          {currency.proveedor_preferido_codigo}
                        </span>
                      ) : (
                        <span className="text-sm text-gray-400 italic">Auto (por prioridad)</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {lastRate ? (
                        <div>
                          <span className="font-mono text-gray-900 dark:text-white">
                            {formatNumber(lastRate.unidades_por_usd, 4)}
                          </span>
                          <span className="text-xs text-gray-400 ml-1">
                            ({formatDate(lastRate.fecha)})
                          </span>
                        </div>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {lastRate ? (
                        <span className={`px-2 py-1 text-xs rounded font-medium ${
                          lastRate.es_override_manual
                            ? 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400'
                            : lastRate.es_arrastrada
                            ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
                            : 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                        }`}>
                          {lastRate.proveedor}
                          {lastRate.es_arrastrada && ' (arrastrada)'}
                        </span>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          onClick={() => handleEditCurrency(currency)}
                          className="p-1 text-gray-500 hover:text-cabrera-gold"
                          title="Editar"
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                        {!currency.es_moneda_base && (
                          <button
                            onClick={() => handleDeleteCurrency(currency.codigo_iso)}
                            className="p-1 text-gray-500 hover:text-red-600"
                            title="Eliminar"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })}
              {currencies.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No hay monedas configuradas
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Logs de Sincronización */}
      <div className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <div className="p-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
            Historial de Sincronización
          </h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gray-50 dark:bg-gray-700">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Fecha/Hora</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Proveedor</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Rango</th>
                <th className="px-4 py-3 text-center text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Estado</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Monedas</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Días</th>
                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Duración</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
              {logs.map((log, idx) => (
                <tr key={idx} className="hover:bg-gray-50 dark:hover:bg-gray-700/50">
                  <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                    {formatDateTime(log.fecha_ejecucion)}
                  </td>
                  <td className="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                    {log.proveedor}
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                    {formatDate(log.fecha_desde)} - {formatDate(log.fecha_hasta)}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-0.5 text-xs font-medium rounded ${
                      log.estado === 'OK'
                        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                        : log.estado === 'PARCIAL'
                        ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
                        : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    }`}>
                      {log.estado}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right text-sm text-gray-600 dark:text-gray-400">
                    {log.monedas_obtenidas}
                  </td>
                  <td className="px-4 py-3 text-right text-sm text-gray-600 dark:text-gray-400">
                    {log.dias_obtenidos}
                    {log.dias_arrastrados > 0 && (
                      <span className="text-amber-600 ml-1">+{log.dias_arrastrados}</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right text-sm text-gray-600 dark:text-gray-400">
                    {log.duracion_ms ? `${log.duracion_ms}ms` : '-'}
                  </td>
                </tr>
              ))}
              {logs.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">
                    No hay logs de sincronización
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Agregar/Editar Moneda */}
      {showAddCurrency && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl w-full max-w-md mx-4">
            <div className="p-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                {editingCurrency ? 'Editar Moneda' : 'Agregar Moneda'}
              </h3>
              <button onClick={() => setShowAddCurrency(false)} className="text-gray-500 hover:text-gray-700">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-4 space-y-4">
              {!editingCurrency && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                    Código ISO (3 letras)
                  </label>
                  <input
                    type="text"
                    maxLength={3}
                    value={currencyForm.codigo_iso}
                    onChange={e => setCurrencyForm({ ...currencyForm, codigo_iso: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                    placeholder="Ej: CLP"
                  />
                </div>
              )}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Nombre
                </label>
                <input
                  type="text"
                  value={currencyForm.nombre}
                  onChange={e => setCurrencyForm({ ...currencyForm, nombre: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  placeholder="Ej: Peso Chileno"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Símbolo (opcional)
                </label>
                <input
                  type="text"
                  maxLength={5}
                  value={currencyForm.simbolo}
                  onChange={e => setCurrencyForm({ ...currencyForm, simbolo: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  placeholder="Ej: $"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Proveedor Preferido
                </label>
                <select
                  value={currencyForm.proveedor_preferido_id || ''}
                  onChange={e => setCurrencyForm({
                    ...currencyForm,
                    proveedor_preferido_id: e.target.value ? parseInt(e.target.value) : null
                  })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                >
                  <option value="">Automático (por prioridad)</option>
                  {providers.map(p => (
                    <option key={p.proveedor_id} value={p.proveedor_id}>
                      {p.codigo} - {p.nombre} (Prioridad: {p.prioridad})
                    </option>
                  ))}
                </select>
                <p className="text-xs text-muted-foreground mt-1">
                  Si no seleccionas un proveedor, se usará el de mayor prioridad que soporte la moneda
                </p>
              </div>
            </div>
            <div className="p-4 border-t border-gray-200 dark:border-gray-700 flex justify-end gap-3">
              <button
                onClick={() => setShowAddCurrency(false)}
                className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
              >
                Cancelar
              </button>
              <button
                onClick={handleSaveCurrency}
                disabled={saving || !currencyForm.nombre || (!editingCurrency && !currencyForm.codigo_iso)}
                className="flex items-center gap-2 px-4 py-2 bg-cabrera-gold text-cabrera-charcoal rounded-lg font-medium hover:bg-cabrera-gold/90 disabled:opacity-50"
              >
                <Save className="w-4 h-4" />
                {saving ? 'Guardando...' : 'Guardar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// Helper components and functions
function StatCard({
  icon: Icon,
  title,
  value,
  subtitle,
  color
}: {
  icon: React.ElementType
  title: string
  value: number
  subtitle?: string
  color: 'blue' | 'green' | 'amber' | 'purple'
}) {
  const colors = {
    blue: 'bg-blue-100 dark:bg-blue-900/30 text-blue-600',
    green: 'bg-green-100 dark:bg-green-900/30 text-green-600',
    amber: 'bg-amber-100 dark:bg-amber-900/30 text-amber-600',
    purple: 'bg-purple-100 dark:bg-purple-900/30 text-purple-600',
  }

  return (
    <div className="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4">
      <div className="flex items-center gap-3">
        <div className={`p-2 rounded-lg ${colors[color]}`}>
          <Icon className="w-5 h-5" />
        </div>
        <div>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">{value}</p>
          <p className="text-sm text-muted-foreground">{title}</p>
          {subtitle && <p className="text-xs text-muted-foreground">{subtitle}</p>}
        </div>
      </div>
    </div>
  )
}

function getToday(): string {
  return new Date().toISOString().split('T')[0]
}

function getDateDaysAgo(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return d.toISOString().split('T')[0]
}

function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleDateString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit'
  })
}

function formatDateTime(dateStr: string): string {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}
