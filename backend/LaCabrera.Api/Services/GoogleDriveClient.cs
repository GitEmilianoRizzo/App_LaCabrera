using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaCabrera.Api.Services;

/// <summary>
/// Cliente mínimo de Google Drive (solo lectura) con una cuenta de servicio.
/// La clave JSON de la cuenta de servicio se lee de GoogleDrive:CredencialesPath (fuera del repo);
/// nunca se loguea ni se devuelve. El scope es drive.readonly: no puede modificar ni borrar archivos.
/// </summary>
public interface IGoogleDriveClient
{
    bool EstaConfigurado { get; }
    Task<List<DriveArchivo>> ListarArchivosAsync(string carpetaId, CancellationToken ct = default);
    Task<string> DescargarTextoAsync(string archivoId, CancellationToken ct = default);
}

public record DriveArchivo(string Id, string Nombre, DateTime? Modificado, long? Tamano);

public class GoogleDriveClient : IGoogleDriveClient
{
    private const string Scope = "https://www.googleapis.com/auth/drive.readonly";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";

    private static readonly SemaphoreSlim _tokenLock = new(1, 1);
    private static (string Token, DateTime Vence)? _token;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDriveClient> _logger;
    private readonly string? _credencialesPath;

    public GoogleDriveClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GoogleDriveClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _credencialesPath = configuration.GetValue<string>("GoogleDrive:CredencialesPath");
    }

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(_credencialesPath) && File.Exists(_credencialesPath);

    public async Task<List<DriveArchivo>> ListarArchivosAsync(string carpetaId, CancellationToken ct = default)
    {
        var client = await CrearClienteAsync(ct);
        var archivos = new List<DriveArchivo>();
        string? pagina = null;

        do
        {
            var query = $"'{carpetaId.Replace("'", "")}' in parents and trashed = false";
            var url = "https://www.googleapis.com/drive/v3/files"
                      + $"?q={Uri.EscapeDataString(query)}"
                      + "&fields=nextPageToken,files(id,name,modifiedTime,size)"
                      + "&pageSize=1000&supportsAllDrives=true&includeItemsFromAllDrives=true"
                      + (pagina != null ? $"&pageToken={Uri.EscapeDataString(pagina)}" : "");

            using var response = await client.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Drive respondió HTTP {(int)response.StatusCode} al listar la carpeta");

            var lista = JsonSerializer.Deserialize<DriveFileList>(json)!;
            archivos.AddRange((lista.Files ?? new()).Select(f => new DriveArchivo(
                f.Id, f.Name, f.ModifiedTime, long.TryParse(f.Size, out var s) ? s : null)));
            pagina = lista.NextPageToken;
        } while (pagina != null);

        return archivos;
    }

    public async Task<string> DescargarTextoAsync(string archivoId, CancellationToken ct = default)
    {
        var client = await CrearClienteAsync(ct);
        using var response = await client.GetAsync(
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(archivoId)}?alt=media&supportsAllDrives=true", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Drive respondió HTTP {(int)response.StatusCode} al descargar el archivo");
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<HttpClient> CrearClienteAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ObtenerTokenAsync(ct));
        return client;
    }

    private async Task<string> ObtenerTokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is { } t && t.Vence > DateTime.UtcNow.AddMinutes(5))
                return t.Token;

            if (!EstaConfigurado)
                throw new InvalidOperationException("Falta la clave de la cuenta de servicio de Google Drive (GoogleDrive:CredencialesPath)");

            var credenciales = JsonSerializer.Deserialize<CuentaServicio>(await File.ReadAllTextAsync(_credencialesPath!, ct))
                ?? throw new InvalidOperationException("La clave de la cuenta de servicio no es válida");

            var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var encabezado = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
            var reclamos = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = credenciales.ClientEmail,
                scope = Scope,
                aud = TokenUrl,
                iat = ahora,
                exp = ahora + 3600
            }));

            using var rsa = RSA.Create();
            rsa.ImportFromPem(credenciales.PrivateKey);
            var firma = Base64Url(rsa.SignData(Encoding.ASCII.GetBytes($"{encabezado}.{reclamos}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = $"{encabezado}.{reclamos}.{firma}"
            }), ct);

            if (!response.IsSuccessStatusCode)
            {
                // El cuerpo de error de Google no trae secretos, pero se limita por las dudas
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Google rechazó la cuenta de servicio: HTTP {Code} {Error}", (int)response.StatusCode, error.Length > 300 ? error[..300] : error);
                throw new HttpRequestException($"No se pudo autenticar con Google Drive (HTTP {(int)response.StatusCode})");
            }

            var token = JsonSerializer.Deserialize<TokenRespuesta>(await response.Content.ReadAsStringAsync(ct))!;
            _token = (token.AccessToken, DateTime.UtcNow.AddSeconds(token.ExpiresIn));
            return token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string Base64Url(byte[] datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private class CuentaServicio
    {
        [JsonPropertyName("client_email")] public string ClientEmail { get; set; } = string.Empty;
        [JsonPropertyName("private_key")] public string PrivateKey { get; set; } = string.Empty;
    }

    private class TokenRespuesta
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class DriveFileList
    {
        [JsonPropertyName("nextPageToken")] public string? NextPageToken { get; set; }
        [JsonPropertyName("files")] public List<DriveFile>? Files { get; set; }
    }

    private class DriveFile
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("modifiedTime")] public DateTime? ModifiedTime { get; set; }
        [JsonPropertyName("size")] public string? Size { get; set; }
    }
}
