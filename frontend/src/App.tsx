import { Routes, Route, Navigate } from 'react-router-dom'
import { MainLayout } from './components/layout/MainLayout'
import { ProtectedRoute, useAuth } from './contexts/AuthContext'
import { Login } from './pages/Login'
import { DashboardHome } from './pages/DashboardHome'
import { VentasFranquicia } from './pages/VentasFranquicia'
import { VentasProducto } from './pages/VentasProducto'
import { VentasMozo } from './pages/VentasMozo'
import { OcupacionMesas } from './pages/OcupacionMesas'
import { Conexiones } from './pages/Conexiones'
import { EstadoIntegracion } from './pages/EstadoIntegracion'
import { TiposCambio } from './pages/TiposCambio'
import { Usuarios } from './pages/Usuarios'
import { Alertas } from './pages/Alertas'

// Redirect to dashboard if already authenticated
function PublicRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary"></div>
      </div>
    )
  }

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}

function App() {
  return (
    <Routes>
      {/* Public routes */}
      <Route
        path="/login"
        element={
          <PublicRoute>
            <Login />
          </PublicRoute>
        }
      />

      {/* Protected routes */}
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <MainLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardHome />} />
        <Route path="ventas/franquicia" element={<VentasFranquicia />} />
        <Route path="ventas/producto" element={<VentasProducto />} />
        <Route path="ventas/mozo" element={<VentasMozo />} />
        <Route path="alertas" element={<Alertas />} />
        <Route path="operaciones/mesas" element={<OcupacionMesas />} />
        <Route path="conexiones" element={<Conexiones />} />
        <Route path="integracion/estado" element={<EstadoIntegracion />} />
        <Route path="admin/tipos-cambio" element={<TiposCambio />} />
        {/* Admin-only routes */}
        <Route
          path="admin/usuarios"
          element={
            <ProtectedRoute requiredRoles={['ADMIN']}>
              <Usuarios />
            </ProtectedRoute>
          }
        />
      </Route>

      {/* Catch all - redirect to home */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
