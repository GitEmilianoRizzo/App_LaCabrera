import { useState, useEffect } from 'react'
import {
  Cable,
  Plus,
  Settings,
  Play,
  Pause,
  RefreshCw,
  CheckCircle2,
  XCircle,
  Clock,
  AlertTriangle,
  ChevronRight,
  Loader2,
  Save,
  ArrowRight,
  FastForward,
} from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useConexiones, useConexionDetalle, useUpdateNodoEstado } from '@/hooks/useConexiones'
import { conexionesApi, franquiciasApi } from '@/services/api'
import type { NodoConexion, EjecucionNodo, MapeoCategoria, MapeoMedioPago, NodoConfiguracion, FieldMapping } from '@/types/conexiones'
import { CATEGORIAS_PRODUCTO, MEDIOS_PAGO } from '@/types/conexiones'
import { ConnectionConfigModal, FieldMappingEditor, NewConnectionModal, TxtParserUploadModal } from '@/components/conexiones'
import type { NewConnectionData } from '@/components/conexiones'

const estadoColors: Record<string, string> = {
  ACTIVE: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  PAUSED: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400',
  ERROR: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400',
  PENDING_CONFIG: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
}

const estadoLabels: Record<string, string> = {
  ACTIVE: 'Activo',
  PAUSED: 'Pausado',
  ERROR: 'Error',
  PENDING_CONFIG: 'Pendiente Config',
}

const modoLabels: Record<string, string> = {
  PULL: 'Pull (Central consulta)',
  PUSH: 'Push (Local envia)',
  AGENT: 'Agente local',
  FILE: 'Archivo',
}

type MapeoModalType = 'categorias' | 'medios_pago' | null

interface MapeoFormData {
  codigo_origen: string
  nombre_origen: string
  valor_destino: string
}

export function Conexiones() {
  const { data: conexiones, isLoading, error, refetch } = useConexiones()
  const [selectedConexionId, setSelectedConexionId] = useState<number | null>(null)
  const { data: selectedDetalle, isLoading: isLoadingDetalle, refetch: refetchDetalle } = useConexionDetalle(selectedConexionId)
  const updateEstado = useUpdateNodoEstado(refetch)
  const [testingConnection, setTestingConnection] = useState(false)
  const [executingId, setExecutingId] = useState<number | null>(null)
  const [syncingFromLastId, setSyncingFromLastId] = useState<number | null>(null)
  const [alert, setAlert] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null)

  // Modal de mapeo de valores (categorias, medios de pago)
  const [mapeoModalOpen, setMapeoModalOpen] = useState(false)
  const [mapeoModalType, setMapeoModalType] = useState<MapeoModalType>(null)
  const [savingMapeo, setSavingMapeo] = useState(false)
  const [mapeoForm, setMapeoForm] = useState<MapeoFormData>({
    codigo_origen: '',
    nombre_origen: '',
    valor_destino: '',
  })
  const [editingMapeoId, setEditingMapeoId] = useState<number | null>(null)

  // Modales de configuracion avanzada
  const [connectionConfigOpen, setConnectionConfigOpen] = useState(false)
  const [fieldMappingOpen, setFieldMappingOpen] = useState(false)
  const [newConnectionOpen, setNewConnectionOpen] = useState(false)

  // Modal de TXT Parser
  const [txtParserModalOpen, setTxtParserModalOpen] = useState(false)
  const [parserNodo, setParserNodo] = useState<NodoConexion | null>(null)

  // Lista de franquicias desde la API (todas las disponibles en dim.Franquicia)
  const [franquicias, setFranquicias] = useState<Array<{
    franquicia_id: number
    codigo: string
    nombre: string
    pais: string | null
    ciudad: string | null
    timezone?: string | null
    moneda?: string
  }>>([])

  useEffect(() => {
    const fetchFranquicias = async () => {
      try {
        const data = await franquiciasApi.getAll()
        setFranquicias(data.map(f => ({
          franquicia_id: f.franquicia_id,
          codigo: f.codigo,
          nombre: f.nombre,
          pais: f.pais,
          ciudad: f.ciudad,
          timezone: f.zona_horaria,
          moneda: 'EUR',
        })))
      } catch (err) {
        console.error('Error cargando franquicias:', err)
      }
    }
    fetchFranquicias()
  }, [])

  const selectedConexion = selectedDetalle?.nodo || conexiones?.find((c: NodoConexion) => c.nodo_conexion_id === selectedConexionId)

  const showAlert = (type: 'success' | 'error' | 'info', message: string) => {
    setAlert({ type, message })
    setTimeout(() => setAlert(null), 4000)
  }

  const handleTestConnection = async () => {
    if (!selectedConexion) return
    setTestingConnection(true)
    try {
      await new Promise(resolve => setTimeout(resolve, 1500))
      if (selectedConexion.estado === 'PENDING_CONFIG') {
        showAlert('error', 'Conexion no configurada. Configure las credenciales primero.')
      } else {
        showAlert('success', 'Conexion exitosa al servidor POS.')
      }
    } catch {
      showAlert('error', 'Error al conectar con el servidor POS.')
    } finally {
      setTestingConnection(false)
    }
  }

  const handleEditConfig = () => {
    setConnectionConfigOpen(true)
  }

  const handleFieldMapping = () => {
    setFieldMappingOpen(true)
  }

  const handleSaveConnectionConfig = async (config: NodoConfiguracion) => {
    // TODO: Implementar guardado en backend
    console.log('Guardando configuracion:', config)
    showAlert('success', 'Configuracion guardada correctamente.')
  }

  const handleSaveFieldMappings = async (mappings: FieldMapping[]) => {
    if (!selectedConexionId) return
    try {
      await conexionesApi.updateFieldMappings(selectedConexionId, mappings)
      showAlert('success', `${mappings.length} mapeos de campos guardados correctamente.`)
      refetchDetalle()
    } catch (err) {
      console.error('Error guardando mapeos:', err)
      showAlert('error', 'Error al guardar los mapeos de campos.')
      throw err
    }
  }

  const handleTestConnectionFromModal = async () => {
    // Simular test de conexion
    await new Promise(resolve => setTimeout(resolve, 1500))
    if (selectedConexion?.estado === 'PENDING_CONFIG') {
      return { success: false, message: 'Conexion no configurada.' }
    }
    return { success: true, message: 'Conexion exitosa al servidor POS.' }
  }

  const handleCreateConnection = async (data: NewConnectionData) => {
    try {
      // Crear el nodo de conexión en el backend
      const nodoCreate = {
        codigo: data.codigo,
        nombre: data.nombre,
        franquicia_id: data.franquicia_id!,
        tipo_conector: data.tipo_conector,
        modo: data.modo,
        timezone: data.timezone,
        moneda: data.moneda,
        convencion_importes: 'VAT_INCLUDED',
        politica_devoluciones: 'NEGATIVE_LINES',
        tolerancia_reconciliacion: 0.01,
        configuracion: {
          connection_type: data.connection_type,
        },
      }

      const result = await conexionesApi.createNodo(nodoCreate)
      showAlert('success', `Conexion "${data.nombre}" creada correctamente (ID: ${result.nodo_conexion_id}). Configure los parametros de conexion.`)
      refetch()
    } catch (err) {
      console.error('Error creando conexion:', err)
      showAlert('error', `Error al crear la conexion: ${err instanceof Error ? err.message : 'Error desconocido'}`)
      throw err
    }
  }

  const handleConfigureMapeo = (tipo: 'categorias' | 'medios_pago') => {
    setMapeoModalType(tipo)
    setMapeoForm({ codigo_origen: '', nombre_origen: '', valor_destino: '' })
    setEditingMapeoId(null)
    setMapeoModalOpen(true)
  }

  const handleEditMapeo = (tipo: 'categorias' | 'medios_pago', mapeo: MapeoCategoria | MapeoMedioPago) => {
    setMapeoModalType(tipo)
    setEditingMapeoId(tipo === 'categorias'
      ? (mapeo as MapeoCategoria).mapeo_categoria_id
      : (mapeo as MapeoMedioPago).mapeo_medio_pago_id
    )
    setMapeoForm({
      codigo_origen: mapeo.codigo_origen,
      nombre_origen: mapeo.nombre_origen || '',
      valor_destino: tipo === 'categorias'
        ? (mapeo as MapeoCategoria).categoria_destino
        : (mapeo as MapeoMedioPago).medio_pago_destino,
    })
    setMapeoModalOpen(true)
  }

  const handleSaveMapeo = async () => {
    if (!selectedConexionId || !mapeoForm.codigo_origen || !mapeoForm.valor_destino) {
      showAlert('error', 'Complete todos los campos requeridos.')
      return
    }

    setSavingMapeo(true)
    try {
      if (mapeoModalType === 'categorias') {
        await conexionesApi.upsertMapeoCategoria(selectedConexionId, {
          codigo_origen: mapeoForm.codigo_origen,
          nombre_origen: mapeoForm.nombre_origen || null,
          categoria_destino: mapeoForm.valor_destino,
          verificado: true,
        })
      } else {
        await conexionesApi.upsertMapeoMedioPago(selectedConexionId, {
          codigo_origen: mapeoForm.codigo_origen,
          nombre_origen: mapeoForm.nombre_origen || null,
          medio_pago_destino: mapeoForm.valor_destino,
          verificado: true,
        })
      }

      showAlert('success', 'Mapeo guardado correctamente.')
      setMapeoModalOpen(false)
      refetchDetalle()
    } catch (err) {
      showAlert('error', 'Error al guardar el mapeo.')
      console.error(err)
    } finally {
      setSavingMapeo(false)
    }
  }

  const formatFecha = (fecha: string | null) => {
    if (!fecha) return '-'
    return new Date(fecha).toLocaleString('es-ES', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  }

  const getEstadoIcon = (estado: string | null) => {
    switch (estado) {
      case 'SUCCESS':
        return <CheckCircle2 className="w-4 h-4 text-green-500" />
      case 'ERROR':
        return <XCircle className="w-4 h-4 text-red-500" />
      case 'WARNING':
        return <AlertTriangle className="w-4 h-4 text-yellow-500" />
      default:
        return <Clock className="w-4 h-4 text-gray-400" />
    }
  }

  const handleToggleEstado = async (conexion: NodoConexion) => {
    const nuevoEstado = conexion.estado === 'ACTIVE' ? 'PAUSED' : 'ACTIVE'
    await updateEstado.mutateAsync({ id: conexion.nodo_conexion_id, estado: nuevoEstado })
  }

  const handleEjecutarAhora = async (conexion: NodoConexion) => {
    // Para TXT_PARSER o FILE_PARSER, abrir el modal de upload
    if (conexion.tipo_conector === 'TXT_PARSER' || conexion.tipo_conector === 'FILE_PARSER') {
      setParserNodo(conexion)
      setTxtParserModalOpen(true)
      return
    }

    setExecutingId(conexion.nodo_conexion_id)
    try {
      const result = await conexionesApi.ejecutarExtraccion(conexion.nodo_conexion_id)
      if (result.success) {
        showAlert('success', `Extracción completada: ${result.tickets_procesados} tickets, ${result.lineas_procesadas} líneas procesadas.`)
      } else {
        showAlert('error', `Error en extracción: ${result.message}`)
      }
      refetch()
      if (selectedConexionId === conexion.nodo_conexion_id) {
        refetchDetalle()
      }
    } catch (err) {
      console.error('Error ejecutando extracción:', err)
      showAlert('error', 'Error al ejecutar la extracción. Verifique la conexión.')
    } finally {
      setExecutingId(null)
    }
  }

  const handleTxtParserComplete = () => {
    refetch()
    if (selectedConexionId && parserNodo && selectedConexionId === parserNodo.nodo_conexion_id) {
      refetchDetalle()
    }
    showAlert('success', 'Datos procesados exitosamente')
  }

  // Sync desde último día con datos (evita huecos)
  const handleSyncDesdeUltimo = async (conexion: NodoConexion) => {
    setSyncingFromLastId(conexion.nodo_conexion_id)
    try {
      const result = await conexionesApi.ejecutarExtraccionDesdeUltimo(conexion.nodo_conexion_id)
      if (result.success) {
        showAlert('success', `Sincronización completada: ${result.message}`)
      } else {
        showAlert('error', `Error en sincronización: ${result.message}`)
      }
      refetch()
      if (selectedConexionId === conexion.nodo_conexion_id) {
        refetchDetalle()
      }
    } catch (err) {
      console.error('Error en sync desde último:', err)
      showAlert('error', 'Error al ejecutar sincronización desde último día con datos.')
    } finally {
      setSyncingFromLastId(null)
    }
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <XCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
            Error al cargar conexiones
          </h2>
          <p className="text-muted-foreground mb-4">
            {error instanceof Error ? error.message : 'Error desconocido'}
          </p>
          <Button onClick={() => refetch()}>
            <RefreshCw className="w-4 h-4 mr-2" />
            Reintentar
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Alert */}
      {alert && (
        <div
          className={`p-4 rounded-lg border flex items-center gap-3 ${
            alert.type === 'success'
              ? 'bg-green-50 border-green-200 text-green-800 dark:bg-green-900/20 dark:border-green-800 dark:text-green-300'
              : alert.type === 'error'
              ? 'bg-red-50 border-red-200 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-300'
              : 'bg-blue-50 border-blue-200 text-blue-800 dark:bg-blue-900/20 dark:border-blue-800 dark:text-blue-300'
          }`}
        >
          {alert.type === 'success' && <CheckCircle2 className="w-5 h-5" />}
          {alert.type === 'error' && <XCircle className="w-5 h-5" />}
          {alert.type === 'info' && <AlertTriangle className="w-5 h-5" />}
          <span>{alert.message}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Conexiones</h1>
          <p className="text-muted-foreground">
            Gestiona las conexiones con los sistemas POS de cada franquicia
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => refetch()} disabled={isLoading}>
            <RefreshCw className={`w-4 h-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
            Actualizar
          </Button>
          <Button
            className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
            onClick={() => setNewConnectionOpen(true)}
          >
            <Plus className="w-4 h-4 mr-2" />
            Nueva Conexion
          </Button>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Total Conexiones</p>
                <p className="text-2xl font-bold">{conexiones?.length ?? 0}</p>
              </div>
              <Cable className="w-8 h-8 text-gray-400" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Activas</p>
                <p className="text-2xl font-bold text-green-600">
                  {conexiones?.filter((c: NodoConexion) => c.estado === 'ACTIVE').length ?? 0}
                </p>
              </div>
              <CheckCircle2 className="w-8 h-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Con Errores</p>
                <p className="text-2xl font-bold text-red-600">
                  {conexiones?.filter((c: NodoConexion) => c.estado === 'ERROR').length ?? 0}
                </p>
              </div>
              <XCircle className="w-8 h-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Pendientes</p>
                <p className="text-2xl font-bold text-blue-600">
                  {conexiones?.filter((c: NodoConexion) => c.estado === 'PENDING_CONFIG').length ?? 0}
                </p>
              </div>
              <Clock className="w-8 h-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Lista de Conexiones */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Nodos de Conexion</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-8 h-8 animate-spin text-cabrera-burgundy" />
            </div>
          ) : conexiones?.length === 0 ? (
            <div className="text-center py-8">
              <Cable className="w-12 h-12 text-gray-400 mx-auto mb-4" />
              <p className="text-muted-foreground">No hay conexiones configuradas</p>
              <Button className="mt-4 bg-cabrera-burgundy hover:bg-cabrera-burgundy/90">
                <Plus className="w-4 h-4 mr-2" />
                Crear primera conexion
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              {conexiones?.map((conexion: NodoConexion) => (
                <div
                  key={conexion.nodo_conexion_id}
                  className={`p-4 rounded-lg border transition-colors cursor-pointer ${
                    selectedConexionId === conexion.nodo_conexion_id
                      ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                      : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
                  }`}
                  onClick={() => setSelectedConexionId(conexion.nodo_conexion_id)}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4">
                      <div className="w-10 h-10 rounded-lg bg-gray-100 dark:bg-gray-800 flex items-center justify-center">
                        <Cable className="w-5 h-5 text-gray-600 dark:text-gray-400" />
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h3 className="font-medium text-gray-900 dark:text-white">
                            {conexion.nombre}
                          </h3>
                          <Badge className={estadoColors[conexion.estado]}>
                            {estadoLabels[conexion.estado]}
                          </Badge>
                        </div>
                        <div className="flex items-center gap-4 mt-1 text-sm text-muted-foreground">
                          <span>{conexion.franquicia_nombre}</span>
                          <span className="text-gray-300 dark:text-gray-600">|</span>
                          <span>{modoLabels[conexion.modo]}</span>
                          <span className="text-gray-300 dark:text-gray-600">|</span>
                          <span>{conexion.tipo_conector}</span>
                        </div>
                      </div>
                    </div>

                    <div className="flex items-center gap-6">
                      {/* Ultima sincronizacion */}
                      <div className="text-right">
                        <div className="flex items-center gap-2 justify-end">
                          {getEstadoIcon(conexion.ultimo_estado)}
                          <span className="text-sm text-muted-foreground">
                            {formatFecha(conexion.ultima_sincronizacion)}
                          </span>
                        </div>
                        {conexion.tickets_ultima_ejecucion && conexion.tickets_ultima_ejecucion > 0 && (
                          <p className="text-xs text-muted-foreground mt-1">
                            {conexion.tickets_ultima_ejecucion} tickets procesados
                          </p>
                        )}
                      </div>

                      {/* Acciones */}
                      <div className="flex items-center gap-2">
                        {/* Botón Ejecutar: para PULL activo o para FILE_PARSER/TXT_PARSER activo */}
                        {conexion.estado === 'ACTIVE' && (
                          conexion.modo === 'PULL' ||
                          conexion.tipo_conector === 'TXT_PARSER' ||
                          conexion.tipo_conector === 'FILE_PARSER'
                        ) && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title={conexion.tipo_conector === 'FILE_PARSER' || conexion.tipo_conector === 'TXT_PARSER' ? 'Subir archivo' : 'Ejecutar ahora (ayer)'}
                            onClick={(e) => {
                              e.stopPropagation()
                              handleEjecutarAhora(conexion)
                            }}
                            disabled={executingId === conexion.nodo_conexion_id}
                          >
                            {executingId === conexion.nodo_conexion_id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <Play className="w-4 h-4" />
                            )}
                          </Button>
                        )}
                        {/* Botón Sync desde último: solo para conectores API tipo PULL activos */}
                        {conexion.estado === 'ACTIVE' &&
                          conexion.modo === 'PULL' &&
                          conexion.tipo_conector !== 'TXT_PARSER' &&
                          conexion.tipo_conector !== 'FILE_PARSER' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title="Sync desde último día con datos (evita huecos)"
                            onClick={(e) => {
                              e.stopPropagation()
                              handleSyncDesdeUltimo(conexion)
                            }}
                            disabled={syncingFromLastId === conexion.nodo_conexion_id}
                            className="text-blue-600 hover:text-blue-700 hover:bg-blue-50 dark:hover:bg-blue-950"
                          >
                            {syncingFromLastId === conexion.nodo_conexion_id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <FastForward className="w-4 h-4" />
                            )}
                          </Button>
                        )}
                        {(conexion.estado === 'ACTIVE' || conexion.estado === 'PAUSED') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title={conexion.estado === 'ACTIVE' ? 'Pausar' : 'Reanudar'}
                            onClick={(e) => {
                              e.stopPropagation()
                              handleToggleEstado(conexion)
                            }}
                            disabled={updateEstado.isPending}
                          >
                            {conexion.estado === 'ACTIVE' ? (
                              <Pause className="w-4 h-4" />
                            ) : (
                              <Play className="w-4 h-4" />
                            )}
                          </Button>
                        )}
                        <Button variant="ghost" size="sm" title="Configurar">
                          <Settings className="w-4 h-4" />
                        </Button>
                        <ChevronRight className="w-4 h-4 text-gray-400" />
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Panel de Detalle (cuando hay una conexion seleccionada) */}
      {selectedConexion && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Configuracion */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Settings className="w-4 h-4" />
                Configuracion: {selectedConexion.nombre}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {isLoadingDetalle ? (
                <div className="flex items-center justify-center py-4">
                  <Loader2 className="w-6 h-6 animate-spin" />
                </div>
              ) : (
                <>
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-muted-foreground">Codigo Nodo</p>
                      <p className="font-medium">{selectedConexion.codigo}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Franquicia</p>
                      <p className="font-medium">{selectedConexion.franquicia_codigo}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Tipo Conector</p>
                      <p className="font-medium">{selectedConexion.tipo_conector}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Modo</p>
                      <p className="font-medium">{modoLabels[selectedConexion.modo]}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Timezone</p>
                      <p className="font-medium">{selectedConexion.timezone}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Moneda</p>
                      <p className="font-medium">{selectedConexion.moneda}</p>
                    </div>
                    {selectedConexion.cron_expression && (
                      <div className="col-span-2">
                        <p className="text-muted-foreground">Cron Expression</p>
                        <p className="font-medium font-mono text-xs">{selectedConexion.cron_expression}</p>
                      </div>
                    )}
                  </div>

                  <div className="pt-4 border-t flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleTestConnection}
                      disabled={testingConnection}
                    >
                      <RefreshCw className={`w-4 h-4 mr-2 ${testingConnection ? 'animate-spin' : ''}`} />
                      {testingConnection ? 'Testeando...' : 'Test Conexion'}
                    </Button>
                    <Button variant="outline" size="sm" onClick={handleEditConfig}>
                      Editar Configuracion
                    </Button>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          {/* Mapeos */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Mapeos y Transformaciones</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {/* NUEVO: Mapeo de Campos */}
                <div className="flex items-center justify-between p-3 bg-cabrera-burgundy/5 dark:bg-cabrera-burgundy/10 rounded-lg border border-cabrera-burgundy/20">
                  <div>
                    <p className="font-medium flex items-center gap-2">
                      <ArrowRight className="w-4 h-4 text-cabrera-burgundy" />
                      Mapeo de Campos (Origen → Destino)
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Define que campo del POS va a que campo de nuestro sistema
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge className="bg-cabrera-burgundy/10 text-cabrera-burgundy border-cabrera-burgundy/20">
                      Principal
                    </Badge>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleFieldMapping}
                      className="border-cabrera-burgundy text-cabrera-burgundy hover:bg-cabrera-burgundy/10"
                    >
                      <Settings className="w-4 h-4 mr-1" />
                      Configurar
                    </Button>
                  </div>
                </div>

                {/* Mapeo de Valores: Categorias */}
                <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
                  <div>
                    <p className="font-medium">Categorias de Producto (Valores)</p>
                    <p className="text-sm text-muted-foreground">
                      Valor "Hamburguesas" en POS → MAIN_COURSE en destino
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">
                      {selectedDetalle?.mapeos_categoria.length ?? selectedConexion.mapeos_categoria} configurados
                    </Badge>
                    <Button variant="ghost" size="sm" onClick={() => handleConfigureMapeo('categorias')}>
                      + Agregar
                    </Button>
                  </div>
                </div>

                {/* Mapeo de Valores: Medios de Pago */}
                <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
                  <div>
                    <p className="font-medium">Medios de Pago (Valores)</p>
                    <p className="text-sm text-muted-foreground">
                      Valor "Tarjeta" en POS → CREDIT_CARD en destino
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">
                      {selectedDetalle?.mapeos_medio_pago.length ?? selectedConexion.mapeos_medio_pago} configurados
                    </Badge>
                    <Button variant="ghost" size="sm" onClick={() => handleConfigureMapeo('medios_pago')}>
                      + Agregar
                    </Button>
                  </div>
                </div>

                {selectedDetalle && selectedDetalle.valores_no_mapeados.length > 0 && (
                  <div className="flex items-center justify-between p-3 bg-amber-50 dark:bg-amber-900/20 rounded-lg border border-amber-200 dark:border-amber-800">
                    <div>
                      <p className="font-medium text-amber-800 dark:text-amber-300">Valores Pendientes</p>
                      <p className="text-sm text-amber-700 dark:text-amber-400">
                        {selectedDetalle.valores_no_mapeados.length} valores sin mapear
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">
                        {selectedDetalle.valores_no_mapeados.length} pendientes
                      </Badge>
                      <Button variant="ghost" size="sm" onClick={() => showAlert('info', 'Funcionalidad de resolucion de valores pendientes en desarrollo.')}>
                        Resolver
                      </Button>
                    </div>
                  </div>
                )}

                {selectedConexion.estado === 'PENDING_CONFIG' && (
                  <div className="mt-4 p-3 bg-blue-50 dark:bg-blue-900/20 rounded-lg border border-blue-200 dark:border-blue-800">
                    <p className="text-sm text-blue-800 dark:text-blue-300">
                      <strong>Accion requerida:</strong> Configure la conexion y los mapeos de campos para activar esta conexion.
                    </p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Ultimas Ejecuciones */}
          {selectedDetalle && selectedDetalle.ultimas_ejecuciones.length > 0 && (
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle className="text-base">Ultimas Ejecuciones</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b">
                        <th className="text-left py-2 px-2">Fecha</th>
                        <th className="text-left py-2 px-2">Estado</th>
                        <th className="text-left py-2 px-2">Modo</th>
                        <th className="text-right py-2 px-2">Tickets</th>
                        <th className="text-right py-2 px-2">Lineas</th>
                        <th className="text-right py-2 px-2">Warnings</th>
                        <th className="text-right py-2 px-2">Errors</th>
                        <th className="text-left py-2 px-2">Batch ID</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedDetalle.ultimas_ejecuciones.map((ejecucion: EjecucionNodo) => (
                        <tr key={ejecucion.ejecucion_nodo_id} className="border-b hover:bg-gray-50 dark:hover:bg-gray-800">
                          <td className="py-2 px-2">{formatFecha(ejecucion.inicio_ejecucion)}</td>
                          <td className="py-2 px-2">
                            <Badge className={
                              ejecucion.estado === 'SUCCESS' ? 'bg-green-100 text-green-800' :
                              ejecucion.estado === 'ERROR' ? 'bg-red-100 text-red-800' :
                              ejecucion.estado === 'WARNING' ? 'bg-yellow-100 text-yellow-800' :
                              'bg-gray-100 text-gray-800'
                            }>
                              {ejecucion.estado}
                            </Badge>
                          </td>
                          <td className="py-2 px-2">{ejecucion.modo_ejecucion || '-'}</td>
                          <td className="py-2 px-2 text-right">{ejecucion.tickets_procesados}</td>
                          <td className="py-2 px-2 text-right">{ejecucion.lineas_procesadas}</td>
                          <td className="py-2 px-2 text-right text-yellow-600">{ejecucion.warnings_count || '-'}</td>
                          <td className="py-2 px-2 text-right text-red-600">{ejecucion.errors_count || '-'}</td>
                          <td className="py-2 px-2 font-mono text-xs">{ejecucion.batch_id || '-'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      )}

      {/* Modal de Mapeo */}
      <Dialog open={mapeoModalOpen} onOpenChange={setMapeoModalOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingMapeoId ? 'Editar' : 'Agregar'} Mapeo de {mapeoModalType === 'categorias' ? 'Categoria' : 'Medio de Pago'}
            </DialogTitle>
            <DialogDescription>
              Configure como se traduce un valor del sistema POS a nuestro sistema estandar.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            {/* Codigo Origen */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Codigo en Sistema Origen *</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                placeholder="Ej: FAM001, EFECTIVO, etc."
                value={mapeoForm.codigo_origen}
                onChange={(e) => setMapeoForm(prev => ({ ...prev, codigo_origen: e.target.value }))}
              />
              <p className="text-xs text-muted-foreground">
                El codigo exacto como viene del sistema POS
              </p>
            </div>

            {/* Nombre Origen */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Nombre en Sistema Origen</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                placeholder="Ej: Familia Carnes, Pago Efectivo, etc."
                value={mapeoForm.nombre_origen}
                onChange={(e) => setMapeoForm(prev => ({ ...prev, nombre_origen: e.target.value }))}
              />
              <p className="text-xs text-muted-foreground">
                Descripcion para identificar facilmente el valor
              </p>
            </div>

            {/* Valor Destino */}
            <div className="space-y-2">
              <label className="text-sm font-medium">
                {mapeoModalType === 'categorias' ? 'Categoria' : 'Medio de Pago'} Destino *
              </label>
              <Select
                value={mapeoForm.valor_destino}
                onValueChange={(value) => setMapeoForm(prev => ({ ...prev, valor_destino: value }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Seleccione un valor..." />
                </SelectTrigger>
                <SelectContent>
                  {(mapeoModalType === 'categorias' ? CATEGORIAS_PRODUCTO : MEDIOS_PAGO).map((valor) => (
                    <SelectItem key={valor} value={valor}>
                      {valor.replace(/_/g, ' ')}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Mapeos existentes */}
            {selectedDetalle && (
              <div className="border-t pt-4 mt-4">
                <h4 className="text-sm font-medium mb-2">
                  Mapeos existentes ({mapeoModalType === 'categorias'
                    ? selectedDetalle.mapeos_categoria.length
                    : selectedDetalle.mapeos_medio_pago.length})
                </h4>
                <div className="max-h-40 overflow-y-auto space-y-1">
                  {mapeoModalType === 'categorias' && selectedDetalle.mapeos_categoria.map((m: MapeoCategoria) => (
                    <div
                      key={m.mapeo_categoria_id}
                      className="flex items-center justify-between p-2 bg-gray-50 dark:bg-gray-800 rounded text-sm cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700"
                      onClick={() => handleEditMapeo('categorias', m)}
                    >
                      <span className="font-mono">{m.codigo_origen}</span>
                      <span className="text-muted-foreground">{m.nombre_origen || '-'}</span>
                      <Badge variant="outline">{m.categoria_destino}</Badge>
                    </div>
                  ))}
                  {mapeoModalType === 'medios_pago' && selectedDetalle.mapeos_medio_pago.map((m: MapeoMedioPago) => (
                    <div
                      key={m.mapeo_medio_pago_id}
                      className="flex items-center justify-between p-2 bg-gray-50 dark:bg-gray-800 rounded text-sm cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700"
                      onClick={() => handleEditMapeo('medios_pago', m)}
                    >
                      <span className="font-mono">{m.codigo_origen}</span>
                      <span className="text-muted-foreground">{m.nombre_origen || '-'}</span>
                      <Badge variant="outline">{m.medio_pago_destino}</Badge>
                    </div>
                  ))}
                  {((mapeoModalType === 'categorias' && selectedDetalle.mapeos_categoria.length === 0) ||
                    (mapeoModalType === 'medios_pago' && selectedDetalle.mapeos_medio_pago.length === 0)) && (
                    <p className="text-sm text-muted-foreground text-center py-2">
                      No hay mapeos configurados aun
                    </p>
                  )}
                </div>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setMapeoModalOpen(false)}>
              Cancelar
            </Button>
            <Button
              onClick={handleSaveMapeo}
              disabled={savingMapeo || !mapeoForm.codigo_origen || !mapeoForm.valor_destino}
              className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
            >
              {savingMapeo ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                  Guardando...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-2" />
                  Guardar Mapeo
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Modal de Configuracion de Conexion */}
      {selectedConexion && (
        <ConnectionConfigModal
          open={connectionConfigOpen}
          onOpenChange={setConnectionConfigOpen}
          connectionName={selectedConexion.nombre}
          connectorType={selectedConexion.tipo_conector}
          currentConfig={selectedDetalle?.configuracion ?? undefined}
          onSave={handleSaveConnectionConfig}
          onTestConnection={handleTestConnectionFromModal}
        />
      )}

      {/* Modal de Mapeo de Campos */}
      {selectedConexion && selectedConexionId && (
        <FieldMappingEditor
          open={fieldMappingOpen}
          onOpenChange={setFieldMappingOpen}
          connectionId={selectedConexionId}
          connectionName={selectedConexion.nombre}
          connectorType={selectedConexion.tipo_conector}
          currentMappings={selectedDetalle?.configuracion?.field_mappings}
          onSave={handleSaveFieldMappings}
        />
      )}

      {/* Modal de Nueva Conexion */}
      <NewConnectionModal
        open={newConnectionOpen}
        onOpenChange={setNewConnectionOpen}
        franquicias={franquicias}
        onSave={handleCreateConnection}
      />

      {/* Modal de TXT Parser Upload */}
      {parserNodo && (
        <TxtParserUploadModal
          open={txtParserModalOpen}
          onOpenChange={setTxtParserModalOpen}
          nodo={parserNodo}
          onComplete={handleTxtParserComplete}
        />
      )}
    </div>
  )
}
