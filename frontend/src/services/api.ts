import axios from 'axios'
import type {
  HomeDashboard,
  VentasResumenDiario,
  VentasPorFranquicia,
  VentasPorProducto,
  VentasPorMozo,
  VentasPorTipoPlato,
  OcupacionMesas,
  EstadoIntegracion,
  DashboardFilters,
  // v1.2 types
  VentasConsolidadas,
  VentasPorProductoConPeso,
  VentasPorMealPeriod,
  DiaRanking,
  VentasDelDia,
  ComparativoMensual,
  // v1.3 types
  VentasPorHora,
  // Transacciones
  Transaccion,
  TransaccionDetalle,
} from '@/types/dashboard'
import type {
  NodoConexion,
  NodoConexionCreate,
  NodoDetalle,
  MapeoCategoria,
  MapeoMedioPago,
  FieldMapping,
} from '@/types/conexiones'
import type {
  AuthResponse,
  LoginRequest,
  GoogleLoginRequest,
  User,
  UserList,
  Rol,
  RegisterRequest,
  UpdateUserRequest,
  ChangePasswordRequest,
} from '@/types/auth'

// Resultado de ejecución
export interface EjecucionResult {
  success: boolean
  message: string
  tickets_procesados: number
  lineas_procesadas: number
  errors_count: number
  ejecucion_id?: number
  batch_id?: number
}

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api/v1'

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Token management - will be set by AuthContext
let accessToken: string | null = null

export const setAccessToken = (token: string | null) => {
  accessToken = token
}

export const getAccessToken = () => accessToken

// Public auth endpoints that don't need token
const PUBLIC_AUTH_ENDPOINTS = ['/auth/login', '/auth/google', '/auth/refresh']

// Request interceptor for logging and auth
apiClient.interceptors.request.use(
  (config) => {
    console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`)

    // Add auth header if we have a token (except for public auth endpoints)
    const isPublicEndpoint = PUBLIC_AUTH_ENDPOINTS.some(
      (endpoint) => config.url?.startsWith(endpoint)
    )
    if (accessToken && !isPublicEndpoint) {
      config.headers.Authorization = `Bearer ${accessToken}`
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('[API Error]', error.response?.data || error.message)
    return Promise.reject(error)
  }
)

function buildQueryString(filters?: DashboardFilters): string {
  if (!filters) return ''

  const params = new URLSearchParams()
  if (filters.fechaDesde) params.append('fechaDesde', filters.fechaDesde)
  if (filters.fechaHasta) params.append('fechaHasta', filters.fechaHasta)
  if (filters.franquiciaId) params.append('franquiciaId', filters.franquiciaId.toString())
  if (filters.grupoEconomicoId) params.append('grupoEconomicoId', filters.grupoEconomicoId.toString())

  const queryString = params.toString()
  return queryString ? `?${queryString}` : ''
}

export const dashboardApi = {
  // Dashboard Home
  async getHomeDashboard(filters?: DashboardFilters): Promise<HomeDashboard[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<HomeDashboard[]>(`/dashboard/home${query}`)
    return response.data
  },

  // Ventas Resumen Diario
  async getVentasResumen(filters?: DashboardFilters): Promise<VentasResumenDiario[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<VentasResumenDiario[]>(`/dashboard/ventas/resumen${query}`)
    return response.data
  },

  // Ventas por Franquicia
  async getVentasPorFranquicia(filters?: DashboardFilters): Promise<VentasPorFranquicia[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<VentasPorFranquicia[]>(`/dashboard/ventas/por-franquicia${query}`)
    return response.data
  },

  // Ventas por Producto
  async getVentasPorProducto(filters?: DashboardFilters): Promise<VentasPorProducto[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<VentasPorProducto[]>(`/dashboard/ventas/por-producto${query}`)
    return response.data
  },

  // Ventas por Mozo
  async getVentasPorMozo(filters?: DashboardFilters): Promise<VentasPorMozo[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<VentasPorMozo[]>(`/dashboard/ventas/por-mozo${query}`)
    return response.data
  },

  // Ventas por Tipo de Plato
  async getVentasPorTipoPlato(filters?: DashboardFilters): Promise<VentasPorTipoPlato[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<VentasPorTipoPlato[]>(`/dashboard/ventas/por-tipo-plato${query}`)
    return response.data
  },

  // Ocupacion de Mesas
  async getOcupacionMesas(filters?: DashboardFilters): Promise<OcupacionMesas[]> {
    const query = buildQueryString(filters)
    const response = await apiClient.get<OcupacionMesas[]>(`/dashboard/ventas/ocupacion-mesas${query}`)
    return response.data
  },

  // Estado de Integracion
  async getEstadoIntegracion(): Promise<EstadoIntegracion[]> {
    const response = await apiClient.get<EstadoIntegracion[]>('/dashboard/integraciones/estado-franquicias')
    return response.data
  },

  // Health check
  async getHealth(): Promise<{ status: string; timestamp: string }> {
    const response = await apiClient.get('/api/health')
    return response.data
  },
}

// Conexiones API
export const conexionesApi = {
  // Obtener todos los nodos
  async getAllNodos(): Promise<NodoConexion[]> {
    const response = await apiClient.get<NodoConexion[]>('/conexiones')
    return response.data
  },

  // Obtener nodo por ID
  async getNodoById(id: number): Promise<NodoConexion> {
    const response = await apiClient.get<NodoConexion>(`/conexiones/${id}`)
    return response.data
  },

  // Obtener nodo por codigo
  async getNodoByCodigo(codigo: string): Promise<NodoConexion> {
    const response = await apiClient.get<NodoConexion>(`/conexiones/codigo/${codigo}`)
    return response.data
  },

  // Obtener detalle completo del nodo
  async getNodoDetalle(id: number): Promise<NodoDetalle> {
    const response = await apiClient.get<NodoDetalle>(`/conexiones/${id}/detalle`)
    return response.data
  },

  // Crear nodo
  async createNodo(nodo: NodoConexionCreate): Promise<{ nodo_conexion_id: number }> {
    const response = await apiClient.post<{ nodo_conexion_id: number }>('/conexiones', nodo)
    return response.data
  },

  // Actualizar estado del nodo
  async updateNodoEstado(id: number, estado: string): Promise<void> {
    await apiClient.patch(`/conexiones/${id}/estado`, { estado })
  },

  // Upsert mapeo de categoria
  async upsertMapeoCategoria(nodoId: number, mapeo: Partial<MapeoCategoria>): Promise<{ mapeo_categoria_id: number }> {
    const response = await apiClient.put<{ mapeo_categoria_id: number }>(
      `/conexiones/${nodoId}/mapeos/categoria`,
      mapeo
    )
    return response.data
  },

  // Upsert mapeo de medio de pago
  async upsertMapeoMedioPago(nodoId: number, mapeo: Partial<MapeoMedioPago>): Promise<{ mapeo_medio_pago_id: number }> {
    const response = await apiClient.put<{ mapeo_medio_pago_id: number }>(
      `/conexiones/${nodoId}/mapeos/medio-pago`,
      mapeo
    )
    return response.data
  },

  // Actualizar mapeos de campos
  async updateFieldMappings(nodoId: number, mappings: FieldMapping[]): Promise<{ field_mappings_count: number }> {
    const response = await apiClient.put<{ field_mappings_count: number }>(
      `/conexiones/${nodoId}/field-mappings`,
      mappings
    )
    return response.data
  },

  // Ejecutar extracción manual
  async ejecutarExtraccion(nodoId: number, fechaNegocio?: string): Promise<EjecucionResult> {
    const params = fechaNegocio ? `?fechaNegocio=${fechaNegocio}` : ''
    const response = await apiClient.post<EjecucionResult>(`/conexiones/${nodoId}/ejecutar${params}`)
    return response.data
  },

  // Obtener catalogos
  async getCategorias(): Promise<string[]> {
    const response = await apiClient.get<string[]>('/conexiones/catalogos/categorias')
    return response.data
  },

  async getMediosPago(): Promise<string[]> {
    const response = await apiClient.get<string[]>('/conexiones/catalogos/medios-pago')
    return response.data
  },
}

// Franquicias API
export interface Franquicia {
  franquicia_id: number
  codigo: string
  nombre: string
  grupo_economico_id: number | null
  grupo_economico_nombre: string | null
  pais: string | null
  ciudad: string | null
  zona_horaria: string | null
  sistema_origen: string | null
  contacto_nombre: string | null
  contacto_email: string | null
  contacto_telefono: string | null
  estado_integracion: string | null
  ultima_sincronizacion: string | null
}

export interface FranquiciaUpdate {
  nombre?: string
  grupo_economico_id?: number
  pais?: string
  ciudad?: string
  sistema_origen?: string
  contacto_nombre?: string
  contacto_email?: string
  contacto_telefono?: string
  zona_horaria?: string
}

export interface GrupoEconomico {
  grupo_economico_id: number
  codigo: string
  nombre: string
  pais: string | null
}

// Preferencias API
export interface PreferenciaColumnas {
  vista_id: string
  columnas_visibles: string[]
  orden_columnas?: string[]
}

export const preferenciasApi = {
  async getColumnPreferences(vistaId: string): Promise<PreferenciaColumnas | null> {
    try {
      const response = await apiClient.get<PreferenciaColumnas>(`/preferencias/columnas/${vistaId}`)
      return response.data
    } catch (error) {
      // Si no hay preferencias guardadas, retornar null
      return null
    }
  },

  async saveColumnPreferences(vistaId: string, columnasVisibles: string[], ordenColumnas?: string[]): Promise<void> {
    await apiClient.put(`/preferencias/columnas/${vistaId}`, {
      columnas_visibles: columnasVisibles,
      orden_columnas: ordenColumnas,
    })
  },
}

export const franquiciasApi = {
  async getAll(): Promise<Franquicia[]> {
    const response = await apiClient.get<Franquicia[]>('/franquicias')
    return response.data
  },

  async getById(id: number): Promise<Franquicia> {
    const response = await apiClient.get<Franquicia>(`/franquicias/${id}`)
    return response.data
  },

  async update(id: number, data: FranquiciaUpdate): Promise<void> {
    await apiClient.put(`/franquicias/${id}`, data)
  },

  async getGruposEconomicos(): Promise<GrupoEconomico[]> {
    const response = await apiClient.get<GrupoEconomico[]>('/grupos-economicos')
    return response.data
  },
}

export default dashboardApi

// v1.2 Extended query builder
function buildQueryStringV2(filters?: DashboardFilters): string {
  if (!filters) return ''

  const params = new URLSearchParams()
  if (filters.fechaDesde) params.append('fechaDesde', filters.fechaDesde)
  if (filters.fechaHasta) params.append('fechaHasta', filters.fechaHasta)
  if (filters.franquiciaId) params.append('franquiciaId', filters.franquiciaId.toString())
  if (filters.grupoEconomicoId) params.append('grupoEconomicoId', filters.grupoEconomicoId.toString())
  if (filters.pais) params.append('pais', filters.pais)
  if (filters.currencyMode) params.append('currencyMode', filters.currencyMode)
  if (filters.taxMode) params.append('taxMode', filters.taxMode)
  if (filters.mealPeriod) params.append('mealPeriod', filters.mealPeriod)
  if (filters.anio) params.append('anio', filters.anio.toString())
  if (filters.mes) params.append('mes', filters.mes.toString())
  // v1.3: Bidirectional filtering
  if (filters.productoId) params.append('productoId', filters.productoId.toString())
  if (filters.hora !== undefined && filters.hora !== null) params.append('hora', filters.hora.toString())

  const queryString = params.toString()
  return queryString ? `?${queryString}` : ''
}

// v1.2 Dashboard API with USD and tax axis support
export const dashboardApiV2 = {
  // Ventas consolidadas con USD y separación de impuestos
  async getVentasConsolidadas(filters?: DashboardFilters): Promise<VentasConsolidadas[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<VentasConsolidadas[]>(`/dashboard/v2/ventas/consolidadas${query}`)
    return response.data
  },

  // Ventas por producto con peso relativo
  async getVentasPorProductoConPeso(filters?: DashboardFilters): Promise<VentasPorProductoConPeso[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<VentasPorProductoConPeso[]>(`/dashboard/v2/ventas/por-producto${query}`)
    return response.data
  },

  // Ventas por turno/período de comida
  async getVentasPorMealPeriod(filters?: DashboardFilters): Promise<VentasPorMealPeriod[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<VentasPorMealPeriod[]>(`/dashboard/v2/ventas/por-turno${query}`)
    return response.data
  },

  // Ranking de días (mejores/peores)
  async getDiaRanking(filters?: DashboardFilters, mejores = true, top = 10): Promise<DiaRanking[]> {
    const query = buildQueryStringV2(filters)
    const separator = query ? '&' : '?'
    const response = await apiClient.get<DiaRanking[]>(`/dashboard/v2/ventas/ranking-dias${query}${separator}mejores=${mejores}&top=${top}`)
    return response.data
  },

  // Ventas del día actual con comparativos
  async getVentasDelDia(filters?: DashboardFilters): Promise<VentasDelDia[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<VentasDelDia[]>(`/dashboard/v2/ventas/hoy${query}`)
    return response.data
  },

  // Comparativo mensual
  async getComparativoMensual(filters?: DashboardFilters): Promise<ComparativoMensual[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<ComparativoMensual[]>(`/dashboard/v2/ventas/comparativo-mensual${query}`)
    return response.data
  },

  // v1.3: Ventas por hora (ClockChart)
  async getVentasPorHora(filters?: DashboardFilters): Promise<VentasPorHora[]> {
    const query = buildQueryStringV2(filters)
    const response = await apiClient.get<VentasPorHora[]>(`/dashboard/v2/ventas/por-hora${query}`)
    return response.data
  },
}

// Transacciones API
export const transaccionesApi = {
  // Obtener transacciones de una franquicia en un rango de fechas
  async getTransaccionesByFranquicia(
    franquiciaId: number,
    fechaDesde: string,
    fechaHasta: string
  ): Promise<Transaccion[]> {
    const params = new URLSearchParams({
      franquiciaId: franquiciaId.toString(),
      fechaDesde,
      fechaHasta,
    })
    const response = await apiClient.get<Transaccion[]>(`/dashboard/transacciones?${params}`)
    return response.data
  },

  // Obtener el detalle de líneas de una transacción
  async getTransaccionDetalle(ticketId: number): Promise<TransaccionDetalle[]> {
    const response = await apiClient.get<TransaccionDetalle[]>(`/dashboard/transacciones/${ticketId}/detalle`)
    return response.data
  },
}

// Auth API
export const authApi = {
  // Login con email/password
  async login(request: LoginRequest): Promise<AuthResponse> {
    const response = await apiClient.post<AuthResponse>('/auth/login', request)
    return response.data
  },

  // Login con Google
  async googleLogin(request: GoogleLoginRequest): Promise<AuthResponse> {
    const response = await apiClient.post<AuthResponse>('/auth/google', request)
    return response.data
  },

  // Refrescar token
  async refreshToken(refreshToken: string): Promise<AuthResponse> {
    const response = await apiClient.post<AuthResponse>('/auth/refresh', { refresh_token: refreshToken })
    return response.data
  },

  // Logout (revocar token)
  async logout(refreshToken: string): Promise<void> {
    await apiClient.post('/auth/logout', { refresh_token: refreshToken })
  },

  // Obtener usuario actual
  async getCurrentUser(): Promise<User> {
    const response = await apiClient.get<User>('/auth/me')
    return response.data
  },

  // Cambiar contraseña
  async changePassword(request: ChangePasswordRequest): Promise<void> {
    await apiClient.post('/auth/change-password', request)
  },

  // --- Admin endpoints ---

  // Listar todos los usuarios (Admin)
  async getAllUsers(): Promise<UserList[]> {
    const response = await apiClient.get<UserList[]>('/auth/users')
    return response.data
  },

  // Crear usuario (Admin)
  async createUser(request: RegisterRequest): Promise<User> {
    const response = await apiClient.post<User>('/auth/users', request)
    return response.data
  },

  // Actualizar usuario (Admin)
  async updateUser(userId: number, request: UpdateUserRequest): Promise<void> {
    await apiClient.put(`/auth/users/${userId}`, request)
  },

  // Desactivar usuario (Admin)
  async deleteUser(userId: number): Promise<void> {
    await apiClient.delete(`/auth/users/${userId}`)
  },

  // Listar roles
  async getRoles(): Promise<Rol[]> {
    const response = await apiClient.get<Rol[]>('/auth/roles')
    return response.data
  },
}
