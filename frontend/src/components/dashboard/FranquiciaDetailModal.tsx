import { useState, useEffect, Fragment } from 'react'
import { X, Calendar, DollarSign, Ticket, Users, TrendingUp, MapPin, Building, ChevronDown, ChevronRight, Loader2, Clock, User, Hash } from 'lucide-react'
import { formatCurrency, formatNumber } from '@/lib/utils'
import { transaccionesApi } from '@/services/api'
import type { Transaccion, TransaccionDetalle } from '@/types/dashboard'

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
  const [transacciones, setTransacciones] = useState<Transaccion[]>([])
  const [loadingTransacciones, setLoadingTransacciones] = useState(false)
  const [expandedTicket, setExpandedTicket] = useState<number | null>(null)
  const [ticketDetalles, setTicketDetalles] = useState<Record<number, TransaccionDetalle[]>>({})
  const [loadingDetalle, setLoadingDetalle] = useState<number | null>(null)

  // Cargar transacciones cuando se abre el modal
  useEffect(() => {
    if (isOpen && franquicia) {
      loadTransacciones()
    }
    // Reset state when closing
    if (!isOpen) {
      setTransacciones([])
      setExpandedTicket(null)
      setTicketDetalles({})
    }
  }, [isOpen, franquicia?.franquicia_id, fechaDesde, fechaHasta])

  const loadTransacciones = async () => {
    if (!franquicia) return
    setLoadingTransacciones(true)
    try {
      const data = await transaccionesApi.getTransaccionesByFranquicia(
        franquicia.franquicia_id,
        fechaDesde,
        fechaHasta
      )
      setTransacciones(data)
    } catch (error) {
      console.error('Error loading transactions:', error)
    } finally {
      setLoadingTransacciones(false)
    }
  }

  const toggleTicketDetalle = async (ticketId: number) => {
    if (expandedTicket === ticketId) {
      setExpandedTicket(null)
      return
    }

    setExpandedTicket(ticketId)

    // Load details if not already loaded
    if (!ticketDetalles[ticketId]) {
      setLoadingDetalle(ticketId)
      try {
        const detalles = await transaccionesApi.getTransaccionDetalle(ticketId)
        setTicketDetalles(prev => ({ ...prev, [ticketId]: detalles }))
      } catch (error) {
        console.error('Error loading ticket details:', error)
      } finally {
        setLoadingDetalle(null)
      }
    }
  }

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr)
    return date.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' })
  }

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr)
    return date.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
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
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="bg-green-50 dark:bg-green-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-green-600 dark:text-green-400 mb-1">
                  <DollarSign className="h-4 w-4" />
                  <span className="text-xs font-medium">Venta Neta (USD)</span>
                </div>
                <p className="text-2xl font-bold text-green-700 dark:text-green-300">
                  {formatCurrency(franquicia.venta_neta, 'USD')}
                </p>
                {franquicia.moneda_codigo && franquicia.moneda_codigo !== 'USD' && (
                  <p className="text-xs text-green-600 dark:text-green-400 mt-1">
                    {formatCurrency(franquicia.venta_neta_local, franquicia.moneda_codigo)} local
                  </p>
                )}
              </div>

              <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400 mb-1">
                  <Ticket className="h-4 w-4" />
                  <span className="text-xs font-medium">Tickets</span>
                </div>
                <p className="text-2xl font-bold text-blue-700 dark:text-blue-300">
                  {formatNumber(franquicia.total_tickets)}
                </p>
              </div>

              <div className="bg-purple-50 dark:bg-purple-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-1">
                  <Users className="h-4 w-4" />
                  <span className="text-xs font-medium">Cubiertos</span>
                </div>
                <p className="text-2xl font-bold text-purple-700 dark:text-purple-300">
                  {formatNumber(franquicia.total_cubiertos)}
                </p>
              </div>

              <div className="bg-amber-50 dark:bg-amber-900/20 rounded-lg p-4">
                <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400 mb-1">
                  <TrendingUp className="h-4 w-4" />
                  <span className="text-xs font-medium">Ticket Promedio</span>
                </div>
                <p className="text-2xl font-bold text-amber-700 dark:text-amber-300">
                  {formatCurrency(franquicia.ticket_promedio, 'USD')}
                </p>
              </div>
            </div>
          </div>

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

          {/* Detalle de Transacciones */}
          <div>
            <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
              Detalle de Transacciones ({transacciones.length} tickets)
            </h3>

            {loadingTransacciones ? (
              <div className="flex items-center justify-center py-12">
                <Loader2 className="h-8 w-8 animate-spin text-blue-500" />
                <span className="ml-2 text-muted-foreground">Cargando transacciones...</span>
              </div>
            ) : transacciones.length === 0 ? (
              <div className="text-center py-8 text-muted-foreground bg-gray-100 dark:bg-gray-800 rounded-lg">
                No hay transacciones en el período seleccionado.
              </div>
            ) : (
              <div className="border dark:border-gray-700 rounded-lg overflow-hidden">
                <table className="w-full text-sm">
                  <thead className="bg-gray-100 dark:bg-gray-800">
                    <tr>
                      <th className="px-3 py-3 text-left font-semibold w-10"></th>
                      <th className="px-3 py-3 text-left font-semibold">Ticket</th>
                      <th className="px-3 py-3 text-left font-semibold">Fecha</th>
                      <th className="px-3 py-3 text-left font-semibold">Hora</th>
                      <th className="px-3 py-3 text-left font-semibold">Mesa</th>
                      <th className="px-3 py-3 text-left font-semibold">Mozo</th>
                      <th className="px-3 py-3 text-right font-semibold">Cubiertos</th>
                      <th className="px-3 py-3 text-right font-semibold">Items</th>
                      <th className="px-3 py-3 text-right font-semibold">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {transacciones.map((tx) => (
                      <Fragment key={tx.ticket_id}>
                        <tr
                          className="border-t dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800/50 cursor-pointer"
                          onClick={() => toggleTicketDetalle(tx.ticket_id)}
                        >
                          <td className="px-3 py-3">
                            {loadingDetalle === tx.ticket_id ? (
                              <Loader2 className="h-4 w-4 animate-spin text-blue-500" />
                            ) : expandedTicket === tx.ticket_id ? (
                              <ChevronDown className="h-4 w-4 text-blue-500" />
                            ) : (
                              <ChevronRight className="h-4 w-4 text-gray-400" />
                            )}
                          </td>
                          <td className="px-3 py-3 font-mono text-xs">{tx.numero_ticket}</td>
                          <td className="px-3 py-3">{formatDate(tx.fecha_negocio)}</td>
                          <td className="px-3 py-3 flex items-center gap-1">
                            <Clock className="h-3 w-3 text-muted-foreground" />
                            {formatTime(tx.fecha_apertura)}
                          </td>
                          <td className="px-3 py-3">
                            {tx.numero_mesa ? (
                              <span className="inline-flex items-center gap-1">
                                <Hash className="h-3 w-3 text-muted-foreground" />
                                {tx.numero_mesa}
                                {tx.area_mesa && <span className="text-xs text-muted-foreground">({tx.area_mesa})</span>}
                              </span>
                            ) : '-'}
                          </td>
                          <td className="px-3 py-3">
                            {tx.nombre_mozo ? (
                              <span className="inline-flex items-center gap-1">
                                <User className="h-3 w-3 text-muted-foreground" />
                                {tx.nombre_mozo}
                              </span>
                            ) : '-'}
                          </td>
                          <td className="px-3 py-3 text-right">{tx.cantidad_cubiertos || '-'}</td>
                          <td className="px-3 py-3 text-right">{tx.cantidad_items}</td>
                          <td className="px-3 py-3 text-right font-semibold">
                            {formatCurrency(tx.importe_total_pagado, tx.moneda_codigo)}
                          </td>
                        </tr>
                        {/* Detalle expandido */}
                        {expandedTicket === tx.ticket_id && (
                          <tr>
                            <td colSpan={9} className="bg-blue-50 dark:bg-blue-900/20 px-6 py-4">
                              {loadingDetalle === tx.ticket_id ? (
                                <div className="flex items-center justify-center py-4">
                                  <Loader2 className="h-5 w-5 animate-spin text-blue-500" />
                                </div>
                              ) : ticketDetalles[tx.ticket_id] ? (
                                <div>
                                  <p className="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                                    Líneas del Ticket
                                  </p>
                                  <table className="w-full text-xs">
                                    <thead className="bg-blue-100 dark:bg-blue-800/30">
                                      <tr>
                                        <th className="px-2 py-2 text-left font-semibold">Producto</th>
                                        <th className="px-2 py-2 text-left font-semibold">Categoría</th>
                                        <th className="px-2 py-2 text-right font-semibold">Cantidad</th>
                                        <th className="px-2 py-2 text-right font-semibold">P. Unit.</th>
                                        <th className="px-2 py-2 text-right font-semibold">Descuento</th>
                                        <th className="px-2 py-2 text-right font-semibold">Subtotal</th>
                                      </tr>
                                    </thead>
                                    <tbody>
                                      {ticketDetalles[tx.ticket_id].map((linea) => (
                                        <tr
                                          key={linea.detalle_id}
                                          className={`border-t border-blue-200 dark:border-blue-800 ${linea.esta_anulado ? 'line-through text-gray-400' : ''}`}
                                        >
                                          <td className="px-2 py-2">{linea.nombre_producto}</td>
                                          <td className="px-2 py-2 text-muted-foreground">{linea.categoria || '-'}</td>
                                          <td className="px-2 py-2 text-right">{linea.cantidad}</td>
                                          <td className="px-2 py-2 text-right">{formatCurrency(linea.precio_unitario, tx.moneda_codigo)}</td>
                                          <td className="px-2 py-2 text-right text-red-600">
                                            {linea.importe_descuento > 0 ? `-${formatCurrency(linea.importe_descuento, tx.moneda_codigo)}` : '-'}
                                          </td>
                                          <td className="px-2 py-2 text-right font-semibold">{formatCurrency(linea.importe_neto, tx.moneda_codigo)}</td>
                                        </tr>
                                      ))}
                                    </tbody>
                                    <tfoot className="bg-blue-100 dark:bg-blue-800/30 font-semibold">
                                      <tr>
                                        <td colSpan={4}></td>
                                        <td className="px-2 py-2 text-right">Total:</td>
                                        <td className="px-2 py-2 text-right">{formatCurrency(tx.importe_neto, tx.moneda_codigo)}</td>
                                      </tr>
                                      {tx.importe_impuesto > 0 && (
                                        <tr>
                                          <td colSpan={4}></td>
                                          <td className="px-2 py-2 text-right text-xs font-normal">+ Impuestos:</td>
                                          <td className="px-2 py-2 text-right text-xs font-normal">{formatCurrency(tx.importe_impuesto, tx.moneda_codigo)}</td>
                                        </tr>
                                      )}
                                      {tx.importe_propina > 0 && (
                                        <tr>
                                          <td colSpan={4}></td>
                                          <td className="px-2 py-2 text-right text-xs font-normal">+ Propina:</td>
                                          <td className="px-2 py-2 text-right text-xs font-normal">{formatCurrency(tx.importe_propina, tx.moneda_codigo)}</td>
                                        </tr>
                                      )}
                                      <tr className="bg-blue-200 dark:bg-blue-700/50">
                                        <td colSpan={4}></td>
                                        <td className="px-2 py-2 text-right">Total Pagado:</td>
                                        <td className="px-2 py-2 text-right">{formatCurrency(tx.importe_total_pagado, tx.moneda_codigo)}</td>
                                      </tr>
                                    </tfoot>
                                  </table>
                                  {tx.tiempo_consumo_minutos && (
                                    <p className="text-xs text-muted-foreground mt-2 flex items-center gap-1">
                                      <Clock className="h-3 w-3" />
                                      Tiempo de consumo: {tx.tiempo_consumo_minutos} minutos
                                    </p>
                                  )}
                                </div>
                              ) : (
                                <p className="text-center text-muted-foreground">No hay detalles disponibles</p>
                              )}
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
