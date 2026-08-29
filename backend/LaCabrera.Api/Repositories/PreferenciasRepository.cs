using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Repositories;

public interface IPreferenciasRepository
{
    Task<PreferenciaColumnas?> GetPreferenciaColumnasAsync(string vistaId);
    Task UpsertPreferenciaColumnasAsync(string vistaId, string columnasVisibles, string? ordenColumnas);
}

public class PreferenciaColumnas
{
    public int PreferenciaId { get; set; }
    public string VistaId { get; set; } = string.Empty;
    public string ColumnasVisibles { get; set; } = string.Empty;
    public string? OrdenColumnas { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaModificacion { get; set; }
}

public class PreferenciasRepository : IPreferenciasRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PreferenciasRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PreferenciaColumnas?> GetPreferenciaColumnasAsync(string vistaId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<PreferenciaColumnas>(@"
            SELECT
                PreferenciaId,
                VistaId,
                ColumnasVisibles,
                OrdenColumnas,
                FechaCreacion,
                FechaModificacion
            FROM cfg.PreferenciaColumnas
            WHERE VistaId = @VistaId",
            new { VistaId = vistaId });
    }

    public async Task UpsertPreferenciaColumnasAsync(string vistaId, string columnasVisibles, string? ordenColumnas)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            MERGE cfg.PreferenciaColumnas AS target
            USING (SELECT @VistaId AS VistaId) AS source
            ON target.VistaId = source.VistaId
            WHEN MATCHED THEN
                UPDATE SET
                    ColumnasVisibles = @ColumnasVisibles,
                    OrdenColumnas = @OrdenColumnas,
                    FechaModificacion = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (VistaId, ColumnasVisibles, OrdenColumnas)
                VALUES (@VistaId, @ColumnasVisibles, @OrdenColumnas);",
            new { VistaId = vistaId, ColumnasVisibles = columnasVisibles, OrdenColumnas = ordenColumnas });
    }
}
