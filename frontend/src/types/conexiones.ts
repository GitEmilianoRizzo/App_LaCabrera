// Types for Conexiones module (aligned with backend DTOs)

export interface NodoConexion {
  nodo_conexion_id: number
  codigo: string
  nombre: string
  tipo_conector: string
  modo: 'PULL' | 'PUSH' | 'AGENT' | 'FILE'
  estado: 'ACTIVE' | 'PAUSED' | 'ERROR' | 'PENDING_CONFIG'
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  pais: string | null
  ciudad: string | null
  timezone: string
  moneda: string
  cron_expression: string | null
  convencion_importes: string
  ultima_sincronizacion: string | null
  ultimo_estado: 'SUCCESS' | 'ERROR' | 'WARNING' | null
  ultimo_batch_id: string | null
  mapeos_categoria: number
  mapeos_medio_pago: number
  valores_pendientes: number
  tickets_ultima_ejecucion: number | null
  activo: boolean
}

export interface NodoConexionCreate {
  codigo: string
  nombre: string
  franquicia_id: number
  tipo_conector: string
  modo: string
  configuracion?: NodoConfiguracion
  cron_expression?: string
  timezone: string
  moneda: string
  convencion_importes: string
  politica_devoluciones: string
  tolerancia_reconciliacion: number
}

export interface NodoConfiguracion {
  // Tipo de conexion
  connection_type: 'API_REST' | 'CLOUD_FILE'

  // Config para API REST
  api_config?: ApiConnectionConfig

  // Config para archivo en la nube
  cloud_file_config?: CloudFileConfig

  // Configuracion de extraccion (comun)
  extraction_config?: ExtractionConfig

  // Configuracion de sincronizacion
  sync_config?: SyncConfig

  // Mapeo de campos origen -> destino
  field_mappings?: FieldMapping[]
}

// ============================================
// CONFIGURACION DE CONEXION API REST
// ============================================
export interface ApiConnectionConfig {
  base_url: string
  auth_type: 'TOKEN' | 'OAUTH2' | 'BASIC' | 'API_KEY'

  // Para TOKEN simple (como Agora)
  api_token?: string
  token_header?: string  // Default: 'Api-Token'

  // Para OAuth2
  oauth2_config?: OAuth2Config

  // Para Basic Auth
  basic_auth?: BasicAuthConfig

  // Para API Key en query params
  api_key_param?: string
  api_key_value?: string

  // Headers adicionales
  additional_headers?: Record<string, string>

  // Configuracion de red
  timeout_seconds: number
  retry?: RetryConfig

  // Formato de respuesta
  response_format: 'JSON' | 'XML'
  accept_header?: string
}

export interface OAuth2Config {
  token_url: string
  client_id: string
  client_secret: string
  scope?: string
  grant_type: 'client_credentials' | 'password' | 'refresh_token'
  username?: string
  password?: string
  refresh_token?: string
  // Token actual cacheado (actualizado automaticamente)
  current_access_token?: string
  token_expires_at?: string
}

export interface BasicAuthConfig {
  username: string
  password: string
}

export interface RetryConfig {
  max_attempts: number
  backoff_seconds?: number[]
}

// ============================================
// CONFIGURACION DE ARCHIVO EN LA NUBE
// ============================================
export interface CloudFileConfig {
  provider: 'GOOGLE_DRIVE' | 'ONEDRIVE' | 'DROPBOX' | 'S3'

  // Google Drive
  google_drive_config?: GoogleDriveConfig

  // OneDrive
  onedrive_config?: OneDriveConfig

  // S3
  s3_config?: S3Config

  // Patron de nombre de archivo
  file_name_pattern: string  // Ej: 'LC_{franchise_code}_{business_date}_*.json'

  // Despues de procesar
  after_processing: 'DELETE' | 'MOVE' | 'KEEP'
  processed_folder_id?: string
}

export interface GoogleDriveConfig {
  folder_id: string
  service_account_json?: string  // Credenciales de service account
  oauth_refresh_token?: string   // O usando OAuth
}

export interface OneDriveConfig {
  folder_path: string  // Ej: '/Shared/LA_CABRERA/Ventas'
  client_id: string
  client_secret: string
  tenant_id: string
  refresh_token?: string
}

export interface S3Config {
  bucket: string
  prefix: string
  region: string
  access_key_id: string
  secret_access_key: string
}

// ============================================
// CONFIGURACION DE EXTRACCION
// ============================================
export interface ExtractionConfig {
  // Endpoint de exportacion (para API)
  export_endpoint?: string  // Ej: '/api/export/'

  // Filtros
  export_filter?: string    // Ej: 'Invoices'
  include_processed: boolean
  mark_processed_after_accept: boolean
  processed_endpoint?: string  // Ej: '/api/doc/processed'

  // Filtrar por locales (Agora workplaces)
  workplace_ids?: number[]

  // Endpoint de datos maestros
  master_data_endpoint?: string  // Ej: '/api/export-master/'
}

// ============================================
// CONFIGURACION DE SINCRONIZACION
// ============================================
export interface SyncConfig {
  // Cron o intervalo
  schedule_type: 'CRON' | 'INTERVAL'
  cron_expression?: string           // Ej: '0 3 * * *' (todos los dias a las 3am)
  interval_minutes?: number          // Ej: 60 (cada hora)

  // Ventana de negocio
  business_day_start_hour: number    // Ej: 6 (6am)
  business_day_end_hour: number      // Ej: 6 (6am siguiente = 24h)

  // Reintentos automaticos
  auto_retry_on_error: boolean
  max_retries_per_day: number

  // Alertas
  alert_on_failure: boolean
  alert_email?: string
}

// ============================================
// MAPEO DE CAMPOS ORIGEN -> DESTINO
// ============================================
export interface FieldMapping {
  mapping_id: string  // UUID

  // Campo de destino (nuestro schema)
  target_entity: 'TICKET' | 'ITEM' | 'PAYMENT' | 'DISCOUNT' | 'BATCH_HEADER'
  target_field: string  // Ej: 'ticket_id', 'product_name', etc.

  // Campo de origen (POS)
  source_path: string   // JSONPath o XPath, Ej: 'Invoice.Serie', 'InvoiceItems[*].Lines[*].ProductName'

  // Transformacion opcional
  transformation?: FieldTransformation

  // Valor por defecto si no viene
  default_value?: string | number | boolean | null

  // Es requerido?
  required: boolean

  // Descripcion
  description?: string
}

export interface FieldTransformation {
  type: TransformationType

  // Parametros segun el tipo
  params?: Record<string, unknown>
}

export type TransformationType =
  | 'NONE'                    // Sin transformacion
  | 'UPPERCASE'               // Convertir a mayusculas
  | 'LOWERCASE'               // Convertir a minusculas
  | 'TRIM'                    // Quitar espacios
  | 'CONCAT'                  // Concatenar campos: params: { fields: ['Serie', 'Number'], separator: '-' }
  | 'SPLIT'                   // Separar: params: { separator: '-', index: 0 }
  | 'REPLACE'                 // Reemplazar: params: { search: 'X', replace: 'Y' }
  | 'REGEX_EXTRACT'           // Extraer con regex: params: { pattern: '...', group: 1 }
  | 'DATE_FORMAT'             // Formatear fecha: params: { input_format: 'YYYY-MM-DDTHH:mm:ss', output_format: 'YYYY-MM-DD' }
  | 'ADD_TIMEZONE'            // Agregar timezone: params: { timezone: 'Europe/Madrid' }
  | 'NUMBER_FORMAT'           // Formatear numero: params: { decimals: 2, multiply_by: 100 }
  | 'INVERT_SIGN'             // Invertir signo (para devoluciones)
  | 'MAP_VALUE'               // Mapear valor: params: { mapping: { 'A': 'X', 'B': 'Y' }, default: 'OTHER' }
  | 'CONDITIONAL'             // Condicional: params: { condition: 'field > 0', then: 'POSITIVE', else: 'NEGATIVE' }
  | 'CALCULATE'               // Calcular: params: { formula: 'Quantity * UnitPrice' }
  | 'CUSTOM_JS'               // JavaScript personalizado (avanzado): params: { code: 'return value.toUpperCase()' }

// ============================================
// SCHEMAS DE CAMPOS CONOCIDOS (para UI)
// ============================================
export interface SourceSchema {
  name: string              // Ej: 'AGORA_POS'
  version: string           // Ej: '8.7.2'
  entities: SourceEntity[]
}

export interface SourceEntity {
  name: string              // Ej: 'Invoice', 'Line', 'Payment'
  path: string              // Ej: 'Invoices[*]', 'InvoiceItems[*].Lines[*]'
  fields: SourceField[]
}

export interface SourceField {
  name: string              // Ej: 'Serie', 'ProductName'
  path: string              // Ej: 'Invoice.Serie'
  type: 'string' | 'number' | 'boolean' | 'date' | 'datetime' | 'array' | 'object'
  description?: string
  example?: unknown
}

// Schema de destino (nuestro schema LA CABRERA)
export interface TargetSchema {
  name: string              // 'LA_CABRERA_DAILY_BATCH'
  version: string           // '1.0'
  entities: TargetEntity[]
}

export interface TargetEntity {
  name: 'BATCH_HEADER' | 'TICKET' | 'ITEM' | 'PAYMENT' | 'DISCOUNT'
  fields: TargetField[]
}

export interface TargetField {
  name: string
  type: 'string' | 'number' | 'boolean' | 'date' | 'datetime' | 'array' | 'object'
  required: boolean
  description?: string
  enum_values?: string[]    // Para campos con valores fijos
}

export interface MapeoCategoria {
  mapeo_categoria_id: number
  nodo_conexion_id: number
  codigo_origen: string
  nombre_origen: string | null
  categoria_destino: string
  familia_destino: string | null
  subfamilia_destino: string | null
  verificado: boolean
}

export interface MapeoMedioPago {
  mapeo_medio_pago_id: number
  nodo_conexion_id: number
  codigo_origen: string
  nombre_origen: string | null
  medio_pago_destino: string
  marca_tarjeta: string | null
  verificado: boolean
}

export interface ValorNoMapeado {
  valor_no_mapeado_id: number
  nodo_conexion_id: number
  tipo_mapeo: string
  codigo_origen: string
  nombre_origen: string | null
  ocurrencias: number
  primera_vez: string
  ultima_vez: string
}

export interface EjecucionNodo {
  ejecucion_nodo_id: number
  nodo_conexion_id: number
  ejecucion_id: string
  inicio_ejecucion: string
  fin_ejecucion: string | null
  fecha_negocio: string
  estado: 'RUNNING' | 'SUCCESS' | 'ERROR' | 'WARNING' | 'CANCELLED'
  modo_ejecucion: 'SCHEDULED' | 'MANUAL' | 'CATCHUP' | 'DRY_RUN' | null
  tickets_procesados: number
  lineas_procesadas: number
  warnings_count: number
  errors_count: number
  batch_id: string | null
  batch_estado: string | null
  api_version_origen: string | null
}

export interface NodoDetalle {
  nodo: NodoConexion
  configuracion: NodoConfiguracion | null
  mapeos_categoria: MapeoCategoria[]
  mapeos_medio_pago: MapeoMedioPago[]
  valores_no_mapeados: ValorNoMapeado[]
  ultimas_ejecuciones: EjecucionNodo[]
}

// Constants for standard values
export const CATEGORIAS_PRODUCTO = [
  'STARTER',
  'MAIN_COURSE',
  'SIDE_DISH',
  'DESSERT',
  'COFFEE',
  'BEVERAGE',
  'WINE',
  'COCKTAIL',
  'OTHER',
] as const

export const MEDIOS_PAGO = [
  'CASH',
  'DEBIT_CARD',
  'CREDIT_CARD',
  'BANK_TRANSFER',
  'QR',
  'MERCADO_PAGO',
  'DELIVERY_APP',
  'OTHER',
] as const

export const ESTADOS_NODO = ['ACTIVE', 'PAUSED', 'ERROR', 'PENDING_CONFIG'] as const
export const MODOS_NODO = ['PULL', 'PUSH', 'AGENT', 'FILE'] as const
