using System.Data;
using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKeyInfo?> ValidateApiKeyAsync(string apiKey);
}

public class ApiKeyInfo
{
    public int FranquiciaId { get; set; }
    public string FranquiciaCodigo { get; set; } = string.Empty;
    public string FranquiciaNombre { get; set; } = string.Empty;
    public int? GrupoEconomicoId { get; set; }
    public string? GrupoEconomicoCodigo { get; set; }
    public string Pais { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string ZonaHoraria { get; set; } = string.Empty;
    public int? MonedaId { get; set; }
    public string? MonedaCodigo { get; set; }
    public int ApiKeyFranquiciaId { get; set; }
    public string? ApiKeyNombre { get; set; }
    public DateTime? FechaExpiracion { get; set; }
}

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApiKeyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ApiKeyInfo?> ValidateApiKeyAsync(string apiKey)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<ApiKeyInfo>(
            "api.sp_ObtenerFranquiciaPorApiKey",
            new { ApiKey = apiKey },
            commandType: CommandType.StoredProcedure);
    }
}
