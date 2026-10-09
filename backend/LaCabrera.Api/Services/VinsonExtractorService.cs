using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;
using Microsoft.Extensions.Options;

namespace LaCabrera.Api.Services;

/// <summary>
/// Servicio extractor para sistemas Vinson Cloud (Aeroparque, Ezeiza, etc.)
/// API: https://apireportes.vinson.com.ar
/// Autenticación: JWT con login (username/password)
/// </summary>
public interface IVinsonExtractorService
{
    /// <param name="reprocesar">Si es true, los tickets que ya existen se actualizan en lugar de omitirse.</param>
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, bool reprocesar = false);
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta, bool reprocesar = false);
    /// <summary>
    /// Sincroniza desde el último día con datos hasta hoy, evitando huecos.
    /// Si no hay datos previos, sincroniza los últimos 30 días.
    /// </summary>
    Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId);
}

public class VinsonExtractorService : IVinsonExtractorService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VinsonExtractorService> _logger;
    private readonly VinsonSettings _settings;

    // Formatos de fecha para GetSalesFullInforAndProducts, en orden de preferencia
    private static readonly string[] FormatosFechaDetalle = { "dd-MM-yyyy", "yyyy-MM-dd", "yyyyMMdd" };

    // Cache del token JWT (por nodo)
    private static readonly Dictionary<int, (string Token, DateTime Expiration)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    public VinsonExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<VinsonExtractorService> logger,
        IOptions<VinsonSettings> settings)
    {
        _connectionFactory = connectionFactory;
        _conexionRepository = conexionRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, bool reprocesar = false)
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
            // IMPORTANTE: formatos de fecha de Vinson
            // - GetTransactionsByDate: yyyyMMdd
            // - GetSalesFullInforAndProducts: hoy solo acepta dd-MM-yyyy. Con yyyy-MM-dd (el formato
            //   anterior) responde HTTP 400 "not recognized as a valid DateOnly" (T-165)
            var businessDayDateOnly = businessDay.ToString("yyyyMMdd");
            var businessDayDateTime = businessDay.ToString("yyyy-MM-dd");

            _logger.LogInformation("Extrayendo datos de Vinson para {Fecha} - Nodo {NodoId} - Tienda {StoreId}{Modo}",
                businessDayDateTime, nodoConexionId, config.Connection.StoreId, reprocesar ? " (reproceso)" : "");

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

            // 6. Obtener detalle con productos. Se prueban los formatos de fecha en orden y se
            //    sigue con el siguiente solo si Vinson rechaza la fecha (HTTP 400).
            HttpResponseMessage responseDet = null!;
            foreach (var formato in FormatosFechaDetalle)
            {
                var fechaDetalle = businessDay.ToString(formato, System.Globalization.CultureInfo.InvariantCulture);
                var urlDetalle = $"{config.Connection.BaseUrl}/api/Transaction/GetSalesFullInforAndProducts/{config.Connection.StoreId}/{fechaDetalle}/{fechaDetalle}";
                responseDet = await client.GetAsync(urlDetalle);
                if (responseDet.StatusCode != System.Net.HttpStatusCode.BadRequest)
                    break;
            }

            List<VinsonSalesFullInfo>? detalle = null;
            if (responseDet.IsSuccessStatusCode)
            {
                var jsonDetalle = await responseDet.Content.ReadAsStringAsync();
                detalle = JsonSerializer.Deserialize<List<VinsonSalesFullInfo>>(jsonDetalle, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.LogInformation("Recibidos {Count} registros de detalle de Vinson", detalle?.Count ?? 0);
            }
            else
            {
                var errorDetalle = await responseDet.Content.ReadAsStringAsync();
                _logger.LogWarning("No se pudo obtener detalle de productos: HTTP {Code} {Body}",
                    (int)responseDet.StatusCode, errorDetalle.Length > 500 ? errorDetalle[..500] : errorDetalle);
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
            var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, businessDay, jsonCabeceras, reprocesar ? "CORRECTION" : "FULL_DAY");
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

                    var (ticketId, lineas) = await ProcesarTransaccionAsync(transaccion, detalleTransaccion, nodo.FranquiciaId, batchId, nodoConexionId, nodo.Moneda, reprocesar);
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

    public async Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta, bool reprocesar = false)
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
                var resultadoDia = await EjecutarExtraccionAsync(nodoConexionId, fecha, reprocesar);

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

    public async Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId)
    {
        try
        {
            // 1. Obtener franquicia del nodo
            var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
            if (nodo == null)
            {
                return new EjecucionResultDto { Success = false, Message = "Nodo no encontrado" };
            }

            // 2. Definir ventana de búsqueda: últimos 60 días
            const int diasVentana = 60;
            var fechaLimite = DateTime.Today.AddDays(-diasVentana);
            var fechaHasta = DateTime.Today.AddDays(-1); // Hasta ayer (hoy puede estar incompleto)

            // 3. Buscar todas las fechas CON datos en la ventana
            using var connection = _connectionFactory.CreateConnection();
            var fechasConDatos = (await connection.QueryAsync<DateTime>(@"
                SELECT DISTINCT CAST(FechaNegocio AS DATE) as Fecha
                FROM fact.VentaTicket
                WHERE FranquiciaId = @FranquiciaId
                  AND FechaNegocio >= @FechaLimite
                  AND FechaNegocio <= @FechaHasta
                ORDER BY Fecha",
                new { FranquiciaId = nodo.FranquiciaId, FechaLimite = fechaLimite, FechaHasta = fechaHasta }))
                .ToHashSet();

            // 4. Encontrar el primer hueco (día sin datos) en la ventana
            DateTime fechaDesde;
            if (fechasConDatos.Count == 0)
            {
                // Sin datos: sincronizar toda la ventana
                fechaDesde = fechaLimite;
                _logger.LogInformation("Sin datos en últimos {Dias} días para franquicia {FranquiciaId}. Sincronizando desde {Fecha}.",
                    diasVentana, nodo.FranquiciaId, fechaDesde.ToString("yyyy-MM-dd"));
            }
            else
            {
                // Buscar el primer día sin datos desde fechaLimite
                fechaDesde = fechaHasta; // Por defecto, todo está completo
                for (var fecha = fechaLimite; fecha <= fechaHasta; fecha = fecha.AddDays(1))
                {
                    if (!fechasConDatos.Contains(fecha.Date))
                    {
                        fechaDesde = fecha;
                        break;
                    }
                }

                if (fechaDesde == fechaHasta && fechasConDatos.Contains(fechaHasta.Date))
                {
                    // Todos los días tienen datos, verificar si ayer está incluido
                    return new EjecucionResultDto
                    {
                        Success = true,
                        Message = $"Datos completos en últimos {diasVentana} días, no hay huecos que llenar",
                        TicketsProcesados = 0
                    };
                }

                var huecosCount = Enumerable.Range(0, (fechaHasta - fechaDesde).Days + 1)
                    .Count(d => !fechasConDatos.Contains(fechaDesde.AddDays(d).Date));

                _logger.LogInformation("Encontrados {Huecos} días sin datos desde {Fecha} para franquicia {FranquiciaId}. Sincronizando para llenar huecos.",
                    huecosCount, fechaDesde.ToString("yyyy-MM-dd"), nodo.FranquiciaId);
            }

            // 5. Ejecutar sincronización del rango
            _logger.LogInformation("Iniciando sincronización para llenar huecos: {Desde} a {Hasta} para nodo {NodoId}",
                fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"), nodoConexionId);

            return await EjecutarExtraccionRangoAsync(nodoConexionId, fechaDesde, fechaHasta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EjecutarExtraccionDesdeUltimoAsync para nodo {NodoId}", nodoConexionId);
            return new EjecucionResultDto { Success = false, Message = ex.Message };
        }
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

    /// <summary>
    /// Guarda una transacción de Vinson. Si el ticket ya existe se omite, salvo en modo reproceso:
    /// ahí se actualiza en el lugar (mismo VentaTicketId) y se reemplazan sus líneas y medio de pago,
    /// así que se puede ejecutar varias veces sin duplicar.
    /// </summary>
    internal async Task<(long ticketId, int lineas)> ProcesarTransaccionAsync(
        VinsonTransaccion transaccion,
        VinsonSalesFullInfo? detalle,
        int franquiciaId,
        long batchId,
        int nodoConexionId,
        string moneda,
        bool reprocesar = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var externalId = transaccion.IdTransaccion.ToString();

        // Verificar si ya existe
        var existe = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket
            WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalId",
            new { FranquiciaId = franquiciaId, ExternalId = externalId });

        if (existe.HasValue && !reprocesar)
        {
            _logger.LogDebug("Ticket {Id} ya existe, omitiendo", externalId);
            return (0, 0);
        }

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

        // Importes: el total del ticket sale de la cabecera del POS y las líneas se guardan
        // con importe extendido (cantidad × unitario). Ver VinsonImportes.
        var productos = detalle?.Products ?? new List<VinsonProduct>();

        // En reproceso, si Vinson no devolvió productos para el ticket se conservan las líneas ya
        // cargadas y solo se corrige la cabecera: no se borran líneas que no se pueden volver a cargar.
        var conservarLineas = existe.HasValue && productos.Count == 0;
        var lineasCalc = conservarLineas
            ? (await connection.QueryAsync<decimal>(
                    "SELECT ImporteBruto FROM fact.VentaTicketDetalle WHERE VentaTicketId = @Id", new { Id = existe!.Value }))
                .Select(b => new VinsonLineaCalculada(1, b, b, 0, b, 0, false)).ToList()
            : productos.Select(VinsonImportes.CalcularLinea).ToList();
        var ticket = VinsonImportes.CalcularTicket(transaccion, detalle, lineasCalc, _settings.TratamientoTicketsNegativos);
        var anularLineas = ticket.EsDevolucion && _settings.TratamientoTicketsNegativos == TratamientoTicketNegativo.MarcarDevolucion;

        var datosTicket = new
        {
            BatchId = batchId,
            FranquiciaId = franquiciaId,
            ExternalId = externalId,
            NumeroTicket = detalle?.TicketNumber?.ToString() ?? transaccion.IdTransaccion.ToString(),
            ticket.Estado,
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
            ticket.ImporteBruto,
            ticket.ImporteDescuento,
            ticket.ImporteNeto,
            ticket.ImporteImpuesto,
            ticket.ImporteNetoSinImpuesto,
            ImportePropina = 0m,
            ImporteTotalPagado = ticket.ImporteBruto,
            TieneDescuento = ticket.ImporteDescuento > 0,
            ticket.EstaAnulado,
            TieneCubiertos = (transaccion.Clientes ?? 0) > 0,
            TieneMozo = transaccion.EmpInicio.HasValue,
            TieneMesa = transaccion.Mesa.HasValue && transaccion.Mesa > 0,
            Usuario = reprocesar ? "VINSON_REPROCESO" : "VINSON"
        };

        connection.Open();
        using var tx = connection.BeginTransaction();

        long ticketId;
        if (existe.HasValue)
        {
            ticketId = existe.Value;
            await connection.ExecuteAsync(@"
                UPDATE fact.VentaTicket SET
                    IngestionBatchId = @BatchId, NumeroTicket = @NumeroTicket, Estado = @Estado,
                    TipoDocumentoFiscal = @TipoDocumentoFiscal,
                    FechaApertura = @FechaApertura, FechaCierre = @FechaCierre, PeriodoComida = @PeriodoComida,
                    NumeroMesa = @NumeroMesa, CantidadCubiertos = @CantidadCubiertos, CodigoMoneda = @CodigoMoneda,
                    ImporteBruto = @ImporteBruto, ImporteDescuento = @ImporteDescuento, ImporteNeto = @ImporteNeto,
                    ImporteImpuesto = @ImporteImpuesto, ImporteNetoSinImpuesto = @ImporteNetoSinImpuesto,
                    ImportesIncluyenImpuesto = 0, CalidadImpuesto = 'POS',
                    ImportePropina = @ImportePropina, ImporteTotalPagado = @ImporteTotalPagado,
                    TieneDescuento = @TieneDescuento, EstaAnulado = @EstaAnulado,
                    TieneDatosCubiertos = @TieneCubiertos, TieneDatosMozo = @TieneMozo, TieneDatosMesa = @TieneMesa,
                    FechaModificacion = SYSDATETIME(), UsuarioModificacion = @Usuario
                WHERE VentaTicketId = @TicketId",
                new { datosTicket.BatchId, datosTicket.NumeroTicket, datosTicket.Estado, datosTicket.TipoDocumentoFiscal,
                      datosTicket.FechaApertura, datosTicket.FechaCierre, datosTicket.PeriodoComida, datosTicket.NumeroMesa,
                      datosTicket.CantidadCubiertos, datosTicket.CodigoMoneda, datosTicket.ImporteBruto, datosTicket.ImporteDescuento,
                      datosTicket.ImporteNeto, datosTicket.ImporteImpuesto, datosTicket.ImporteNetoSinImpuesto,
                      datosTicket.ImportePropina, datosTicket.ImporteTotalPagado, datosTicket.TieneDescuento, datosTicket.EstaAnulado,
                      datosTicket.TieneCubiertos, datosTicket.TieneMozo, datosTicket.TieneMesa, datosTicket.Usuario, TicketId = ticketId },
                tx);

            if (conservarLineas)
            {
                if (anularLineas)
                {
                    await connection.ExecuteAsync(@"
                        UPDATE fact.VentaTicketDetalle
                        SET EstaAnulado = 1, MotivoAnulacion = 'DEVOLUCION_POS',
                            FechaModificacion = SYSDATETIME(), UsuarioModificacion = @Usuario
                        WHERE VentaTicketId = @TicketId",
                        new { TicketId = ticketId, datosTicket.Usuario }, tx);
                }

                _logger.LogWarning("Ticket {Id}: sin detalle de productos en Vinson, se actualiza solo la cabecera", externalId);
                tx.Commit();
                return (ticketId, 0);
            }

            await connection.ExecuteAsync(@"
                DELETE FROM fact.VentaTicketDetalle WHERE VentaTicketId = @TicketId;
                DELETE FROM fact.VentaTicketMedioPago WHERE VentaTicketId = @TicketId;",
                new { TicketId = ticketId }, tx);
        }
        else
        {
            ticketId = await connection.QuerySingleAsync<long>(@"
                INSERT INTO fact.VentaTicket (
                    IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado,
                    TipoDocumentoFiscal,
                    FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
                    NumeroMesa, AreaMesa, MozoId,
                    CantidadCubiertos, CodigoMoneda,
                    ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                    ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
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
                    @ImporteNetoSinImpuesto, 0, 'POS',
                    0, @ImportePropina, @ImporteTotalPagado,
                    @TieneDescuento, @EstaAnulado, @TieneCubiertos, @TieneMozo, @TieneMesa,
                    'VINSON'
                )",
                datosTicket, tx);
        }

        int lineasCount = 0;

        // Insertar líneas de detalle (si hay productos)
        if (productos.Count > 0)
        {
            // Obtener mapeos de categoría
            var mapeosCat = await connection.QueryAsync<(string CodigoOrigen, string CategoriaDestino)>(@"
                SELECT CodigoOrigen, CategoriaDestino FROM dim.MapeoCategoria WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId }, tx);
            var mapeoDict = mapeosCat.ToDictionary(m => m.CodigoOrigen, m => m.CategoriaDestino, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < productos.Count; i++)
            {
                var producto = productos[i];
                var linea = lineasCalc[i];

                // Buscar categoría mapeada (por nombre de categoría o grupo)
                var categoriaEstandar = mapeoDict.GetValueOrDefault(producto.Category ?? "", null)
                    ?? mapeoDict.GetValueOrDefault(producto.Group ?? "", null);

                // Registrar valor no mapeado si no existe
                if (string.IsNullOrEmpty(categoriaEstandar) && !string.IsNullOrEmpty(producto.Category))
                {
                    await RegistrarValorNoMapeadoAsync(nodoConexionId, "CATEGORIA", producto.Category, producto.Category);
                }

                lineasCount++;

                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketDetalle (
                        VentaTicketId, IngestionBatchId, FranquiciaId,
                        ExternalLineId, CodigoProducto, NombreProducto,
                        CategoriaProducto, FamiliaProducto, SubfamiliaProducto,
                        Cantidad, PrecioUnitario,
                        ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                        ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
                        TieneDescuento, EstaAnulado, MotivoAnulacion, Notas
                    )
                    VALUES (
                        @TicketId, @BatchId, @FranquiciaId,
                        @ExternalLineId, @CodigoProducto, @NombreProducto,
                        @CategoriaProducto, @FamiliaProducto, @SubfamiliaProducto,
                        @Cantidad, @PrecioUnitario,
                        @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                        @ImporteNeto, 0, 'POS',
                        @TieneDescuento, @EstaAnulado, @MotivoAnulacion, @Notas
                    )",
                    new
                    {
                        TicketId = ticketId,
                        BatchId = batchId,
                        FranquiciaId = franquiciaId,
                        ExternalLineId = (i + 1).ToString(),
                        CodigoProducto = producto.Name?.Replace(" ", "_").ToUpperInvariant()[..Math.Min(producto.Name.Length, 50)] ?? "UNKNOWN",
                        NombreProducto = producto.Name ?? "Sin nombre",
                        CategoriaProducto = categoriaEstandar,
                        FamiliaProducto = producto.Category,
                        SubfamiliaProducto = producto.Group,
                        linea.Cantidad,
                        linea.PrecioUnitario,
                        linea.ImporteBruto,
                        linea.ImporteDescuento,
                        linea.ImporteNeto,
                        linea.ImporteImpuesto,
                        TieneDescuento = linea.ImporteDescuento > 0,
                        EstaAnulado = linea.EstaAnulado || anularLineas,
                        MotivoAnulacion = anularLineas ? "DEVOLUCION_POS" : null,
                        Notas = producto.DiscountName
                    }, tx);
            }
        }

        // Insertar medio de pago (si viene en detalle)
        if (!string.IsNullOrEmpty(detalle?.MetodoPago))
        {
            var mapeosPago = await connection.QueryAsync<(string CodigoOrigen, string MedioPagoDestino)>(@"
                SELECT CodigoOrigen, MedioPagoDestino FROM dim.MapeoMedioPago WHERE NodoConexionId = @NodoId",
                new { NodoId = nodoConexionId }, tx);
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
                    Importe = ticket.ImporteBruto
                }, tx);
        }

        tx.Commit();
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

    private async Task<long> CrearBatchIngestaAsync(int franquiciaId, DateTime fechaNegocio, string rawJson, string tipoCarga)
    {
        using var connection = _connectionFactory.CreateConnection();

        var batchGuid = Guid.NewGuid().ToString();

        var batchId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO stg.IngestionBatch (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta, Estado)
            OUTPUT INSERTED.IngestionBatchId
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, 'VINSON_V1', @TipoCarga, 'API', 'PROCESSING')",
            new { BatchGuid = batchGuid, FranquiciaId = franquiciaId, FechaNegocio = fechaNegocio, TipoCarga = tipoCarga });

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
