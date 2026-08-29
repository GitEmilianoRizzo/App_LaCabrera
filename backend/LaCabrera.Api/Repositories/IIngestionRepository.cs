using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Repositories;

public interface IIngestionRepository
{
    Task<(long IngestionBatchId, bool IsDuplicate)> RegisterBatchAsync(
        string batchId,
        int franquiciaId,
        DateTime businessDate,
        string schemaVersion,
        string uploadType,
        string origin,
        ControlTotals controlTotals,
        SourceSystem? sourceSystem);

    Task SaveRawJsonAsync(long ingestionBatchId, string jsonContent);

    Task UpdateBatchStatusAsync(
        long ingestionBatchId,
        string status,
        string? message = null,
        int? errorCount = null,
        int? warningCount = null,
        int? ticketCountCalculated = null,
        int? itemLineCountCalculated = null,
        decimal? grossSalesCalculated = null,
        decimal? netSalesCalculated = null);

    Task RegisterErrorAsync(
        long ingestionBatchId,
        string severity,
        string errorCode,
        string errorMessage,
        string? field = null,
        string? receivedValue = null,
        string? ticketId = null,
        string? lineId = null);

    Task<(long VentaTicketId, bool IsDuplicate)> InsertTicketAsync(
        long ingestionBatchId,
        int franquiciaId,
        TicketDto ticket,
        int? mesaId,
        int? mozoId,
        int? monedaId,
        // v1.2: Tax axis
        decimal? importeNetoSinImpuesto = null,
        bool? importesIncluyenImpuesto = null,
        string? calidadImpuesto = null,
        // v1.2: Shift
        string? mealPeriodOrigen = null);

    Task<(long VentaTicketDetalleId, bool IsDuplicate)> InsertTicketDetailAsync(
        long ventaTicketId,
        long ingestionBatchId,
        int franquiciaId,
        TicketItemDto item,
        int? productoId,
        int? tipoPlatoId,
        // v1.2: Tax axis
        decimal? importeNetoSinImpuesto = null,
        decimal? tasaImpuesto = null,
        bool? importesIncluyenImpuesto = null,
        string? calidadImpuesto = null);

    Task InsertTicketDiscountAsync(
        long ventaTicketId,
        long ingestionBatchId,
        int franquiciaId,
        DiscountDto discount);

    Task InsertTicketPaymentMethodAsync(
        long ventaTicketId,
        long ingestionBatchId,
        int franquiciaId,
        PaymentMethodDto payment,
        int? medioPagoId);

    Task<BatchStatusResponse?> GetBatchStatusAsync(string batchId, int? franquiciaId = null);

    Task<int?> GetOrCreateMozoAsync(int franquiciaId, string externalId, string nombre);
    Task<int?> GetOrCreateMesaAsync(int franquiciaId, string numeroMesa, string? area);
    Task<(int? ProductoId, int? TipoPlatoId)> GetOrCreateProductoAsync(
        int franquiciaId, string externalId, string codigo, string nombre,
        string? categoria, string? familia, string? subfamilia);
    Task<int?> GetMedioPagoIdAsync(string codigo);
    Task<int?> GetMonedaIdAsync(string codigoIso);
}
