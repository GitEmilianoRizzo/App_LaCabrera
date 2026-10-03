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
/// Servicio extractor para sistemas Ayres IT POP (Palermo Norte, Palermo Sur, etc.)
/// API: http://{endpoint}:8520
/// Autenticación: JWT con login (email/pass/idsucursal)
/// Documentación: https://ayresit.ar/documentacion/pos/api/
/// </summary>
public interface IAyresItExtractorService
{
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null);
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta);
    Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId);
}

public class AyresItExtractorService : IAyresItExtractorService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AyresItExtractorService> _logger;

    // Cache del token JWT (por nodo)
    private static readonly Dictionary<int, (string Token, DateTime Expiration)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    public AyresItExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<AyresItExtractorService> logger)
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

            var config = JsonSerializer.Deserialize<AyresItConfiguracion>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (config?.Connection == null)
            {
                return new EjecucionResultDto { Success = false, Message = "Configuración de conexión inválida" };
            }

            // Validar campos requeridos
            if (string.IsNullOrEmpty(config.Connection.Email) || string.IsNullOrEmpty(config.Connection.Pass))
            {
                return new EjecucionResultDto { Success = false, Message = "Faltan credenciales (email/pass) para Ayres IT" };
            }

            if (config.Connection.IdSucursal <= 0)
            {
                return new EjecucionResultDto { Success = false, Message = "Falta idsucursal para Ayres IT" };
            }

            // 2. Registrar inicio de ejecución
            var businessDay = fechaNegocio ?? DateTime.Today.AddDays(-1);
            ejecucionId = await RegistrarInicioEjecucionAsync(nodoConexionId, businessDay);
            resultado.EjecucionId = ejecucionId;

            _logger.LogInformation("Extrayendo datos de Ayres IT para {Fecha} - Nodo {NodoId} - Sucursal {SucursalId}",
                businessDay.ToString("yyyy-MM-dd"), nodoConexionId, config.Connection.IdSucursal);

            // 3. Obtener token JWT
            var token = await ObtenerTokenAsync(nodoConexionId, config.Connection);
            if (string.IsNullOrEmpty(token))
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, "No se pudo obtener token JWT");
                return new EjecucionResultDto { Success = false, Message = "Error de autenticación con Ayres IT", EjecucionId = ejecucionId };
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(config.Connection.TimeoutSeconds > 0 ? config.Connection.TimeoutSeconds : 60);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // 4. Obtener ventas del día (API permite máximo 10 días)
            var fechaDesdeStr = businessDay.ToString("yyyy-MM-dd");
            var fechaHastaStr = businessDay.ToString("yyyy-MM-dd");
            var urlVentas = $"{config.Connection.BaseUrl}/ventas?fechaContableDesde={fechaDesdeStr}&fechaContableHasta={fechaHastaStr}";

            _logger.LogInformation("Consultando ventas: {Url}", urlVentas);
            var responseVentas = await client.GetAsync(urlVentas);

            if (!responseVentas.IsSuccessStatusCode)
            {
                var errorBody = await responseVentas.Content.ReadAsStringAsync();
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, $"HTTP {(int)responseVentas.StatusCode}: {errorBody}");
                return new EjecucionResultDto { Success = false, Message = $"Error API Ayres IT: HTTP {(int)responseVentas.StatusCode}", EjecucionId = ejecucionId };
            }

            var jsonVentas = await responseVentas.Content.ReadAsStringAsync();
            var ventasResponse = JsonSerializer.Deserialize<AyresItVentasResponse>(jsonVentas, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (ventasResponse?.ResultCode != "SUCCESS")
            {
                var errorMsg = $"API retornó: {ventasResponse?.ResultCode} - {ventasResponse?.ResultDescription}";
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, errorMsg);
                return new EjecucionResultDto { Success = false, Message = errorMsg, EjecucionId = ejecucionId };
            }

            var ventas = ventasResponse.Content?.Ventas ?? new List<AyresItVentaWrapper>();

            if (ventas.Count == 0)
            {
                await RegistrarFinEjecucionAsync(ejecucionId.Value, "SUCCESS", 0, 0, 0, "Sin transacciones para procesar");
                return new EjecucionResultDto { Success = true, Message = "Sin transacciones para procesar", TicketsProcesados = 0, EjecucionId = ejecucionId };
            }

            _logger.LogInformation("Recibidas {Count} ventas de Ayres IT", ventas.Count);

            // 5. Crear batch de ingesta
            var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, businessDay, jsonVentas);
            resultado.BatchId = batchId;

            // 6. Procesar ventas
            int ticketsProcesados = 0;
            int lineasProcesadas = 0;
            int errores = 0;

            foreach (var ventaWrapper in ventas)
            {
                try
                {
                    var venta = ventaWrapper.Venta;
                    if (venta == null) continue;

                    var (ticketId, lineas) = await ProcesarVentaAsync(venta, nodo.FranquiciaId, batchId, nodoConexionId, nodo.Moneda ?? "ARS");
                    if (ticketId > 0)
                    {
                        ticketsProcesados++;
                        lineasProcesadas += lineas;
                    }
                }
                catch (Exception ex)
                {
                    errores++;
                    _logger.LogError(ex, "Error procesando venta {Id}", ventaWrapper.Venta?.IdVenta);
                    await RegistrarErrorIngestaAsync(batchId, $"Error venta {ventaWrapper.Venta?.IdVenta}: {ex.Message}");
                }
            }

            // 7. Actualizar batch
            await ActualizarBatchAsync(batchId, ticketsProcesados, lineasProcesadas, errores);

            // 8. Registrar fin de ejecución
            var estado = errores > 0 ? (ticketsProcesados > 0 ? "WARNING" : "ERROR") : "SUCCESS";
            await RegistrarFinEjecucionAsync(ejecucionId.Value, estado, ticketsProcesados, lineasProcesadas, errores, null);

            // 9. Actualizar estado de sincronización
            await ActualizarEstadoSincronizacionAsync(nodoConexionId, nodo.FranquiciaId, estado, batchId);

            resultado.Success = true;
            resultado.Message = $"Procesados {ticketsProcesados} tickets, {lineasProcesadas} líneas" + (errores > 0 ? $", {errores} errores" : "");
            resultado.TicketsProcesados = ticketsProcesados;
            resultado.LineasProcesadas = lineasProcesadas;
            resultado.ErrorsCount = errores;

            _logger.LogInformation("Extracción Ayres IT completada: {Message}", resultado.Message);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en extracción Ayres IT para nodo {NodoId}", nodoConexionId);

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

        _logger.LogInformation("Iniciando extracción Ayres IT de rango {Desde} a {Hasta} para nodo {NodoId}",
            fechaDesde.ToString("yyyy-MM-dd"), fechaHasta.ToString("yyyy-MM-dd"), nodoConexionId);

        // Ayres IT tiene límite de 10 días por consulta, procesamos día a día para mantener consistencia
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

        _logger.LogInformation("Extracción Ayres IT de rango completada: {Dias} días, {Tickets} tickets, {Lineas} líneas, {Errores} errores",
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
                .ToList();

            DateTime fechaDesde;

            if (fechasConDatos.Count == 0)
            {
                // Sin datos previos: sincronizar últimos 30 días
                fechaDesde = DateTime.Today.AddDays(-30);
                _logger.LogInformation("Sin datos previos para franquicia {Id}, sincronizando últimos 30 días desde {Fecha}",
                    nodo.FranquiciaId, fechaDesde.ToString("yyyy-MM-dd"));
            }
            else
            {
                // Encontrar el primer hueco o usar día siguiente al último
                var ultimaFechaConDatos = fechasConDatos.Max();

                // Buscar huecos
                DateTime? primerHueco = null;
                for (var fecha = fechasConDatos.Min(); fecha <= fechaHasta; fecha = fecha.AddDays(1))
                {
                    if (!fechasConDatos.Contains(fecha))
                    {
                        primerHueco = fecha;
                        break;
                    }
                }

                if (primerHueco.HasValue)
                {
                    fechaDesde = primerHueco.Value;
                    _logger.LogInformation("Encontrado hueco en {Fecha} para franquicia {Id}, sincronizando desde ahí",
                        fechaDesde.ToString("yyyy-MM-dd"), nodo.FranquiciaId);
                }
                else
                {
                    // Sin huecos, continuar desde día siguiente al último
                    fechaDesde = ultimaFechaConDatos.AddDays(1);
                    _logger.LogInformation("Sin huecos, continuando desde {Fecha} para franquicia {Id}",
                        fechaDesde.ToString("yyyy-MM-dd"), nodo.FranquiciaId);
                }
            }

            // 4. Si fechaDesde > fechaHasta, no hay nada que sincronizar
            if (fechaDesde > fechaHasta)
            {
                return new EjecucionResultDto
                {
                    Success = true,
                    Message = "Datos actualizados, no hay días pendientes de sincronizar",
                    TicketsProcesados = 0
                };
            }

            // 5. Ejecutar extracción del rango
            return await EjecutarExtraccionRangoAsync(nodoConexionId, fechaDesde, fechaHasta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EjecutarExtraccionDesdeUltimoAsync para nodo {NodoId}", nodoConexionId);
            return new EjecucionResultDto { Success = false, Message = ex.Message };
        }
    }

    #region Autenticación

    private async Task<string?> ObtenerTokenAsync(int nodoConexionId, AyresItConnectionConfig config)
    {
        // Verificar cache
        lock (_tokenLock)
        {
            if (_tokenCache.TryGetValue(nodoConexionId, out var cached))
            {
                if (cached.Expiration > DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogDebug("Usando token cacheado para nodo {NodoId}", nodoConexionId);
                    return cached.Token;
                }
            }
        }

        // Solicitar nuevo token
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var loginPayload = new
            {
                email = config.Email,
                pass = config.Pass,
                idsucursal = config.IdSucursal
            };

            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{config.BaseUrl}/login", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error de login en Ayres IT: HTTP {Code} - {Body}", (int)response.StatusCode, errorBody);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<AyresItLoginResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (loginResponse?.ResultCode != "SUCCESS" || string.IsNullOrEmpty(loginResponse.Content?.TokenAccess))
            {
                _logger.LogError("Login Ayres IT fallido: {Code} - {Desc}", loginResponse?.ResultCode, loginResponse?.ResultDescription);
                return null;
            }

            var token = loginResponse.Content.TokenAccess;
            var expiration = DateTime.UtcNow.AddSeconds(loginResponse.Content.AliveTime - 60); // Margen de 60 segundos

            // Guardar en cache
            lock (_tokenLock)
            {
                _tokenCache[nodoConexionId] = (token, expiration);
            }

            _logger.LogInformation("Token Ayres IT obtenido para nodo {NodoId}, expira en {Segundos}s", nodoConexionId, loginResponse.Content.AliveTime);
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo token de Ayres IT para nodo {NodoId}", nodoConexionId);
            return null;
        }
    }

    #endregion

    #region Procesamiento de ventas

    private async Task<(long TicketId, int LineasProcesadas)> ProcesarVentaAsync(
        AyresItVenta venta,
        int franquiciaId,
        long batchId,
        int nodoConexionId,
        string moneda)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Parsear fechas (formato dd/MM/yyyy o dd/MM/yyyy HH:mm:ss)
        DateTime? fechaApertura = ParseFechaAyresIt(venta.FechaHoraApertura);
        DateTime? fechaCierre = ParseFechaAyresIt(venta.FechaHoraCierre);
        DateTime? fechaNegocio = ParseFechaAyresIt(venta.FechaContable);

        if (!fechaNegocio.HasValue)
        {
            _logger.LogWarning("Venta {Id} sin fecha contable válida", venta.IdVenta);
            return (0, 0);
        }

        // Verificar si el ticket ya existe
        var existingTicketId = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket
            WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalTicketId",
            new { FranquiciaId = franquiciaId, ExternalTicketId = venta.IdVenta.ToString() });

        if (existingTicketId.HasValue)
        {
            _logger.LogDebug("Ticket {Id} ya existe, omitiendo", venta.IdVenta);
            return (0, 0);
        }

        // Determinar estado
        var estaAnulado = venta.Estado == "X" || venta.Estado == "A";
        var estaCerrado = venta.Estado == "C";
        var estadoTicket = estaAnulado ? "ANULADO" : (estaCerrado ? "CERRADO" : "ABIERTO");

        // Calcular montos
        var importeBruto = venta.FacturaMontoTotal ?? 0;
        var importeNeto = venta.FacturaMontoGravado ?? importeBruto;
        var importeImpuestos = venta.FacturaMontoIVA ?? 0;
        var importeServicio = venta.FacturaMontoServicio ?? 0;

        // VentaNeta = Bruto - IVA - Servicio (propina)
        var importeVentaNeta = importeBruto - importeImpuestos - importeServicio;

        // Insertar ticket
        var ticketId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO fact.VentaTicket (
                FranquiciaId, IngestionBatchId, ExternalTicketId, NumeroTicket,
                FechaNegocio, FechaApertura, FechaCierre,
                CantidadCubiertos, NumeroMesa, AreaMesa,
                MozoId, NombreMozo,
                ImporteBruto, ImporteNetoSinImpuesto, ImporteImpuesto, ImportePropina,
                ImporteNeto, ImporteDescuento, ImporteTotalPagado,
                TieneDescuento, EstaAnulado, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa,
                FuenteSistema, Estado, CodigoMoneda
            ) VALUES (
                @FranquiciaId, @BatchId, @ExternalTicketId, @NumeroTicket,
                @FechaNegocio, @FechaApertura, @FechaCierre,
                @Cubiertos, @Mesa, @Sector,
                @MozoId, @MozoNombre,
                @ImporteBruto, @ImporteNetoSinImpuesto, @ImporteImpuesto, @ImporteServicio,
                @ImporteNeto, 0, @ImporteBruto,
                0, @EstaAnulado, @TieneCubiertos, @TieneMozo, @TieneMesa,
                'AYRESIT', @Estado, @CodigoMoneda
            );
            SELECT SCOPE_IDENTITY();",
            new
            {
                FranquiciaId = franquiciaId,
                BatchId = batchId,
                ExternalTicketId = venta.IdVenta.ToString(),
                NumeroTicket = venta.FacturaNumero?.ToString() ?? venta.IdVenta.ToString(),
                FechaNegocio = fechaNegocio.Value,
                FechaApertura = fechaApertura,
                FechaCierre = fechaCierre,
                Cubiertos = venta.CantidadConsumidores ?? 0,
                Mesa = venta.SectorTipo,
                Sector = venta.SectorTipo,
                MozoId = venta.IdVendedor?.ToString(),
                MozoNombre = (string?)null,
                ImporteBruto = importeBruto,
                ImporteNetoSinImpuesto = importeNeto,
                ImporteImpuesto = importeImpuestos,
                ImporteServicio = importeServicio,
                ImporteNeto = importeVentaNeta,
                EstaAnulado = estaAnulado,
                TieneCubiertos = (venta.CantidadConsumidores ?? 0) > 0,
                TieneMozo = venta.IdVendedor.HasValue,
                TieneMesa = !string.IsNullOrEmpty(venta.SectorTipo),
                Estado = estadoTicket,
                CodigoMoneda = moneda
            });

        // Procesar items
        int lineasProcesadas = 0;
        if (venta.Items != null)
        {
            foreach (var item in venta.Items)
            {
                if (item.TipoComandaItem != "P") continue; // Solo productos, no modificadores

                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketDetalle (
                        VentaTicketId, CodigoProducto, NombreProducto,
                        Cantidad, PrecioUnitario, ImporteBruto, ImporteNeto,
                        ImporteDescuento, TieneDescuento, CategoriaProducto
                    ) VALUES (
                        @TicketId, @CodigoProducto, @NombreProducto,
                        @Cantidad, @PrecioUnitario, @ImporteBruto, @ImporteNeto,
                        @Descuento, @TieneDescuento, @Categoria
                    )",
                    new
                    {
                        TicketId = ticketId,
                        CodigoProducto = item.IdArticulo.ToString(),
                        NombreProducto = item.IdArticulo.ToString(), // TODO: obtener nombre de catálogo
                        Cantidad = item.Cantidad ?? 1,
                        PrecioUnitario = item.PrecioUnitario ?? 0,
                        ImporteBruto = item.MontoConIVA ?? 0,
                        ImporteNeto = item.MontoSinIVA ?? 0,
                        Descuento = 0m,
                        TieneDescuento = false,
                        Categoria = (string?)null
                    });

                lineasProcesadas++;
            }
        }

        // Procesar medios de pago (cobranzas)
        if (venta.Cobranzas != null)
        {
            foreach (var cobranza in venta.Cobranzas)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO fact.VentaTicketMedioPago (
                        VentaTicketId, CodigoMedioPago, MarcaTarjeta, Importe
                    ) VALUES (
                        @TicketId, @CodigoMedioPago, @MarcaTarjeta, @Importe
                    )",
                    new
                    {
                        TicketId = ticketId,
                        CodigoMedioPago = cobranza.IdMedioPago?.ToString() ?? "0",
                        MarcaTarjeta = cobranza.DescripcionMedioPago ?? "Desconocido",
                        Importe = cobranza.Monto ?? 0
                    });
            }
        }

        return (ticketId, lineasProcesadas);
    }

    private DateTime? ParseFechaAyresIt(string? fechaStr)
    {
        if (string.IsNullOrEmpty(fechaStr)) return null;

        // Formatos posibles: "dd/MM/yyyy" o "dd/MM/yyyy HH:mm:ss"
        if (DateTime.TryParseExact(fechaStr, "dd/MM/yyyy HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var fecha1))
            return fecha1;
        if (DateTime.TryParseExact(fechaStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fecha2))
            return fecha2;

        return null;
    }

    #endregion

    #region Helpers de base de datos

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
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, 'AYRESIT_V1', 'FULL_DAY', 'API', 'PROCESSING')",
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

#region DTOs para deserializar respuestas de Ayres IT

public class AyresItConfiguracion
{
    [JsonPropertyName("connection")]
    public AyresItConnectionConfig? Connection { get; set; }

    [JsonPropertyName("field_mappings")]
    public List<AyresItFieldMapping>? FieldMappings { get; set; }
}

public class AyresItFieldMapping
{
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }

    [JsonPropertyName("destino")]
    public string? Destino { get; set; }

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}

public class AyresItConnectionConfig
{
    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("pass")]
    public string Pass { get; set; } = string.Empty;

    [JsonPropertyName("idsucursal")]
    public int IdSucursal { get; set; }

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 60;
}

public class AyresItLoginResponse
{
    [JsonPropertyName("resultCode")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("resultDescription")]
    public string? ResultDescription { get; set; }

    [JsonPropertyName("content")]
    public AyresItLoginContent? Content { get; set; }
}

public class AyresItLoginContent
{
    [JsonPropertyName("tokenAccess")]
    public string? TokenAccess { get; set; }

    [JsonPropertyName("aliveTime")]
    public int AliveTime { get; set; }
}

public class AyresItVentasResponse
{
    [JsonPropertyName("resultCode")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("resultDescription")]
    public string? ResultDescription { get; set; }

    [JsonPropertyName("content")]
    public AyresItVentasContent? Content { get; set; }
}

public class AyresItVentasContent
{
    [JsonPropertyName("cantidad")]
    public int Cantidad { get; set; }

    [JsonPropertyName("ventas")]
    public List<AyresItVentaWrapper>? Ventas { get; set; }
}

public class AyresItVentaWrapper
{
    [JsonPropertyName("venta")]
    public AyresItVenta? Venta { get; set; }
}

public class AyresItVenta
{
    [JsonPropertyName("idVenta")]
    public long IdVenta { get; set; }

    [JsonPropertyName("idCliente")]
    public int? IdCliente { get; set; }

    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    [JsonPropertyName("fechaContable")]
    public string? FechaContable { get; set; }

    [JsonPropertyName("fechaHoraApertura")]
    public string? FechaHoraApertura { get; set; }

    [JsonPropertyName("fechaHoraCierre")]
    public string? FechaHoraCierre { get; set; }

    [JsonPropertyName("idVendedor")]
    public int? IdVendedor { get; set; }

    [JsonPropertyName("sectorTipo")]
    public string? SectorTipo { get; set; }

    [JsonPropertyName("cantidadConsumidores")]
    public int? CantidadConsumidores { get; set; }

    [JsonPropertyName("facturaNumero")]
    public int? FacturaNumero { get; set; }

    [JsonPropertyName("facturaMontoGravado")]
    public decimal? FacturaMontoGravado { get; set; }

    [JsonPropertyName("facturaMontoIVA")]
    public decimal? FacturaMontoIVA { get; set; }

    [JsonPropertyName("facturaMontoTotal")]
    public decimal? FacturaMontoTotal { get; set; }

    [JsonPropertyName("facturaMontoServicio")]
    public decimal? FacturaMontoServicio { get; set; }

    [JsonPropertyName("items")]
    public List<AyresItItem>? Items { get; set; }

    [JsonPropertyName("cobranzas")]
    public List<AyresItCobranza>? Cobranzas { get; set; }
}

public class AyresItItem
{
    [JsonPropertyName("tipoComandaItem")]
    public string? TipoComandaItem { get; set; }

    [JsonPropertyName("idArticulo")]
    public int IdArticulo { get; set; }

    [JsonPropertyName("cantidad")]
    public decimal? Cantidad { get; set; }

    [JsonPropertyName("precioUnitario")]
    public decimal? PrecioUnitario { get; set; }

    [JsonPropertyName("montoSinIVA")]
    public decimal? MontoSinIVA { get; set; }

    [JsonPropertyName("montoConIVA")]
    public decimal? MontoConIVA { get; set; }

    [JsonPropertyName("montoIVA")]
    public decimal? MontoIVA { get; set; }

    [JsonPropertyName("ivaAlicuota")]
    public decimal? IvaAlicuota { get; set; }
}

public class AyresItCobranza
{
    [JsonPropertyName("idMedioPago")]
    public int? IdMedioPago { get; set; }

    [JsonPropertyName("descripcionMedioPago")]
    public string? DescripcionMedioPago { get; set; }

    [JsonPropertyName("monto")]
    public decimal? Monto { get; set; }
}

#endregion
