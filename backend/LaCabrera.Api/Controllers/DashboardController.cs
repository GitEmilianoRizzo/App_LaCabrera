using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Obtiene datos para el dashboard principal (home)
    /// </summary>
    [HttpGet("home")]
    [SwaggerOperation(Summary = "Dashboard principal", Description = "Matriz con indicadores por franquicia en rango de fechas (por defecto ultimo mes)")]
    [SwaggerResponse(200, "Datos del dashboard", typeof(IEnumerable<HomeDashboardDto>))]
    public async Task<ActionResult<IEnumerable<HomeDashboardDto>>> GetHomeDashboard(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? franquiciaId,
        [FromQuery] string? pais)
    {
        var filters = new DashboardFilters
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            FranquiciaId = franquiciaId,
            Pais = pais
        };

        var data = await _dashboardService.GetHomeDashboardAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene resumen diario de ventas
    /// </summary>
    [HttpGet("ventas/resumen")]
    [SwaggerOperation(Summary = "Resumen diario de ventas", Description = "Ventas agregadas por dia y franquicia")]
    [SwaggerResponse(200, "Resumen de ventas", typeof(IEnumerable<VentasResumenDiarioDto>))]
    public async Task<ActionResult<IEnumerable<VentasResumenDiarioDto>>> GetVentasResumen(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? franquiciaId,
        [FromQuery] int? grupoEconomicoId)
    {
        var filters = new DashboardFilters
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            FranquiciaId = franquiciaId,
            GrupoEconomicoId = grupoEconomicoId
        };

        var data = await _dashboardService.GetVentasResumenDiarioAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por franquicia
    /// </summary>
    [HttpGet("ventas/por-franquicia")]
    [SwaggerOperation(Summary = "Ventas por franquicia", Description = "Ventas acumuladas por franquicia, con filtros opcionales de fecha")]
    [SwaggerResponse(200, "Ventas por franquicia", typeof(IEnumerable<VentasPorFranquiciaDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorFranquiciaDto>>> GetVentasPorFranquicia(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? franquiciaId)
    {
        var filters = new DashboardFilters
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            FranquiciaId = franquiciaId
        };

        var data = await _dashboardService.GetVentasPorFranquiciaAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por producto
    /// </summary>
    [HttpGet("ventas/por-producto")]
    [SwaggerOperation(Summary = "Ventas por producto", Description = "Ranking de productos mas vendidos")]
    [SwaggerResponse(200, "Ventas por producto", typeof(IEnumerable<VentasPorProductoDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorProductoDto>>> GetVentasPorProducto(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };

        var data = await _dashboardService.GetVentasPorProductoAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por mozo
    /// </summary>
    [HttpGet("ventas/por-mozo")]
    [SwaggerOperation(Summary = "Ventas por mozo", Description = "Ranking de mozos por venta")]
    [SwaggerResponse(200, "Ventas por mozo", typeof(IEnumerable<VentasPorMozoDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorMozoDto>>> GetVentasPorMozo(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde
        };

        var data = await _dashboardService.GetVentasPorMozoAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por tipo de plato
    /// </summary>
    [HttpGet("ventas/por-tipo-plato")]
    [SwaggerOperation(Summary = "Ventas por tipo de plato", Description = "Distribucion de ventas por categoria de producto")]
    [SwaggerResponse(200, "Ventas por tipo de plato", typeof(IEnumerable<VentasPorTipoPlatoDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorTipoPlatoDto>>> GetVentasPorTipoPlato(
        [FromQuery] int? franquiciaId)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId
        };

        var data = await _dashboardService.GetVentasPorTipoPlatoAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ocupacion y rotacion de mesas
    /// </summary>
    [HttpGet("ventas/ocupacion-mesas")]
    [SwaggerOperation(Summary = "Ocupacion de mesas", Description = "Metricas de ocupacion y rotacion de mesas")]
    [SwaggerResponse(200, "Ocupacion de mesas", typeof(IEnumerable<OcupacionMesasDto>))]
    public async Task<ActionResult<IEnumerable<OcupacionMesasDto>>> GetOcupacionMesas(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde
        };

        var data = await _dashboardService.GetOcupacionMesasAsync(filters);
        return Ok(data);
    }

    #region v1.2 Endpoints - USD Consolidation and Tax Axis

    /// <summary>
    /// Obtiene ventas consolidadas con soporte USD y separación de impuestos
    /// </summary>
    [HttpGet("v2/ventas/consolidadas")]
    [SwaggerOperation(Summary = "Ventas consolidadas v2", Description = "Ventas con conversión USD y separación de impuestos. Incluye indicadores de calidad de datos.")]
    [SwaggerResponse(200, "Ventas consolidadas", typeof(IEnumerable<VentasConsolidadasDto>))]
    public async Task<ActionResult<IEnumerable<VentasConsolidadasDto>>> GetVentasConsolidadas(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? franquiciaId,
        [FromQuery] string? pais,
        [FromQuery] string currencyMode = "LOCAL",
        [FromQuery] string taxMode = "NETO")
    {
        var filters = new DashboardFilters
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            FranquiciaId = franquiciaId,
            Pais = pais,
            CurrencyMode = currencyMode,
            TaxMode = taxMode
        };

        var data = await _dashboardService.GetVentasConsolidadasAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por producto con peso relativo
    /// </summary>
    [HttpGet("v2/ventas/por-producto")]
    [SwaggerOperation(Summary = "Ventas por producto v2", Description = "Ranking de productos con peso relativo sobre total y categoría")]
    [SwaggerResponse(200, "Ventas por producto con peso", typeof(IEnumerable<VentasPorProductoConPesoDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorProductoConPesoDto>>> GetVentasPorProductoConPeso(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };

        var data = await _dashboardService.GetVentasPorProductoConPesoAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas por turno/período de comida
    /// </summary>
    [HttpGet("v2/ventas/por-turno")]
    [SwaggerOperation(Summary = "Ventas por turno", Description = "Análisis de ventas por período de comida (desayuno, almuerzo, cena, etc.)")]
    [SwaggerResponse(200, "Ventas por turno", typeof(IEnumerable<VentasPorMealPeriodDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorMealPeriodDto>>> GetVentasPorMealPeriod(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] string? mealPeriod)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            MealPeriod = mealPeriod
        };

        var data = await _dashboardService.GetVentasPorMealPeriodAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ranking de mejores/peores días
    /// </summary>
    [HttpGet("v2/ventas/ranking-dias")]
    [SwaggerOperation(Summary = "Ranking de días", Description = "Días con mayor/menor venta")]
    [SwaggerResponse(200, "Ranking de días", typeof(IEnumerable<DiaRankingDto>))]
    public async Task<ActionResult<IEnumerable<DiaRankingDto>>> GetDiaRanking(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] bool mejores = true,
        [FromQuery] int top = 10)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };

        var data = await _dashboardService.GetDiaRankingAsync(filters, mejores, top);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas del día actual con comparativos
    /// </summary>
    [HttpGet("v2/ventas/hoy")]
    [SwaggerOperation(Summary = "Ventas del día", Description = "Ventas de hoy vs mismo día semana pasada vs promedio mensual")]
    [SwaggerResponse(200, "Ventas del día", typeof(IEnumerable<VentasDelDiaDto>))]
    public async Task<ActionResult<IEnumerable<VentasDelDiaDto>>> GetVentasDelDia(
        [FromQuery] int? franquiciaId)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId
        };

        var data = await _dashboardService.GetVentasDelDiaAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene comparativo mensual
    /// </summary>
    [HttpGet("v2/ventas/comparativo-mensual")]
    [SwaggerOperation(Summary = "Comparativo mensual", Description = "Ventas del mes vs mes anterior vs mismo mes año anterior")]
    [SwaggerResponse(200, "Comparativo mensual", typeof(IEnumerable<ComparativoMensualDto>))]
    public async Task<ActionResult<IEnumerable<ComparativoMensualDto>>> GetComparativoMensual(
        [FromQuery] int? franquiciaId,
        [FromQuery] int? anio,
        [FromQuery] int? mes)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            Anio = anio,
            Mes = mes
        };

        var data = await _dashboardService.GetComparativoMensualAsync(filters);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene ventas agrupadas por hora del día
    /// </summary>
    [HttpGet("v2/ventas/por-hora")]
    [SwaggerOperation(Summary = "Ventas por hora", Description = "Distribución de ventas/cubiertos por hora del día (0-23) para visualización tipo reloj")]
    [SwaggerResponse(200, "Ventas por hora", typeof(IEnumerable<VentasPorHoraDto>))]
    public async Task<ActionResult<IEnumerable<VentasPorHoraDto>>> GetVentasPorHora(
        [FromQuery] int? franquiciaId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? productoId,
        [FromQuery] string? mealPeriod)
    {
        var filters = new DashboardFilters
        {
            FranquiciaId = franquiciaId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            ProductoId = productoId,
            MealPeriod = mealPeriod
        };

        var data = await _dashboardService.GetVentasPorHoraAsync(filters);
        return Ok(data);
    }

    #endregion

    #region Tickets / Transacciones

    /// <summary>
    /// Obtiene transacciones de una franquicia en un rango de fechas
    /// </summary>
    [HttpGet("transacciones")]
    [SwaggerOperation(Summary = "Transacciones por franquicia", Description = "Lista de transacciones/tickets de una franquicia en un período")]
    [SwaggerResponse(200, "Lista de transacciones", typeof(IEnumerable<TransaccionDto>))]
    public async Task<ActionResult<IEnumerable<TransaccionDto>>> GetTransaccionesByFranquicia(
        [FromQuery] int franquiciaId,
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        var data = await _dashboardService.GetTransaccionesByFranquiciaAsync(franquiciaId, fechaDesde, fechaHasta);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene el detalle de líneas de una transacción
    /// </summary>
    [HttpGet("transacciones/{ticketId}/detalle")]
    [SwaggerOperation(Summary = "Detalle de transacción", Description = "Líneas de productos de una transacción específica")]
    [SwaggerResponse(200, "Detalle de la transacción", typeof(IEnumerable<TransaccionDetalleDto>))]
    public async Task<ActionResult<IEnumerable<TransaccionDetalleDto>>> GetTransaccionDetalle(long ticketId)
    {
        var data = await _dashboardService.GetTransaccionDetalleAsync(ticketId);
        return Ok(data);
    }

    #endregion
}
