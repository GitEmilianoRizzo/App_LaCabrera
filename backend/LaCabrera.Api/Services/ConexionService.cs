using System.Text.Json;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

public interface IConexionService
{
    Task<IEnumerable<NodoConexionDto>> GetAllNodosAsync();
    Task<NodoConexionDto?> GetNodoByIdAsync(int nodoConexionId);
    Task<NodoConexionDto?> GetNodoByCodigoAsync(string codigo);
    Task<NodoDetalleDto> GetNodoDetalleAsync(int nodoConexionId);
    Task<NodoConfiguracionDto?> GetConfiguracionAsync(int nodoConexionId);
    Task UpdateFieldMappingsAsync(int nodoConexionId, List<FieldMappingDto> fieldMappings);
    Task<int> CreateNodoAsync(NodoConexionCreateDto nodo);
    Task UpdateNodoEstadoAsync(int nodoConexionId, string estado);
    Task<int> UpsertMapeoCategoriaAsync(MapeoCategoriaDto mapeo);
    Task<int> UpsertMapeoMedioPagoAsync(MapeoMedioPagoDto mapeo);
    Task<int> ResolverValoresYaMapeadosAsync(int nodoConexionId);
    Task LogEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string estado, int ticketsProcesados, int lineasProcesadas, int errorsCount, string? batchId, string? mensaje);
}

public class ConexionService : IConexionService
{
    private readonly IConexionRepository _conexionRepository;

    public ConexionService(IConexionRepository conexionRepository)
    {
        _conexionRepository = conexionRepository;
    }

    public async Task<IEnumerable<NodoConexionDto>> GetAllNodosAsync()
    {
        return await _conexionRepository.GetAllNodosAsync();
    }

    public async Task<NodoConexionDto?> GetNodoByIdAsync(int nodoConexionId)
    {
        return await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
    }

    public async Task<NodoConexionDto?> GetNodoByCodigoAsync(string codigo)
    {
        return await _conexionRepository.GetNodoByCodigoAsync(codigo);
    }

    public async Task<NodoDetalleDto> GetNodoDetalleAsync(int nodoConexionId)
    {
        var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
        if (nodo == null)
        {
            throw new KeyNotFoundException($"Nodo con id {nodoConexionId} no encontrado");
        }

        var configuracion = await GetConfiguracionAsync(nodoConexionId);
        var mapeosCategoria = await _conexionRepository.GetMapeosCategoriaAsync(nodoConexionId);
        var mapeosMedioPago = await _conexionRepository.GetMapeosMedioPagoAsync(nodoConexionId);
        var valoresNoMapeados = await _conexionRepository.GetValoresNoMapeadosAsync(nodoConexionId);
        var ejecuciones = await _conexionRepository.GetEjecucionesAsync(nodoConexionId, 10);

        return new NodoDetalleDto
        {
            Nodo = nodo,
            Configuracion = configuracion,
            MapeosCategoria = mapeosCategoria.ToList(),
            MapeosMedioPago = mapeosMedioPago.ToList(),
            ValoresNoMapeados = valoresNoMapeados.ToList(),
            UltimasEjecuciones = ejecuciones.ToList()
        };
    }

    public async Task<NodoConfiguracionDto?> GetConfiguracionAsync(int nodoConexionId)
    {
        var json = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<NodoConfiguracionDto>(json, options);
    }

    public async Task UpdateFieldMappingsAsync(int nodoConexionId, List<FieldMappingDto> fieldMappings)
    {
        var json = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);

        NodoConfiguracionDto config;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        if (string.IsNullOrEmpty(json))
        {
            config = new NodoConfiguracionDto();
        }
        else
        {
            config = JsonSerializer.Deserialize<NodoConfiguracionDto>(json, options) ?? new NodoConfiguracionDto();
        }

        config.FieldMappings = fieldMappings;

        var newJson = JsonSerializer.Serialize(config, options);
        await _conexionRepository.UpdateConfiguracionJsonAsync(nodoConexionId, newJson);
    }

    public async Task<int> CreateNodoAsync(NodoConexionCreateDto nodo)
    {
        return await _conexionRepository.CreateNodoAsync(nodo);
    }

    public async Task UpdateNodoEstadoAsync(int nodoConexionId, string estado)
    {
        var estadosValidos = new[] { "ACTIVE", "PAUSED", "ERROR", "PENDING_CONFIG" };
        if (!estadosValidos.Contains(estado))
        {
            throw new ArgumentException($"Estado '{estado}' no valido. Estados validos: {string.Join(", ", estadosValidos)}");
        }

        await _conexionRepository.UpdateNodoEstadoAsync(nodoConexionId, estado);
    }

    public async Task<int> UpsertMapeoCategoriaAsync(MapeoCategoriaDto mapeo)
    {
        var categoriasValidas = CategoriasProducto.Valores;
        if (!categoriasValidas.Contains(mapeo.CategoriaDestino))
        {
            throw new ArgumentException($"Categoria '{mapeo.CategoriaDestino}' no valida. Categorias validas: {string.Join(", ", categoriasValidas)}");
        }

        var result = await _conexionRepository.UpsertMapeoCategoriaAsync(mapeo);

        // Marcar como resueltos los valores que ahora tienen mapeo
        await _conexionRepository.ResolverValoresYaMapeadosAsync(mapeo.NodoConexionId);

        return result;
    }

    public async Task<int> UpsertMapeoMedioPagoAsync(MapeoMedioPagoDto mapeo)
    {
        var mediosValidos = MediosPago.Valores;
        if (!mediosValidos.Contains(mapeo.MedioPagoDestino))
        {
            throw new ArgumentException($"Medio de pago '{mapeo.MedioPagoDestino}' no valido. Medios validos: {string.Join(", ", mediosValidos)}");
        }

        var result = await _conexionRepository.UpsertMapeoMedioPagoAsync(mapeo);

        // Marcar como resueltos los valores que ahora tienen mapeo
        await _conexionRepository.ResolverValoresYaMapeadosAsync(mapeo.NodoConexionId);

        return result;
    }

    public async Task<int> ResolverValoresYaMapeadosAsync(int nodoConexionId)
    {
        return await _conexionRepository.ResolverValoresYaMapeadosAsync(nodoConexionId);
    }

    public async Task LogEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string estado, int ticketsProcesados, int lineasProcesadas, int errorsCount, string? batchId, string? mensaje)
    {
        await _conexionRepository.LogEjecucionAsync(nodoConexionId, fechaNegocio, estado, ticketsProcesados, lineasProcesadas, errorsCount, batchId, mensaje);
    }
}
