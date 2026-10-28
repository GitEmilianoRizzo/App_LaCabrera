using System.IO.Compression;
using System.Text;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace LaCabrera.Api.Services;

/// <summary>
/// Carga de las franquicias de Chile (Oracle MICROS) a partir de los mails que reciben en una casilla.
///
/// - Una sola casilla (MicrosMail) para todos los locales. Cada local es un nodo con la sección
///   "micros": { "codigo_tienda": "CAB", "auto": true } en ConfiguracionJson.
/// - Solo se leen mails de remitentes del dominio autorizado (MicrosMail:DominioRemitente).
/// - De cada adjunto (.zip o GL_*.txt) se toma cada archivo GeneralLedger y se identifica el local por su
///   GLID; los archivos de otros locales o de otros restaurantes (p. ej. Piegari) se descartan.
/// - Cada archivo se carga con IngestionService; un reenvío idéntico se descarta como duplicado.
/// - El mail cargado sin errores se mueve a la carpeta de procesados; si algo falla queda donde está y se
///   reintenta en la próxima corrida. Los mails no se borran.
/// - La contraseña de la casilla vive en el servidor (MicrosMail:PasswordPath) y nunca se loguea.
/// </summary>
public interface IMicrosMailConectorService
{
    Task<EjecucionResultDto> SincronizarAsync(string modo = "MANUAL", CancellationToken ct = default);
    Task<EjecucionResultDto> ImportarArchivoAsync(int nodoConexionId, string contenido, string origen, CancellationToken ct = default);
}

public class MicrosMailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public string Usuario { get; set; } = string.Empty;
    public string? PasswordPath { get; set; }
    public string DominioRemitente { get; set; } = string.Empty;
    public string CarpetaProcesados { get; set; } = "Procesados";
    public string[] Carpetas { get; set; } = { "INBOX" };
}

public class MicrosMailConectorService : IMicrosMailConectorService
{
    public const string MarcaEjecucion = "MICROS_MAIL";

    private static readonly SemaphoreSlim _candado = new(1, 1);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IIngestionService _ingestionService;
    private readonly MicrosMailSettings _settings;
    private readonly ILogger<MicrosMailConectorService> _logger;

    public MicrosMailConectorService(
        IDbConnectionFactory connectionFactory,
        IIngestionService ingestionService,
        Microsoft.Extensions.Options.IOptions<MicrosMailSettings> settings,
        ILogger<MicrosMailConectorService> logger)
    {
        _connectionFactory = connectionFactory;
        _ingestionService = ingestionService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EjecucionResultDto> SincronizarAsync(string modo = "MANUAL", CancellationToken ct = default)
    {
        await _candado.WaitAsync(ct);
        try
        {
            var nodos = await GetNodosAsync(soloAuto: modo == "SCHEDULED");
            if (nodos.Count == 0)
                return new EjecucionResultDto { Success = true, Message = "No hay locales MICROS activos para la casilla" };

            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Usuario)
                || string.IsNullOrWhiteSpace(_settings.PasswordPath) || !File.Exists(_settings.PasswordPath)
                || string.IsNullOrWhiteSpace(_settings.DominioRemitente))
                return new EjecucionResultDto { Success = false, Message = "Falta configurar la casilla MICROS (host, usuario, contraseña o dominio)", ErrorsCount = 1 };

            var resumen = new ResumenRangoExtraccion();
            int mailsCargados = 0, mailsIgnorados = 0, mailsConError = 0;

            using var imap = new ImapClient { Timeout = 60_000 };
            await imap.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.SslOnConnect, ct);
            await imap.AuthenticateAsync(_settings.Usuario, (await File.ReadAllTextAsync(_settings.PasswordPath, ct)).Trim(), ct);

            var procesados = await GetOCrearCarpetaProcesadosAsync(imap, ct);

            foreach (var nombreCarpeta in _settings.Carpetas)
            {
                IMailFolder carpeta;
                try { carpeta = await imap.GetFolderAsync(nombreCarpeta, ct); }
                catch (FolderNotFoundException) { continue; }

                await carpeta.OpenAsync(FolderAccess.ReadWrite, ct);
                var uids = await carpeta.SearchAsync(SearchQuery.All, ct);
                var aMover = new List<UniqueId>();

                foreach (var uid in uids)
                {
                    var mensaje = await carpeta.GetMessageAsync(uid, ct);
                    var remitente = mensaje.From.Mailboxes.FirstOrDefault()?.Address ?? "";
                    if (!RemitenteAutorizado(remitente))
                    {
                        mailsIgnorados++;
                        continue;
                    }

                    var errores = 0;
                    var archivos = 0;
                    foreach (var (nombre, contenido) in ArchivosGeneralLedger(mensaje))
                    {
                        var tienda = MicrosGlParser.LeerCodigoTienda(contenido);
                        if (tienda == null || !nodos.TryGetValue(Normalizar(tienda), out var nodo))
                        {
                            _logger.LogInformation("MICROS: {Archivo} es de {Tienda}, que no está configurado: se descarta", nombre, tienda ?? "?");
                            continue;
                        }

                        archivos++;
                        var resultado = await CargarAsync(nodo, contenido, $"mail {mensaje.Date:dd/MM HH:mm} de {remitente}: {nombre}", modo, ct);
                        resumen.Agregar(resultado.Fecha, resultado.Resultado);
                        if (!resultado.Resultado.Success) errores++;
                    }

                    if (errores > 0) { mailsConError++; continue; }
                    if (archivos > 0) mailsCargados++;
                    aMover.Add(uid);
                }

                // Lo cargado (o sin archivos de locales configurados) se archiva; lo que falló queda para reintentar
                if (aMover.Count > 0 && procesados != null)
                    await carpeta.MoveToAsync(aMover, procesados, ct);
                await carpeta.CloseAsync(false, ct);
            }

            await imap.DisconnectAsync(true, ct);

            var total = resumen.Resultado();
            total.Message = $"Casilla MICROS: {mailsCargados} mails cargados, {mailsConError} con error, {mailsIgnorados} de remitentes no autorizados. " + total.Message;
            return total;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MICROS: error leyendo la casilla");
            return new EjecucionResultDto { Success = false, Message = $"Error leyendo la casilla MICROS: {ex.Message}", ErrorsCount = 1 };
        }
        finally
        {
            _candado.Release();
        }
    }

    public async Task<EjecucionResultDto> ImportarArchivoAsync(int nodoConexionId, string contenido, string origen, CancellationToken ct = default)
    {
        var nodo = (await GetNodosAsync(soloAuto: false)).Values.FirstOrDefault(n => n.NodoConexionId == nodoConexionId);
        if (nodo == null) return new EjecucionResultDto { Success = false, Message = "El nodo no es un local MICROS activo" };

        var tienda = MicrosGlParser.LeerCodigoTienda(contenido);
        if (tienda == null) return new EjecucionResultDto { Success = false, Message = "El archivo no es un export standard.GeneralLedger (falta GLID)" };
        if (Normalizar(tienda) != Normalizar(nodo.CodigoTienda))
            return new EjecucionResultDto { Success = false, Message = $"El archivo es del local {tienda}, no de {nodo.CodigoTienda}" };

        return (await CargarAsync(nodo, contenido, origen, "MANUAL", ct)).Resultado;
    }

    private async Task<(DateTime Fecha, EjecucionResultDto Resultado)> CargarAsync(NodoMicros nodo, string contenido, string origen, string modo, CancellationToken ct)
    {
        MicrosGlParser.Resultado parseo;
        try
        {
            parseo = MicrosGlParser.Parsear(contenido, nodo.FranquiciaCodigo, nodo.ZonaHoraria, nodo.Moneda);
        }
        catch (Exception ex)
        {
            var id = await RegistrarInicioEjecucionAsync(nodo.NodoConexionId, DateTime.Today, modo);
            await RegistrarFinEjecucionAsync(id, "ERROR", 0, 0, 1, $"{origen}: archivo ilegible ({ex.Message})");
            return (DateTime.Today, new EjecucionResultDto { Success = false, Message = $"{origen}: archivo ilegible", ErrorsCount = 1, EjecucionId = id });
        }

        var ejecucionId = await RegistrarInicioEjecucionAsync(nodo.NodoConexionId, parseo.FechaNegocio, modo);
        try
        {
            var respuesta = await _ingestionService.ProcessBatchAsync(parseo.Batch, nodo.FranquiciaId, contenido);
            if (respuesta.Status == "REJECTED")
            {
                var detalle = string.Join("; ", (respuesta.Errors ?? new()).Take(3).Select(e => e.Message));
                await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, $"{origen}: rechazado. {detalle}");
                await ActualizarEstadoAsync(nodo, "ERROR");
                return (parseo.FechaNegocio, new EjecucionResultDto { Success = false, Message = $"{origen}: rechazado. {detalle}", ErrorsCount = 1, EjecucionId = ejecucionId });
            }

            var tickets = respuesta.Summary?.TicketsProcessed ?? 0;
            var lineas = respuesta.Summary?.ItemsProcessed ?? 0;
            var mensaje = (respuesta.Status == "DUPLICATE" ? $"{origen}: ya estaba cargado" : origen)
                + (parseo.Avisos.Count > 0 ? $" | avisos: {string.Join(" / ", parseo.Avisos.Take(5))}" : "");
            await RegistrarFinEjecucionAsync(ejecucionId, parseo.Avisos.Count > 0 ? "WARNING" : "SUCCESS", tickets, lineas, 0, mensaje);
            await ActualizarEstadoAsync(nodo, "SUCCESS");
            _logger.LogInformation("MICROS {Tienda} {Fecha:yyyy-MM-dd}: {Estado}, {Tickets} tickets", nodo.CodigoTienda, parseo.FechaNegocio, respuesta.Status, tickets);

            return (parseo.FechaNegocio, new EjecucionResultDto
            {
                Success = true, Message = mensaje, TicketsProcesados = tickets, LineasProcesadas = lineas, EjecucionId = ejecucionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MICROS: error cargando {Origen}", origen);
            await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, $"{origen}: {ex.Message}");
            return (parseo.FechaNegocio, new EjecucionResultDto { Success = false, Message = $"{origen}: {ex.Message}", ErrorsCount = 1, EjecucionId = ejecucionId });
        }
    }

    /// <summary>Archivos GL_*.txt del mail: sueltos o dentro de zips.</summary>
    public static IEnumerable<(string Nombre, string Contenido)> ArchivosGeneralLedger(MimeMessage mensaje)
    {
        foreach (var adjunto in mensaje.Attachments.OfType<MimePart>())
        {
            var nombre = adjunto.FileName ?? "";
            using var datos = new MemoryStream();
            adjunto.Content?.DecodeTo(datos);
            datos.Position = 0;

            if (nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = new ZipArchive(datos, ZipArchiveMode.Read);
                foreach (var entrada in zip.Entries.Where(e => EsGeneralLedger(e.Name)))
                {
                    using var lector = new StreamReader(entrada.Open(), Encoding.UTF8);
                    yield return ($"{nombre}/{entrada.Name}", lector.ReadToEnd());
                }
            }
            else if (EsGeneralLedger(nombre))
            {
                using var lector = new StreamReader(datos, Encoding.UTF8);
                yield return (nombre, lector.ReadToEnd());
            }
        }
    }

    private static bool EsGeneralLedger(string nombre) =>
        nombre.StartsWith("GL_", StringComparison.OrdinalIgnoreCase) && nombre.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private bool RemitenteAutorizado(string direccion) =>
        direccion.EndsWith("@" + _settings.DominioRemitente.TrimStart('@'), StringComparison.OrdinalIgnoreCase);

    private static string Normalizar(string tienda) => new string(tienda.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private async Task<IMailFolder?> GetOCrearCarpetaProcesadosAsync(ImapClient imap, CancellationToken ct)
    {
        var inbox = imap.Inbox;
        try
        {
            return await imap.GetFolderAsync($"{inbox.FullName}{inbox.DirectorySeparator}{_settings.CarpetaProcesados}", ct);
        }
        catch (FolderNotFoundException)
        {
            return await inbox.CreateAsync(_settings.CarpetaProcesados, true, ct);
        }
    }

    private record NodoMicros(int NodoConexionId, int FranquiciaId, string FranquiciaCodigo, string CodigoTienda, string ZonaHoraria, string Moneda);

    private async Task<Dictionary<string, NodoMicros>> GetNodosAsync(bool soloAuto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var filas = await connection.QueryAsync<NodoMicros>(@"
            SELECT n.NodoConexionId, n.FranquiciaId, f.Codigo AS FranquiciaCodigo,
                   JSON_VALUE(n.ConfiguracionJson, '$.micros.codigo_tienda') AS CodigoTienda,
                   COALESCE(f.ZonaHoraria, n.Timezone, 'America/Santiago') AS ZonaHoraria,
                   COALESCE(n.Moneda, 'CLP') AS Moneda
            FROM dim.NodoConexion n
            JOIN dim.Franquicia f ON f.FranquiciaId = n.FranquiciaId
            WHERE n.Estado = 'ACTIVE' AND n.Activo = 1
              AND JSON_VALUE(n.ConfiguracionJson, '$.micros.codigo_tienda') IS NOT NULL
              AND (@SoloAuto = 0 OR JSON_VALUE(n.ConfiguracionJson, '$.micros.auto') = 'true')",
            new { SoloAuto = soloAuto });
        return filas.GroupBy(n => Normalizar(n.CodigoTienda)).ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<long> RegistrarInicioEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string modo)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(@"
            INSERT INTO log.EjecucionNodo (NodoConexionId, FechaNegocio, InicioEjecucion, Estado, ModoEjecucion, ApiVersionOrigen)
            OUTPUT INSERTED.EjecucionNodoId
            VALUES (@NodoConexionId, @FechaNegocio, GETUTCDATE(), 'RUNNING', @Modo, @Marca)",
            new { NodoConexionId = nodoConexionId, FechaNegocio = fechaNegocio, Modo = modo, Marca = MarcaEjecucion });
    }

    private async Task RegistrarFinEjecucionAsync(long ejecucionId, string estado, int tickets, int lineas, int errores, string? mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE log.EjecucionNodo
            SET FinEjecucion = GETUTCDATE(), Estado = @Estado, TicketsProcesados = @Tickets,
                LineasProcesadas = @Lineas, ErrorsCount = @Errores, Mensajes = @Mensaje
            WHERE EjecucionNodoId = @EjecucionId",
            new { EjecucionId = ejecucionId, Estado = estado, Tickets = tickets, Lineas = lineas, Errores = errores, Mensaje = mensaje });
    }

    private async Task ActualizarEstadoAsync(NodoMicros nodo, string estado)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion SET UltimaSincronizacion = GETUTCDATE(), UltimoEstado = @Estado, ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoId;
            UPDATE dim.Franquicia SET EstadoIntegracion = @EstadoIntegracion, UltimaSincronizacion = GETUTCDATE()
            WHERE FranquiciaId = @FranquiciaId;",
            new { NodoId = nodo.NodoConexionId, Estado = estado, FranquiciaId = nodo.FranquiciaId, EstadoIntegracion = estado == "SUCCESS" ? "OK" : "ERROR" });
    }
}
