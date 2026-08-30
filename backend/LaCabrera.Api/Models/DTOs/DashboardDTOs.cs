using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

public class HomeDashboardDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_codigo")]
    public string FranquiciaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("grupo_economico_codigo")]
    public string? GrupoEconomicoCodigo { get; set; }

    [JsonPropertyName("grupo_economico_nombre")]
    public string? GrupoEconomicoNombre { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("moneda_codigo")]
    public string? MonedaCodigo { get; set; }

    [JsonPropertyName("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [JsonPropertyName("estado_integracion")]
    public string? EstadoIntegracion { get; set; }

    [JsonPropertyName("dias_sin_sincronizar")]
    public int? DiasSinSincronizar { get; set; }

    [JsonPropertyName("alerta_sincronizacion")]
    public bool AlertaSincronizacion { get; set; }

    // Moneda local (para referencia)
    [JsonPropertyName("venta_neta_local")]
    public decimal VentaNetaLocal { get; set; }

    // Valores en USD (consolidados)
    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int TotalCubiertos { get; set; }

    [JsonPropertyName("total_tickets")]
    public int TotalTickets { get; set; }

    // Indicadores de calidad de conversión
    [JsonPropertyName("dias_con_tasa")]
    public int DiasTasaDirecta { get; set; }

    [JsonPropertyName("dias_sin_tasa")]
    public int DiasTasaArrastrada { get; set; }

    [JsonPropertyName("calidad_conversion")]
    public string CalidadConversion => DiasTasaArrastrada == 0 ? "OK" :
                                       DiasTasaArrastrada < DiasTasaDirecta ? "PARCIAL" : "SIN_TASA";

    [JsonPropertyName("venta_por_cubierto")]
    public decimal? VentaPorCubierto => TotalCubiertos > 0 ? VentaNeta / TotalCubiertos : null;

    [JsonPropertyName("ticket_promedio")]
    public decimal? TicketPromedio => TotalTickets > 0 ? VentaNeta / TotalTickets : null;

    // Columnas adicionales por período - Mes Actual
    [JsonPropertyName("venta_mes_actual")]
    public decimal VentaMesActual { get; set; }

    [JsonPropertyName("tickets_mes_actual")]
    public int TicketsMesActual { get; set; }

    [JsonPropertyName("cubiertos_mes_actual")]
    public int CubiertosMesActual { get; set; }

    // Mes Anterior
    [JsonPropertyName("venta_mes_anterior")]
    public decimal VentaMesAnterior { get; set; }

    [JsonPropertyName("tickets_mes_anterior")]
    public int TicketsMesAnterior { get; set; }

    [JsonPropertyName("cubiertos_mes_anterior")]
    public int CubiertosMesAnterior { get; set; }

    // Acumulado Año Previo (YTD del año anterior hasta el mes actual)
    [JsonPropertyName("venta_acum_anio_previo")]
    public decimal VentaAcumAnioPrevio { get; set; }

    [JsonPropertyName("tickets_acum_anio_previo")]
    public int TicketsAcumAnioPrevio { get; set; }

    [JsonPropertyName("cubiertos_acum_anio_previo")]
    public int CubiertosAcumAnioPrevio { get; set; }

    // Acumulado Año Actual (YTD)
    [JsonPropertyName("venta_acum_anio_actual")]
    public decimal VentaAcumAnioActual { get; set; }

    [JsonPropertyName("tickets_acum_anio_actual")]
    public int TicketsAcumAnioActual { get; set; }

    [JsonPropertyName("cubiertos_acum_anio_actual")]
    public int CubiertosAcumAnioActual { get; set; }
}

public class VentasResumenDiarioDto
{
    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_codigo")]
    public string FranquiciaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("grupo_economico_nombre")]
    public string? GrupoEconomicoNombre { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("moneda_codigo")]
    public string? MonedaCodigo { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    [JsonPropertyName("venta_bruta")]
    public decimal VentaBruta { get; set; }

    [JsonPropertyName("total_descuentos")]
    public decimal TotalDescuentos { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("tipo_cambio")]
    public decimal? TipoCambio { get; set; }

    [JsonPropertyName("ticket_promedio")]
    public decimal? TicketPromedio { get; set; }

    [JsonPropertyName("venta_por_cubierto")]
    public decimal? VentaPorCubierto { get; set; }
}

public class VentasPorFranquiciaDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_codigo")]
    public string FranquiciaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("grupo_economico_nombre")]
    public string? GrupoEconomicoNombre { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("moneda_codigo")]
    public string? MonedaCodigo { get; set; }

    [JsonPropertyName("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [JsonPropertyName("estado_integracion")]
    public string? EstadoIntegracion { get; set; }

    [JsonPropertyName("venta_neta_ytd")]
    public decimal VentaNetaYtd { get; set; }

    [JsonPropertyName("cubiertos_ytd")]
    public int CubiertosYtd { get; set; }

    [JsonPropertyName("tickets_ytd")]
    public int TicketsYtd { get; set; }

    [JsonPropertyName("venta_neta_mtd")]
    public decimal VentaNetaMtd { get; set; }

    [JsonPropertyName("cubiertos_mtd")]
    public int CubiertosMtd { get; set; }

    [JsonPropertyName("tickets_mtd")]
    public int TicketsMtd { get; set; }

    [JsonPropertyName("venta_neta_total")]
    public decimal VentaNetaTotal { get; set; }

    [JsonPropertyName("primera_venta")]
    public DateTime? PrimeraVenta { get; set; }

    [JsonPropertyName("ultima_venta")]
    public DateTime? UltimaVenta { get; set; }
}

public class VentasPorProductoDto
{
    [JsonPropertyName("producto_codigo")]
    public string ProductoCodigo { get; set; } = string.Empty;

    [JsonPropertyName("producto_nombre")]
    public string ProductoNombre { get; set; } = string.Empty;

    [JsonPropertyName("categoria")]
    public string? Categoria { get; set; }

    [JsonPropertyName("familia")]
    public string? Familia { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("cantidad_vendida")]
    public decimal CantidadVendida { get; set; }

    [JsonPropertyName("venta_bruta")]
    public decimal VentaBruta { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }
}

public class VentasPorMozoDto
{
    [JsonPropertyName("mozo_id")]
    public int MozoId { get; set; }

    [JsonPropertyName("mozo_nombre")]
    public string MozoNombre { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("total_propinas")]
    public decimal? TotalPropinas { get; set; }

    [JsonPropertyName("ticket_promedio")]
    public decimal? TicketPromedio { get; set; }
}

public class VentasPorTipoPlatoDto
{
    [JsonPropertyName("tipo_plato_codigo")]
    public string TipoPlatoCodigo { get; set; } = string.Empty;

    [JsonPropertyName("tipo_plato_nombre")]
    public string TipoPlatoNombre { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string? FranquiciaNombre { get; set; }

    [JsonPropertyName("cantidad_vendida")]
    public decimal CantidadVendida { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("porcentaje_venta")]
    public decimal? PorcentajeVenta { get; set; }
}

public class OcupacionMesasDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("mesas_totales")]
    public int? MesasTotales { get; set; }

    [JsonPropertyName("mesas_utilizadas")]
    public int MesasUtilizadas { get; set; }

    [JsonPropertyName("rotacion_mesas")]
    public decimal RotacionMesas { get; set; }

    [JsonPropertyName("porcentaje_ocupacion")]
    public decimal? PorcentajeOcupacion { get; set; }

    [JsonPropertyName("tiempo_promedio_minutos")]
    public int? TiempoPromedioMinutos { get; set; }

    [JsonPropertyName("total_tickets")]
    public int TotalTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }
}

public class EstadoIntegracionDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_codigo")]
    public string FranquiciaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("grupo_economico_nombre")]
    public string? GrupoEconomicoNombre { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("estado_integracion")]
    public string? EstadoIntegracion { get; set; }

    [JsonPropertyName("sistema_origen")]
    public string? SistemaOrigen { get; set; }

    [JsonPropertyName("contacto_nombre")]
    public string? ContactoNombre { get; set; }

    [JsonPropertyName("contacto_email")]
    public string? ContactoEmail { get; set; }

    [JsonPropertyName("contacto_telefono")]
    public string? ContactoTelefono { get; set; }

    [JsonPropertyName("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [JsonPropertyName("dias_sin_sincronizar")]
    public int? DiasSinSincronizar { get; set; }

    [JsonPropertyName("alerta_sincronizacion")]
    public bool AlertaSincronizacion { get; set; }

    [JsonPropertyName("ultimo_batch_id")]
    public string? UltimoBatchId { get; set; }

    [JsonPropertyName("ultimo_batch_estado")]
    public string? UltimoBatchEstado { get; set; }

    [JsonPropertyName("batches_ultimo_mes")]
    public int BatchesUltimoMes { get; set; }

    [JsonPropertyName("batches_con_error_ultimo_mes")]
    public int BatchesConErrorUltimoMes { get; set; }
}

public class DashboardFilters
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int? FranquiciaId { get; set; }
    public int? GrupoEconomicoId { get; set; }
    public string? Pais { get; set; }
    public int? Anio { get; set; }
    public int? Mes { get; set; }

    // v1.2: Currency and tax modes
    /// <summary>
    /// LOCAL = moneda local de cada franquicia (default)
    /// USD = consolidado en dólares usando tipo de cambio del día
    /// </summary>
    public string CurrencyMode { get; set; } = "LOCAL";

    /// <summary>
    /// NETO = ImporteNeto (con impuesto si aplica, default)
    /// NETO_SIN_IMP = ImporteNetoSinImpuesto (sin impuesto, comparable entre países)
    /// </summary>
    public string TaxMode { get; set; } = "NETO";

    /// <summary>
    /// Meal period filter for shift analysis
    /// </summary>
    public string? MealPeriod { get; set; }

    // v1.3: Bidirectional filtering for ClockChart
    /// <summary>
    /// Filter by specific product (for bidirectional filtering)
    /// </summary>
    public int? ProductoId { get; set; }

    /// <summary>
    /// Filter by specific hour (0-23) for bidirectional filtering
    /// </summary>
    public int? Hora { get; set; }
}

public class FranquiciaUpdateDto
{
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [JsonPropertyName("grupo_economico_id")]
    public int? GrupoEconomicoId { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("sistema_origen")]
    public string? SistemaOrigen { get; set; }

    [JsonPropertyName("contacto_nombre")]
    public string? ContactoNombre { get; set; }

    [JsonPropertyName("contacto_email")]
    public string? ContactoEmail { get; set; }

    [JsonPropertyName("contacto_telefono")]
    public string? ContactoTelefono { get; set; }

    [JsonPropertyName("zona_horaria")]
    public string? ZonaHoraria { get; set; }
}

public class FranquiciaDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("grupo_economico_id")]
    public int? GrupoEconomicoId { get; set; }

    [JsonPropertyName("grupo_economico_nombre")]
    public string? GrupoEconomicoNombre { get; set; }

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("zona_horaria")]
    public string? ZonaHoraria { get; set; }

    [JsonPropertyName("sistema_origen")]
    public string? SistemaOrigen { get; set; }

    [JsonPropertyName("contacto_nombre")]
    public string? ContactoNombre { get; set; }

    [JsonPropertyName("contacto_email")]
    public string? ContactoEmail { get; set; }

    [JsonPropertyName("contacto_telefono")]
    public string? ContactoTelefono { get; set; }

    [JsonPropertyName("estado_integracion")]
    public string? EstadoIntegracion { get; set; }

    [JsonPropertyName("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }
}

#region v1.2 Dashboard DTOs

/// <summary>
/// Ventas consolidadas con soporte para USD y separación de impuestos
/// </summary>
public class VentasConsolidadasDto
{
    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("moneda_local")]
    public string? MonedaLocal { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    // Local currency amounts
    [JsonPropertyName("venta_bruta_local")]
    public decimal VentaBrutaLocal { get; set; }

    [JsonPropertyName("venta_neta_local")]
    public decimal VentaNetaLocal { get; set; }

    [JsonPropertyName("venta_neta_sin_imp_local")]
    public decimal? VentaNetaSinImpLocal { get; set; }

    [JsonPropertyName("impuesto_local")]
    public decimal? ImpuestoLocal { get; set; }

    // USD amounts (when currency_mode=USD)
    [JsonPropertyName("tipo_cambio")]
    public decimal? TipoCambio { get; set; }

    [JsonPropertyName("venta_bruta_usd")]
    public decimal? VentaBrutaUsd { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("venta_neta_sin_imp_usd")]
    public decimal? VentaNetaSinImpUsd { get; set; }

    // Quality indicators
    [JsonPropertyName("calidad_impuesto")]
    public string? CalidadImpuesto { get; set; }

    [JsonPropertyName("calidad_tipo_cambio")]
    public string? CalidadTipoCambio { get; set; }

    // Calculated metrics
    [JsonPropertyName("ticket_promedio")]
    public decimal? TicketPromedio { get; set; }

    [JsonPropertyName("venta_por_cubierto")]
    public decimal? VentaPorCubierto { get; set; }
}

/// <summary>
/// Ventas por producto con peso relativo
/// </summary>
public class VentasPorProductoConPesoDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("producto_codigo")]
    public string ProductoCodigo { get; set; } = string.Empty;

    [JsonPropertyName("producto_nombre")]
    public string ProductoNombre { get; set; } = string.Empty;

    [JsonPropertyName("categoria")]
    public string? Categoria { get; set; }

    [JsonPropertyName("cantidad_vendida")]
    public decimal CantidadVendida { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("pct_sobre_total_franquicia")]
    public decimal? PctSobreTotalFranquicia { get; set; }

    [JsonPropertyName("pct_sobre_categoria")]
    public decimal? PctSobreCategoria { get; set; }

    [JsonPropertyName("pct_categoria_sobre_total")]
    public decimal? PctCategoriaSobreTotal { get; set; }

    [JsonPropertyName("ranking_franquicia")]
    public int? RankingFranquicia { get; set; }

    [JsonPropertyName("ranking_categoria")]
    public int? RankingCategoria { get; set; }
}

/// <summary>
/// Ventas por período de comida (turno)
/// </summary>
public class VentasPorMealPeriodDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("meal_period")]
    public string MealPeriod { get; set; } = string.Empty;

    [JsonPropertyName("meal_period_origen")]
    public string? MealPeriodOrigen { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("venta_neta_sin_imp")]
    public decimal? VentaNetaSinImp { get; set; }

    [JsonPropertyName("ticket_promedio")]
    public decimal? TicketPromedio { get; set; }

    [JsonPropertyName("pct_sobre_total")]
    public decimal? PctSobreTotal { get; set; }
}

/// <summary>
/// Ranking de días (mejores/peores)
/// </summary>
public class DiaRankingDto
{
    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("dia_semana")]
    public string? DiaSemana { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    [JsonPropertyName("ranking_mejor")]
    public int RankingMejor { get; set; }

    [JsonPropertyName("ranking_peor")]
    public int RankingPeor { get; set; }
}

/// <summary>
/// Ventas del día con comparativos
/// </summary>
public class VentasDelDiaDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("fecha_hoy")]
    public DateTime FechaHoy { get; set; }

    [JsonPropertyName("venta_hoy")]
    public decimal VentaHoy { get; set; }

    [JsonPropertyName("venta_hoy_usd")]
    public decimal? VentaHoyUsd { get; set; }

    [JsonPropertyName("tickets_hoy")]
    public int TicketsHoy { get; set; }

    [JsonPropertyName("cubiertos_hoy")]
    public int? CubiertosHoy { get; set; }

    [JsonPropertyName("venta_mismo_dia_semana_pasada")]
    public decimal? VentaMismoDiaSemPasada { get; set; }

    [JsonPropertyName("var_vs_semana_pasada_pct")]
    public decimal? VarVsSemPasadaPct { get; set; }

    [JsonPropertyName("promedio_mensual")]
    public decimal? PromedioMensual { get; set; }

    [JsonPropertyName("var_vs_promedio_pct")]
    public decimal? VarVsPromedioPct { get; set; }
}

/// <summary>
/// Comparativo mensual
/// </summary>
public class ComparativoMensualDto
{
    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("anio")]
    public int Anio { get; set; }

    [JsonPropertyName("mes")]
    public int Mes { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal? VentaNetaUsd { get; set; }

    [JsonPropertyName("cantidad_tickets")]
    public int CantidadTickets { get; set; }

    [JsonPropertyName("total_cubiertos")]
    public int? TotalCubiertos { get; set; }

    [JsonPropertyName("venta_mes_anterior")]
    public decimal? VentaMesAnterior { get; set; }

    [JsonPropertyName("var_vs_mes_anterior_pct")]
    public decimal? VarVsMesAnteriorPct { get; set; }

    [JsonPropertyName("venta_mismo_mes_anio_anterior")]
    public decimal? VentaMismoMesAnioAnterior { get; set; }

    [JsonPropertyName("var_vs_anio_anterior_pct")]
    public decimal? VarVsAnioAnteriorPct { get; set; }
}

#endregion

public class GrupoEconomicoDto
{
    [JsonPropertyName("grupo_economico_id")]
    public int GrupoEconomicoId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }
}

#region Transacciones / Tickets DTOs

/// <summary>
/// Ticket/transacción con datos de encabezado para dashboard
/// </summary>
public class TransaccionDto
{
    [JsonPropertyName("ticket_id")]
    public long TicketId { get; set; }

    [JsonPropertyName("numero_ticket")]
    public string NumeroTicket { get; set; } = string.Empty;

    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("fecha_apertura")]
    public DateTime FechaApertura { get; set; }

    [JsonPropertyName("fecha_cierre")]
    public DateTime? FechaCierre { get; set; }

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("periodo_comida")]
    public string? PeriodoComida { get; set; }

    [JsonPropertyName("numero_mesa")]
    public string? NumeroMesa { get; set; }

    [JsonPropertyName("area_mesa")]
    public string? AreaMesa { get; set; }

    [JsonPropertyName("nombre_mozo")]
    public string? NombreMozo { get; set; }

    [JsonPropertyName("cantidad_cubiertos")]
    public int? CantidadCubiertos { get; set; }

    [JsonPropertyName("moneda_codigo")]
    public string MonedaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("importe_bruto")]
    public decimal ImporteBruto { get; set; }

    [JsonPropertyName("importe_descuento")]
    public decimal ImporteDescuento { get; set; }

    [JsonPropertyName("importe_neto")]
    public decimal ImporteNeto { get; set; }

    [JsonPropertyName("importe_impuesto")]
    public decimal ImporteImpuesto { get; set; }

    [JsonPropertyName("importe_propina")]
    public decimal ImportePropina { get; set; }

    [JsonPropertyName("importe_total_pagado")]
    public decimal ImporteTotalPagado { get; set; }

    [JsonPropertyName("tiempo_consumo_minutos")]
    public int? TiempoConsumoMinutos { get; set; }

    [JsonPropertyName("cantidad_items")]
    public int CantidadItems { get; set; }
}

/// <summary>
/// Detalle de línea de un ticket para dashboard
/// </summary>
public class TransaccionDetalleDto
{
    [JsonPropertyName("detalle_id")]
    public long DetalleId { get; set; }

    [JsonPropertyName("ticket_id")]
    public long TicketId { get; set; }

    [JsonPropertyName("codigo_producto")]
    public string CodigoProducto { get; set; } = string.Empty;

    [JsonPropertyName("nombre_producto")]
    public string NombreProducto { get; set; } = string.Empty;

    [JsonPropertyName("categoria")]
    public string? Categoria { get; set; }

    [JsonPropertyName("familia")]
    public string? Familia { get; set; }

    [JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }

    [JsonPropertyName("precio_unitario")]
    public decimal PrecioUnitario { get; set; }

    [JsonPropertyName("importe_bruto")]
    public decimal ImporteBruto { get; set; }

    [JsonPropertyName("importe_descuento")]
    public decimal ImporteDescuento { get; set; }

    [JsonPropertyName("importe_neto")]
    public decimal ImporteNeto { get; set; }

    [JsonPropertyName("esta_anulado")]
    public bool EstaAnulado { get; set; }

    [JsonPropertyName("notas")]
    public string? Notas { get; set; }
}

/// <summary>
/// DTO para ventas agrupadas por hora del día
/// </summary>
public class VentasPorHoraDto
{
    [JsonPropertyName("hora")]
    public int Hora { get; set; }

    [JsonPropertyName("franquicia_id")]
    public int? FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_nombre")]
    public string? FranquiciaNombre { get; set; }

    [JsonPropertyName("cubiertos")]
    public int Cubiertos { get; set; }

    [JsonPropertyName("tickets")]
    public int Tickets { get; set; }

    [JsonPropertyName("venta_neta")]
    public decimal VentaNeta { get; set; }

    [JsonPropertyName("venta_neta_usd")]
    public decimal VentaNetaUsd { get; set; }
}

#endregion
