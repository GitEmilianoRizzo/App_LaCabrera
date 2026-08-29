using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Repositories;

public interface IFranquiciaRepository
{
    Task<FranquiciaInfo?> GetByCodigoAsync(string codigo);
    Task<FranquiciaInfo?> GetByIdAsync(int franquiciaId);
    Task UpdateUltimaSincronizacionAsync(int franquiciaId, string? estadoIntegracion = null);
}

public class FranquiciaInfo
{
    public int FranquiciaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? GrupoEconomicoId { get; set; }
    public string? GrupoEconomicoCodigo { get; set; }
    public string Pais { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string ZonaHoraria { get; set; } = string.Empty;
    public int? MonedaId { get; set; }
    public string? MonedaCodigo { get; set; }
    public bool Activo { get; set; }
}

public class FranquiciaRepository : IFranquiciaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FranquiciaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<FranquiciaInfo?> GetByCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<FranquiciaInfo>(@"
            SELECT
                f.FranquiciaId,
                f.Codigo,
                f.Nombre,
                f.GrupoEconomicoId,
                ge.Codigo AS GrupoEconomicoCodigo,
                f.Pais,
                f.Ciudad,
                f.ZonaHoraria,
                f.MonedaId,
                m.CodigoISO AS MonedaCodigo,
                f.Activo
            FROM dim.Franquicia f
            LEFT JOIN dim.GrupoEconomico ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
            LEFT JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
            WHERE f.Codigo = @Codigo AND f.Activo = 1",
            new { Codigo = codigo });
    }

    public async Task<FranquiciaInfo?> GetByIdAsync(int franquiciaId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<FranquiciaInfo>(@"
            SELECT
                f.FranquiciaId,
                f.Codigo,
                f.Nombre,
                f.GrupoEconomicoId,
                ge.Codigo AS GrupoEconomicoCodigo,
                f.Pais,
                f.Ciudad,
                f.ZonaHoraria,
                f.MonedaId,
                m.CodigoISO AS MonedaCodigo,
                f.Activo
            FROM dim.Franquicia f
            LEFT JOIN dim.GrupoEconomico ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
            LEFT JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
            WHERE f.FranquiciaId = @FranquiciaId",
            new { FranquiciaId = franquiciaId });
    }

    public async Task UpdateUltimaSincronizacionAsync(int franquiciaId, string? estadoIntegracion = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "dim.sp_ActualizarUltimaSincronizacion",
            new { FranquiciaId = franquiciaId, EstadoIntegracion = estadoIntegracion },
            commandType: System.Data.CommandType.StoredProcedure);
    }
}
