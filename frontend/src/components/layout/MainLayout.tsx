import { useState, useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Header } from './Header'

export function MainLayout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    // Recuperar estado del localStorage
    const saved = localStorage.getItem('sidebar-collapsed')
    return saved === 'true'
  })
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  // Guardar estado en localStorage
  useEffect(() => {
    localStorage.setItem('sidebar-collapsed', String(sidebarCollapsed))
  }, [sidebarCollapsed])

  // Cerrar menú móvil en resize a desktop
  useEffect(() => {
    const handleResize = () => {
      if (window.innerWidth >= 1024) {
        setMobileMenuOpen(false)
      }
    }
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [])

  const toggleSidebar = () => {
    setSidebarCollapsed(!sidebarCollapsed)
  }

  const openMobileMenu = () => {
    setMobileMenuOpen(true)
  }

  const closeMobileMenu = () => {
    setMobileMenuOpen(false)
  }

  return (
    <div className="flex h-screen bg-gray-50 dark:bg-gray-950 transition-colors">
      {/* Desktop Sidebar */}
      <Sidebar
        collapsed={sidebarCollapsed}
        onToggle={toggleSidebar}
      />

      {/* Mobile Sidebar */}
      <Sidebar
        isMobile={true}
        mobileOpen={mobileMenuOpen}
        onMobileClose={closeMobileMenu}
      />

      <div className="flex-1 flex flex-col overflow-hidden">
        <Header onMenuClick={openMobileMenu} />
        <main className="flex-1 overflow-y-auto p-4 md:p-6 bg-gray-50 dark:bg-gray-950 transition-colors">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
