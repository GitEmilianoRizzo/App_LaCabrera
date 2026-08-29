using System.Data;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Repositories;

public class IngestionRepository : IIngestionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IngestionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<(long IngestionBatchId, bool IsDuplicate)> RegisterBatchAsync(
        string batchId, int franquiciaId, DateTime businessDate, string schemaVersion,
        string uploadType, string origin, ControlTotals controlTotals, SourceSystem? sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("BatchId", batchId);
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("FechaNegocio", businessDate);
        parameters.Add("SchemaVersion", schemaVersion);
        parameters.Add("TipoCarga", uploadType);
        parameters.Add("OrigenIngesta", origin);
        parameters.Add("TicketCountRecibido", controlTotals.TicketCount);
        parameters.Add("ItemLineCountRecibido", controlTotals.ItemLineCount);
        parameters.Add("GrossSalesRecibido", controlTotals.GrossSalesAmount);
        parameters.Add("DiscountRecibido", controlTotals.DiscountAmount);
        parameters.Add("NetSalesRecibido", controlTotals.NetSalesAmount);
        parameters.Add("TaxRecibido", controlTotals.TaxAmount);
        parameters.Add("CoversTotalRecibido", controlTotals.CoversTotal);
        parameters.Add("SistemaOrigenNombre", sourceSystem?.SystemName);
        parameters.Add("SistemaOrigenVersion", sourceSystem?.SystemVersion);
        parameters.Add("ExportadoPor", sourceSystem?.ExportedBy);
        parameters.Add("IngestionBatchId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("EsDuplicado", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("stg.sp_RegistrarBatchIngesta", parameters, commandType: CommandType.StoredProcedure);

        return (parameters.Get<long>("IngestionBatchId"), parameters.Get<bool>("EsDuplicado"));
    }

    public async Task SaveRawJsonAsync(long ingestionBatchId, string jsonContent)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "stg.sp_GuardarJsonCrudo",
            new { IngestionBatchId = ingestionBatchId, JsonContent = jsonContent },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateBatchStatusAsync(long ingestionBatchId, string status, string? message = null,
        int? errorCount = null, int? warningCount = null, int? ticketCountCalculated = null,
        int? itemLineCountCalculated = null, decimal? grossSalesCalculated = null, decimal? netSalesCalculated = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "stg.sp_ActualizarEstadoBatch",
            new
            {
                IngestionBatchId = ingestionBatchId,
                Estado = status,
                MensajeEstado = message,
                CantidadErrores = errorCount,
                CantidadWarnings = warningCount,
                TicketCountCalculado = ticketCountCalculated,
                ItemLineCountCalculado = itemLineCountCalculated,
                GrossSalesCalculado = grossSalesCalculated,
                NetSalesCalculado = netSalesCalculated
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RegisterErrorAsync(long ingestionBatchId, string severity, string errorCode,
        string errorMessage, string? field = null, string? receivedValue = null,
        string? ticketId = null, string? lineId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "stg.sp_RegistrarErrorIngesta",
            new
            {
                IngestionBatchId = ingestionBatchId,
                Severidad = severity,
                CodigoError = errorCode,
                MensajeError = errorMessage,
                CampoAfectado = field,
                ValorRecibido = receivedValue,
                TicketId = ticketId,
                LineaId = lineId
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<(long VentaTicketId, bool IsDuplicate)> InsertTicketAsync(
        long ingestionBatchId, int franquiciaId, TicketDto ticket, int? mesaId, int? mozoId, int? monedaId,
        // v1.2: Tax axis
        decimal? importeNetoSinImpuesto = null,
        bool? importesIncluyenImpuesto = null,
        string? calidadImpuesto = null,
        // v1.2: Shift
        string? mealPeriodOrigen = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("IngestionBatchId", ingestionBatchId);
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("ExternalTicketId", ticket.TicketId);
        parameters.Add("NumeroTicket", ticket.TicketNumber);
        parameters.Add("ExternalOrderId", ticket.ExternalOrderId);
        parameters.Add("TipoDocumentoFiscal", ticket.FiscalDocument?.DocumentType);
        parameters.Add("NumeroDocumentoFiscal", ticket.FiscalDocument?.DocumentNumber);
        parameters.Add("Estado", ticket.Status);
        parameters.Add("FechaNegocio", DateTime.Parse(ticket.BusinessDate));
        parameters.Add("FechaApertura", DateTimeOffset.Parse(ticket.OpenedAt).UtcDateTime);
        parameters.Add("FechaCierre", string.IsNullOrEmpty(ticket.ClosedAt) ? null : (DateTime?)DateTimeOffset.Parse(ticket.ClosedAt).UtcDateTime);
        parameters.Add("PeriodoComida", ticket.MealPeriod);
        parameters.Add("MesaId", mesaId);
        parameters.Add("NumeroMesa", ticket.Table?.TableNumber);
        parameters.Add("AreaMesa", ticket.Table?.TableArea);
        parameters.Add("MozoId", mozoId);
        parameters.Add("NombreMozo", ticket.Waiter?.WaiterName);
        parameters.Add("CantidadCubiertos", ticket.Covers);
        parameters.Add("MonedaId", monedaId);
        parameters.Add("CodigoMoneda", ticket.Currency);
        parameters.Add("ImporteBruto", ticket.Amounts.GrossAmount);
        parameters.Add("ImporteDescuento", ticket.Amounts.DiscountAmount);
        parameters.Add("ImporteNeto", ticket.Amounts.NetAmount);
        parameters.Add("ImporteImpuesto", ticket.Amounts.TaxAmount ?? 0);
        parameters.Add("ImporteServicio", ticket.Amounts.ServiceChargeAmount ?? 0);
        parameters.Add("ImportePropina", ticket.Amounts.TipAmount ?? 0);
        parameters.Add("ImporteTotalPagado", ticket.Amounts.TotalPaidAmount ?? 0);

        // v1.2: Tax axis
        parameters.Add("ImporteNetoSinImpuesto", importeNetoSinImpuesto);
        parameters.Add("ImportesIncluyenImpuesto", importesIncluyenImpuesto);
        parameters.Add("CalidadImpuesto", calidadImpuesto);

        // v1.2: Shift
        parameters.Add("TurnoCodigo", ticket.Shift?.ShiftId);
        parameters.Add("TurnoNombre", ticket.Shift?.ShiftName);
        parameters.Add("TurnoApertura", string.IsNullOrEmpty(ticket.Shift?.OpenedAt) ? null : (DateTime?)DateTimeOffset.Parse(ticket.Shift.OpenedAt).UtcDateTime);
        parameters.Add("TurnoCierre", string.IsNullOrEmpty(ticket.Shift?.ClosedAt) ? null : (DateTime?)DateTimeOffset.Parse(ticket.Shift.ClosedAt).UtcDateTime);
        parameters.Add("MealPeriodOrigen", mealPeriodOrigen);

        parameters.Add("VentaTicketId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("EsDuplicado", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("fact.sp_InsertarTicketIdempotente", parameters, commandType: CommandType.StoredProcedure);

        return (parameters.Get<long>("VentaTicketId"), parameters.Get<bool>("EsDuplicado"));
    }

    public async Task<(long VentaTicketDetalleId, bool IsDuplicate)> InsertTicketDetailAsync(
        long ventaTicketId, long ingestionBatchId, int franquiciaId, TicketItemDto item,
        int? productoId, int? tipoPlatoId,
        // v1.2: Tax axis
        decimal? importeNetoSinImpuesto = null,
        decimal? tasaImpuesto = null,
        bool? importesIncluyenImpuesto = null,
        string? calidadImpuesto = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("VentaTicketId", ventaTicketId);
        parameters.Add("IngestionBatchId", ingestionBatchId);
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("ExternalLineId", item.LineId);
        parameters.Add("ProductoId", productoId);
        parameters.Add("CodigoProducto", item.ProductCode);
        parameters.Add("NombreProducto", item.ProductName);
        parameters.Add("TipoPlatoId", tipoPlatoId);
        parameters.Add("CategoriaProducto", item.ProductCategory);
        parameters.Add("FamiliaProducto", item.ProductFamily);
        parameters.Add("SubFamiliaProducto", item.ProductSubfamily);
        parameters.Add("Cantidad", item.Quantity);
        parameters.Add("PrecioUnitario", item.UnitPrice);
        parameters.Add("ImporteBruto", item.GrossAmount);
        parameters.Add("ImporteDescuento", item.DiscountAmount);
        parameters.Add("ImporteNeto", item.NetAmount);
        parameters.Add("ImporteImpuesto", item.TaxAmount ?? 0);
        parameters.Add("EstaAnulado", item.IsVoided ?? false);
        parameters.Add("MotivoAnulacion", item.VoidReason);
        parameters.Add("FechaPedido", string.IsNullOrEmpty(item.OrderedAt) ? null : (DateTime?)DateTimeOffset.Parse(item.OrderedAt).UtcDateTime);
        parameters.Add("FechaServido", string.IsNullOrEmpty(item.ServedAt) ? null : (DateTime?)DateTimeOffset.Parse(item.ServedAt).UtcDateTime);
        parameters.Add("Notas", item.Notes);

        // v1.2: Tax axis
        parameters.Add("ImporteNetoSinImpuesto", importeNetoSinImpuesto);
        parameters.Add("TasaImpuesto", tasaImpuesto);
        parameters.Add("ImportesIncluyenImpuesto", importesIncluyenImpuesto);
        parameters.Add("CalidadImpuesto", calidadImpuesto);

        parameters.Add("VentaTicketDetalleId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("EsDuplicado", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("fact.sp_InsertarTicketDetalle", parameters, commandType: CommandType.StoredProcedure);

        return (parameters.Get<long>("VentaTicketDetalleId"), parameters.Get<bool>("EsDuplicado"));
    }

    public async Task InsertTicketDiscountAsync(long ventaTicketId, long ingestionBatchId, int franquiciaId, DiscountDto discount)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            INSERT INTO fact.VentaTicketDescuento
            (VentaTicketId, IngestionBatchId, FranquiciaId, CodigoDescuento, NombreDescuento, TipoDescuento, ValorDescuento, ImporteDescuento)
            VALUES
            (@VentaTicketId, @IngestionBatchId, @FranquiciaId, @CodigoDescuento, @NombreDescuento, @TipoDescuento, @ValorDescuento, @ImporteDescuento)",
            new
            {
                VentaTicketId = ventaTicketId,
                IngestionBatchId = ingestionBatchId,
                FranquiciaId = franquiciaId,
                CodigoDescuento = discount.DiscountCode,
                NombreDescuento = discount.DiscountName,
                TipoDescuento = discount.DiscountType,
                ValorDescuento = discount.DiscountValue,
                ImporteDescuento = discount.DiscountAmount
            });
    }

    public async Task InsertTicketPaymentMethodAsync(long ventaTicketId, long ingestionBatchId, int franquiciaId, PaymentMethodDto payment, int? medioPagoId)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            INSERT INTO fact.VentaTicketMedioPago
            (VentaTicketId, IngestionBatchId, FranquiciaId, MedioPagoId, CodigoMedioPago, MarcaTarjeta, Importe)
            VALUES
            (@VentaTicketId, @IngestionBatchId, @FranquiciaId, @MedioPagoId, @CodigoMedioPago, @MarcaTarjeta, @Importe)",
            new
            {
                VentaTicketId = ventaTicketId,
                IngestionBatchId = ingestionBatchId,
                FranquiciaId = franquiciaId,
                MedioPagoId = medioPagoId,
                CodigoMedioPago = payment.PaymentMethod,
                MarcaTarjeta = payment.PaymentBrand,
                Importe = payment.Amount
            });
    }

    public async Task<BatchStatusResponse?> GetBatchStatusAsync(string batchId, int? franquiciaId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<BatchStatusResponse>(
            "stg.sp_ObtenerEstadoBatch",
            new { BatchId = batchId, FranquiciaId = franquiciaId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int?> GetOrCreateMozoAsync(int franquiciaId, string externalId, string nombre)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("ExternalId", externalId);
        parameters.Add("Nombre", nombre);
        parameters.Add("MozoId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dim.sp_InsertarActualizarMozo", parameters, commandType: CommandType.StoredProcedure);

        return parameters.Get<int?>("MozoId");
    }

    public async Task<int?> GetOrCreateMesaAsync(int franquiciaId, string numeroMesa, string? area)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("NumeroMesa", numeroMesa);
        parameters.Add("Area", area);
        parameters.Add("MesaId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dim.sp_InsertarActualizarMesa", parameters, commandType: CommandType.StoredProcedure);

        return parameters.Get<int?>("MesaId");
    }

    public async Task<(int? ProductoId, int? TipoPlatoId)> GetOrCreateProductoAsync(
        int franquiciaId, string externalId, string codigo, string nombre,
        string? categoria, string? familia, string? subfamilia)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("FranquiciaId", franquiciaId);
        parameters.Add("ExternalId", externalId);
        parameters.Add("Codigo", codigo);
        parameters.Add("Nombre", nombre);
        parameters.Add("CategoriaProducto", categoria);
        parameters.Add("Familia", familia);
        parameters.Add("SubFamilia", subfamilia);
        parameters.Add("ProductoId", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("TipoPlatoId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dim.sp_InsertarActualizarProducto", parameters, commandType: CommandType.StoredProcedure);

        return (parameters.Get<int?>("ProductoId"), parameters.Get<int?>("TipoPlatoId"));
    }

    public async Task<int?> GetMedioPagoIdAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT MedioPagoId FROM dim.MedioPago WHERE Codigo = @Codigo AND Activo = 1",
            new { Codigo = codigo });
    }

    public async Task<int?> GetMonedaIdAsync(string codigoIso)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = @CodigoISO AND Activo = 1",
            new { CodigoISO = codigoIso });
    }
}
