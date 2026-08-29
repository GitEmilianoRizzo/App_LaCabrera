import { useState, useMemo, useEffect } from 'react'
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
  ArrowRight,
  Plus,
  Trash2,
  Edit2,
  ChevronDown,
  ChevronUp,
  Wand2,
  AlertTriangle,
  CheckCircle2,
  Copy,
  Info,
  Search,
} from 'lucide-react'
import type { FieldMapping, FieldTransformation, TransformationType } from '@/types/conexiones'
import { AGORA_SCHEMA, VINSON_SCHEMA, LACABRERA_SCHEMA, TRANSFORMATIONS, DEFAULT_AGORA_MAPPINGS, DEFAULT_VINSON_MAPPINGS } from '@/lib/schemas'

interface FieldMappingEditorProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  connectionId: number
  connectionName: string
  connectorType: string
  currentMappings?: FieldMapping[]
  onSave: (mappings: FieldMapping[]) => Promise<void>
}

type EntityType = 'TICKET' | 'ITEM' | 'PAYMENT' | 'DISCOUNT' | 'BATCH_HEADER'

export function FieldMappingEditor({
  open,
  onOpenChange,
  connectionId,
  connectionName,
  connectorType,
  currentMappings,
  onSave,
}: FieldMappingEditorProps) {
  const [saving, setSaving] = useState(false)
  // Initialize with currentMappings from backend (if available) or empty array
  const [mappings, setMappings] = useState<FieldMapping[]>(() => {
    if (currentMappings && currentMappings.length > 0) {
      // Add mapping_id if not present
      return currentMappings.map((m, i) => ({
        ...m,
        mapping_id: m.mapping_id || `db-${i}`,
      }))
    }
    return []
  })
  const [expandedEntity, setExpandedEntity] = useState<EntityType | null>('TICKET')
  const [editingMapping, setEditingMapping] = useState<FieldMapping | null>(null)
  const [searchTerm, setSearchTerm] = useState('')

  // Sync mappings when currentMappings changes (e.g., when modal opens with new data)
  useEffect(() => {
    if (open && currentMappings && currentMappings.length > 0) {
      setMappings(currentMappings.map((m, i) => ({
        ...m,
        mapping_id: m.mapping_id || `db-${i}`,
      })))
    }
  }, [open, currentMappings])

  // Get mappings grouped by entity
  const mappingsByEntity = useMemo(() => {
    const grouped: Record<EntityType, FieldMapping[]> = {
      BATCH_HEADER: [],
      TICKET: [],
      ITEM: [],
      PAYMENT: [],
      DISCOUNT: [],
    }
    mappings.forEach(m => {
      if (grouped[m.target_entity]) {
        grouped[m.target_entity].push(m)
      }
    })
    return grouped
  }, [mappings])

  // Get target fields for an entity
  const getTargetFieldsForEntity = (entity: EntityType) => {
    const entityDef = LACABRERA_SCHEMA.entities.find(e => e.name === entity)
    return entityDef?.fields || []
  }

  // Get the appropriate schema based on connector type
  const sourceSchema = useMemo(() => {
    if (connectorType === 'VINSON') return VINSON_SCHEMA
    return AGORA_SCHEMA // Default to AGORA for AGORA_HTTP and others
  }, [connectorType])

  // Get source fields from the appropriate schema
  const sourceFields = useMemo(() => {
    const fields: { path: string; name: string; type: string; entity: string; description?: string }[] = []
    sourceSchema.entities.forEach(entity => {
      entity.fields.forEach(field => {
        fields.push({
          path: field.path,
          name: field.name,
          type: field.type,
          entity: entity.name,
          description: field.description,
        })
      })
    })
    return fields
  }, [sourceSchema])

  // Filter source fields by search term
  const filteredSourceFields = useMemo(() => {
    if (!searchTerm) return sourceFields
    const lower = searchTerm.toLowerCase()
    return sourceFields.filter(f =>
      f.name.toLowerCase().includes(lower) ||
      f.path.toLowerCase().includes(lower) ||
      f.entity.toLowerCase().includes(lower) ||
      (f.description?.toLowerCase().includes(lower))
    )
  }, [sourceFields, searchTerm])

  // Get the appropriate default mappings based on connector type
  const defaultMappings = useMemo(() => {
    if (connectorType === 'VINSON') return DEFAULT_VINSON_MAPPINGS
    return DEFAULT_AGORA_MAPPINGS
  }, [connectorType])

  // Get connector label for the button
  const connectorLabel = useMemo(() => {
    if (connectorType === 'VINSON') return 'Vinson'
    if (connectorType === 'AGORA_HTTP') return 'Agora'
    return connectorType
  }, [connectorType])

  // Load default mappings
  const handleLoadDefaults = () => {
    const defaults = defaultMappings.map((m, i) => ({
      ...m,
      mapping_id: `default-${i}`,
      transformation: m.transformation as FieldTransformation | undefined,
      default_value: (m as { default_value?: unknown }).default_value ?? null,
      description: (m as { description?: string }).description,
    })) as FieldMapping[]
    setMappings(defaults)
  }

  // Save mapping being edited
  const handleSaveMapping = () => {
    if (!editingMapping) return

    setMappings(prev => {
      const existing = prev.findIndex(m => m.mapping_id === editingMapping.mapping_id)
      if (existing >= 0) {
        const updated = [...prev]
        updated[existing] = editingMapping
        return updated
      }
      return [...prev, editingMapping]
    })
    setEditingMapping(null)
  }

  // Delete mapping
  const handleDeleteMapping = (mappingId: string) => {
    setMappings(prev => prev.filter(m => m.mapping_id !== mappingId))
  }

  // Save all mappings
  const handleSave = async () => {
    setSaving(true)
    try {
      await onSave(mappings)
      onOpenChange(false)
    } finally {
      setSaving(false)
    }
  }

  // Check if a target field has a mapping
  const hasMapping = (entity: EntityType, field: string) => {
    return mappings.some(m => m.target_entity === entity && m.target_field === field)
  }

  // Get mapping for a target field
  const getMapping = (entity: EntityType, field: string) => {
    return mappings.find(m => m.target_entity === entity && m.target_field === field)
  }

  // Render transformation badge
  const renderTransformationBadge = (transformation?: FieldTransformation) => {
    if (!transformation || transformation.type === 'NONE') return null
    const def = TRANSFORMATIONS.find(t => t.type === transformation.type)
    return (
      <Badge variant="outline" className="text-xs">
        <Wand2 className="w-3 h-3 mr-1" />
        {def?.label || transformation.type}
      </Badge>
    )
  }

  const entities: { id: EntityType; label: string; description: string }[] = [
    { id: 'TICKET', label: 'Ticket', description: 'Datos del ticket/factura' },
    { id: 'ITEM', label: 'Item (Linea)', description: 'Productos vendidos' },
    { id: 'PAYMENT', label: 'Pago', description: 'Medios de pago' },
    { id: 'DISCOUNT', label: 'Descuento', description: 'Descuentos aplicados' },
  ]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-5xl max-h-[90vh] overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ArrowRight className="w-5 h-5" />
            Mapeo de Campos: {connectionName}
          </DialogTitle>
          <DialogDescription>
            Configure como se traducen los campos del sistema {connectorType} a nuestro formato estandar.
          </DialogDescription>
        </DialogHeader>

        <div className="flex gap-2 pb-4 border-b">
          <Button variant="outline" size="sm" onClick={handleLoadDefaults}>
            <Copy className="w-4 h-4 mr-2" />
            Cargar Mapeo Default {connectorLabel}
          </Button>
          <div className="flex-1" />
          <Badge variant="secondary">
            {mappings.length} campos mapeados
          </Badge>
        </div>

        <div className="flex-1 overflow-y-auto py-4">
          {/* Entity Sections */}
          <div className="space-y-3">
            {entities.map(entity => {
              const entityMappings = mappingsByEntity[entity.id]
              const targetFields = getTargetFieldsForEntity(entity.id)
              const requiredFields = targetFields.filter(f => f.required)
              const mappedRequired = requiredFields.filter(f => hasMapping(entity.id, f.name))
              const isExpanded = expandedEntity === entity.id

              return (
                <div key={entity.id} className="border rounded-lg overflow-hidden">
                  {/* Entity Header */}
                  <button
                    onClick={() => setExpandedEntity(isExpanded ? null : entity.id)}
                    className="w-full flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                  >
                    <div className="flex items-center gap-3">
                      <div>
                        <h3 className="font-medium text-left">{entity.label}</h3>
                        <p className="text-xs text-muted-foreground">{entity.description}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-3">
                      {mappedRequired.length === requiredFields.length ? (
                        <Badge className="bg-green-100 text-green-800">
                          <CheckCircle2 className="w-3 h-3 mr-1" />
                          {entityMappings.length} mapeados
                        </Badge>
                      ) : (
                        <Badge className="bg-amber-100 text-amber-800">
                          <AlertTriangle className="w-3 h-3 mr-1" />
                          {mappedRequired.length}/{requiredFields.length} requeridos
                        </Badge>
                      )}
                      {isExpanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                    </div>
                  </button>

                  {/* Entity Fields */}
                  {isExpanded && (
                    <div className="p-4 space-y-2">
                      {targetFields.map(field => {
                        const mapping = getMapping(entity.id, field.name)
                        return (
                          <div
                            key={field.name}
                            className={`flex items-center gap-3 p-3 rounded-lg border transition-colors ${
                              mapping
                                ? 'border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-900/20'
                                : field.required
                                ? 'border-amber-200 bg-amber-50 dark:border-amber-800 dark:bg-amber-900/20'
                                : 'border-gray-200 dark:border-gray-700'
                            }`}
                          >
                            {/* Target Field */}
                            <div className="w-1/3">
                              <div className="flex items-center gap-2">
                                <span className="font-mono text-sm">{field.name}</span>
                                {field.required && (
                                  <Badge variant="destructive" className="text-[10px] px-1 py-0">REQ</Badge>
                                )}
                              </div>
                              <p className="text-xs text-muted-foreground">{field.description}</p>
                              {field.enum_values && (
                                <p className="text-xs text-blue-600 dark:text-blue-400">
                                  Valores: {field.enum_values.slice(0, 3).join(', ')}...
                                </p>
                              )}
                            </div>

                            {/* Arrow */}
                            <ArrowRight className={`w-4 h-4 flex-shrink-0 ${mapping ? 'text-green-500' : 'text-gray-300'}`} />

                            {/* Source Field / Mapping */}
                            <div className="flex-1">
                              {mapping ? (
                                <div className="flex items-center gap-2">
                                  <span className="font-mono text-sm text-green-700 dark:text-green-300">
                                    {mapping.source_path || <span className="italic text-gray-400">Sin origen</span>}
                                  </span>
                                  {renderTransformationBadge(mapping.transformation)}
                                  {mapping.default_value !== undefined && mapping.default_value !== null && (
                                    <Badge variant="outline" className="text-xs">
                                      Default: {String(mapping.default_value)}
                                    </Badge>
                                  )}
                                </div>
                              ) : (
                                <span className="text-sm text-muted-foreground italic">No mapeado</span>
                              )}
                            </div>

                            {/* Actions */}
                            <div className="flex gap-1">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setEditingMapping(mapping || {
                                  mapping_id: `new-${Date.now()}`,
                                  target_entity: entity.id,
                                  target_field: field.name,
                                  source_path: '',
                                  required: field.required,
                                })}
                              >
                                {mapping ? <Edit2 className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
                              </Button>
                              {mapping && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => handleDeleteMapping(mapping.mapping_id)}
                                >
                                  <Trash2 className="w-4 h-4 text-red-500" />
                                </Button>
                              )}
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>

        <DialogFooter className="border-t pt-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
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
                Guardar Mapeos
              </>
            )}
          </Button>
        </DialogFooter>

        {/* Edit Mapping Modal */}
        {editingMapping && (
          <Dialog open={true} onOpenChange={() => setEditingMapping(null)}>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>
                  {editingMapping.source_path ? 'Editar' : 'Nuevo'} Mapeo: {editingMapping.target_field}
                </DialogTitle>
                <DialogDescription>
                  Entidad: {editingMapping.target_entity}
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-4 py-4">
                {/* Search Source Fields */}
                <div className="space-y-2">
                  <label className="text-sm font-medium">Campo Origen (Sistema POS)</label>
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                    <input
                      type="text"
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                      placeholder="Buscar campo..."
                      className="w-full pl-10 pr-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>

                {/* Source Field List */}
                <div className="max-h-48 overflow-y-auto border rounded-lg">
                  {filteredSourceFields.map((field, i) => (
                    <button
                      key={i}
                      type="button"
                      onClick={() => setEditingMapping({ ...editingMapping, source_path: field.path })}
                      className={`w-full flex items-center justify-between p-2 text-left hover:bg-gray-100 dark:hover:bg-gray-800 border-b last:border-b-0 ${
                        editingMapping.source_path === field.path ? 'bg-cabrera-burgundy/10' : ''
                      }`}
                    >
                      <div>
                        <span className="font-mono text-sm">{field.path}</span>
                        <p className="text-xs text-muted-foreground">{field.entity}: {field.description}</p>
                      </div>
                      <Badge variant="outline" className="text-xs">{field.type}</Badge>
                    </button>
                  ))}
                </div>

                {/* Manual Path Input */}
                <div className="space-y-2">
                  <label className="text-sm font-medium">O escribir path manualmente:</label>
                  <input
                    type="text"
                    value={editingMapping.source_path}
                    onChange={(e) => setEditingMapping({ ...editingMapping, source_path: e.target.value })}
                    placeholder="Ej: Invoice.Totals.GrossAmount"
                    className="w-full px-3 py-2 border rounded-md text-sm font-mono bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                {/* Transformation */}
                <div className="space-y-2">
                  <label className="text-sm font-medium">Transformacion</label>
                  <Select
                    value={editingMapping.transformation?.type || 'NONE'}
                    onValueChange={(v) => setEditingMapping({
                      ...editingMapping,
                      transformation: v === 'NONE' ? undefined : { type: v as TransformationType }
                    })}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {TRANSFORMATIONS.map(t => (
                        <SelectItem key={t.type} value={t.type}>
                          <div className="flex items-center gap-2">
                            <Wand2 className="w-3 h-3" />
                            <span>{t.label}</span>
                            <span className="text-xs text-muted-foreground">- {t.description}</span>
                          </div>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Transformation Params (simplified for now) */}
                {editingMapping.transformation && editingMapping.transformation.type !== 'NONE' && (
                  <div className="p-3 bg-blue-50 dark:bg-blue-900/20 rounded-lg border border-blue-200 dark:border-blue-800">
                    <p className="text-sm text-blue-800 dark:text-blue-300 flex items-start gap-2">
                      <Info className="w-4 h-4 mt-0.5 flex-shrink-0" />
                      <span>
                        Los parametros de transformacion avanzados se pueden configurar en el JSON de configuracion.
                      </span>
                    </p>
                  </div>
                )}

                {/* Default Value */}
                <div className="space-y-2">
                  <label className="text-sm font-medium">Valor por defecto (si no viene)</label>
                  <input
                    type="text"
                    value={editingMapping.default_value !== null && editingMapping.default_value !== undefined
                      ? String(editingMapping.default_value)
                      : ''
                    }
                    onChange={(e) => setEditingMapping({
                      ...editingMapping,
                      default_value: e.target.value || null
                    })}
                    placeholder="Dejar vacio si no hay default"
                    className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                {/* Description */}
                <div className="space-y-2">
                  <label className="text-sm font-medium">Notas / Descripcion</label>
                  <input
                    type="text"
                    value={editingMapping.description || ''}
                    onChange={(e) => setEditingMapping({ ...editingMapping, description: e.target.value })}
                    placeholder="Notas sobre este mapeo..."
                    className="w-full px-3 py-2 border rounded-md text-sm bg-background dark:bg-gray-900 dark:border-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => setEditingMapping(null)}>
                  Cancelar
                </Button>
                <Button
                  onClick={handleSaveMapping}
                  disabled={!editingMapping.source_path && !editingMapping.default_value}
                  className="bg-cabrera-burgundy hover:bg-cabrera-burgundy/90"
                >
                  <Save className="w-4 h-4 mr-2" />
                  Guardar Campo
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        )}
      </DialogContent>
    </Dialog>
  )
}
