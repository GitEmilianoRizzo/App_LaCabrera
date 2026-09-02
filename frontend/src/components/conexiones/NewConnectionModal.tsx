import { useState, useEffect, useMemo } from 'react'
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
  Cable,
  Globe,
  FileJson,
  Building2,
  Plus,
  Search,
  Upload,
} from 'lucide-react'

interface Franquicia {
  franquicia_id: number
  codigo: string
  nombre: string
  pais?: string | null
  ciudad?: string | null
  timezone: string
  moneda: string
}

interface NewConnectionModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  franquicias: Franquicia[]
  onSave: (data: NewConnectionData) => Promise<void>
}

export interface NewConnectionData {
  codigo: string
  nombre: string
  franquicia_id?: number
  // Nueva franquicia (si no selecciona existente)
  nueva_franquicia?: {
    codigo: string
    nombre: string
    pais: string
    ciudad: string
    timezone: string
    moneda: string
  }
  tipo_conector: string
  modo: 'PULL' | 'PUSH' | 'AGENT' | 'FILE'
  timezone: string
  moneda: string
  connection_type: 'API_REST' | 'CLOUD_FILE' | 'TXT_PARSER'
  parser_code?: string
}

const CONECTORES = [
  { id: 'AGORA_POS', label: 'Agora POS', description: 'Sistema POS Agora (REST API)', icon: 'globe' },
  { id: 'GENERIC_JSON', label: 'JSON Generico', description: 'Archivo JSON en formato LA CABRERA', icon: 'file' },
  { id: 'FILE_UPLOAD', label: 'Subir Archivo', description: 'Subir archivos HTML, CSV o TXT para parsear', icon: 'upload' },
  { id: 'CUSTOM_API', label: 'API Personalizada', description: 'API REST con formato custom', icon: 'globe' },
]

const PARSERS = [
  { code: 'TOAST_HTML', name: 'Toast HTML', description: 'Exportacion HTML de Toast POS', extensions: '.html, .htm' },
  { code: 'TOAST_PARSER', name: 'Toast TXT', description: 'Reporte de texto de Toast POS', extensions: '.txt' },
]

const MODOS = [
  { id: 'PULL', label: 'Pull (Central consulta)', description: 'El sistema central consulta periodicamente al POS' },
  { id: 'FILE', label: 'Archivo', description: 'Se lee un archivo JSON desde la nube' },
  { id: 'PUSH', label: 'Push (Local envia)', description: 'El POS envia datos al sistema central' },
]

const TIMEZONES = [
  { id: 'Europe/Madrid', label: 'Madrid (CET/CEST)' },
  { id: 'America/Argentina/Buenos_Aires', label: 'Buenos Aires (ART)' },
  { id: 'America/New_York', label: 'New York / Miami (EST)' },
  { id: 'America/Santiago', label: 'Santiago (CLT)' },
  { id: 'America/Sao_Paulo', label: 'Sao Paulo (BRT)' },
  { id: 'America/Mexico_City', label: 'Mexico City (CST)' },
  { id: 'America/Bogota', label: 'Bogota (COT)' },
  { id: 'America/Lima', label: 'Lima (PET)' },
]

const MONEDAS = [
  { id: 'EUR', label: 'Euro (EUR)' },
  { id: 'ARS', label: 'Peso Argentino (ARS)' },
  { id: 'USD', label: 'Dolar USA (USD)' },
  { id: 'CLP', label: 'Peso Chileno (CLP)' },
  { id: 'BRL', label: 'Real Brasileno (BRL)' },
  { id: 'MXN', label: 'Peso Mexicano (MXN)' },
  { id: 'COP', label: 'Peso Colombiano (COP)' },
  { id: 'PEN', label: 'Sol Peruano (PEN)' },
  { id: 'PYG', label: 'Guarani (PYG)' },
]

export function NewConnectionModal({
  open,
  onOpenChange,
  franquicias,
  onSave,
}: NewConnectionModalProps) {
  const [saving, setSaving] = useState(false)
  const [step, setStep] = useState(1)

  // Modo: seleccionar existente o crear nueva
  const [crearNuevaFranquicia, setCrearNuevaFranquicia] = useState(false)

  // Buscador de franquicias
  const [searchTerm, setSearchTerm] = useState('')

  // Form state - Franquicia existente
  const [selectedFranquicia, setSelectedFranquicia] = useState<number | null>(null)

  // Form state - Nueva franquicia
  const [franquiciaCodigo, setFranquiciaCodigo] = useState('')
  const [franquiciaNombre, setFranquiciaNombre] = useState('')
  const [franquiciaPais, setFranquiciaPais] = useState('')
  const [franquiciaCiudad, setFranquiciaCiudad] = useState('')
  const [franquiciaTimezone, setFranquiciaTimezone] = useState('Europe/Madrid')
  const [franquiciaMoneda, setFranquiciaMoneda] = useState('EUR')

  // Form state - Conexion
  const [codigo, setCodigo] = useState('')
  const [nombre, setNombre] = useState('')
  const [tipoConector, setTipoConector] = useState('AGORA_POS')
  const [modo, setModo] = useState<'PULL' | 'PUSH' | 'AGENT' | 'FILE'>('PULL')
  const [connectionType, setConnectionType] = useState<'API_REST' | 'CLOUD_FILE' | 'TXT_PARSER'>('API_REST')
  const [parserCode, setParserCode] = useState('TOAST_HTML')

  // Timezone y Moneda para conexion (editables)
  const [conexionTimezone, setConexionTimezone] = useState('Europe/Madrid')
  const [conexionMoneda, setConexionMoneda] = useState('EUR')

  // Inferir moneda y timezone desde el pais
  const inferirConfiguracionPais = (pais: string | null | undefined) => {
    const paisLower = (pais || '').toLowerCase()
    if (paisLower.includes('argentina')) return { moneda: 'ARS', timezone: 'America/Argentina/Buenos_Aires' }
    if (paisLower.includes('chile')) return { moneda: 'CLP', timezone: 'America/Santiago' }
    if (paisLower.includes('brasil')) return { moneda: 'BRL', timezone: 'America/Sao_Paulo' }
    if (paisLower.includes('mexico') || paisLower.includes('méxico')) return { moneda: 'MXN', timezone: 'America/Mexico_City' }
    if (paisLower.includes('colombia')) return { moneda: 'COP', timezone: 'America/Bogota' }
    if (paisLower.includes('peru') || paisLower.includes('perú')) return { moneda: 'PEN', timezone: 'America/Lima' }
    if (paisLower.includes('paraguay')) return { moneda: 'PYG', timezone: 'America/Asuncion' }
    if (paisLower.includes('usa') || paisLower.includes('estados unidos')) return { moneda: 'USD', timezone: 'America/New_York' }
    // Default: España
    return { moneda: 'EUR', timezone: 'Europe/Madrid' }
  }

  // Auto-fill when franquicia changes
  useEffect(() => {
    if (selectedFranquicia && !crearNuevaFranquicia) {
      const franq = franquicias.find(f => f.franquicia_id === selectedFranquicia)
      if (franq) {
        setCodigo(`NODO_${franq.codigo}`)
        setNombre(`Conector ${franq.nombre}`)
        // Inferir moneda y timezone del pais
        const config = inferirConfiguracionPais(franq.pais)
        setConexionMoneda(config.moneda)
        setConexionTimezone(config.timezone)
      }
    }
  }, [selectedFranquicia, franquicias, crearNuevaFranquicia])

  // Auto-fill when creating new franquicia
  useEffect(() => {
    if (crearNuevaFranquicia && franquiciaCodigo) {
      setCodigo(`NODO_${franquiciaCodigo}`)
      if (franquiciaNombre) {
        setNombre(`Conector ${franquiciaNombre}`)
      }
    }
  }, [crearNuevaFranquicia, franquiciaCodigo, franquiciaNombre])

  // Reset when modal opens
  useEffect(() => {
    if (open) {
      setStep(1)
      setCrearNuevaFranquicia(franquicias.length === 0)
      setSelectedFranquicia(null)
      setSearchTerm('')
      setFranquiciaCodigo('')
      setFranquiciaNombre('')
      setFranquiciaPais('')
      setFranquiciaCiudad('')
      setFranquiciaTimezone('Europe/Madrid')
      setFranquiciaMoneda('EUR')
      setCodigo('')
      setNombre('')
      setTipoConector('AGORA_POS')
      setModo('PULL')
      setConnectionType('API_REST')
      setParserCode('TOAST_HTML')
      setConexionTimezone('Europe/Madrid')
      setConexionMoneda('EUR')
    }
  }, [open, franquicias.length])

  const selectedFranquiciaData = franquicias.find(f => f.franquicia_id === selectedFranquicia)

  // Filtrar franquicias segun busqueda
  const filteredFranquicias = useMemo(() => {
    if (!searchTerm.trim()) return franquicias
    const term = searchTerm.toLowerCase().trim()
    return franquicias.filter(f =>
      f.nombre.toLowerCase().includes(term) ||
      f.codigo.toLowerCase().includes(term) ||
      f.pais?.toLowerCase().includes(term) ||
      f.ciudad?.toLowerCase().includes(term)
    )
  }, [franquicias, searchTerm])

  const handleSave = async () => {
    if (!codigo || !nombre) return
    if (!crearNuevaFranquicia && !selectedFranquicia) return
    if (crearNuevaFranquicia && (!franquiciaCodigo || !franquiciaNombre)) return

    setSaving(true)
    try {
      await onSave({
        codigo,
        nombre,
        franquicia_id: crearNuevaFranquicia ? undefined : selectedFranquicia!,
        nueva_franquicia: crearNuevaFranquicia ? {
          codigo: franquiciaCodigo,
          nombre: franquiciaNombre,
          pais: franquiciaPais,
          ciudad: franquiciaCiudad,
          timezone: franquiciaTimezone,
          moneda: franquiciaMoneda,
        } : undefined,
        tipo_conector: tipoConector === 'FILE_UPLOAD' ? 'FILE_PARSER' : tipoConector,
        modo,
        timezone: crearNuevaFranquicia ? franquiciaTimezone : conexionTimezone,
        moneda: crearNuevaFranquicia ? franquiciaMoneda : conexionMoneda,
        connection_type: connectionType,
        parser_code: tipoConector === 'FILE_UPLOAD' ? parserCode : undefined,
      })
      onOpenChange(false)
    } finally {
      setSaving(false)
    }
  }

  const canProceedStep1 = crearNuevaFranquicia
    ? (franquiciaCodigo && franquiciaNombre)
    : selectedFranquicia !== null

  const canProceedStep2 = codigo && nombre

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Cable className="w-5 h-5" />
            Nueva Conexion
          </DialogTitle>
          <DialogDescription>
            Paso {step} de 2: {step === 1 ? 'Franquicia' : 'Configurar conexion'}
          </DialogDescription>
        </DialogHeader>

        {/* Progress indicator */}
        <div className="flex gap-2 mb-4">
          <div className={`flex-1 h-1 rounded ${step >= 1 ? 'bg-cabrera-burgundy' : 'bg-gray-200'}`} />
          <div className={`flex-1 h-1 rounded ${step >= 2 ? 'bg-cabrera-burgundy' : 'bg-gray-200'}`} />
        </div>

        <div className="py-4 space-y-6">
          {/* STEP 1: Select or Create Franchise */}
          {step === 1 && (
            <div className="space-y-4">
              {/* Toggle: Existente vs Nueva */}
              <div className="flex gap-2 p-1 bg-gray-100 dark:bg-gray-800 rounded-lg">
                <button
                  type="button"
                  onClick={() => setCrearNuevaFranquicia(false)}
                  disabled={franquicias.length === 0}
                  className={`flex-1 py-2 px-4 rounded-md text-sm font-medium transition-all ${
                    !crearNuevaFranquicia
                      ? 'bg-white dark:bg-gray-700 shadow text-cabrera-burgundy'
                      : 'text-muted-foreground hover:text-foreground'
                  } ${franquicias.length === 0 ? 'opacity-50 cursor-not-allowed' : ''}`}
                >
                  Franquicia Existente
                </button>
                <button
                  type="button"
                  onClick={() => setCrearNuevaFranquicia(true)}
                  className={`flex-1 py-2 px-4 rounded-md text-sm font-medium transition-all ${
                    crearNuevaFranquicia
                      ? 'bg-white dark:bg-gray-700 shadow text-cabrera-burgundy'
                      : 'text-muted-foreground hover:text-foreground'
                  }`}
                >
                  <Plus className="w-4 h-4 inline mr-1" />
                  Crear Nueva
                </button>
              </div>

              {/* Seleccionar existente */}
              {!crearNuevaFranquicia && (
                <div className="space-y-3">
                  <label className="text-sm font-medium">Seleccionar Franquicia *</label>

                  {/* Buscador */}
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                    <input
                      type="text"
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                      placeholder="Buscar por nombre, codigo, pais o ciudad..."
                      className="w-full pl-9 pr-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>

                  {/* Lista de franquicias filtradas */}
                  <div className="grid grid-cols-1 gap-2 max-h-48 overflow-y-auto">
                    {filteredFranquicias.length === 0 ? (
                      <div className="text-center py-4 text-muted-foreground text-sm">
                        No se encontraron franquicias con "{searchTerm}"
                      </div>
                    ) : filteredFranquicias.map(franq => (
                      <button
                        key={franq.franquicia_id}
                        type="button"
                        onClick={() => setSelectedFranquicia(franq.franquicia_id)}
                        className={`flex items-center justify-between p-3 rounded-lg border text-left transition-all ${
                          selectedFranquicia === franq.franquicia_id
                            ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                            : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          <Building2 className={`w-5 h-5 ${
                            selectedFranquicia === franq.franquicia_id ? 'text-cabrera-burgundy' : 'text-gray-400'
                          }`} />
                          <div>
                            <p className="font-medium">{franq.nombre}</p>
                            <p className="text-xs text-muted-foreground">
                              {franq.codigo} - {franq.pais || 'Sin pais'}, {franq.ciudad || 'Sin ciudad'}
                            </p>
                          </div>
                        </div>
                        <Badge variant="outline">{franq.moneda}</Badge>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Crear nueva franquicia */}
              {crearNuevaFranquicia && (
                <div className="space-y-4 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-dashed">
                  <h4 className="font-medium flex items-center gap-2">
                    <Building2 className="w-4 h-4" />
                    Nueva Franquicia
                  </h4>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <label className="text-sm font-medium">Codigo *</label>
                      <input
                        type="text"
                        value={franquiciaCodigo}
                        onChange={(e) => setFranquiciaCodigo(e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, ''))}
                        placeholder="FR_MARBELLA"
                        className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Nombre *</label>
                      <input
                        type="text"
                        value={franquiciaNombre}
                        onChange={(e) => setFranquiciaNombre(e.target.value)}
                        placeholder="La Cabrera Marbella"
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Pais</label>
                      <input
                        type="text"
                        value={franquiciaPais}
                        onChange={(e) => setFranquiciaPais(e.target.value)}
                        placeholder="Espana"
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Ciudad</label>
                      <input
                        type="text"
                        value={franquiciaCiudad}
                        onChange={(e) => setFranquiciaCiudad(e.target.value)}
                        placeholder="Marbella"
                        className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Zona Horaria</label>
                      <Select value={franquiciaTimezone} onValueChange={setFranquiciaTimezone}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {TIMEZONES.map(tz => (
                            <SelectItem key={tz.id} value={tz.id}>{tz.label}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="space-y-2">
                      <label className="text-sm font-medium">Moneda</label>
                      <Select value={franquiciaMoneda} onValueChange={setFranquiciaMoneda}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {MONEDAS.map(m => (
                            <SelectItem key={m.id} value={m.id}>{m.label}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* STEP 2: Configure Connection */}
          {step === 2 && (
            <div className="space-y-4">
              {/* Selected/New franchise info */}
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg flex items-center gap-3">
                <Building2 className="w-5 h-5 text-cabrera-burgundy" />
                <div>
                  <p className="font-medium">
                    {crearNuevaFranquicia ? franquiciaNombre : selectedFranquiciaData?.nombre}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {crearNuevaFranquicia ? franquiciaCodigo : selectedFranquiciaData?.codigo}
                    {crearNuevaFranquicia && <Badge variant="outline" className="ml-2 text-[10px]">Nueva</Badge>}
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">Codigo de Nodo *</label>
                  <input
                    type="text"
                    value={codigo}
                    onChange={(e) => setCodigo(e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, ''))}
                    placeholder="NODO_MARBELLA"
                    className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">Nombre *</label>
                  <input
                    type="text"
                    value={nombre}
                    onChange={(e) => setNombre(e.target.value)}
                    placeholder="Conector Marbella - Agora POS"
                    className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium">Tipo de Conector</label>
                <div className="grid grid-cols-1 gap-2">
                  {CONECTORES.map(conector => (
                    <button
                      key={conector.id}
                      type="button"
                      onClick={() => {
                        setTipoConector(conector.id)
                        if (conector.id === 'FILE_UPLOAD') {
                          setModo('FILE')
                          setConnectionType('TXT_PARSER')
                        }
                      }}
                      className={`flex items-center gap-3 p-3 rounded-lg border text-left transition-all ${
                        tipoConector === conector.id
                          ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                          : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                      }`}
                    >
                      {conector.icon === 'upload' ? (
                        <Upload className={`w-5 h-5 ${tipoConector === conector.id ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                      ) : conector.icon === 'file' ? (
                        <FileJson className={`w-5 h-5 ${tipoConector === conector.id ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                      ) : (
                        <Globe className={`w-5 h-5 ${tipoConector === conector.id ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                      )}
                      <div>
                        <p className="font-medium">{conector.label}</p>
                        <p className="text-xs text-muted-foreground">{conector.description}</p>
                      </div>
                    </button>
                  ))}
                </div>
              </div>

              {/* Parser selector - only for FILE_UPLOAD */}
              {tipoConector === 'FILE_UPLOAD' && (
                <div className="space-y-2">
                  <label className="text-sm font-medium">Tipo de Parser</label>
                  <div className="grid grid-cols-1 gap-2">
                    {PARSERS.map(parser => (
                      <button
                        key={parser.code}
                        type="button"
                        onClick={() => setParserCode(parser.code)}
                        className={`flex items-center gap-3 p-3 rounded-lg border text-left transition-all ${
                          parserCode === parser.code
                            ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                            : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                        }`}
                      >
                        <div className="flex-1">
                          <p className="font-medium">{parser.name}</p>
                          <p className="text-xs text-muted-foreground">{parser.description}</p>
                        </div>
                        <Badge variant="outline" className="text-xs">{parser.extensions}</Badge>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Modo de Conexion - hide for FILE_UPLOAD since it's automatic */}
              {tipoConector !== 'FILE_UPLOAD' && (
                <div className="space-y-2">
                  <label className="text-sm font-medium">Modo de Conexion</label>
                  <div className="grid grid-cols-2 gap-2">
                    {MODOS.map(m => (
                      <button
                        key={m.id}
                        type="button"
                        onClick={() => {
                          setModo(m.id as typeof modo)
                          setConnectionType(m.id === 'FILE' ? 'CLOUD_FILE' : 'API_REST')
                        }}
                        className={`flex items-center gap-3 p-3 rounded-lg border text-left transition-all ${
                          modo === m.id
                            ? 'border-cabrera-burgundy bg-cabrera-burgundy/5'
                            : 'border-gray-200 dark:border-gray-700 hover:border-gray-300'
                        }`}
                      >
                        {m.id === 'FILE' ? (
                          <FileJson className={`w-5 h-5 ${modo === m.id ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                        ) : (
                          <Globe className={`w-5 h-5 ${modo === m.id ? 'text-cabrera-burgundy' : 'text-gray-400'}`} />
                        )}
                        <div>
                          <p className="font-medium text-sm">{m.label}</p>
                          <p className="text-xs text-muted-foreground">{m.description}</p>
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Timezone y Moneda (solo para franquicia existente) */}
              {!crearNuevaFranquicia && (
                <div className="grid grid-cols-2 gap-4 pt-2 border-t">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Zona Horaria</label>
                    <Select value={conexionTimezone} onValueChange={setConexionTimezone}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {TIMEZONES.map(tz => (
                          <SelectItem key={tz.id} value={tz.id}>{tz.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium">Moneda</label>
                    <Select value={conexionMoneda} onValueChange={setConexionMoneda}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {MONEDAS.map(m => (
                          <SelectItem key={m.id} value={m.id}>{m.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          {step > 1 && (
            <Button variant="outline" onClick={() => setStep(step - 1)}>
              Anterior
            </Button>
          )}
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          {step < 2 ? (
            <Button
              onClick={() => setStep(step + 1)}
              disabled={!canProceedStep1}
              className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
            >
              Siguiente
            </Button>
          ) : (
            <Button
              onClick={handleSave}
              disabled={saving || !canProceedStep2}
              className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
            >
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                  Creando...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-2" />
                  Crear Conexion
                </>
              )}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
