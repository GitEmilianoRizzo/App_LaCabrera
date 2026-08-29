using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

public interface IApiKeyService
{
    Task<ApiKeyInfo?> ValidateApiKeyAsync(string? apiKey);
    Task<bool> ValidateApiKeyForFranchiseAsync(string? apiKey, string franchiseCode);
}

public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(IApiKeyRepository apiKeyRepository, ILogger<ApiKeyService> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _logger = logger;
    }

    public async Task<ApiKeyInfo?> ValidateApiKeyAsync(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API Key no proporcionada");
            return null;
        }

        var apiKeyInfo = await _apiKeyRepository.ValidateApiKeyAsync(apiKey);

        if (apiKeyInfo == null)
        {
            _logger.LogWarning("API Key invalida o expirada: {ApiKey}", apiKey[..Math.Min(8, apiKey.Length)] + "...");
            return null;
        }

        return apiKeyInfo;
    }

    public async Task<bool> ValidateApiKeyForFranchiseAsync(string? apiKey, string franchiseCode)
    {
        var apiKeyInfo = await ValidateApiKeyAsync(apiKey);

        if (apiKeyInfo == null)
            return false;

        if (!apiKeyInfo.FranquiciaCodigo.Equals(franchiseCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("API Key no coincide con franchise_code. API Key es de {ApiKeyFranchise}, pero se envio {RequestFranchise}",
                apiKeyInfo.FranquiciaCodigo, franchiseCode);
            return false;
        }

        return true;
    }
}
