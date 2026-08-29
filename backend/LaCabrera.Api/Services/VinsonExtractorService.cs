using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

/// <summary>
/// Servicio extractor para sistemas Vinson Cloud (Aeroparque, Ezeiza, etc.)
/// API: https://apireportes.vinson.com.ar
/// Autenticación: JWT con login (username/password)
/// </summary>
public interface IVinsonExtractorService
{
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null);
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta);
}

public class VinsonExtractorService : IVinsonExtractorService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VinsonExtractorService> _logger;

    // Cache del token JWT (por nodo)
    private static readonly Dictionary<int, (string Token, DateTime Expiration)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    public VinsonExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<VinsonExtractorService> logger)
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

            // Validar campos requeridos para Vinson
            if (string.IsNullOrEmpty(config.Connection.Username) || string.IsNullOrEmpty(config.Connection.Password))
            {
                return new EjecucionResultDto { Success = false, Message = "Faltan credenciales (username/password) para Vinson" };
            }

            if (string.IsNullOrEmpty(config.Connection.StoreId))
            {
                return new EjecucionResultDto { Success = false, Message = "Falta store_id (id_tienda) para Vinson" };
            }

            // 2. Registrar inicio de ejecución
            ejecucionId = await RegistrarInicioEjecucionAsync(nodoConexionId, fechaNegocio ?? DateTime.Today.AddDays(-1));
            resultado.EjecucionId = ejecucionId;

            // 3. Determinar fecha de negocio (por defecto: ayer)
            var businessDay = fechaNegocio ?? DateTime.Today.AddDays(-1);
            // IMPORTANTE: Vinson usa formatos diferentes segun el endpoint:
            // - GetTransactionsByDate: yyyyMMdd (DateOnly)
            // - GetSalesFullInforAndProducts: yyyy-MM-dd (DateTime)
            var businessDayDateOnly = businessDay.ToString("yyyyMMdd");
            var businessDayDateTime = businessDay.ToString("yyyy-MM-dd");

            _logger.LogInformation("Extrayendo datos de Vinson para {Fecha} - Nodo {NodoId} - Tienda {StoreId}",
                businessDayDateTime, nodoConexionId, config.Connection.StoreId);

            // 4. Obtener token JWT
            var token = await ObtenerTokenAsync(nodoConexionId, config.Connection);
            if (string.IsNullOrEmpty(token))
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, "No se pudo obtener token JWT");
                return new EjecucionResultDto { Success = false, Message = "Error de autenticación con Vinson", EjecucionId = ejecucionId };
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(config.Connection.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // 5. Obtener cabeceras de transacciones (usa formato DateOnly: yyyyMMdd)
            var urlCabeceras = $"{config.Connection.BaseUrl}/api/Transaction/GetTransactionsByDate/{config.Connection.StoreId}/{businessDayDateOnly}";
            var responseCab = await client.GetAsync(urlCabeceras);

            if (!responseCab.IsSuccessStatusCode)
            {
                var errorBody = await responseCab.Content.ReadAsStringAsync();
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, $"HTTP {(int)responseCab.StatusCode}: {errorBody}");
                return new EjecucionResultDto { Success = false, Message = $"Error API Vinson (cabeceras): HTTP {(int)responseCab.StatusCode}", EjecucionId = ejecucionId };
            }

            var jsonCabeceras = await responseCab.Content.ReadAsStringAsync();
            var cabeceras = JsonSerializer.Deserialize<List<VinsonTransaccion>>(jsonCabeceras, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cabeceras == null || cabeceras.Count == 0)
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "SUCCESS", 0, 0, 0, "Sin transacciones para procesar");
                return new EjecucionResultDto { Success = true, Message = "Sin transacciones para procesar", TicketsProcesados = 0, EjecucionId = ejecucionId };
            }

            _logger.LogInformation("Recibidas {Count} cabeceras de Vinson", cabeceras.Count);

            // 6. Obtener detalle con productos (usa formato DateTime: yyyy-MM-dd)
            var urlDetalle = $"{config.Connection.BaseUrl}/api/Transaction/GetSalesFullInforAndProducts/{config.Connection.StoreId}/{businessDayDateTime}/{businessDayDateTime}";
            var responseDet = await client.GetAsync(urlDetalle);

            List<VinsonSalesFullInfo>? detalle = null;
            if (responseDet.IsSuccessStatusCode)
            {
                var jsonDetalle = await responseDet.Content.ReadAsStringAsync();
                detalle = JsonSerializer.Deserialize<List<VinsonSalesFullInfo>>(jsonDetalle, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.LogInformation("Recibidos {Count} registros de detalle de Vinson", detalle?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning("No se pudo obtener detalle de productos: HTTP {Code}", (int)responseDet.StatusCode);
            }

            // 7. Indexar detalle por transactionId para JOIN (usando GroupBy para evitar duplicados)
            var detalleDict = detalle?
                .GroupBy(d => d.TransactionId.ToString())
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<string, VinsonSalesFullInfo>();

            // Log si hay duplicados
            var duplicados = detalle?.GroupBy(d => d.TransactionId).Where(g => g.Count() > 1).ToList();
            if (duplicados?.Any() == true)
            {
                _logger.LogWarning("Se encontraron {Count} TransactionIds duplicados en detalle de Vinson: {Ids}",
                    duplicados.Count,
                    string.Join(", ", duplicados.Take(5).Select(g => g.Key)));
            }

            // 8. Crear batch de ingesta
            var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, businessDay, jsonCabeceras);
            resultado.BatchId = batchId;

            // 9. Procesar transacciones
            int ticketsProcesados = 0;
            int lineasProcesadas = 0;
            int errores = 0;

            foreach (var transaccion in cabeceras)
            {
                try
                {
                    // Buscar detalle correspondiente
                    detalleDict.TryGetValue(transaccion.IdTransaccion.ToString(), out var detalleTransaccion);

                    var (ticketId, lineas) = await ProcesarTransaccionAsync(transaccion, detalleTransaccion, nodo.FranquiciaId, batchId, nodoConexionId, nodo.Moneda);
                    if (ticketId > 0)
                    {
                        ticketsProcesados++;
                        lineasProcesadas += lineas;
                    }
                }
                catch (Exception ex)
                {
                    errores++;
                    _logger.LogError(ex, "Error procesando transacción {Id}", transaccion.IdTransaccion);
                    await RegistrarErrorIngestaAsync(batchId, $"Error transacción {transaccion.IdTransaccion}: {ex.Message}");
                }
            }

            // 10. Actualizar batch
            await ActualizarBatchAsync(batchId, ticketsProcesados, lineasProcesadas, errores);

            // 11. Registrar fin de ejecución
            var estado = errores > 0 ? (ticketsProcesados > 0 ? "WARNING" : "ERROR") : "SUCCESS";
            await RegistrarFinEjecucionAsync(ejecucionId.Value, estado, ticketsProcesados, lineasProcesadas, errores, null);

            // 12. Actualizar estado de sincronización
            await ActualizarEstadoSincronizacionAsync(nodoConexionId, nodo.FranquiciaId, estado, batchId);

            resultado.Success = true;
            resultado.Message = $"Procesados {ticketsProcesados} tickets, {lineasProcesadas} líneas" + (errores > 0 ? $", {errores} errores" : "");
            resultado.TicketsProcesados = ticketsProcesados;
            resultado.LineasProcesadas = lineasProcesadas;
            resultado.ErrorsCount = errores;

            _logger.LogInformation("Extracción Vinson completada: {Message}", resultado.Message);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en extracción Vinson para nodo {NodoId}", nodoConexionId);

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

        _logger.LogInformation("Iniciando extracción Vinson de rango {Desde} a {Hasta} para nodo {NodoId}",
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

                // Pausa para no sobrecargar la API
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

        _logger.LogInformation("Extracción Vinson de rango completada: {Dias} días, {Tickets} tickets, {Lineas} líneas, {Errores} errores",
            diasProcesados, totalTickets, totalLineas, totalErrores);

        return resultado;
    }

    private async Task<string?> ObtenerTokenAsync(int nodoConexionId, ConnectionConfigDto connection)
    {
        // Verificar cache
        lock (_tokenLock)
        {
            if (_tokenCache.TryGetValue(nodoConexionId, out var cached) && cached.Expiration > DateTime.UtcNow.AddMinutes(2))
            {
                return cached.Token;
            }
        }

        // Obtener nuevo token
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var loginUrl = $"{connection.BaseUrl}/api/Auth/login";
            var loginBody = JsonSerializer.Serialize(new { username = connection.Username, password = connection.Password });
            var content = new StringContent(loginBody, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(loginUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error en login Vinson: HTTP {Code}", (int)response.StatusCode);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var loginResult = JsonSerializer.Deserialize<VinsonLoginResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(loginResult?.Token))
            {
                _logger.LogError("Token vacío en respuesta de login Vinson");
                return null;
            }

            // Decodificar JWT para obtener expiración
            var expiration = DateTime.UtcNow.AddHours(1); // Default: 1 hora
            try
            {
                var payload = loginResult.Token.Split('.')[1];
                var paddedPayload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var decodedPayload = Convert.FromBase64String(paddedPayload.Replace('-', '+').Replace('_', '/'));
                var payloadJson = Encoding.UTF8.GetString(decodedPayload);
                var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
                if (claims != null && claims.TryGetValue("exp", out var expClaim))
                {
                    var expUnix = expClaim.GetInt64();
                    expiration = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                }
            }
            catch { /* Ignorar errores de decodificación */ }

            // Guardar en cache
            lock (_tokenLock)
            {
                _tokenCache[nodoConexionId] = (loginResult.Token, expiration);
            }

            _logger.LogInformation("Token Vinson obtenido, expira: {Expiration}", expiration);
            return loginResult.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo token Vinson");
            return null;
        }
    }

    private async Task<(long ticketId, int lineas)> ProcesarTransaccionAsync(
        VinsonTransaccion transaccion,
        VinsonSalesFullInfo? detalle,
        int franquiciaId,
        long batchId,
        int nodoConexionId,
        string moneda)
    {
        using var connection = _connectionFactory.CreateConnection();

        var externalId = transaccion.IdTransaccion.ToString();

        // Verificar si ya existe
        var existe = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket
            WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalId",
            new { FranquiciaId = franquiciaId, ExternalId = externalId });

        if (existe.HasValue)
        {
            _logger.LogDebug("Ticket {Id} ya existe, omitiendo", externalId);
            return (0, 0);
        }

        // Mapear estado (Vinson usa códigos numéricos)
        // Según la API de Vinson:
        // 0 = Open, 1 = Closed, 2 = Closed (variante), 3 = Closed (pagado), 4 = Closed (otra forma)
        // 5 = Voided (anulado real)
        // Solo marcar como CANCELLED/VOIDED estados que realmente representan anulaciones
        var estado = transaccion.Estado switch
        {
            0 => "OPEN",
            1 or 2 or 3 or 4 => "CLOSED",
            5 => "VOIDED",
            _ => "CLOSED"
        };

        // Calcular período de comida
        // StartHour viene como string "HH:mm:ss", extraemos la hora
        int hora = transaccion.Inicio?.Hour ??
            (int.TryParse(detalle?.StartHour?.Split(':')[0], out var h) ? h : 12);
        var periodoComida = hora switch
        {
            >= 6 and < 12 => "BREAKFAST",
            >= 12 and < 17 => "LUNCH",
            >= 17 and < 21 => "DINNER",
            _ => "LATE_NIGHT"
        };

        // Importes de Vinson:
        // - montoNeto = SIN IVA (base imponible)
        // - montoFinal = CON IVA
        // Para nuestro modelo: ImporteBruto = montoFinal, ImporteNeto = base para calcular
        decimal importeBruto = detalle?.TotalAmount ?? transaccion.MontoFinal ?? 0;
        decimal importeNeto = detalle?.NetAmount ?? transaccion.MontoNeto ?? 0;
        decimal importeImpuesto = detalle?.TaxNetAmount ?? (importeBruto - importeNeto);
        decimal importeDescuento = 0;
        decimal importePropina = 0;

        int lineasCount = 0;

        // Insertar ticket
        var ticketId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO fact.VentaTicket (
                IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado,
                TipoDocumentoFiscal,
                FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
                NumeroMesa, AreaMesa, MozoId,
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
                @NumeroMesa, @AreaMesa, @MozoId,
                @CantidadCubiertos, @CodigoMoneda,
                @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                0, @ImportePropina, @ImporteTotalPagado,
                @TieneDescuento, @EstaAnulado, @TieneCubiertos, @TieneMozo, @TieneMesa,
                'VINSON'
            )",
            new
            {
                BatchId = batchId,
                FranquiciaId = franquiciaId,
                ExternalId = externalId,
                NumeroTicket = detalle?.TicketNumber?.ToString() ?? transaccion.IdTransaccion.ToString(),
                Estado = estado,
                TipoDocumentoFiscal = detalle?.TicketType,
                FechaNegocio = transaccion.OpenDate ?? DateTime.Today,
                FechaApertura = transaccion.Inicio,
                FechaCierre = transaccion.Fin,
                PeriodoComida = periodoComida,
                NumeroMesa = transaccion.Mesa?.ToString(),
                AreaMesa = (string?)null,
                MozoId = (int?)null, // No hay FK a dim.Mozo - empInicio de Vinson es solo informativo
                CantidadCubiertos = transaccion.Clientes ?? 0,
                CodigoMoneda = moneda,
                ImporteBruto = importeBruto,
                ImporteDescuento = importeDescuento,
                ImporteNeto = importeNeto,
                ImporteImpuesto = importeImpuesto,
                ImportePropina = importePropina,
                ImporteTotalPagado = importeBruto,
                TieneDescuento = importeDescuento > 0,
                EstaAnulado = estado == "VOIDED" || estado == "CANCELLED",
                TieneCubiertos = (transaccion.Clientes ?? 0) > 0,
                TieneMozo = transaccion.EmpInicio.HasValue,
                TieneMesa = transaccion.Mesa.HasValue && transaccion.Mesa > 0
            });

        // Insertar líneas de detalle (si hay productos)
        if (detalle?.Products != null && detalle.Products.Count > 0)
        {
            // Obtener mapeos de categoría
            var mapeosCat = await connection.QueryAsync<(string CodigoOrigen, string CategoriaDestino)>(@"
                SELECT CodigoOrigen, CategoriaDestino FROM dim.MapeoCategoria WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId });
            var mapeoDict = mapeosCat.ToDictionary(m => m.CodigoOrigen, m => m.CategoriaDestino, StringComparer.OrdinalIgnoreCase);

            int lineIndex = 0;
            foreach (var producto in detalle.Products)
            {
                lineIndex++;

                // Buscar categoría mapeada (por nombre de categoría o grupo)
                var categoriaEstandar = mapeoDict.GetValueOrDefault(producto.Category ?? "", null)
                    ?? mapeoDict.GetValueOrDefault(producto.Group ?? "", null);

                // Registrar valor no mapeado si no existe
                if (string.IsNullOrEmpty(categoriaEstandar) && !string.IsNullOrEmpty(producto.Category))
                {
                    await RegistrarValorNoMapeadoAsync(nodoConexionId, "CATEGORIA", producto.Category, producto.Category);
                }

                // Importes del producto Vinson:
                // grossAmount = CON IVA, netAmount = SIN IVA
                decimal lineImporteBruto = producto.GrossAmount ?? 0;
                decimal lineImporteNeto = producto.NetAmount ?? 0;
                decimal lineDescuento = producto.DiscountAmount ?? 0;
                decimal lineImporteImpuesto = lineImporteBruto - lineImporteNeto;

                lineasCount++;

                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketDetalle (
                        VentaTicketId, IngestionBatchId, FranquiciaId,
                        ExternalLineId, CodigoProducto, NombreProducto,
                        CategoriaProducto, FamiliaProducto, SubfamiliaProducto,
                        Cantidad, PrecioUnitario,
                        ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                        TieneDescuento, EstaAnulado, MotivoAnulacion, Notas
                    )
                    VALUES (
                        @TicketId, @BatchId, @FranquiciaId,
                        @ExternalLineId, @CodigoProducto, @NombreProducto,
                        @CategoriaProducto, @FamiliaProducto, @SubfamiliaProducto,
                        @Cantidad, @PrecioUnitario,
                        @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                        @TieneDescuento, @EstaAnulado, @MotivoAnulacion, @Notas
                    )",
                    new
                    {
                        TicketId = ticketId,
                        BatchId = batchId,
                        FranquiciaId = franquiciaId,
                        ExternalLineId = lineIndex.ToString(),
                        CodigoProducto = producto.Name?.Replace(" ", "_").ToUpperInvariant()[..Math.Min(producto.Name.Length, 50)] ?? "UNKNOWN",
                        NombreProducto = producto.Name ?? "Sin nombre",
                        CategoriaProducto = categoriaEstandar,
                        FamiliaProducto = producto.Category,
                        SubfamiliaProducto = producto.Group,
                        Cantidad = producto.Quantity ?? 1,
                        PrecioUnitario = producto.UnitPrice ?? 0,
                        ImporteBruto = lineImporteBruto,
                        ImporteDescuento = lineDescuento,
                        ImporteNeto = lineImporteNeto,
                        ImporteImpuesto = lineImporteImpuesto,
                        TieneDescuento = lineDescuento > 0,
                        EstaAnulado = string.Equals(producto.CanceledItem, "True", StringComparison.OrdinalIgnoreCase),
                        MotivoAnulacion = (string?)null,
                        Notas = producto.DiscountName
                    });
            }
        }

        // Insertar medio de pago (si viene en detalle)
        if (!string.IsNullOrEmpty(detalle?.MetodoPago))
        {
            var mapeosPago = await connection.QueryAsync<(string CodigoOrigen, string MedioPagoDestino)>(@"
                SELECT CodigoOrigen, MedioPagoDestino FROM dim.MapeoMedioPago WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId });
            var mapeosPagoDict = mapeosPago.ToDictionary(m => m.CodigoOrigen, m => m.MedioPagoDestino, StringComparer.OrdinalIgnoreCase);

            var codigoMedioPago = mapeosPagoDict.GetValueOrDefault(detalle.MetodoPago, "OTHER");

            if (codigoMedioPago == "OTHER" && !string.IsNullOrEmpty(detalle.MetodoPago))
            {
                await RegistrarValorNoMapeadoAsync(nodoConexionId, "MEDIO_PAGO", detalle.MetodoPago, detalle.MetodoPago);
            }

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
                    Importe = importeBruto
                });
        }

        // Recalcular totales del ticket desde las lineas de detalle
        // (los importes de cabecera de Vinson no siempre vienen completos)
        if (lineasCount > 0)
        {
            await connection.ExecuteAsync(@"
                UPDATE vt
                SET ImporteBruto = COALESCE(lineas.TotalBruto, vt.ImporteBruto),
                    ImporteNeto = COALESCE(lineas.TotalNeto, vt.ImporteNeto),
                    ImporteImpuesto = COALESCE(lineas.TotalImpuesto, vt.ImporteImpuesto)
                FROM fact.VentaTicket vt
                INNER JOIN (
                    SELECT VentaTicketId,
                           SUM(ImporteBruto) as TotalBruto,
                           SUM(ImporteNeto) as TotalNeto,
                           SUM(ImporteImpuesto) as TotalImpuesto
                    FROM fact.VentaTicketDetalle
                    WHERE VentaTicketId = @TicketId
                    GROUP BY VentaTicketId
                ) lineas ON vt.VentaTicketId = lineas.VentaTicketId
                WHERE vt.VentaTicketId = @TicketId",
                new { TicketId = ticketId });
        }

        return (ticketId, lineasCount);
    }

    private async Task RegistrarValorNoMapeadoAsync(int nodoConexionId, string tipoMapeo, string codigoOrigen, string? nombreOrigen)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            MERGE dim.ValorNoMapeado AS target
            USING (SELECT @NodoId AS NodoConexionId, @Tipo AS TipoMapeo, @Codigo AS CodigoOrigen) AS source
            ON target.NodoConexionId = source.NodoConexionId AND target.TipoMapeo = source.TipoMapeo AND target.CodigoOrigen = source.CodigoOrigen
            WHEN MATCHED THEN
                UPDATE SET Ocurrencias = target.Ocurrencias + 1, UltimaVez = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (NodoConexionId, TipoMapeo, CodigoOrigen, NombreOrigen, Ocurrencias, PrimeraVez, UltimaVez)
                VALUES (@NodoId, @Tipo, @Codigo, @Nombre, 1, GETUTCDATE(), GETUTCDATE());",
            new { NodoId = nodoConexionId, Tipo = tipoMapeo, Codigo = codigoOrigen, Nombre = nombreOrigen });
    }

    #region Métodos auxiliares (similares a AgoraExtractor)

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

        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion
            SET UltimaSincronizacion = GETUTCDATE(),
                UltimoEstado = @Estado,
                UltimoBatchId = @BatchId,
                ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoId",
            new { NodoId = nodoConexionId, Estado = estado, BatchId = batchId.ToString() });

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
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, 'VINSON_V1', 'FULL_DAY', 'API', 'PROCESSING')",
            new { BatchGuid = batchGuid, FranquiciaId = franquiciaId, FechaNegocio = fechaNegocio });

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

    #endregion
}

#region DTOs para deserializar respuestas de Vinson

public class VinsonLoginResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public class VinsonTransaccion
{
    [JsonPropertyName("idTransaccion")]
    public long IdTransaccion { get; set; }

    [JsonPropertyName("openDate")]
    public DateTime? OpenDate { get; set; }

    [JsonPropertyName("mesa")]
    public int? Mesa { get; set; }

    [JsonPropertyName("clientes")]
    public int? Clientes { get; set; }

    [JsonPropertyName("inicio")]
    public DateTime? Inicio { get; set; }

    [JsonPropertyName("fin")]
    public DateTime? Fin { get; set; }

    [JsonPropertyName("estado")]
    public int? Estado { get; set; }

    [JsonPropertyName("empInicio")]
    public int? EmpInicio { get; set; }

    [JsonPropertyName("montoNeto")]
    public decimal? MontoNeto { get; set; }

    [JsonPropertyName("montoFinal")]
    public decimal? MontoFinal { get; set; }
}

public class VinsonSalesFullInfo
{
    [JsonPropertyName("transactionId")]
    public long TransactionId { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; set; }

    [JsonPropertyName("netAmount")]
    public decimal? NetAmount { get; set; }

    [JsonPropertyName("taxNetAmount")]
    public decimal? TaxNetAmount { get; set; }

    [JsonPropertyName("metodoPago")]
    public string? MetodoPago { get; set; }

    [JsonPropertyName("modoVenta")]
    public string? ModoVenta { get; set; }

    [JsonPropertyName("startHour")]
    public string? StartHour { get; set; }

    [JsonPropertyName("endHour")]
    public string? EndHour { get; set; }

    [JsonPropertyName("ticketNumber")]
    public int? TicketNumber { get; set; }

    [JsonPropertyName("PV")]
    public int? PV { get; set; }

    [JsonPropertyName("ticketType")]
    public string? TicketType { get; set; }

    [JsonPropertyName("products")]
    public List<VinsonProduct>? Products { get; set; }
}

public class VinsonProduct
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("grossAmount")]
    public decimal? GrossAmount { get; set; }

    [JsonPropertyName("netAmount")]
    public decimal? NetAmount { get; set; }

    [JsonPropertyName("discountAmount")]
    public decimal? DiscountAmount { get; set; }

    [JsonPropertyName("discountName")]
    public string? DiscountName { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("canceledItem")]
    public string? CanceledItem { get; set; } // "True" o "False" como string
}

#endregion
