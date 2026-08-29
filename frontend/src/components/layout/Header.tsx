import { Bell, RefreshCw, User, LogOut, Settings, ChevronDown } from 'lucide-react'
import { formatDate } from '@/lib/utils'
import { ThemeToggle } from './ThemeToggle'
import { useAuth } from '@/contexts/AuthContext'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export function Header() {
  const today = new Date()
  const { user, logout } = useAuth()

  const handleLogout = async () => {
    await logout()
  }

  // Get initials from user name
  const getInitials = (nombre: string, apellido?: string | null) => {
    const first = nombre?.charAt(0) || ''
    const last = apellido?.charAt(0) || ''
    return (first + last).toUpperCase() || 'U'
  }

  return (
    <header className="h-16 bg-white dark:bg-gray-900 border-b border-gray-200 dark:border-gray-700 px-6 flex items-center justify-between transition-colors">
      {/* Left side - Page title will be set by each page */}
      <div className="flex items-center gap-4">
        <div>
          <p className="text-sm text-muted-foreground">
            {formatDate(today, 'long')}
          </p>
        </div>
      </div>

      {/* Right side - Actions */}
      <div className="flex items-center gap-2">
        <ThemeToggle />
        <button
          className="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-600 dark:text-gray-300 transition-colors"
          title="Actualizar datos"
        >
          <RefreshCw className="w-5 h-5" />
        </button>
        <button
          className="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-600 dark:text-gray-300 transition-colors relative"
          title="Notificaciones"
        >
          <Bell className="w-5 h-5" />
          <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
        </button>

        {/* User Menu */}
        <div className="ml-2 pl-4 border-l border-gray-200 dark:border-gray-700">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button className="flex items-center gap-2 px-3 py-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors focus:outline-none">
                {user?.google_picture_url ? (
                  <img
                    src={user.google_picture_url}
                    alt={user.nombre}
                    className="w-8 h-8 rounded-full"
                  />
                ) : (
                  <div className="w-8 h-8 rounded-full bg-cabrera-burgundy flex items-center justify-center">
                    <span className="text-sm font-medium text-white">
                      {user ? getInitials(user.nombre, user.apellido) : 'U'}
                    </span>
                  </div>
                )}
                <div className="text-left hidden sm:block">
                  <p className="text-sm font-medium text-gray-900 dark:text-white">
                    {user?.nombre || 'Usuario'}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {user?.rol?.nombre || 'Sin rol'}
                  </p>
                </div>
                <ChevronDown className="w-4 h-4 text-muted-foreground" />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel>
                <div className="flex flex-col space-y-1">
                  <p className="text-sm font-medium">{user?.nombre} {user?.apellido}</p>
                  <p className="text-xs text-muted-foreground">{user?.email}</p>
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem disabled>
                <Settings className="mr-2 h-4 w-4" />
                <span>Configuración</span>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={handleLogout} className="text-red-600 focus:text-red-600 cursor-pointer">
                <LogOut className="mr-2 h-4 w-4" />
                <span>Cerrar sesión</span>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  )
}
