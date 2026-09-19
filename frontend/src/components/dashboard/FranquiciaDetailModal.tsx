import { useState, useEffect } from 'react'
import { X, Calendar, DollarSign, Ticket, Users, TrendingUp, MapPin, Building, Loader2, Banknote, ShoppingBag } from 'lucide-react'
import { formatCurrency, formatNumber } from '@/lib/utils'
import { dashboardApiV2 } from '@/services/api'
import type { VentasPorProductoConPeso } from '@/types/dashboard'
import { ClockChart } from '@/components/charts/ClockChart'

interface FranquiciaData {
  franquicia_id: number
  franquicia_codigo: string
  franquicia_nombre: string
  grupo_economico_nombre?: string
  pais?: string
  ciudad?: string
  moneda_codigo?: string
  venta_neta: number
  venta_neta_local: number
  total_tickets: number
  total_cubiertos: number
  ticket_promedio: number
  venta_mes_actual: number
  tickets_mes_actual: number
  venta_mes_anterior: number
  tickets_mes_anterior: number
  venta_acum_anio_actual: number
  tickets_acum_anio_actual: number
}

interface FranquiciaDetailModalProps {
  isOpen: boolean
  onClose: () => void
  franquicia: FranquiciaData | null
  fechaDesde: string
  fechaHasta: string
}

export function FranquiciaDetailModal({
  isOpen,
  onClose,
  franquicia,
  fechaDesde,
  fechaHasta
}: FranquiciaDetailModalProps) {
  const [ventasPorHora, setVentasPorHora] = useState<{ hora: number; cubiertos: number; tickets: number; venta: number }[]>([])
  const [loadingHoras, setLoadingHoras] = useState(false)
  const [productos, setProductos] = useState<VentasPorProductoConPeso[]>([])
  const [loadingProductos, setLoadingProductos] = useState(false)

  // Cargar datos por hora y productos cuando se abre el modal
  useEffect(() => {
    if (isOpen && franquicia) {
      loadVentasPorHora()
      loadProductos()
    }
    // Reset state when closing
    if (!isOpen) {
      setVentasPorHora([])
      setProductos([])
    }
  }, [isOpen, franquicia?.franquicia_id, fechaDesde, fechaHasta])

  const loadVentasPorHora = async () => {
    if (!franquicia) return
    setLoadingHoras(true)
    try {
      const data = await dashboardApiV2.getVentasPorHora({
        fechaDesde,
        fechaHasta,
        franquiciaId: franquicia.franquicia_id
      })
      // Agrupar por hora
      const byHour = data.reduce((acc: Record<number, { hora: number; cubiertos: number; tickets: number; venta: number }>, item: { hora: number; cubiertos?: number; tickets?: number; venta_neta?: number }) => {
        const hora = item.hora
        if (!acc[hora]) {
          acc[hora] = { hora, cubiertos: 0, tickets: 0, venta: 0 }
        }
        acc[hora].cubiertos += item.cubiertos || 0
        acc[hora].tickets += item.tickets || 0
        acc[hora].venta += item.venta_neta || 0
        return acc
      }, {})
      setVentasPorHora(Object.values(byHour))
    } catch (error) {
      console.error('Error loading hourly data:', error)
    } finally {
      setLoadingHoras(false)
    }
  }

  const loadProductos = async () => {
    if (!franquicia) return
    setLoadingProductos(true)
    try {
      const data = await dashboardApiV2.getVentasPorProductoConPeso({
        fechaDesde,
        fechaHasta,
        franquiciaId: franquicia.franquicia_id
      })
      // Filtrar por la franquicia y ordenar por venta
      const filtered = data
        .filter(p => p.franquicia_id === franquicia.franquicia_id)
        .sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0))
      setProductos(filtered)
    } catch (error) {
      console.error('Error loading products:', error)
    } finally {
      setLoadingProductos(false)
    }
  }

  if (!isOpen || !franquicia) return null

  const variacionMes = franquicia.venta_mes_anterior > 0
    ? ((franquicia.venta_mes_actual - franquicia.venta_mes_anterior) / franquicia.venta_mes_anterior) * 100
    : 0

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative bg-white dark:bg-gray-900 rounded-lg shadow-xl w-full max-w-5xl max-h-[95vh] overflow-hidden mx-4">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b dark:border-gray-700">
          <div>
            <h2 className="text-xl font-bold text-gray-900 dark:text-white">
              {franquicia.franquicia_nombre}
            </h2>
            <div className="flex items-center gap-4 text-sm text-muted-foreground mt-1">
              {franquicia.grupo_economico_nombre && (
                <span className="flex items-center gap-1">
                  <Building className="h-3 w-3" />
                  {franquicia.grupo_economico_nombre}
                </span>
              )}
              <span className="flex items-center gap-1">
                <MapPin className="h-3 w-3" />
                {franquicia.ciudad}, {franquicia.pais}
              </span>
              <span className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                {fechaDesde} a {fechaHasta}
              </span>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-full transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto max-h-[calc(95vh-120px)]">
          {/* KPIs del Período */}
          <div className="mb-6">
            <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
              Resumen del Período
            </h3>
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
              {/* Venta USD */}
              <div className="bg-green-50 dark:bg-green-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-green-600 dark:text-green-400 mb-1">
                  <DollarSign className="h-4 w-4" />
                  <span className="text-xs font-medium">Venta (USD)</span>
                </div>
                <p className="text-xl font-bold text-green-700 dark:text-green-300">
                  {formatCurrency(franquicia.venta_neta, 'USD')}
                </p>
              </div>

              {/* Venta Moneda Local */}
              {franquicia.moneda_codigo && franquicia.moneda_codigo !== 'USD' && (
                <div className="bg-emerald-50 dark:bg-emerald-900/20 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400 mb-1">
                    <Banknote className="h-4 w-4" />
                    <span className="text-xs font-medium">Venta ({franquicia.moneda_codigo})</span>
                  </div>
                  <p className="text-xl font-bold text-emerald-700 dark:text-emerald-300">
                    {formatCurrency(franquicia.venta_neta_local, franquicia.moneda_codigo)}
                  </p>
                </div>
              )}

              {/* Tickets */}
              <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400 mb-1">
                  <Ticket className="h-4 w-4" />
                  <span className="text-xs font-medium">Tickets</span>
                </div>
                <p className="text-xl font-bold text-blue-700 dark:text-blue-300">
                  {formatNumber(franquicia.total_tickets)}
                </p>
              </div>

              {/* Cubiertos */}
              <div className="bg-purple-50 dark:bg-purple-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-1">
                  <Users className="h-4 w-4" />
                  <span className="text-xs font-medium">Cubiertos</span>
                </div>
                <p className="text-xl font-bold text-purple-700 dark:text-purple-300">
                  {formatNumber(franquicia.total_cubiertos)}
                </p>
              </div>

              {/* Ticket Promedio USD */}
              <div className="bg-amber-50 dark:bg-amber-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400 mb-1">
                  <TrendingUp className="h-4 w-4" />
                  <span className="text-xs font-medium">Ticket Prom. (USD)</span>
                </div>
                <p className="text-xl font-bold text-amber-700 dark:text-amber-300">
                  {formatCurrency(franquicia.ticket_promedio, 'USD')}
                </p>
              </div>

              {/* Ticket Promedio Moneda Local */}
              {franquicia.moneda_codigo && franquicia.moneda_codigo !== 'USD' && (
                <div className="bg-orange-50 dark:bg-orange-900/20 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-orange-600 dark:text-orange-400 mb-1">
                    <TrendingUp className="h-4 w-4" />
                    <span className="text-xs font-medium">Ticket Prom. ({franquicia.moneda_codigo})</span>
                  </div>
                  <p className="text-xl font-bold text-orange-700 dark:text-orange-300">
                    {formatCurrency(
                      franquicia.total_tickets > 0 ? franquicia.venta_neta_local / franquicia.total_tickets : 0,
                      franquicia.moneda_codigo
                    )}
                  </p>
                </div>
              )}
            </div>
          </div>

          {/* Consumo por Hora */}
          {ventasPorHora.length > 0 && (
            <div className="mb-6">
              <ClockChart
                title="Consumo por Hora"
                subtitle="Distribución horaria del período seleccionado"
                data={ventasPorHora}
                metric="cubiertos"
              />
            </div>
          )}
          {loadingHoras && (
            <div className="mb-6 flex items-center justify-center py-8 bg-gray-50 dark:bg-gray-800 rounded-lg">
              <Loader2 className="h-6 w-6 animate-spin text-blue-500" />
              <span className="ml-2 text-muted-foreground">Cargando datos por hora...</span>
            </div>
          )}

          {/* Comparativo Mensual */}
          <div className="mb-6">
            <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
              Comparativo Mensual
            </h3>
            <div className="bg-gray-50 dark:bg-gray-800 rounded-lg p-4">
              <div className="grid grid-cols-3 gap-4">
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Mes Actual</p>
                  <p className="text-lg font-semibold text-gray-900 dark:text-white">
                    {formatCurrency(franquicia.venta_mes_actual, 'USD')}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatNumber(franquicia.tickets_mes_actual)} tickets
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Mes Anterior</p>
                  <p className="text-lg font-semibold text-gray-900 dark:text-white">
                    {formatCurrency(franquicia.venta_mes_anterior, 'USD')}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatNumber(franquicia.tickets_mes_anterior)} tickets
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground mb-1">Variación</p>
                  <p className={`text-lg font-semibold ${variacionMes >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                    {variacionMes >= 0 ? '+' : ''}{variacionMes.toFixed(1)}%
                  </p>
                  <p className="text-xs text-muted-foreground">
                    vs mes anterior
                  </p>
                </div>
              </div>
            </div>
          </div>

          {/* Productos del Período */}
          <div className="mb-6">
            <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3 flex items-center gap-2">
              <ShoppingBag className="h-4 w-4" />
              Productos del Período ({productos.length} productos)
            </h3>

            {loadingProductos ? (
              <div className="flex items-center justify-center py-8 bg-gray-50 dark:bg-gray-800 rounded-lg">
                <Loader2 className="h-6 w-6 animate-spin text-blue-500" />
                <span className="ml-2 text-muted-foreground">Cargando productos...</span>
              </div>
            ) : productos.length === 0 ? (
              <div className="text-center py-6 text-muted-foreground bg-gray-100 dark:bg-gray-800 rounded-lg">
                No hay productos en el período seleccionado.
              </div>
            ) : (() => {
              // Agrupar por categoría
              const byCategory = productos.reduce((acc, p) => {
                const cat = p.categoria || 'Sin Categoría'
                if (!acc[cat]) acc[cat] = []
                acc[cat].push(p)
                return acc
              }, {} as Record<string, VentasPorProductoConPeso[]>)

              const totalCantidad = productos.reduce((sum, p) => sum + p.cantidad_vendida, 0)
              const totalNeto = productos.reduce((sum, p) => sum + (p.venta_neta || 0), 0)

              const fmt = (val: number) => val.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

              return (
                <>
                  {/* Resumen de productos */}
                  <div className="grid grid-cols-3 gap-4 mb-4">
                    <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-3 text-center">
                      <p className="text-xs text-muted-foreground">Productos</p>
                      <p className="text-lg font-bold text-gray-900 dark:text-white">{productos.length}</p>
                    </div>
                    <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-3 text-center">
                      <p className="text-xs text-muted-foreground">Cantidad Total</p>
                      <p className="text-lg font-bold text-gray-900 dark:text-white">{totalCantidad.toLocaleString()}</p>
                    </div>
                    <div className="bg-gray-100 dark:bg-gray-800 rounded-lg p-3 text-center">
                      <p className="text-xs text-muted-foreground">Venta Neta ({franquicia?.moneda_codigo || 'USD'})</p>
                      <p className="text-lg font-bold text-cabrera-burgundy">${fmt(totalNeto)}</p>
                    </div>
                  </div>

                  {/* Tabla de productos - responsive */}
                  <div className="border dark:border-gray-700 rounded-lg overflow-hidden max-h-[300px] overflow-y-auto overflow-x-auto">
                    <table className="w-full text-xs sm:text-sm min-w-[320px]">
                      <thead className="bg-gray-100 dark:bg-gray-800 sticky top-0">
                        <tr>
                          <th className="hidden sm:table-cell px-2 sm:px-3 py-2 text-left font-semibold text-gray-700 dark:text-gray-300">Categoría</th>
                          <th className="px-2 sm:px-3 py-2 text-left font-semibold text-gray-700 dark:text-gray-300">Producto</th>
                          <th className="px-1 sm:px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Cant</th>
                          <th className="px-1 sm:px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Neto</th>
                          <th className="px-1 sm:px-3 py-2 text-right font-semibold text-gray-700 dark:text-gray-300">Unit.</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                        {Object.entries(byCategory).sort((a, b) => a[0].localeCompare(b[0])).map(([cat, prods]) => (
                          prods.sort((a, b) => (b.venta_neta || 0) - (a.venta_neta || 0)).map((p, idx) => (
                            <tr key={`${p.producto_codigo}-${idx}`} className="hover:bg-gray-50 dark:hover:bg-gray-800/50">
                              <td className="hidden sm:table-cell px-2 sm:px-3 py-1.5 sm:py-2 text-gray-600 dark:text-gray-400 text-xs">
                                {idx === 0 ? cat : ''}
                              </td>
                              <td className="px-2 sm:px-3 py-1.5 sm:py-2 text-gray-900 dark:text-white truncate max-w-[120px] sm:max-w-none">{p.producto_nombre}</td>
                              <td className="px-1 sm:px-3 py-1.5 sm:py-2 text-right text-gray-700 dark:text-gray-300">{p.cantidad_vendida}</td>
                              <td className="px-1 sm:px-3 py-1.5 sm:py-2 text-right text-gray-900 dark:text-white font-medium">${fmt(p.venta_neta || 0)}</td>
                              <td className="px-1 sm:px-3 py-1.5 sm:py-2 text-right text-gray-600 dark:text-gray-400">
                                ${p.cantidad_vendida > 0 ? fmt((p.venta_neta || 0) / p.cantidad_vendida) : '0.00'}
                              </td>
                            </tr>
                          ))
                        ))}
                      </tbody>
                      <tfoot className="bg-cabrera-burgundy/10 dark:bg-cabrera-burgundy/20 border-t-2 border-cabrera-burgundy sticky bottom-0">
                        <tr className="font-bold">
                          <td className="hidden sm:table-cell px-2 sm:px-3 py-2 text-cabrera-burgundy">TOTAL</td>
                          <td className="sm:hidden px-2 py-2 text-cabrera-burgundy">TOTAL</td>
                          <td className="hidden sm:table-cell"></td>
                          <td className="px-1 sm:px-3 py-2 text-right text-cabrera-burgundy">{totalCantidad.toLocaleString()}</td>
                          <td className="px-1 sm:px-3 py-2 text-right text-cabrera-burgundy">${fmt(totalNeto)}</td>
                          <td className="px-1 sm:px-3 py-2 text-right text-gray-600 dark:text-gray-400">
                            ${totalCantidad > 0 ? fmt(totalNeto / totalCantidad) : '0.00'}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                </>
              )
            })()}
          </div>
        </div>
      </div>
    </div>
  )
}
