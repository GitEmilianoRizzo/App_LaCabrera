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

    /// <summary>v1.3: abre una transacción; mientras dure, las operaciones de tickets la usan.</summary>
    Task<IIngestionTransaction> BeginTransactionAsync();

    /// <summary>v1.3: ticket_id cargados para una franquicia y fecha de negocio.</summary>
    Task<List<string>> GetTicketIdsDelDiaAsync(int franquiciaId, DateTime fechaNegocio);

    /// <summary>v1.3: respalda en bak.VentaTicketReemplazado y borra el día (externalTicketId null) o un ticket. Devuelve los ticket_id quitados.</summary>
    Task<List<string>> RespaldarYEliminarTicketsAsync(long ingestionBatchId, int franquiciaId, DateTime fechaNegocio, string? externalTicketId, string motivo);

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
        string? mealPeriodOrigen = null,
        // v1.3: EstaAnulado explícito (REFUNDED = 0); null = regla histórica del SP
        bool? estaAnuladoForzado = null);

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

public interface IIngestionTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task SavepointAsync(string nombre);
    Task RollbackToSavepointAsync(string nombre);
}
