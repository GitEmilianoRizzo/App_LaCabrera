// Auth Types

export interface User {
  user_id: number
  email: string
  nombre: string
  apellido: string | null
  nombre_completo: string
  rol: Rol
  google_picture_url: string | null
  activo: boolean
  fecha_ultimo_acceso: string | null
  fecha_creacion: string
}

export interface Rol {
  rol_id: number
  codigo: string
  nombre: string
  descripcion: string | null
}

export interface UserList {
  user_id: number
  email: string
  nombre: string
  apellido: string | null
  rol_codigo: string
  rol_nombre: string
  activo: boolean
  tiene_google: boolean
  fecha_ultimo_acceso: string | null
  fecha_creacion: string
}

export interface AuthResponse {
  access_token: string
  refresh_token: string
  expires_in: number
  token_type: string
  user: User
}

export interface LoginRequest {
  email: string
  password: string
}

export interface GoogleLoginRequest {
  credential: string
}

export interface RegisterRequest {
  email: string
  password: string
  nombre: string
  apellido?: string
  rol_codigo: string
}

export interface UpdateUserRequest {
  nombre?: string
  apellido?: string
  rol_codigo?: string
  activo?: boolean
}

export interface ChangePasswordRequest {
  current_password: string
  new_password: string
}

export interface RefreshTokenRequest {
  refresh_token: string
}

// Auth state for context
export interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isAuthenticated: boolean
  isLoading: boolean
}
