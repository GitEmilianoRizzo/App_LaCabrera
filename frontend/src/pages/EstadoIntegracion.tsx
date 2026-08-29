import { useState, useEffect, useMemo } from 'react'
import { Plug, CheckCircle, AlertTriangle, XCircle, Clock, Pencil, Save, Loader2, Search, X } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
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
import { DataTable } from '@/components/dashboard/DataTable'
import { LoadingPage } from '@/components/dashboard/LoadingState'
import { ErrorBanner } from '@/components/dashboard/ErrorState'
import { useEstadoIntegracion } from '@/hooks/useDashboard'
import { franquiciasApi, type GrupoEconomico, type FranquiciaUpdate } from '@/services/api'
import { formatDate, getStatusColor, cn } from '@/lib/utils'
import type { EstadoIntegracion as EstadoIntegracionType } from '@/types/dashboard'

const POS_OPTIONS = ['AGORA', 'VINSON', 'TOAST', 'MICROS', 'ORACLE', 'SQUARE', 'CLOVER', 'OTRO']

interface EditFormData {
  nombre: string
  grupo_economico_id: number | null
  pais: string
  ciudad: string
  sistema_origen: string
  contacto_nombre: string
  contacto_email: string
  contacto_telefono: string
}

export function EstadoIntegracion() {
  const { data, loading, error, refetch } = useEstadoIntegracion()
  const [editModalOpen, setEditModalOpen] = useState(false)
  const [editingFranquicia, setEditingFranquicia] = useState<EstadoIntegracionType | null>(null)
  const [grupos, setGrupos] = useState<GrupoEconomico[]>([])
  const [saving, setSaving] = useState(false)
  const [alert, setAlert] = useState<{ type: 'success' | 'error'; message: string } | null>(null)
  const [formData, setFormData] = useState<EditFormData>({
    nombre: '',
    grupo_economico_id: null,
    pais: '',
    ciudad: '',
    sistema_origen: '',
    contacto_nombre: '',
    contacto_email: '',
    contacto_telefono: '',
  })

  // Buscador global
  const [searchTerm, setSearchTerm] = useState('')

  // Filtrar datos por búsqueda
  const filteredData = useMemo(() => {
    if (!data) return []
    if (!searchTerm.trim()) return data

    const term = searchTerm.toLowerCase().trim()
    return data.filter((item) => {
      return (
        item.franquicia_nombre?.toLowerCase().includes(term) ||
        item.franquicia_codigo?.toLowerCase().includes(term) ||
        item.grupo_economico_nombre?.toLowerCase().includes(term) ||
        item.contacto_nombre?.toLowerCase().includes(term) ||
        item.contacto_email?.toLowerCase().includes(term) ||
        item.sistema_origen?.toLowerCase().includes(term) ||
        item.pais?.toLowerCase().includes(term) ||
        item.ciudad?.toLowerCase().includes(term) ||
        item.estado_integracion?.toLowerCase().includes(term)
      )
    })
  }, [data, searchTerm])

  // Cargar grupos economicos
  useEffect(() => {
    franquiciasApi.getGruposEconomicos().then(setGrupos).catch(console.error)
  }, [])

  const showAlert = (type: 'success' | 'error', message: string) => {
    setAlert({ type, message })
    setTimeout(() => setAlert(null), 4000)
  }

  const handleEdit = (item: EstadoIntegracionType) => {
    setEditingFranquicia(item)
    setFormData({
      nombre: item.franquicia_nombre || '',
      grupo_economico_id: null, // Will need to look up from grupo_economico_nombre
      pais: item.pais || '',
      ciudad: item.ciudad || '',
      sistema_origen: item.sistema_origen || '',
      contacto_nombre: item.contacto_nombre || '',
      contacto_email: item.contacto_email || '',
      contacto_telefono: item.contacto_telefono || '',
    })
    // Find grupo_economico_id from nombre
    const grupo = grupos.find(g => g.nombre === item.grupo_economico_nombre)
    if (grupo) {
      setFormData(prev => ({ ...prev, grupo_economico_id: grupo.grupo_economico_id }))
    }
    setEditModalOpen(true)
  }

  const handleSave = async () => {
    if (!editingFranquicia) return

    setSaving(true)
    try {
      const updateData: FranquiciaUpdate = {}

      if (formData.nombre) updateData.nombre = formData.nombre
      if (formData.grupo_economico_id) updateData.grupo_economico_id = formData.grupo_economico_id
      if (formData.pais) updateData.pais = formData.pais
      if (formData.ciudad) updateData.ciudad = formData.ciudad
      if (formData.sistema_origen) updateData.sistema_origen = formData.sistema_origen
      if (formData.contacto_nombre) updateData.contacto_nombre = formData.contacto_nombre
      if (formData.contacto_email) updateData.contacto_email = formData.contacto_email
      if (formData.contacto_telefono) updateData.contacto_telefono = formData.contacto_telefono

      await franquiciasApi.update(editingFranquicia.franquicia_id, updateData)
      showAlert('success', 'Franquicia actualizada correctamente')
      setEditModalOpen(false)
      refetch()
    } catch (err) {
      console.error('Error guardando:', err)
      showAlert('error', 'Error al guardar los cambios')
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <LoadingPage />
  }

  if (error) {
    return <ErrorBanner message={error} onRetry={refetch} />
  }

  // Contar por estado - usando dias_sin_sincronizar para determinar alerta
  const getAlertaNivel = (item: EstadoIntegracionType) => {
    if (item.ultima_sincronizacion === null) return 'NEVER_SYNCED'
    if (item.dias_sin_sincronizar === null) return 'OK'
    if (item.dias_sin_sincronizar > 3) return 'CRITICAL'
    if (item.dias_sin_sincronizar > 1) return 'WARNING'
    return 'OK'
  }

  const counts = {
    ok: data?.filter(f => getAlertaNivel(f) === 'OK').length || 0,
    warning: data?.filter(f => getAlertaNivel(f) === 'WARNING').length || 0,
    critical: data?.filter(f => getAlertaNivel(f) === 'CRITICAL').length || 0,
    neverSynced: data?.filter(f => getAlertaNivel(f) === 'NEVER_SYNCED').length || 0,
  }

  const columns = [
    {
      key: 'franquicia_nombre',
      header: 'Franquicia',
      sortable: true,
      render: (item: EstadoIntegracionType) => (
        <div>
          <p className="font-medium">{item.franquicia_nombre}</p>
          <p className="text-xs text-muted-foreground">{item.franquicia_codigo}</p>
        </div>
      ),
    },
    { key: 'grupo_economico_nombre', header: 'Grupo', sortable: true },
    {
      key: 'contacto_nombre',
      header: 'Contacto',
      sortable: true,
      render: (item: EstadoIntegracionType) => (
        <div>
          <p className="font-medium">{item.contacto_nombre || '-'}</p>
          {item.contacto_email && (
            <p className="text-xs text-muted-foreground">{item.contacto_email}</p>
          )}
        </div>
      ),
    },
    {
      key: 'sistema_origen',
      header: 'POS',
      sortable: true,
      render: (item: EstadoIntegracionType) => (
        <span className="font-mono text-sm">{item.sistema_origen || '-'}</span>
      ),
    },
    { key: 'pais', header: 'Pais', sortable: true },
    {
      key: 'estado_integracion',
      header: 'Estado',
      align: 'center' as const,
      sortable: true,
      render: (item: EstadoIntegracionType) => (
        <Badge className={getStatusColor(item.estado_integracion || 'UNKNOWN')}>
          {item.estado_integracion || 'N/A'}
        </Badge>
      ),
    },
    {
      key: 'ultima_sincronizacion',
      header: 'Ultima Sync',
      sortable: true,
      render: (item: EstadoIntegracionType) =>
        item.ultima_sincronizacion ? formatDate(item.ultima_sincronizacion, 'time') : 'Nunca',
    },
    {
      key: 'dias_sin_sincronizar',
      header: 'Dias',
      align: 'center' as const,
      sortable: true,
      render: (item: EstadoIntegracionType) => (
        <span
          className={cn(
            'font-medium',
            item.dias_sin_sincronizar === null
              ? 'text-gray-500'
              : item.dias_sin_sincronizar > 3
                ? 'text-red-600'
                : item.dias_sin_sincronizar > 1
                  ? 'text-yellow-600'
                  : 'text-green-600'
          )}
        >
          {item.dias_sin_sincronizar ?? '-'}
        </span>
      ),
    },
    {
      key: 'alerta',
      header: 'Alerta',
      align: 'center' as const,
      render: (item: EstadoIntegracionType) => {
        const nivel = getAlertaNivel(item)
        const icons = {
          OK: <CheckCircle className="w-5 h-5 text-green-500" />,
          WARNING: <AlertTriangle className="w-5 h-5 text-yellow-500" />,
          CRITICAL: <XCircle className="w-5 h-5 text-red-500" />,
          NEVER_SYNCED: <Clock className="w-5 h-5 text-gray-400" />,
        }
        return icons[nivel as keyof typeof icons] || null
      },
    },
    {
      key: 'acciones',
      header: '',
      align: 'center' as const,
      render: (item: EstadoIntegracionType) => (
        <Button
          variant="ghost"
          size="sm"
          onClick={(e: React.MouseEvent) => {
            e.stopPropagation()
            handleEdit(item)
          }}
          title="Editar franquicia"
        >
          <Pencil className="w-4 h-4" />
        </Button>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      {/* Alert */}
      {alert && (
        <div
          className={`p-4 rounded-lg border flex items-center gap-3 ${
            alert.type === 'success'
              ? 'bg-green-50 border-green-200 text-green-800'
              : 'bg-red-50 border-red-200 text-red-800'
          }`}
        >
          {alert.type === 'success' ? <CheckCircle className="w-5 h-5" /> : <XCircle className="w-5 h-5" />}
          <span>{alert.message}</span>
        </div>
      )}

      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Estado de Integracion</h1>
          <p className="text-muted-foreground">Monitoreo de sincronizacion de franquicias</p>
        </div>

        {/* Buscador Global */}
        <div className="relative w-full md:w-80">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <input
            type="text"
            placeholder="Buscar franquicia, contacto, POS, país..."
            className="w-full pl-10 pr-10 py-2 border rounded-lg text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              onClick={() => setSearchTerm('')}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            >
              <X className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      {/* Indicador de resultados filtrados */}
      {searchTerm && (
        <p className="text-sm text-muted-foreground">
          Mostrando {filteredData.length} de {data?.length || 0} franquicias
        </p>
      )}

      {/* Status Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card className="border-l-4 border-l-green-500">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Sincronizadas</p>
                <p className="text-2xl font-bold text-green-600">{counts.ok}</p>
              </div>
              <CheckCircle className="w-8 h-8 text-green-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-yellow-500">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Con Alerta</p>
                <p className="text-2xl font-bold text-yellow-600">{counts.warning}</p>
              </div>
              <AlertTriangle className="w-8 h-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-red-500">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Criticas</p>
                <p className="text-2xl font-bold text-red-600">{counts.critical}</p>
              </div>
              <XCircle className="w-8 h-8 text-red-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-gray-400">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Sin Integrar</p>
                <p className="text-2xl font-bold text-gray-600">{counts.neverSynced}</p>
              </div>
              <Plug className="w-8 h-8 text-gray-400" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Alert Summary */}
      {(counts.critical > 0 || counts.warning > 0) && (
        <Card className="bg-amber-50 border-amber-200">
          <CardHeader className="pb-2">
            <CardTitle className="text-base flex items-center gap-2 text-amber-800">
              <AlertTriangle className="w-5 h-5" />
              Atencion Requerida
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="text-sm text-amber-700 space-y-1">
              {counts.critical > 0 && (
                <li>
                  {counts.critical} franquicia(s) con estado critico - mas de 3 dias sin sincronizar
                </li>
              )}
              {counts.warning > 0 && (
                <li>
                  {counts.warning} franquicia(s) con alerta - entre 1 y 3 dias sin sincronizar
                </li>
              )}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* Table */}
      <DataTable
        title="Estado por Franquicia"
        data={filteredData}
        columns={columns}
      />

      {/* Edit Modal */}
      <Dialog open={editModalOpen} onOpenChange={setEditModalOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Editar Franquicia</DialogTitle>
            <DialogDescription>
              {editingFranquicia?.franquicia_nombre} ({editingFranquicia?.franquicia_codigo})
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-4 py-4">
            {/* Nombre */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Nombre</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.nombre}
                onChange={(e) => setFormData(prev => ({ ...prev, nombre: e.target.value }))}
              />
            </div>

            {/* Grupo Economico */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Grupo Economico</label>
              <Select
                value={formData.grupo_economico_id?.toString() || ''}
                onValueChange={(value) => setFormData(prev => ({ ...prev, grupo_economico_id: parseInt(value) }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Seleccionar grupo..." />
                </SelectTrigger>
                <SelectContent>
                  {grupos.map((grupo) => (
                    <SelectItem key={grupo.grupo_economico_id} value={grupo.grupo_economico_id.toString()}>
                      {grupo.nombre}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Pais */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Pais</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.pais}
                onChange={(e) => setFormData(prev => ({ ...prev, pais: e.target.value }))}
              />
            </div>

            {/* Ciudad */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Ciudad</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.ciudad}
                onChange={(e) => setFormData(prev => ({ ...prev, ciudad: e.target.value }))}
              />
            </div>

            {/* POS / Sistema Origen */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Sistema POS</label>
              <Select
                value={formData.sistema_origen}
                onValueChange={(value) => setFormData(prev => ({ ...prev, sistema_origen: value }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Seleccionar POS..." />
                </SelectTrigger>
                <SelectContent>
                  {POS_OPTIONS.map((pos) => (
                    <SelectItem key={pos} value={pos}>
                      {pos}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Contacto Nombre */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Contacto</label>
              <input
                type="text"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.contacto_nombre}
                onChange={(e) => setFormData(prev => ({ ...prev, contacto_nombre: e.target.value }))}
              />
            </div>

            {/* Contacto Email */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Email</label>
              <input
                type="email"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.contacto_email}
                onChange={(e) => setFormData(prev => ({ ...prev, contacto_email: e.target.value }))}
              />
            </div>

            {/* Contacto Telefono */}
            <div className="space-y-2">
              <label className="text-sm font-medium">Telefono</label>
              <input
                type="tel"
                className="w-full px-3 py-2 border rounded-md text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring"
                value={formData.contacto_telefono}
                onChange={(e) => setFormData(prev => ({ ...prev, contacto_telefono: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditModalOpen(false)}>
              Cancelar
            </Button>
            <Button
              onClick={handleSave}
              disabled={saving}
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
                  Guardar
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
