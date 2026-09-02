using ClosedXML.Excel;
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

    /// <summary>
    /// Exporta transacciones a Excel con moneda de origen, USD y tipo de cambio
    /// </summary>
    [HttpGet("transacciones/export/excel")]
    [SwaggerOperation(Summary = "Exportar transacciones a Excel", Description = "Genera archivo Excel con transacciones incluyendo moneda de origen, montos USD y tipo de cambio")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportTransaccionesExcel(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] string? pais,
        [FromQuery] int? franquiciaId)
    {
        var filters = new DashboardFilters
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Pais = pais,
            FranquiciaId = franquiciaId
        };

        var transacciones = await _dashboardService.GetTransaccionesExportAsync(filters);
        var items = await _dashboardService.GetTransaccionesItemsExportAsync(filters);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transacciones");

        // Headers
        var headers = new[] {
            "Ticket ID", "Numero Ticket", "Fecha Negocio", "Fecha Apertura", "Estado",
            "Franquicia Codigo", "Franquicia Nombre", "Pais", "Ciudad",
            "Mesa", "Area", "Mozo", "Cubiertos",
            "Moneda", "Bruto Local", "Descuento Local", "Neto Local", "Impuesto Local", "Propina Local", "Total Local",
            "Tipo Cambio", "Bruto USD", "Descuento USD", "Neto USD", "Impuesto USD", "Propina USD", "Total USD",
            "Calidad TC"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        // Data rows
        int row = 2;
        foreach (var t in transacciones)
        {
            worksheet.Cell(row, 1).Value = t.TicketId;
            worksheet.Cell(row, 2).Value = t.NumeroTicket;
            worksheet.Cell(row, 3).Value = t.FechaNegocio;
            worksheet.Cell(row, 4).Value = t.FechaApertura;
            worksheet.Cell(row, 5).Value = t.Estado;
            worksheet.Cell(row, 6).Value = t.FranquiciaCodigo;
            worksheet.Cell(row, 7).Value = t.FranquiciaNombre;
            worksheet.Cell(row, 8).Value = t.Pais;
            worksheet.Cell(row, 9).Value = t.Ciudad;
            worksheet.Cell(row, 10).Value = t.NumeroMesa;
            worksheet.Cell(row, 11).Value = t.AreaMesa;
            worksheet.Cell(row, 12).Value = t.NombreMozo;
            worksheet.Cell(row, 13).Value = t.CantidadCubiertos;
            worksheet.Cell(row, 14).Value = t.MonedaCodigo;
            worksheet.Cell(row, 15).Value = t.ImporteBrutoLocal;
            worksheet.Cell(row, 16).Value = t.ImporteDescuentoLocal;
            worksheet.Cell(row, 17).Value = t.ImporteNetoLocal;
            worksheet.Cell(row, 18).Value = t.ImporteImpuestoLocal;
            worksheet.Cell(row, 19).Value = t.ImportePropinaLocal;
            worksheet.Cell(row, 20).Value = t.ImporteTotalLocal;
            worksheet.Cell(row, 21).Value = t.TipoCambio;
            worksheet.Cell(row, 22).Value = t.ImporteBrutoUsd;
            worksheet.Cell(row, 23).Value = t.ImporteDescuentoUsd;
            worksheet.Cell(row, 24).Value = t.ImporteNetoUsd;
            worksheet.Cell(row, 25).Value = t.ImporteImpuestoUsd;
            worksheet.Cell(row, 26).Value = t.ImportePropinaUsd;
            worksheet.Cell(row, 27).Value = t.ImporteTotalUsd;
            worksheet.Cell(row, 28).Value = t.CalidadTipoCambio;

            // Format currency columns
            for (int c = 15; c <= 20; c++) worksheet.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 21).Style.NumberFormat.Format = "#,##0.0000";
            for (int c = 22; c <= 27; c++) worksheet.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";

            row++;
        }

        worksheet.Columns().AdjustToContents();

        // Add summary sheet
        var summarySheet = workbook.Worksheets.Add("Resumen");
        summarySheet.Cell(1, 1).Value = "Exportación de Transacciones";
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 14;

        summarySheet.Cell(3, 1).Value = "Período:";
        summarySheet.Cell(3, 2).Value = $"{fechaDesde:yyyy-MM-dd} a {fechaHasta:yyyy-MM-dd}";
        summarySheet.Cell(4, 1).Value = "País:";
        summarySheet.Cell(4, 2).Value = pais ?? "Todos";
        summarySheet.Cell(5, 1).Value = "Franquicia:";
        summarySheet.Cell(5, 2).Value = franquiciaId?.ToString() ?? "Todas";
        summarySheet.Cell(6, 1).Value = "Total Transacciones:";
        summarySheet.Cell(6, 2).Value = transacciones.Count();
        summarySheet.Cell(7, 1).Value = "Generado:";
        summarySheet.Cell(7, 2).Value = DateTime.Now;

        summarySheet.Cell(9, 1).Value = "Totales USD:";
        summarySheet.Cell(9, 1).Style.Font.Bold = true;

        // Neto = subtotal sin impuestos
        summarySheet.Cell(10, 1).Value = "Subtotal (Neto) USD:";
        summarySheet.Cell(10, 2).Value = transacciones.Sum(t => t.ImporteNetoUsd);
        summarySheet.Cell(10, 2).Style.NumberFormat.Format = "#,##0.00";

        // Impuestos
        summarySheet.Cell(11, 1).Value = "Impuestos USD:";
        summarySheet.Cell(11, 2).Value = transacciones.Sum(t => t.ImporteImpuestoUsd);
        summarySheet.Cell(11, 2).Style.NumberFormat.Format = "#,##0.00";

        // Descuentos
        summarySheet.Cell(12, 1).Value = "Descuentos USD:";
        summarySheet.Cell(12, 2).Value = transacciones.Sum(t => t.ImporteDescuentoUsd);
        summarySheet.Cell(12, 2).Style.NumberFormat.Format = "#,##0.00";

        // Propinas
        summarySheet.Cell(13, 1).Value = "Propinas USD:";
        summarySheet.Cell(13, 2).Value = transacciones.Sum(t => t.ImportePropinaUsd);
        summarySheet.Cell(13, 2).Style.NumberFormat.Format = "#,##0.00";

        // Total Pagado = lo que realmente se cobró
        summarySheet.Cell(14, 1).Value = "TOTAL PAGADO USD:";
        summarySheet.Cell(14, 1).Style.Font.Bold = true;
        summarySheet.Cell(14, 2).Value = transacciones.Sum(t => t.ImporteTotalUsd);
        summarySheet.Cell(14, 2).Style.NumberFormat.Format = "#,##0.00";
        summarySheet.Cell(14, 2).Style.Font.Bold = true;

        summarySheet.Columns().AdjustToContents();

        // Add AperturaTotal sheet (item by item)
        var itemsSheet = workbook.Worksheets.Add("AperturaTotal");

        var itemHeaders = new[] {
            "Ticket ID", "Numero Ticket", "Fecha Negocio", "Fecha Apertura", "Periodo Comida",
            "Franquicia Codigo", "Franquicia Nombre", "Pais", "Ciudad",
            "Mesa", "Area", "Mozo",
            "Detalle ID", "Codigo Producto", "Nombre Producto", "Categoria", "Familia",
            "Cantidad", "Precio Unitario",
            "Moneda", "Bruto Local", "Descuento Local", "Neto Local",
            "Tipo Cambio", "Bruto USD", "Descuento USD", "Neto USD",
            "Anulado", "Notas"
        };

        for (int i = 0; i < itemHeaders.Length; i++)
        {
            itemsSheet.Cell(1, i + 1).Value = itemHeaders[i];
            itemsSheet.Cell(1, i + 1).Style.Font.Bold = true;
            itemsSheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        int itemRow = 2;
        foreach (var item in items)
        {
            itemsSheet.Cell(itemRow, 1).Value = item.TicketId;
            itemsSheet.Cell(itemRow, 2).Value = item.NumeroTicket;
            itemsSheet.Cell(itemRow, 3).Value = item.FechaNegocio;
            itemsSheet.Cell(itemRow, 4).Value = item.FechaApertura;
            itemsSheet.Cell(itemRow, 5).Value = item.PeriodoComida;
            itemsSheet.Cell(itemRow, 6).Value = item.FranquiciaCodigo;
            itemsSheet.Cell(itemRow, 7).Value = item.FranquiciaNombre;
            itemsSheet.Cell(itemRow, 8).Value = item.Pais;
            itemsSheet.Cell(itemRow, 9).Value = item.Ciudad;
            itemsSheet.Cell(itemRow, 10).Value = item.NumeroMesa;
            itemsSheet.Cell(itemRow, 11).Value = item.AreaMesa;
            itemsSheet.Cell(itemRow, 12).Value = item.NombreMozo;
            itemsSheet.Cell(itemRow, 13).Value = item.DetalleId;
            itemsSheet.Cell(itemRow, 14).Value = item.CodigoProducto;
            itemsSheet.Cell(itemRow, 15).Value = item.NombreProducto;
            itemsSheet.Cell(itemRow, 16).Value = item.Categoria;
            itemsSheet.Cell(itemRow, 17).Value = item.Familia;
            itemsSheet.Cell(itemRow, 18).Value = item.Cantidad;
            itemsSheet.Cell(itemRow, 19).Value = item.PrecioUnitario;
            itemsSheet.Cell(itemRow, 20).Value = item.MonedaCodigo;
            itemsSheet.Cell(itemRow, 21).Value = item.ImporteBrutoLocal;
            itemsSheet.Cell(itemRow, 22).Value = item.ImporteDescuentoLocal;
            itemsSheet.Cell(itemRow, 23).Value = item.ImporteNetoLocal;
            itemsSheet.Cell(itemRow, 24).Value = item.TipoCambio;
            itemsSheet.Cell(itemRow, 25).Value = item.ImporteBrutoUsd;
            itemsSheet.Cell(itemRow, 26).Value = item.ImporteDescuentoUsd;
            itemsSheet.Cell(itemRow, 27).Value = item.ImporteNetoUsd;
            itemsSheet.Cell(itemRow, 28).Value = item.EstaAnulado ? "SI" : "NO";
            itemsSheet.Cell(itemRow, 29).Value = item.Notas;

            // Format numeric columns
            itemsSheet.Cell(itemRow, 18).Style.NumberFormat.Format = "#,##0.00";
            itemsSheet.Cell(itemRow, 19).Style.NumberFormat.Format = "#,##0.00";
            for (int c = 21; c <= 23; c++) itemsSheet.Cell(itemRow, c).Style.NumberFormat.Format = "#,##0.00";
            itemsSheet.Cell(itemRow, 24).Style.NumberFormat.Format = "#,##0.0000";
            for (int c = 25; c <= 27; c++) itemsSheet.Cell(itemRow, c).Style.NumberFormat.Format = "#,##0.00";

            itemRow++;
        }

        itemsSheet.Columns().AdjustToContents();

        // Update summary with item count
        summarySheet.Cell(16, 1).Value = "Total Items (Líneas):";
        summarySheet.Cell(16, 2).Value = items.Count();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"transacciones_{fechaDesde:yyyyMMdd}_{fechaHasta:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    #endregion
}
