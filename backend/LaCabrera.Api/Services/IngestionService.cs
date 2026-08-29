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
                Code = "VALIDATION_ERROR",
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

            // Validate control totals
            if (request.BatchHeader.ControlTotals.GrossSalesAmount.HasValue)
            {
                var diff = Math.Abs(totalGross - request.BatchHeader.ControlTotals.GrossSalesAmount.Value);
                var tolerance = request.BatchHeader.ControlTotals.GrossSalesAmount.Value * (decimal)(_settings.ControlTotalsTolerancePercent / 100);
                if (diff > tolerance && tolerance > 0)
                {
                    warnings.Add(new BatchWarning
                    {
                        Code = "CONTROL_TOTALS_MISMATCH",
                        Message = $"Diferencia en gross_sales: recibido {request.BatchHeader.ControlTotals.GrossSalesAmount}, calculado {totalGross}"
                    });
                }
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
