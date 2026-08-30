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
  ChevronLeft,
  BadgeDollarSign,
  UserCog,
  Shield,
  X,
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

interface SidebarProps {
  collapsed?: boolean
  onToggle?: () => void
  isMobile?: boolean
  mobileOpen?: boolean
  onMobileClose?: () => void
}

export function Sidebar({
  collapsed = false,
  onToggle,
  isMobile = false,
  mobileOpen = false,
  onMobileClose
}: SidebarProps) {
  const { user } = useAuth()
  const isAdmin = user?.rol?.codigo === 'ADMIN'

  // Mobile overlay
  if (isMobile) {
    return (
      <>
        {/* Backdrop */}
        <div
          className={cn(
            "fixed inset-0 bg-black/50 z-40 transition-opacity lg:hidden",
            mobileOpen ? "opacity-100" : "opacity-0 pointer-events-none"
          )}
          onClick={onMobileClose}
        />

        {/* Mobile Drawer */}
        <aside
          className={cn(
            "fixed left-0 top-0 h-full w-72 bg-cabrera-charcoal text-white flex flex-col z-50 transform transition-transform duration-300 ease-in-out lg:hidden",
            mobileOpen ? "translate-x-0" : "-translate-x-full"
          )}
        >
          {/* Header with close button */}
          <div className="h-16 flex items-center justify-between px-4 border-b border-white/10">
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 rounded-full bg-cabrera-gold flex items-center justify-center">
                <span className="text-cabrera-charcoal font-bold text-sm">LC</span>
              </div>
              <div>
                <h1 className="font-semibold text-lg tracking-tight">LA CABRERA</h1>
                <p className="text-[10px] text-gray-400 -mt-0.5">Central de Franquicias</p>
              </div>
            </div>
            <button
              onClick={onMobileClose}
              className="p-2 rounded-lg hover:bg-white/10 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Navigation */}
          <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
            {navigation.map((item) => (
              <NavLink
                key={item.name}
                to={item.href}
                onClick={onMobileClose}
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

            {/* Admin Section */}
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
                    onClick={onMobileClose}
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
      </>
    )
  }

  // Desktop sidebar
  return (
    <aside
      className={cn(
        "bg-cabrera-charcoal text-white flex-col hidden lg:flex transition-all duration-300",
        collapsed ? "w-16" : "w-64"
      )}
    >
      {/* Logo */}
      <div className="h-16 flex items-center justify-center border-b border-white/10">
        <div className={cn("flex items-center", collapsed ? "justify-center" : "gap-3")}>
          <div className="w-8 h-8 rounded-full bg-cabrera-gold flex items-center justify-center flex-shrink-0">
            <span className="text-cabrera-charcoal font-bold text-sm">LC</span>
          </div>
          {!collapsed && (
            <div>
              <h1 className="font-semibold text-lg tracking-tight">LA CABRERA</h1>
              <p className="text-[10px] text-gray-400 -mt-0.5">Central de Franquicias</p>
            </div>
          )}
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-2 py-4 space-y-1 overflow-y-auto">
        {navigation.map((item) => (
          <NavLink
            key={item.name}
            to={item.href}
            title={collapsed ? item.name : undefined}
            className={({ isActive }) =>
              cn(
                'flex items-center rounded-lg text-sm font-medium transition-colors group',
                collapsed ? 'justify-center px-2 py-2.5' : 'gap-3 px-3 py-2.5',
                isActive
                  ? 'bg-cabrera-gold/20 text-cabrera-gold'
                  : 'text-gray-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            {({ isActive }) => (
              <>
                <item.icon className={cn('w-5 h-5 flex-shrink-0', isActive && 'text-cabrera-gold')} />
                {!collapsed && (
                  <>
                    <span className="flex-1">{item.name}</span>
                    <ChevronRight
                      className={cn(
                        'w-4 h-4 opacity-0 transition-opacity',
                        isActive && 'opacity-100'
                      )}
                    />
                  </>
                )}
              </>
            )}
          </NavLink>
        ))}

        {/* Admin Section - Only for ADMIN role */}
        {isAdmin && (
          <>
            <div className={cn("pt-4 pb-2", collapsed && "hidden")}>
              <div className="flex items-center gap-2 px-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                <Shield className="w-3 h-3" />
                <span>Administración</span>
              </div>
            </div>
            {collapsed && <div className="pt-2 border-t border-white/10 mt-2" />}
            {adminNavigation.map((item) => (
              <NavLink
                key={item.name}
                to={item.href}
                title={collapsed ? item.name : undefined}
                className={({ isActive }) =>
                  cn(
                    'flex items-center rounded-lg text-sm font-medium transition-colors group',
                    collapsed ? 'justify-center px-2 py-2.5' : 'gap-3 px-3 py-2.5',
                    isActive
                      ? 'bg-cabrera-gold/20 text-cabrera-gold'
                      : 'text-gray-300 hover:bg-white/5 hover:text-white'
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon className={cn('w-5 h-5 flex-shrink-0', isActive && 'text-cabrera-gold')} />
                    {!collapsed && (
                      <>
                        <span className="flex-1">{item.name}</span>
                        <ChevronRight
                          className={cn(
                            'w-4 h-4 opacity-0 transition-opacity',
                            isActive && 'opacity-100'
                          )}
                        />
                      </>
                    )}
                  </>
                )}
              </NavLink>
            ))}
          </>
        )}
      </nav>

      {/* Toggle Button */}
      <div className="p-2 border-t border-white/10">
        <button
          onClick={onToggle}
          className={cn(
            "w-full flex items-center justify-center p-2 rounded-lg hover:bg-white/10 transition-colors text-gray-400 hover:text-white",
            collapsed ? "" : "gap-2"
          )}
          title={collapsed ? "Expandir menú" : "Contraer menú"}
        >
          {collapsed ? (
            <ChevronRight className="w-5 h-5" />
          ) : (
            <>
              <ChevronLeft className="w-5 h-5" />
              <span className="text-sm">Contraer</span>
            </>
          )}
        </button>
      </div>

      {/* Footer */}
      {!collapsed && (
        <div className="p-4 border-t border-white/10">
          <div className="px-3 py-2 rounded-lg bg-white/5">
            <p className="text-xs text-gray-400">Sistema de Gestion</p>
            <p className="text-sm font-medium">v1.2.0</p>
          </div>
        </div>
      )}
    </aside>
  )
}
