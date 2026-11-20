using System.Text.Json;
using FluentValidation;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;
using Microsoft.Extensions.Options;

namespace LaCabrera.Api.Services;

public interface IIngestionService
{
    Task<BatchResponse> ProcessBatchAsync(DailySalesBatchRequest request, int franquiciaId, string rawJson);
    Task<BatchStatusResponse?> GetBatchStatusAsync(string batchId, int? franquiciaId = null);
}

public class IngestionService : IIngestionService
{
    private readonly IIngestionRepository _ingestionRepository;
    private readonly IFranquiciaRepository _franquiciaRepository;
    private readonly IValidator<DailySalesBatchRequest> _validator;
    private readonly ApiSettings _settings;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IIngestionRepository ingestionRepository,
        IFranquiciaRepository franquiciaRepository,
        IValidator<DailySalesBatchRequest> validator,
        IOptions<ApiSettings> settings,
        ILogger<IngestionService> logger)
    {
        _ingestionRepository = ingestionRepository;
        _franquiciaRepository = franquiciaRepository;
        _validator = validator;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<BatchResponse> ProcessBatchAsync(DailySalesBatchRequest request, int franquiciaId, string rawJson)
    {
        var warnings = new List<BatchWarning>();
        var errors = new List<BatchError>();

        // Validate request
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            errors.AddRange(validationResult.Errors.Select(e => new BatchError
            {
                // Las reglas v1.3 traen un código propio (NEGATIVE_AMOUNT_NOT_REFUNDED, INVALID_REFUND...);
                // las de siempre siguen saliendo como VALIDATION_ERROR
                Code = EsCodigoPropio(e.ErrorCode) ? e.ErrorCode : "VALIDATION_ERROR",
                Message = e.ErrorMessage,
                Field = e.PropertyName
            }));
            return BatchResponse.Rejected(errors);
        }

        // Register batch
        var (ingestionBatchId, isDuplicate) = await _ingestionRepository.RegisterBatchAsync(
            request.BatchHeader.BatchId,
            franquiciaId,
            DateTime.Parse(request.BatchHeader.BusinessDate),
            request.SchemaVersion,
            request.BatchHeader.UploadType,
            "API",
            request.BatchHeader.ControlTotals,
            request.BatchHeader.SourceSystem);

        if (isDuplicate)
        {
            _logger.LogWarning("Batch duplicado: {BatchId} para franquicia {FranquiciaId}", request.BatchHeader.BatchId, franquiciaId);
            return BatchResponse.Duplicate(request.BatchHeader.BatchId, ingestionBatchId);
        }

        if (DailySalesBatchV13.Es(request))
        {
            return await ProcessBatchV13Async(request, franquiciaId, ingestionBatchId, rawJson);
        }

        try
        {
            // Save raw JSON
            await _ingestionRepository.SaveRawJsonAsync(ingestionBatchId, rawJson);

            // Update status to VALIDATING
            await _ingestionRepository.UpdateBatchStatusAsync(ingestionBatchId, "VALIDATING");

            // Get currency ID
            var monedaId = await _ingestionRepository.GetMonedaIdAsync(request.BatchHeader.Franchise.Currency);

            int ticketsProcessed = 0;
            int itemsProcessed = 0;
            decimal totalGross = 0;
            decimal totalNet = 0;
            decimal totalServicio = 0;
            decimal totalImpuesto = 0;
            decimal totalPropina = 0;

            // Process tickets
            foreach (var ticket in request.Tickets)
            {
                try
                {
                    // Get or create mesa
                    int? mesaId = null;
                    if (!string.IsNullOrEmpty(ticket.Table?.TableNumber))
                    {
                        mesaId = await _ingestionRepository.GetOrCreateMesaAsync(
                            franquiciaId, ticket.Table.TableNumber, ticket.Table.TableArea);
                    }
                    else
                    {
                        warnings.Add(new BatchWarning
                        {
                            Code = "MISSING_TABLE",
                            Message = "Ticket sin datos de mesa",
                            TicketId = ticket.TicketId
                        });
                    }

                    // Get or create mozo
                    int? mozoId = null;
                    if (!string.IsNullOrEmpty(ticket.Waiter?.WaiterId))
                    {
                        mozoId = await _ingestionRepository.GetOrCreateMozoAsync(
                            franquiciaId, ticket.Waiter.WaiterId, ticket.Waiter.WaiterName ?? "Sin nombre");
                    }
                    else
                    {
                        warnings.Add(new BatchWarning
                        {
                            Code = "MISSING_WAITER",
                            Message = "Ticket sin datos de mozo",
                            TicketId = ticket.TicketId
                        });
                    }

                    // Check covers
                    if (!ticket.Covers.HasValue || ticket.Covers == 0)
                    {
                        warnings.Add(new BatchWarning
                        {
                            Code = "MISSING_COVERS",
                            Message = "Ticket sin datos de cubiertos",
                            TicketId = ticket.TicketId
                        });
                    }

                    // v1.2: Calculate tax axis values for ticket
                    var (ticketNetoSinImpuesto, ticketCalidadImpuesto, ticketMealPeriodOrigen) =
                        CalculateTaxInfoForTicket(request.BatchHeader, ticket);

                    // Insert ticket
                    var (ventaTicketId, ticketDuplicate) = await _ingestionRepository.InsertTicketAsync(
                        ingestionBatchId, franquiciaId, ticket, mesaId, mozoId, monedaId,
                        // v1.2 parameters
                        importeNetoSinImpuesto: ticketNetoSinImpuesto,
                        importesIncluyenImpuesto: request.BatchHeader.AmountsIncludeTax,
                        calidadImpuesto: ticketCalidadImpuesto,
                        mealPeriodOrigen: ticketMealPeriodOrigen);

                    if (ticketDuplicate)
                    {
                        _logger.LogWarning("Ticket duplicado ignorado: {TicketId}", ticket.TicketId);
                        continue;
                    }

                    // Process items
                    foreach (var item in ticket.Items)
                    {
                        var (productoId, tipoPlatoId) = await _ingestionRepository.GetOrCreateProductoAsync(
                            franquiciaId, item.ProductCode, item.ProductCode, item.ProductName,
                            item.ProductCategory, item.ProductFamily, item.ProductSubfamily);

                        // v1.2: Calculate tax axis values for line item
                        var (itemNetoSinImpuesto, itemCalidadImpuesto) =
                            CalculateTaxInfoForItem(request.BatchHeader, item);

                        var (_, itemDuplicate) = await _ingestionRepository.InsertTicketDetailAsync(
                            ventaTicketId, ingestionBatchId, franquiciaId, item, productoId, tipoPlatoId,
                            // v1.2 parameters
                            importeNetoSinImpuesto: itemNetoSinImpuesto,
                            tasaImpuesto: item.TaxRate,
                            importesIncluyenImpuesto: request.BatchHeader.AmountsIncludeTax,
                            calidadImpuesto: itemCalidadImpuesto);

                        if (!itemDuplicate) itemsProcessed++;
                    }

                    // Process discounts
                    if (ticket.Discounts != null)
                    {
                        foreach (var discount in ticket.Discounts)
                        {
                            await _ingestionRepository.InsertTicketDiscountAsync(
                                ventaTicketId, ingestionBatchId, franquiciaId, discount);
                        }
                    }

                    // Process payment methods
                    if (ticket.PaymentMethods != null)
                    {
                        foreach (var payment in ticket.PaymentMethods)
                        {
                            var medioPagoId = await _ingestionRepository.GetMedioPagoIdAsync(payment.PaymentMethod);
                            await _ingestionRepository.InsertTicketPaymentMethodAsync(
                                ventaTicketId, ingestionBatchId, franquiciaId, payment, medioPagoId);
                        }
                    }

                    ticketsProcessed++;
                    totalGross += ticket.Amounts.GrossAmount;
                    totalNet += ticket.Amounts.NetAmount;
                    totalServicio += ticket.Amounts.ServiceChargeAmount ?? 0;
                    totalImpuesto += ticket.Amounts.TaxAmount ?? 0;
                    totalPropina += ticket.Amounts.TipAmount ?? 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando ticket {TicketId}", ticket.TicketId);
                    await _ingestionRepository.RegisterErrorAsync(
                        ingestionBatchId, "ERROR", "TICKET_PROCESSING_ERROR",
                        ex.Message, ticketId: ticket.TicketId);
                    errors.Add(new BatchError
                    {
                        Code = "TICKET_PROCESSING_ERROR",
                        Message = $"Error procesando ticket: {ex.Message}",
                        Field = ticket.TicketId
                    });
                }
            }

            // Validate control totals (bruto, neto, servicio, impuestos y propinas declarados por el origen)
            var avisosControl = ControlTotalesBatch.Comparar(
                request.BatchHeader.ControlTotals,
                new ControlTotalesBatch.Sumas(totalGross, totalNet, totalServicio, totalImpuesto, totalPropina),
                _settings.ControlTotalsTolerancePercent);
            foreach (var aviso in avisosControl)
            {
                _logger.LogWarning("Batch {BatchId}: {Mensaje}", request.BatchHeader.BatchId, aviso.Message);
                warnings.Add(aviso);
            }

            // Update batch status
            var finalStatus = errors.Any() ? "ACCEPTED_WITH_WARNINGS" :
                              warnings.Any() ? "ACCEPTED_WITH_WARNINGS" : "PROCESSED";

            await _ingestionRepository.UpdateBatchStatusAsync(
                ingestionBatchId, finalStatus,
                errorCount: errors.Count,
                warningCount: warnings.Count,
                ticketCountCalculated: ticketsProcessed,
                itemLineCountCalculated: itemsProcessed,
                grossSalesCalculated: totalGross,
                netSalesCalculated: totalNet);

            // Update franchise sync status
            await _franquiciaRepository.UpdateUltimaSincronizacionAsync(franquiciaId, "ACTIVE");

            var summary = new BatchSummary
            {
                TicketsReceived = request.Tickets.Count,
                TicketsProcessed = ticketsProcessed,
                ItemsProcessed = itemsProcessed,
                GrossAmount = totalGross,
                NetAmount = totalNet
            };

            if (warnings.Any() || errors.Any())
            {
                return BatchResponse.AcceptedWithWarnings(request.BatchHeader.BatchId, ingestionBatchId, summary, warnings);
            }

            _logger.LogInformation("Batch {BatchId} procesado: {TicketsProcessed} tickets, {ItemsProcessed} items",
                request.BatchHeader.BatchId, ticketsProcessed, itemsProcessed);

            return BatchResponse.Accepted(request.BatchHeader.BatchId, ingestionBatchId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando batch {BatchId}", request.BatchHeader.BatchId);
            await _ingestionRepository.UpdateBatchStatusAsync(ingestionBatchId, "FAILED", ex.Message);
            return BatchResponse.Failed($"Error interno: {ex.Message}");
        }
    }

    private static bool EsCodigoPropio(string? codigo)
        => !string.IsNullOrEmpty(codigo) && codigo.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

    /// <summary>
    /// Ingesta v1.3 (T-380). Todo el procesamiento de tickets va en una transacción:
    /// - FULL_DAY: respalda en bak.VentaTicketReemplazado y quita el día de la franquicia, y carga el batch.
    /// - INCREMENTAL / CORRECTION: upsert por (franquicia, ticket_id, business_date); el anterior queda respaldado.
    /// - Cada ticket tiene su savepoint: si falla, se deshace solo ese ticket y se informa en rejected_tickets.
    /// - Si un FULL_DAY no logra cargar ningún ticket, se deshace todo (no se borra el día) y se rechaza.
    /// </summary>
    private async Task<BatchResponse> ProcessBatchV13Async(DailySalesBatchRequest request, int franquiciaId, long ingestionBatchId, string rawJson)
    {
        var header = request.BatchHeader;
        var warnings = new List<BatchWarning>();
        var rechazados = new List<BatchError>();
        var actualizados = new List<string>();
        var cargados = new HashSet<string>(StringComparer.Ordinal);
        var fullDay = string.IsNullOrEmpty(header.UploadType) || header.UploadType == "FULL_DAY";
        var fecha = DateTime.Parse(header.BusinessDate);

        int itemsProcessed = 0, aceptados = 0;
        decimal totalGross = 0, totalNet = 0, totalServicio = 0, totalImpuesto = 0, totalPropina = 0;
        List<string> quitados = new();

        try
        {
            await _ingestionRepository.SaveRawJsonAsync(ingestionBatchId, rawJson);
            await _ingestionRepository.UpdateBatchStatusAsync(ingestionBatchId, "VALIDATING");

            await using (var tx = await _ingestionRepository.BeginTransactionAsync())
            {
                var monedaId = await _ingestionRepository.GetMonedaIdAsync(header.Franchise.Currency);

                var previos = new HashSet<string>(StringComparer.Ordinal);
                if (fullDay)
                {
                    previos = (await _ingestionRepository.GetTicketIdsDelDiaAsync(franquiciaId, fecha)).ToHashSet(StringComparer.Ordinal);
                    await _ingestionRepository.RespaldarYEliminarTicketsAsync(ingestionBatchId, franquiciaId, fecha, null, "FULL_DAY");
                }

                for (var i = 0; i < request.Tickets.Count; i++)
                {
                    var ticket = request.Tickets[i];
                    var savepoint = $"t{i}";
                    await tx.SavepointAsync(savepoint);
                    try
                    {
                        bool existia;
                        if (fullDay)
                        {
                            existia = previos.Contains(ticket.TicketId);
                        }
                        else
                        {
                            var reemplazados = await _ingestionRepository.RespaldarYEliminarTicketsAsync(
                                ingestionBatchId, franquiciaId, DateTime.Parse(ticket.BusinessDate), ticket.TicketId, "UPSERT");
                            existia = reemplazados.Count > 0;
                        }

                        var items = await CargarTicketV13Async(request, ticket, franquiciaId, ingestionBatchId, monedaId, warnings);
                        warnings.AddRange(DailySalesBatchV13.Consistencia(ticket));

                        itemsProcessed += items;
                        cargados.Add(ticket.TicketId);
                        if (existia) actualizados.Add(ticket.TicketId); else aceptados++;
                        totalGross += ticket.Amounts.GrossAmount;
                        totalNet += ticket.Amounts.NetAmount;
                        totalServicio += ticket.Amounts.ServiceChargeAmount ?? 0;
                        totalImpuesto += ticket.Amounts.TaxAmount ?? 0;
                        totalPropina += ticket.Amounts.TipAmount ?? 0;
                    }
                    catch (Exception ex)
                    {
                        // Si la transacción quedó inutilizable, el rollback al savepoint falla y el batch entero va a FAILED
                        _logger.LogWarning(ex, "Batch {BatchId}: ticket {TicketId} rechazado", header.BatchId, ticket.TicketId);
                        await tx.RollbackToSavepointAsync(savepoint);
                        rechazados.Add(new BatchError
                        {
                            Code = "TICKET_PROCESSING_ERROR",
                            Message = $"ticket {ticket.TicketId} not loaded: {ex.Message}",
                            Field = $"tickets[{i}]",
                            TicketId = ticket.TicketId
                        });
                        await _ingestionRepository.RegisterErrorAsync(
                            ingestionBatchId, "ERROR", "TICKET_PROCESSING_ERROR", ex.Message, ticketId: ticket.TicketId);
                    }
                }

                if (fullDay && request.Tickets.Count > 0 && cargados.Count == 0)
                {
                    // No se commitea: el día que estaba cargado queda intacto
                    await tx.DisposeAsync();
                    await _ingestionRepository.UpdateBatchStatusAsync(ingestionBatchId, "REJECTED",
                        "FULL_DAY sin ningún ticket cargable; no se reemplazó el día", errorCount: rechazados.Count);
                    var rechazo = BatchResponse.Rejected(rechazados);
                    rechazo.BatchId = header.BatchId;
                    rechazo.IngestionBatchId = ingestionBatchId;
                    rechazo.Message = "FULL_DAY rejected: no ticket could be loaded; previously loaded data for that day was kept";
                    return rechazo;
                }

                quitados = previos.Where(id => !cargados.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
                await tx.CommitAsync();
            }

            foreach (var aviso in ControlTotalesBatch.Comparar(
                         header.ControlTotals,
                         new ControlTotalesBatch.Sumas(totalGross, totalNet, totalServicio, totalImpuesto, totalPropina),
                         _settings.ControlTotalsTolerancePercent))
            {
                warnings.Add(aviso);
            }

            var hayAvisos = warnings.Count > 0 || rechazados.Count > 0;
            await _ingestionRepository.UpdateBatchStatusAsync(
                ingestionBatchId, hayAvisos ? "ACCEPTED_WITH_WARNINGS" : "PROCESSED",
                errorCount: rechazados.Count,
                warningCount: warnings.Count,
                ticketCountCalculated: cargados.Count,
                itemLineCountCalculated: itemsProcessed,
                grossSalesCalculated: totalGross,
                netSalesCalculated: totalNet);

            await _franquiciaRepository.UpdateUltimaSincronizacionAsync(franquiciaId, "ACTIVE");

            var summary = new BatchSummary
            {
                TicketsReceived = request.Tickets.Count,
                TicketsProcessed = cargados.Count,
                ItemsProcessed = itemsProcessed,
                GrossAmount = totalGross,
                NetAmount = totalNet,
                TicketsAccepted = aceptados,
                TicketsUpdated = actualizados.Count,
                TicketsRejected = rechazados.Count,
                TicketsRemoved = fullDay ? quitados.Count : null,
                UpdatedTicketIds = actualizados,
                RemovedTicketIds = fullDay ? quitados : null,
                RejectedTickets = rechazados
            };

            _logger.LogInformation(
                "Batch v1.3 {BatchId} ({UploadType}): {Aceptados} nuevos, {Actualizados} actualizados, {Rechazados} rechazados, {Quitados} quitados",
                header.BatchId, fullDay ? "FULL_DAY" : header.UploadType, aceptados, actualizados.Count, rechazados.Count, quitados.Count);

            if (hayAvisos)
            {
                var respuesta = BatchResponse.AcceptedWithWarnings(header.BatchId, ingestionBatchId, summary, warnings);
                if (rechazados.Count > 0) respuesta.Errors = rechazados;
                return respuesta;
            }

            return BatchResponse.Accepted(header.BatchId, ingestionBatchId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando batch v1.3 {BatchId}", header.BatchId);
            await _ingestionRepository.UpdateBatchStatusAsync(ingestionBatchId, "FAILED", ex.Message);
            return BatchResponse.Failed($"Error interno: {ex.Message}");
        }
    }

    /// <summary>Inserta un ticket v1.3 con sus líneas, descuentos y pagos. Devuelve la cantidad de líneas.</summary>
    private async Task<int> CargarTicketV13Async(
        DailySalesBatchRequest request, TicketDto ticket, int franquiciaId, long ingestionBatchId, int? monedaId, List<BatchWarning> warnings)
    {
        var incluyeImpuesto = request.BatchHeader.AmountsIncludeTax;

        int? mesaId = null;
        if (!string.IsNullOrEmpty(ticket.Table?.TableNumber))
            mesaId = await _ingestionRepository.GetOrCreateMesaAsync(franquiciaId, ticket.Table.TableNumber, ticket.Table.TableArea);
        else
            warnings.Add(new BatchWarning { Code = "MISSING_TABLE", Message = "Ticket without table data", TicketId = ticket.TicketId });

        int? mozoId = null;
        if (!string.IsNullOrEmpty(ticket.Waiter?.WaiterId))
            mozoId = await _ingestionRepository.GetOrCreateMozoAsync(franquiciaId, ticket.Waiter.WaiterId, ticket.Waiter.WaiterName ?? "Sin nombre");
        else
            warnings.Add(new BatchWarning { Code = "MISSING_WAITER", Message = "Ticket without waiter data", TicketId = ticket.TicketId });

        // Una devolución lleva covers 0 a propósito: no se avisa
        if ((!ticket.Covers.HasValue || ticket.Covers == 0) && ticket.Status != "REFUNDED")
            warnings.Add(new BatchWarning { Code = "MISSING_COVERS", Message = "Ticket without covers", TicketId = ticket.TicketId });

        var netoSinImpuesto = DailySalesBatchV13.NetoSinImpuesto(
            incluyeImpuesto, ticket.Amounts.NetAmount, ticket.Amounts.NetAmountExclTax, ticket.Amounts.TaxAmount);

        var (ventaTicketId, duplicado) = await _ingestionRepository.InsertTicketAsync(
            ingestionBatchId, franquiciaId, ticket, mesaId, mozoId, monedaId,
            importeNetoSinImpuesto: DailySalesBatchV13.NetoSinImpuestoInterno(netoSinImpuesto, ticket.Amounts.ServiceChargeAmount),
            importesIncluyenImpuesto: incluyeImpuesto,
            calidadImpuesto: netoSinImpuesto.HasValue ? "POS" : "DESCONOCIDO",
            mealPeriodOrigen: string.IsNullOrEmpty(ticket.MealPeriod) ? null : "POS",
            estaAnuladoForzado: DailySalesBatchV13.EstaAnulado(ticket.Status));

        if (duplicado)
            throw new ApplicationException("ticket already exists for this franchise and business_date");

        var items = 0;
        foreach (var item in ticket.Items)
        {
            var (productoId, tipoPlatoId) = await _ingestionRepository.GetOrCreateProductoAsync(
                franquiciaId, item.ProductCode, item.ProductCode, item.ProductName,
                item.ProductCategory, item.ProductFamily, item.ProductSubfamily);

            var itemNeto = DailySalesBatchV13.NetoSinImpuesto(incluyeImpuesto, item.NetAmount, item.NetAmountExclTax, item.TaxAmount);
            if (!itemNeto.HasValue && item.TaxRate.HasValue)
                itemNeto = incluyeImpuesto == false ? item.NetAmount : Math.Round(item.NetAmount / (1 + item.TaxRate.Value), 4);

            var (_, lineaDuplicada) = await _ingestionRepository.InsertTicketDetailAsync(
                ventaTicketId, ingestionBatchId, franquiciaId, item, productoId, tipoPlatoId,
                importeNetoSinImpuesto: itemNeto,
                tasaImpuesto: item.TaxRate,
                importesIncluyenImpuesto: incluyeImpuesto,
                calidadImpuesto: itemNeto.HasValue ? "POS" : "DESCONOCIDO");

            if (lineaDuplicada)
                throw new ApplicationException($"line_id {item.LineId} is repeated in the ticket");
            items++;
        }

        foreach (var discount in ticket.Discounts ?? new List<DiscountDto>())
            await _ingestionRepository.InsertTicketDiscountAsync(ventaTicketId, ingestionBatchId, franquiciaId, discount);

        foreach (var payment in ticket.PaymentMethods ?? new List<PaymentMethodDto>())
        {
            var medioPagoId = await _ingestionRepository.GetMedioPagoIdAsync(payment.PaymentMethod);
            if (medioPagoId == null)
                warnings.Add(new BatchWarning
                {
                    Code = "UNKNOWN_PAYMENT_METHOD",
                    Message = $"payment_method {payment.PaymentMethod} is not one of the accepted values; stored without mapping",
                    TicketId = ticket.TicketId
                });
            await _ingestionRepository.InsertTicketPaymentMethodAsync(ventaTicketId, ingestionBatchId, franquiciaId, payment, medioPagoId);
        }

        return items;
    }

    public async Task<BatchStatusResponse?> GetBatchStatusAsync(string batchId, int? franquiciaId = null)
    {
        return await _ingestionRepository.GetBatchStatusAsync(batchId, franquiciaId);
    }

    #region v1.2 Tax Calculation Helpers

    /// <summary>
    /// Calculates tax axis values for a ticket using the cascade logic:
    /// 1. If net_amount_excl_tax provided -> CalidadImpuesto = 'POS'
    /// 2. If tax_amount provided -> CalidadImpuesto = 'POS'
    /// 3. Otherwise -> CalidadImpuesto = 'DESCONOCIDO'
    /// </summary>
    private (decimal? ImporteNetoSinImpuesto, string CalidadImpuesto, string? MealPeriodOrigen)
        CalculateTaxInfoForTicket(BatchHeader header, TicketDto ticket)
    {
        decimal? importeNetoSinImpuesto = null;
        string calidadImpuesto = "DESCONOCIDO";

        // 1. If net_amount_excl_tax is provided directly by POS (v1.2)
        if (ticket.Amounts.NetAmountExclTax.HasValue)
        {
            importeNetoSinImpuesto = ticket.Amounts.NetAmountExclTax.Value;
            calidadImpuesto = "POS";
        }
        // 2. If tax_amount is provided, calculate net excl tax
        else if (ticket.Amounts.TaxAmount.HasValue && ticket.Amounts.TaxAmount.Value > 0)
        {
            // If amounts_include_tax = true, then net already includes tax, so subtract
            // If amounts_include_tax = false, then net is already without tax
            if (header.AmountsIncludeTax == true)
            {
                importeNetoSinImpuesto = ticket.Amounts.NetAmount - ticket.Amounts.TaxAmount.Value;
            }
            else if (header.AmountsIncludeTax == false)
            {
                importeNetoSinImpuesto = ticket.Amounts.NetAmount;
            }
            else
            {
                // Unknown if tax is included, but we have tax amount
                // Assume the common case: net is the final after discount but before/after tax is unknown
                // Best effort: assume tax is already separate from net
                importeNetoSinImpuesto = ticket.Amounts.NetAmount;
            }
            calidadImpuesto = "POS";
        }
        // 3. Could add derivation from cfg.AlicuotaImpuestoFranquicia here
        // For now, mark as unknown

        // MealPeriodOrigen
        string? mealPeriodOrigen = null;
        if (!string.IsNullOrEmpty(ticket.MealPeriod))
        {
            mealPeriodOrigen = "POS";
        }
        // Could derive from cfg.FranjaHorariaFranquicia using ticket time here
        // For now, leave as null if not provided

        return (importeNetoSinImpuesto, calidadImpuesto, mealPeriodOrigen);
    }

    /// <summary>
    /// Calculates tax axis values for a line item using the cascade logic.
    /// </summary>
    private (decimal? ImporteNetoSinImpuesto, string CalidadImpuesto)
        CalculateTaxInfoForItem(BatchHeader header, TicketItemDto item)
    {
        decimal? importeNetoSinImpuesto = null;
        string calidadImpuesto = "DESCONOCIDO";

        // 1. If net_amount_excl_tax is provided directly (v1.2)
        if (item.NetAmountExclTax.HasValue)
        {
            importeNetoSinImpuesto = item.NetAmountExclTax.Value;
            calidadImpuesto = "POS";
        }
        // 2. If tax_amount is provided
        else if (item.TaxAmount.HasValue && item.TaxAmount.Value > 0)
        {
            if (header.AmountsIncludeTax == true)
            {
                importeNetoSinImpuesto = item.NetAmount - item.TaxAmount.Value;
            }
            else if (header.AmountsIncludeTax == false)
            {
                importeNetoSinImpuesto = item.NetAmount;
            }
            else
            {
                importeNetoSinImpuesto = item.NetAmount;
            }
            calidadImpuesto = "POS";
        }
        // 3. If tax_rate is provided, calculate
        else if (item.TaxRate.HasValue && item.TaxRate.Value > 0)
        {
            // If amounts include tax: net_excl = net / (1 + rate)
            // If amounts exclude tax: net_excl = net
            if (header.AmountsIncludeTax == true)
            {
                importeNetoSinImpuesto = Math.Round(item.NetAmount / (1 + item.TaxRate.Value), 4);
            }
            else if (header.AmountsIncludeTax == false)
            {
                importeNetoSinImpuesto = item.NetAmount;
            }
            else
            {
                // Assume tax included (common for EUR, ARS)
                importeNetoSinImpuesto = Math.Round(item.NetAmount / (1 + item.TaxRate.Value), 4);
            }
            calidadImpuesto = "POS";
        }

        return (importeNetoSinImpuesto, calidadImpuesto);
    }

    #endregion
}
