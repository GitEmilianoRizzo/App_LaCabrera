using System.Net.Http.Headers;
using System.Text.Json;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

public interface IAgoraExtractorService
{
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null);
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta);
}

public class EjecucionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TicketsProcesados { get; set; }
    public int LineasProcesadas { get; set; }
    public int ErrorsCount { get; set; }
    public long? EjecucionId { get; set; }
    public long? BatchId { get; set; }
}

public class AgoraExtractorService : IAgoraExtractorService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AgoraExtractorService> _logger;

    public AgoraExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<AgoraExtractorService> logger)
    {
        _connectionFactory = connectionFactory;
        _conexionRepository = conexionRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null)
    {
        var resultado = new EjecucionResultDto();
        var inicioEjecucion = DateTime.UtcNow;
        long? ejecucionId = null;

        try
        {
            // 1. Obtener configuración del nodo
            var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
            if (nodo == null)
            {
                return new EjecucionResultDto { Success = false, Message = "Nodo no encontrado" };
            }

            var configJson = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);
            if (string.IsNullOrEmpty(configJson))
            {
                return new EjecucionResultDto { Success = false, Message = "Nodo sin configuración" };
            }

            var config = JsonSerializer.Deserialize<NodoConfiguracionDto>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (config?.Connection == null)
            {
                return new EjecucionResultDto { Success = false, Message = "Configuración de conexión inválida" };
            }

            // 2. Registrar inicio de ejecución
            ejecucionId = await RegistrarInicioEjecucionAsync(nodoConexionId, fechaNegocio ?? DateTime.Today.AddDays(-1));
            resultado.EjecucionId = ejecucionId;

            // 3. Determinar fecha de negocio (por defecto: ayer)
            var businessDay = fechaNegocio ?? DateTime.Today.AddDays(-1);
            var businessDayStr = businessDay.ToString("yyyy-MM-dd");

            _logger.LogInformation("Extrayendo datos de Ágora para {Fecha} - Nodo {NodoId}", businessDayStr, nodoConexionId);

            // 4. Hacer request a API de Ágora
            var includeProcessed = config.IncludeProcessed ? "true" : "false";
            var url = $"{config.Connection.BaseUrl}/api/export/?business-day={businessDayStr}&filter={config.ExportFilter ?? "Invoices"}&include-processed={includeProcessed}";

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(config.Connection.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Api-Token", config.Connection.ApiToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, $"HTTP {(int)response.StatusCode}: {errorBody}");
                return new EjecucionResultDto { Success = false, Message = $"Error API Ágora: HTTP {(int)response.StatusCode}", EjecucionId = ejecucionId };
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var agoraData = JsonSerializer.Deserialize<AgoraExportResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (agoraData?.Invoices == null || agoraData.Invoices.Count == 0)
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "SUCCESS", 0, 0, 0, "Sin facturas para procesar");
                return new EjecucionResultDto { Success = true, Message = "Sin facturas para procesar", TicketsProcesados = 0, EjecucionId = ejecucionId };
            }

            _logger.LogInformation("Recibidas {Count} facturas de Ágora", agoraData.Invoices.Count);

            // 5. Crear batch de ingesta
            var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, businessDay, jsonResponse);
            resultado.BatchId = batchId;

            // 6. Procesar facturas
            int ticketsProcesados = 0;
            int lineasProcesadas = 0;
            int errores = 0;

            foreach (var invoice in agoraData.Invoices)
            {
                try
                {
                    var (ticketId, lineas) = await ProcesarFacturaAsync(invoice, nodo.FranquiciaId, batchId, nodoConexionId);
                    if (ticketId > 0)
                    {
                        ticketsProcesados++;
                        lineasProcesadas += lineas;
                    }
                }
                catch (Exception ex)
                {
                    errores++;
                    _logger.LogError(ex, "Error procesando factura {Serie}-{Number}", invoice.Serie, invoice.Number);
                    await RegistrarErrorIngestaAsync(batchId, $"Error factura {invoice.Serie}-{invoice.Number}: {ex.Message}");
                }
            }

            // 7. Actualizar batch
            await ActualizarBatchAsync(batchId, ticketsProcesados, lineasProcesadas, errores);

            // 8. Registrar fin de ejecución
            var estado = errores > 0 ? (ticketsProcesados > 0 ? "WARNING" : "ERROR") : "SUCCESS";
            await RegistrarFinEjecucionAsync(ejecucionId.Value, estado, ticketsProcesados, lineasProcesadas, errores, null);

            // 9. Actualizar estado de sincronización en NodoConexion y Franquicia
            await ActualizarEstadoSincronizacionAsync(nodoConexionId, nodo.FranquiciaId, estado, batchId);

            resultado.Success = true;
            resultado.Message = $"Procesados {ticketsProcesados} tickets, {lineasProcesadas} líneas" + (errores > 0 ? $", {errores} errores" : "");
            resultado.TicketsProcesados = ticketsProcesados;
            resultado.LineasProcesadas = lineasProcesadas;
            resultado.ErrorsCount = errores;

            _logger.LogInformation("Extracción completada: {Message}", resultado.Message);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en extracción Ágora para nodo {NodoId}", nodoConexionId);

            if (ejecucionId.HasValue)
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, ex.Message);
            }

            return new EjecucionResultDto { Success = false, Message = ex.Message, EjecucionId = ejecucionId };
        }
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta)
    {
        var resultado = new EjecucionResultDto { Success = true };
        int totalTickets = 0;
        int totalLineas = 0;
        int totalErrores = 0;
        int diasProcesados = 0;

        _logger.LogInformation("Iniciando extracción de rango {Desde} a {Hasta} para nodo {NodoId}",
            fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"), nodoConexionId);

        for (var fecha = fechaDesde; fecha <= fechaHasta; fecha = fecha.AddDays(1))
        {
            try
            {
                _logger.LogInformation("Procesando día {Fecha}...", fecha.ToString("yyyy-MM-dd"));
                var resultadoDia = await EjecutarExtraccionAsync(nodoConexionId, fecha);

                totalTickets += resultadoDia.TicketsProcesados;
                totalLineas += resultadoDia.LineasProcesadas;
                totalErrores += resultadoDia.ErrorsCount;
                diasProcesados++;

                // Pequeña pausa para no sobrecargar la API
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error procesando día {Fecha}, continuando...", fecha.ToString("yyyy-MM-dd"));
                totalErrores++;
            }
        }

        resultado.TicketsProcesados = totalTickets;
        resultado.LineasProcesadas = totalLineas;
        resultado.ErrorsCount = totalErrores;
        resultado.Message = $"Rango completado: {diasProcesados} días procesados, {totalTickets} tickets, {totalLineas} líneas";

        _logger.LogInformation("Extracción de rango completada: {Dias} días, {Tickets} tickets, {Lineas} líneas, {Errores} errores",
            diasProcesados, totalTickets, totalLineas, totalErrores);

        return resultado;
    }

    private async Task<long> RegistrarInicioEjecucionAsync(int nodoConexionId, DateTime fechaNegocio)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QuerySingleAsync<long>(@"
            INSERT INTO log.EjecucionNodo (NodoConexionId, FechaNegocio, InicioEjecucion, Estado, ModoEjecucion)
            OUTPUT INSERTED.EjecucionNodoId
            VALUES (@NodoConexionId, @FechaNegocio, GETUTCDATE(), 'RUNNING', 'MANUAL')",
            new { NodoConexionId = nodoConexionId, FechaNegocio = fechaNegocio });
    }

    private async Task RegistrarFinEjecucionAsync(long ejecucionId, string estado, int tickets, int lineas, int errores, string? mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            UPDATE log.EjecucionNodo
            SET FinEjecucion = GETUTCDATE(),
                Estado = @Estado,
                TicketsProcesados = @Tickets,
                LineasProcesadas = @Lineas,
                ErrorsCount = @Errores,
                Mensajes = @Mensaje
            WHERE EjecucionNodoId = @EjecucionId",
            new { EjecucionId = ejecucionId, Estado = estado, Tickets = tickets, Lineas = lineas, Errores = errores, Mensaje = mensaje });
    }

    private async Task ActualizarEstadoSincronizacionAsync(int nodoConexionId, int franquiciaId, string estado, long batchId)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Actualizar NodoConexion
        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion
            SET UltimaSincronizacion = GETUTCDATE(),
                UltimoEstado = @Estado,
                UltimoBatchId = @BatchId,
                ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoId",
            new { NodoId = nodoConexionId, Estado = estado, BatchId = batchId.ToString() });

        // Actualizar Franquicia si la sincronización fue exitosa
        var estadoIntegracion = estado == "SUCCESS" ? "OK" : (estado == "WARNING" ? "WARNING" : "ERROR");
        await connection.ExecuteAsync(@"
            UPDATE dim.Franquicia
            SET EstadoIntegracion = @EstadoIntegracion,
                UltimaSincronizacion = GETUTCDATE()
            WHERE FranquiciaId = @FranquiciaId",
            new { FranquiciaId = franquiciaId, EstadoIntegracion = estadoIntegracion });
    }

    private async Task<long> CrearBatchIngestaAsync(int franquiciaId, DateTime fechaNegocio, string rawJson)
    {
        using var connection = _connectionFactory.CreateConnection();

        var batchGuid = Guid.NewGuid().ToString();

        var batchId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO stg.IngestionBatch (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta, Estado)
            OUTPUT INSERTED.IngestionBatchId
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, 'AGORA_V1', 'FULL_DAY', 'API', 'PROCESSING')",
            new { BatchGuid = batchGuid, FranquiciaId = franquiciaId, FechaNegocio = fechaNegocio });

        // Guardar JSON raw
        await connection.ExecuteAsync(@"
            INSERT INTO stg.IngestionBatchRawJson (IngestionBatchId, JsonContent)
            VALUES (@BatchId, @RawJson)",
            new { BatchId = batchId, RawJson = rawJson });

        return batchId;
    }

    private async Task ActualizarBatchAsync(long batchId, int tickets, int lineas, int errores)
    {
        using var connection = _connectionFactory.CreateConnection();

        var estado = errores > 0 ? (tickets > 0 ? "COMPLETED_WITH_ERRORS" : "ERROR") : "COMPLETED";

        await connection.ExecuteAsync(@"
            UPDATE stg.IngestionBatch
            SET Estado = @Estado,
                TicketCountCalculado = @Tickets,
                ItemLineCountCalculado = @Lineas
            WHERE IngestionBatchId = @BatchId",
            new { BatchId = batchId, Estado = estado, Tickets = tickets, Lineas = lineas });
    }

    private async Task RegistrarErrorIngestaAsync(long batchId, string mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            INSERT INTO stg.IngestionError (IngestionBatchId, Severidad, CodigoError, MensajeError)
            VALUES (@BatchId, 'ERROR', 'PROCESSING_ERROR', @Mensaje)",
            new { BatchId = batchId, Mensaje = mensaje });
    }

    private async Task<(long ticketId, int lineas)> ProcesarFacturaAsync(AgoraInvoice invoice, int franquiciaId, long batchId, int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Obtener GlobalId del primer InvoiceItem
        var globalId = invoice.InvoiceItems?.FirstOrDefault()?.GlobalId ?? $"{invoice.Serie}-{invoice.Number}";
        var invoiceItem = invoice.InvoiceItems?.FirstOrDefault();

        // Verificar si ya existe
        var existe = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket
            WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalId",
            new { FranquiciaId = franquiciaId, ExternalId = globalId });

        if (existe.HasValue)
        {
            _logger.LogDebug("Ticket {GlobalId} ya existe, omitiendo", globalId);
            return (0, 0);
        }

        // Mapear estado
        var estado = invoice.DocumentType switch
        {
            "StandardInvoice" or "BasicInvoice" => "CLOSED",
            "StandardRefund" or "BasicRefund" => "REFUNDED",
            _ => "CLOSED"
        };

        // Calcular período de comida según hora
        var hora = invoice.Date?.Hour ?? 12;
        var periodoComida = hora switch
        {
            >= 6 and < 12 => "BREAKFAST",
            >= 12 and < 17 => "LUNCH",
            >= 17 and < 21 => "DINNER",
            _ => "LATE_NIGHT"
        };

        // Usar totales de Ágora (nivel invoice o invoiceItem)
        var totals = invoice.Totals ?? invoiceItem?.Totals;

        // ImporteBruto = GrossAmount (con IVA)
        decimal importeBruto = totals?.GrossAmount ?? 0;
        // ImporteNeto = NetAmount (SIN IVA - base imponible)
        decimal importeNeto = totals?.NetAmount ?? 0;
        // ImporteImpuesto = VatAmount (IVA)
        decimal importeImpuesto = totals?.VatAmount ?? 0;

        // Calcular descuentos sumando de las líneas
        decimal importeDescuento = 0;
        int lineasCount = 0;

        if (invoiceItem?.Lines != null)
        {
            foreach (var line in invoiceItem.Lines)
            {
                importeDescuento += line.CashDiscount ?? 0;
                lineasCount++;
            }
        }

        // Si no hay Totals, calcular de forma aproximada (fallback)
        if (totals == null && invoiceItem?.Lines != null)
        {
            importeBruto = invoiceItem.Lines.Sum(l => l.TotalAmount ?? 0);
            // Asumir IVA del 10% si no tenemos datos
            importeNeto = Math.Round(importeBruto / 1.10m, 2);
            importeImpuesto = importeBruto - importeNeto;
        }

        // Propinas de los pagos
        decimal importePropina = 0;
        var payments = invoice.Payments ?? invoiceItem?.Payments;
        if (payments != null)
        {
            importePropina = payments.Sum(p => p.TipAmount ?? 0);
        }

        // Total pagado
        decimal importeTotalPagado = importeBruto + importePropina;

        // Insertar ticket
        var ticketId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO fact.VentaTicket (
                IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado,
                TipoDocumentoFiscal,
                FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
                NumeroMesa, AreaMesa, NombreMozo,
                CantidadCubiertos, CodigoMoneda,
                ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                ImporteServicio, ImportePropina, ImporteTotalPagado,
                TieneDescuento, EstaAnulado, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa,
                FuenteSistema
            )
            OUTPUT INSERTED.VentaTicketId
            VALUES (
                @BatchId, @FranquiciaId, @ExternalId, @NumeroTicket, @Estado,
                @TipoDocumentoFiscal,
                @FechaNegocio, @FechaApertura, @FechaCierre, @PeriodoComida,
                @NumeroMesa, @AreaMesa, @NombreMozo,
                @Cubiertos, 'EUR',
                @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                0, @ImportePropina, @ImporteTotalPagado,
                @TieneDescuento, 0, @TieneCubiertos, @TieneMozo, @TieneMesa,
                'AGORA'
            )",
            new
            {
                BatchId = batchId,
                FranquiciaId = franquiciaId,
                ExternalId = globalId,
                NumeroTicket = $"{invoice.Serie}-{invoice.Number}",
                Estado = estado,
                TipoDocumentoFiscal = invoice.DocumentType,
                FechaNegocio = DateTime.Parse(invoice.BusinessDay ?? DateTime.Today.ToString("yyyy-MM-dd")),
                FechaApertura = invoiceItem?.Date ?? invoice.Date ?? DateTime.Now,
                FechaCierre = invoice.Date,
                PeriodoComida = periodoComida,
                NumeroMesa = invoiceItem?.SaleCenter?.Location,
                AreaMesa = invoiceItem?.SaleCenter?.Name,
                NombreMozo = invoice.User?.Name,
                Cubiertos = invoiceItem?.Guests ?? 0,
                ImporteBruto = importeBruto,
                ImporteDescuento = importeDescuento,
                ImporteNeto = importeNeto,
                ImporteImpuesto = importeImpuesto,
                ImportePropina = importePropina,
                ImporteTotalPagado = importeTotalPagado,
                TieneDescuento = importeDescuento > 0,
                TieneCubiertos = (invoiceItem?.Guests ?? 0) > 0,
                TieneMozo = !string.IsNullOrEmpty(invoice.User?.Name),
                TieneMesa = !string.IsNullOrEmpty(invoiceItem?.SaleCenter?.Location)
            });

        // Insertar líneas de detalle
        if (invoiceItem?.Lines != null)
        {
            // Obtener mapeos de categoría
            var mapeosCat = await connection.QueryAsync<(string CodigoOrigen, string CategoriaDestino)>(@"
                SELECT CodigoOrigen, CategoriaDestino FROM dim.MapeoCategoria WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId });
            var mapeoDict = mapeosCat.ToDictionary(m => m.CodigoOrigen, m => m.CategoriaDestino);

            int lineIndex = 0;
            foreach (var line in invoiceItem.Lines)
            {
                lineIndex++;
                var categoriaEstandar = mapeoDict.GetValueOrDefault(line.FamilyId?.ToString() ?? "", null);

                // Calcular ImporteNeto e ImporteImpuesto por línea
                // TotalAmount viene CON IVA incluido, VatRate es la tasa (ej: 0.10 = 10%)
                decimal lineImporteBruto = line.TotalAmount ?? 0;
                decimal lineDescuento = line.CashDiscount ?? 0;
                decimal lineVatRate = line.VatRate ?? 0.10m; // Default 10% si no viene

                // Calcular base imponible: Bruto / (1 + VatRate)
                decimal lineImporteNeto = Math.Round((lineImporteBruto - lineDescuento) / (1 + lineVatRate), 2);
                decimal lineImporteImpuesto = (lineImporteBruto - lineDescuento) - lineImporteNeto;

                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketDetalle (
                        VentaTicketId, IngestionBatchId, FranquiciaId,
                        ExternalLineId, CodigoProducto, NombreProducto,
                        CategoriaProducto, FamiliaProducto,
                        Cantidad, PrecioUnitario,
                        ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                        TieneDescuento, EstaAnulado
                    )
                    VALUES (
                        @TicketId, @BatchId, @FranquiciaId,
                        @ExternalLineId, @CodigoProducto, @NombreProducto,
                        @CategoriaProducto, @FamiliaProducto,
                        @Cantidad, @PrecioUnitario,
                        @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                        @TieneDescuento, 0
                    )",
                    new
                    {
                        TicketId = ticketId,
                        BatchId = batchId,
                        FranquiciaId = franquiciaId,
                        ExternalLineId = line.Index?.ToString() ?? lineIndex.ToString(),
                        CodigoProducto = line.ProductId?.ToString() ?? "UNKNOWN",
                        NombreProducto = line.ProductName ?? "Sin nombre",
                        CategoriaProducto = categoriaEstandar,
                        FamiliaProducto = line.FamilyName,
                        Cantidad = line.Quantity ?? 1,
                        PrecioUnitario = line.UnitPrice ?? 0,
                        ImporteBruto = lineImporteBruto,
                        ImporteDescuento = lineDescuento,
                        ImporteNeto = lineImporteNeto,
                        ImporteImpuesto = lineImporteImpuesto,
                        TieneDescuento = lineDescuento > 0
                    });
            }
        }

        // Insertar medios de pago (si existen)
        if (invoiceItem?.Payments != null)
        {
            var mapeosPago = await connection.QueryAsync<(string CodigoOrigen, string MedioPagoDestino)>(@"
                SELECT CodigoOrigen, MedioPagoDestino FROM dim.MapeoMedioPago WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId });
            var mapeosPagoDict = mapeosPago.ToDictionary(m => m.CodigoOrigen, m => m.MedioPagoDestino);

            foreach (var pago in invoiceItem.Payments)
            {
                var codigoMedioPago = mapeosPagoDict.GetValueOrDefault(pago.MethodId?.ToString() ?? "", "OTHER");

                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketMedioPago (
                        VentaTicketId, IngestionBatchId, FranquiciaId,
                        CodigoMedioPago, Importe
                    )
                    VALUES (@TicketId, @BatchId, @FranquiciaId, @CodigoMedioPago, @Importe)",
                    new
                    {
                        TicketId = ticketId,
                        BatchId = batchId,
                        FranquiciaId = franquiciaId,
                        CodigoMedioPago = codigoMedioPago,
                        Importe = pago.Amount ?? 0
                    });
            }
        }

        return (ticketId, lineasCount);
    }
}

// DTOs para deserializar respuesta de Ágora
public class AgoraExportResponse
{
    public List<AgoraInvoice>? Invoices { get; set; }
}

public class AgoraInvoice
{
    public string? Serie { get; set; }
    public int? Number { get; set; }
    public string? BusinessDay { get; set; }
    public DateTime? Date { get; set; }
    public string? DocumentType { get; set; }
    public bool? VatIncluded { get; set; }
    public AgoraUser? User { get; set; }
    public AgoraWorkplace? Workplace { get; set; }
    public List<AgoraInvoiceItem>? InvoiceItems { get; set; }
    public List<AgoraPayment>? Payments { get; set; }
    public AgoraTotals? Totals { get; set; }
}

public class AgoraUser
{
    public int? Id { get; set; }
    public string? Name { get; set; }
}

public class AgoraWorkplace
{
    public int? Id { get; set; }
    public string? Name { get; set; }
}

public class AgoraInvoiceItem
{
    public string? GlobalId { get; set; }
    public DateTime? Date { get; set; }
    public int? Guests { get; set; }
    public bool? VatIncluded { get; set; }
    public AgoraSaleCenter? SaleCenter { get; set; }
    public List<AgoraLine>? Lines { get; set; }
    public List<AgoraPayment>? Payments { get; set; }
    public AgoraTotals? Totals { get; set; }
}

public class AgoraSaleCenter
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
}

public class AgoraLine
{
    public int? Index { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public int? FamilyId { get; set; }
    public string? FamilyName { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? CashDiscount { get; set; }
    public decimal? VatRate { get; set; }
    public int? VatId { get; set; }
    public string? Notes { get; set; }
}

public class AgoraPayment
{
    public int? MethodId { get; set; }
    public string? MethodName { get; set; }
    public decimal? Amount { get; set; }
    public decimal? TipAmount { get; set; }
}

public class AgoraTotals
{
    public decimal? GrossAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? SurchargeAmount { get; set; }
    public List<AgoraTaxDetail>? Taxes { get; set; }
}

public class AgoraTaxDetail
{
    public decimal? VatRate { get; set; }
    public decimal? SurchargeRate { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? VatAmount { get; set; }
}
