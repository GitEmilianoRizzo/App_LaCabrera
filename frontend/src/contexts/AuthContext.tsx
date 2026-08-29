import { createContext, useContext, useEffect, useState, useCallback, ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi, setAccessToken } from '@/services/api'
import type { User, LoginRequest, GoogleLoginRequest } from '@/types/auth'

// Storage keys
const STORAGE_KEYS = {
  ACCESS_TOKEN: 'lacabrera-access-token',
  REFRESH_TOKEN: 'lacabrera-refresh-token',
  USER: 'lacabrera-user',
}

interface AuthContextType {
  user: User | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (request: LoginRequest) => Promise<void>
  googleLogin: (request: GoogleLoginRequest) => Promise<void>
  logout: () => Promise<void>
  refreshAuth: () => Promise<boolean>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [refreshToken, setRefreshToken] = useState<string | null>(null)

  // Initialize auth state from localStorage
  useEffect(() => {
    const initAuth = async () => {
      try {
        const storedToken = localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN)
        const storedRefresh = localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN)
        const storedUser = localStorage.getItem(STORAGE_KEYS.USER)

        if (storedToken && storedRefresh && storedUser) {
          setAccessToken(storedToken)
          setRefreshToken(storedRefresh)
          setUser(JSON.parse(storedUser))

          // Verify token is still valid by fetching current user
          try {
            const currentUser = await authApi.getCurrentUser()
            setUser(currentUser)
            localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(currentUser))
          } catch {
            // Token might be expired, try to refresh
            try {
              const response = await authApi.refreshToken(storedRefresh)
              setAccessToken(response.access_token)
              setRefreshToken(response.refresh_token)
              setUser(response.user)

              localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, response.access_token)
              localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, response.refresh_token)
              localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(response.user))
            } catch {
              // Refresh failed, clear auth
              clearAuth()
            }
          }
        }
      } catch (error) {
        console.error('Error initializing auth:', error)
        clearAuth()
      } finally {
        setIsLoading(false)
      }
    }

    initAuth()
  }, [])

  const clearAuth = useCallback(() => {
    setAccessToken(null)
    setRefreshToken(null)
    setUser(null)
    localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN)
    localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
    localStorage.removeItem(STORAGE_KEYS.USER)
  }, [])

  const login = useCallback(async (request: LoginRequest) => {
    setIsLoading(true)
    try {
      const response = await authApi.login(request)

      setAccessToken(response.access_token)
      setRefreshToken(response.refresh_token)
      setUser(response.user)

      localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, response.access_token)
      localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, response.refresh_token)
      localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(response.user))
    } finally {
      setIsLoading(false)
    }
  }, [])

  const googleLogin = useCallback(async (request: GoogleLoginRequest) => {
    setIsLoading(true)
    try {
      const response = await authApi.googleLogin(request)

      setAccessToken(response.access_token)
      setRefreshToken(response.refresh_token)
      setUser(response.user)

      localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, response.access_token)
      localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, response.refresh_token)
      localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(response.user))
    } finally {
      setIsLoading(false)
    }
  }, [])

  const logout = useCallback(async () => {
    try {
      if (refreshToken) {
        await authApi.logout(refreshToken)
      }
    } catch (error) {
      console.error('Logout error:', error)
    } finally {
      clearAuth()
    }
  }, [refreshToken, clearAuth])

  const refreshAuth = useCallback(async (): Promise<boolean> => {
    if (!refreshToken) return false

    try {
      const response = await authApi.refreshToken(refreshToken)

      setAccessToken(response.access_token)
      setRefreshToken(response.refresh_token)
      setUser(response.user)

      localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, response.access_token)
      localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, response.refresh_token)
      localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(response.user))

      return true
    } catch {
      clearAuth()
      return false
    }
  }, [refreshToken, clearAuth])

  const value: AuthContextType = {
    user,
    isAuthenticated: !!user,
    isLoading,
    login,
    googleLogin,
    logout,
    refreshAuth,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

// Protected Route component
interface ProtectedRouteProps {
  children: ReactNode
  requiredRoles?: string[]
}

export function ProtectedRoute({ children, requiredRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      navigate('/login', { replace: true })
    }
  }, [isLoading, isAuthenticated, navigate])

  // Check role if required
  if (requiredRoles && user) {
    const hasRole = requiredRoles.includes(user.rol.codigo)
    if (!hasRole) {
      return (
        <div className="flex items-center justify-center h-screen">
          <div className="text-center">
            <h1 className="text-2xl font-bold text-red-600">Acceso Denegado</h1>
            <p className="text-muted-foreground mt-2">
              No tienes permisos para acceder a esta página.
            </p>
          </div>
        </div>
      )
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-muted-foreground">Verificando sesión...</p>
        </div>
      </div>
    )
  }

  if (!isAuthenticated) {
    return null
  }

  return <>{children}</>
}
