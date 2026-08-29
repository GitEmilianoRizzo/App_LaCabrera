import { NavLink } from 'react-router-dom'
import {
  LayoutDashboard,
  Store,
  ShoppingBag,
  Users,
  Table2,
  Plug,
  Cable,
  ChevronRight,
  BadgeDollarSign,
  UserCog,
  Shield,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useAuth } from '@/contexts/AuthContext'

const navigation = [
  { name: 'Dashboard', href: '/', icon: LayoutDashboard },
  { name: 'Por Franquicia', href: '/ventas/franquicia', icon: Store },
  { name: 'Por Producto', href: '/ventas/producto', icon: ShoppingBag },
  { name: 'Por Mozo', href: '/ventas/mozo', icon: Users },
  { name: 'Mesas', href: '/operaciones/mesas', icon: Table2 },
  { name: 'Conexiones', href: '/conexiones', icon: Cable },
  { name: 'Integracion', href: '/integracion/estado', icon: Plug },
  { name: 'Tipos de Cambio', href: '/admin/tipos-cambio', icon: BadgeDollarSign },
]

// Admin-only navigation items
const adminNavigation = [
  { name: 'Usuarios', href: '/admin/usuarios', icon: UserCog },
]

export function Sidebar() {
  const { user } = useAuth()
  const isAdmin = user?.rol?.codigo === 'ADMIN'
  return (
    <aside className="w-64 bg-cabrera-charcoal text-white flex flex-col">
      {/* Logo */}
      <div className="h-16 flex items-center justify-center border-b border-white/10">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-cabrera-gold flex items-center justify-center">
            <span className="text-cabrera-charcoal font-bold text-sm">LC</span>
          </div>
          <div>
            <h1 className="font-semibold text-lg tracking-tight">LA CABRERA</h1>
            <p className="text-[10px] text-gray-400 -mt-0.5">Central de Franquicias</p>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        {navigation.map((item) => (
          <NavLink
            key={item.name}
            to={item.href}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors group',
                isActive
                  ? 'bg-cabrera-gold/20 text-cabrera-gold'
                  : 'text-gray-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            {({ isActive }) => (
              <>
                <item.icon className={cn('w-5 h-5', isActive && 'text-cabrera-gold')} />
                <span className="flex-1">{item.name}</span>
                <ChevronRight
                  className={cn(
                    'w-4 h-4 opacity-0 transition-opacity',
                    isActive && 'opacity-100'
                  )}
                />
              </>
            )}
          </NavLink>
        ))}

        {/* Admin Section - Only for ADMIN role */}
        {isAdmin && (
          <>
            <div className="pt-4 pb-2">
              <div className="flex items-center gap-2 px-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                <Shield className="w-3 h-3" />
                <span>Administración</span>
              </div>
            </div>
            {adminNavigation.map((item) => (
              <NavLink
                key={item.name}
                to={item.href}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors group',
                    isActive
                      ? 'bg-cabrera-gold/20 text-cabrera-gold'
                      : 'text-gray-300 hover:bg-white/5 hover:text-white'
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon className={cn('w-5 h-5', isActive && 'text-cabrera-gold')} />
                    <span className="flex-1">{item.name}</span>
                    <ChevronRight
                      className={cn(
                        'w-4 h-4 opacity-0 transition-opacity',
                        isActive && 'opacity-100'
                      )}
                    />
                  </>
                )}
              </NavLink>
            ))}
          </>
        )}
      </nav>

      {/* Footer */}
      <div className="p-4 border-t border-white/10">
        <div className="px-3 py-2 rounded-lg bg-white/5">
          <p className="text-xs text-gray-400">Sistema de Gestion</p>
          <p className="text-sm font-medium">v1.2.0</p>
        </div>
      </div>
    </aside>
  )
}
