using System.Data;
using System.Net.Http.Headers;
using System.Text.Json;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Configuration;
using Microsoft.Data.SqlClient;

namespace LaCabrera.Api.Services;

public interface ITxtParserService
{
    Task<List<ParserDto>> GetParsersAsync();
    Task<ParserDto?> GetParserByIdAsync(int parserId);
    Task<ParserDto?> GetParserByCodigoAsync(string codigo);
    Task<ParseBatchResultDto> ParseFilesAsync(string parserCode, List<IFormFile> files);
}

public class TxtParserService : ITxtParserService
{
    private readonly HttpClient _httpClient;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TxtParserService> _logger;
    private readonly string _parserServiceUrl;

    public TxtParserService(
        HttpClient httpClient,
        IDbConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<TxtParserService> logger)
    {
        _httpClient = httpClient;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _parserServiceUrl = configuration.GetValue<string>("ParserService:BaseUrl") ?? "http://localhost:8000";
    }

    public async Task<List<ParserDto>> GetParsersAsync()
    {
        var parsers = new List<ParserDto>();

        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ParserId, Codigo, Nombre, Descripcion, ArchivoScript,
                   ExtensionesPermitidas, PaisesAplica, MonedaDefault, TimezoneDefault, Activo
            FROM cfg.Parser
            WHERE Activo = 1
            ORDER BY Nombre";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            parsers.Add(MapToParserDto(reader));
        }

        return parsers;
    }

    public async Task<ParserDto?> GetParserByIdAsync(int parserId)
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ParserId, Codigo, Nombre, Descripcion, ArchivoScript,
                   ExtensionesPermitidas, PaisesAplica, MonedaDefault, TimezoneDefault, Activo
            FROM cfg.Parser
            WHERE ParserId = @ParserId";

        cmd.Parameters.AddWithValue("@ParserId", parserId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToParserDto(reader);
        }

        return null;
    }

    public async Task<ParserDto?> GetParserByCodigoAsync(string codigo)
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ParserId, Codigo, Nombre, Descripcion, ArchivoScript,
                   ExtensionesPermitidas, PaisesAplica, MonedaDefault, TimezoneDefault, Activo
            FROM cfg.Parser
            WHERE Codigo = @Codigo AND Activo = 1";

        cmd.Parameters.AddWithValue("@Codigo", codigo);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToParserDto(reader);
        }

        return null;
    }

    private static ParserDto MapToParserDto(SqlDataReader reader)
    {
        return new ParserDto
        {
            ParserId = reader.GetInt32(0),
            Codigo = reader.GetString(1),
            Nombre = reader.GetString(2),
            Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
            ArchivoScript = reader.GetString(4),
            ExtensionesPermitidas = reader.GetString(5),
            PaisesAplica = reader.IsDBNull(6) ? null : reader.GetString(6),
            MonedaDefault = reader.GetString(7),
            TimezoneDefault = reader.GetString(8),
            Activo = reader.GetBoolean(9)
        };
    }

    public async Task<ParseBatchResultDto> ParseFilesAsync(string parserCode, List<IFormFile> files)
    {
        var results = new List<ParseFileResultDto>();
        int successful = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                var result = await ParseSingleFileAsync(parserCode, file);
                results.Add(result);

                if (result.Success)
                    successful++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing file {Filename}", file.FileName);
                results.Add(new ParseFileResultDto
                {
                    Success = false,
                    Filename = file.FileName,
                    Error = $"Error interno: {ex.Message}"
                });
                failed++;
            }
        }

        return new ParseBatchResultDto
        {
            TotalFiles = files.Count,
            Successful = successful,
            Failed = failed,
            Results = results
        };
    }

    private async Task<ParseFileResultDto> ParseSingleFileAsync(string parserCode, IFormFile file)
    {
        using var content = new MultipartFormDataContent();

        // Add file
        using var fileStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", file.FileName);

        // Add parser_code
        content.Add(new StringContent(parserCode), "parser_code");

        // Call parser service
        _logger.LogInformation("Sending file {Filename} to parser service at {Url}", file.FileName, _parserServiceUrl);

        var response = await _httpClient.PostAsync($"{_parserServiceUrl}/parse", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Parser service returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            return new ParseFileResultDto
            {
                Success = false,
                Filename = file.FileName,
                Error = $"Parser service error: {response.StatusCode}"
            };
        }

        // Parse response
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var parseResult = JsonSerializer.Deserialize<ParseFileResultDto>(responseBody, options);

        return parseResult ?? new ParseFileResultDto
        {
            Success = false,
            Filename = file.FileName,
            Error = "Failed to parse response from parser service"
        };
    }
}
