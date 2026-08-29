using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Repositories;

public interface IConexionRepository
{
    Task<IEnumerable<NodoConexionDto>> GetAllNodosAsync();
    Task<NodoConexionDto?> GetNodoByIdAsync(int nodoConexionId);
    Task<NodoConexionDto?> GetNodoByCodigoAsync(string codigo);
    Task<string?> GetConfiguracionJsonAsync(int nodoConexionId);
    Task UpdateConfiguracionJsonAsync(int nodoConexionId, string configuracionJson);
    Task<IEnumerable<MapeoCategoriaDto>> GetMapeosCategoriaAsync(int nodoConexionId);
    Task<IEnumerable<MapeoMedioPagoDto>> GetMapeosMedioPagoAsync(int nodoConexionId);
    Task<IEnumerable<ValorNoMapeadoDto>> GetValoresNoMapeadosAsync(int nodoConexionId);
    Task<IEnumerable<EjecucionNodoDto>> GetEjecucionesAsync(int nodoConexionId, int limite = 10);
    Task<int> CreateNodoAsync(NodoConexionCreateDto nodo);
    Task UpdateNodoEstadoAsync(int nodoConexionId, string estado);
    Task<int> UpsertMapeoCategoriaAsync(MapeoCategoriaDto mapeo);
    Task<int> UpsertMapeoMedioPagoAsync(MapeoMedioPagoDto mapeo);
    Task<int> ResolverValoresYaMapeadosAsync(int nodoConexionId);
}

public class ConexionRepository : IConexionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConexionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<NodoConexionDto>> GetAllNodosAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<NodoConexionDto>(@"
            SELECT
                NodoConexionId,
                NodoCodigo AS Codigo,
                NodoNombre AS Nombre,
                TipoConector,
                Modo,
                Estado,
                FranquiciaId,
                FranquiciaCodigo,
                FranquiciaNombre,
                Pais,
                Ciudad,
                Timezone,
                Moneda,
                CronExpression,
                ConvencionImportes,
                UltimaSincronizacion,
                UltimoEstado,
                UltimoBatchId,
                MapeosCategoria,
                MapeosMedioPago,
                ValoresPendientes,
                TicketsUltimaEjecucion,
                Activo
            FROM dim.vw_EstadoNodosConexion
            ORDER BY FranquiciaNombre");
    }

    public async Task<NodoConexionDto?> GetNodoByIdAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<NodoConexionDto>(@"
            SELECT
                NodoConexionId,
                NodoCodigo AS Codigo,
                NodoNombre AS Nombre,
                TipoConector,
                Modo,
                Estado,
                FranquiciaId,
                FranquiciaCodigo,
                FranquiciaNombre,
                Pais,
                Ciudad,
                Timezone,
                Moneda,
                CronExpression,
                ConvencionImportes,
                UltimaSincronizacion,
                UltimoEstado,
                UltimoBatchId,
                MapeosCategoria,
                MapeosMedioPago,
                ValoresPendientes,
                TicketsUltimaEjecucion,
                Activo
            FROM dim.vw_EstadoNodosConexion
            WHERE NodoConexionId = @NodoConexionId",
            new { NodoConexionId = nodoConexionId });
    }

    public async Task<NodoConexionDto?> GetNodoByCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<NodoConexionDto>(@"
            SELECT
                NodoConexionId,
                NodoCodigo AS Codigo,
                NodoNombre AS Nombre,
                TipoConector,
                Modo,
                Estado,
                FranquiciaId,
                FranquiciaCodigo,
                FranquiciaNombre,
                Pais,
                Ciudad,
                Timezone,
                Moneda,
                CronExpression,
                ConvencionImportes,
                UltimaSincronizacion,
                UltimoEstado,
                UltimoBatchId,
                MapeosCategoria,
                MapeosMedioPago,
                ValoresPendientes,
                TicketsUltimaEjecucion,
                Activo
            FROM dim.vw_EstadoNodosConexion
            WHERE NodoCodigo = @Codigo",
            new { Codigo = codigo });
    }

    public async Task<string?> GetConfiguracionJsonAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<string>(@"
            SELECT ConfiguracionJson
            FROM dim.NodoConexion
            WHERE NodoConexionId = @NodoConexionId",
            new { NodoConexionId = nodoConexionId });
    }

    public async Task UpdateConfiguracionJsonAsync(int nodoConexionId, string configuracionJson)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion
            SET ConfiguracionJson = @ConfiguracionJson, ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoConexionId",
            new { NodoConexionId = nodoConexionId, ConfiguracionJson = configuracionJson });
    }

    public async Task<IEnumerable<MapeoCategoriaDto>> GetMapeosCategoriaAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<MapeoCategoriaDto>(@"
            SELECT
                MapeoCategoriaId,
                NodoConexionId,
                CodigoOrigen,
                NombreOrigen,
                CategoriaDestino,
                FamiliaDestino,
                SubfamiliaDestino,
                Verificado
            FROM dim.MapeoCategoria
            WHERE NodoConexionId = @NodoConexionId AND Activo = 1
            ORDER BY NombreOrigen",
            new { NodoConexionId = nodoConexionId });
    }

    public async Task<IEnumerable<MapeoMedioPagoDto>> GetMapeosMedioPagoAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<MapeoMedioPagoDto>(@"
            SELECT
                MapeoMedioPagoId,
                NodoConexionId,
                CodigoOrigen,
                NombreOrigen,
                MedioPagoDestino,
                MarcaTarjeta,
                Verificado
            FROM dim.MapeoMedioPago
            WHERE NodoConexionId = @NodoConexionId AND Activo = 1
            ORDER BY NombreOrigen",
            new { NodoConexionId = nodoConexionId });
    }

    public async Task<IEnumerable<ValorNoMapeadoDto>> GetValoresNoMapeadosAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<ValorNoMapeadoDto>(@"
            SELECT
                ValorNoMapeadoId,
                NodoConexionId,
                TipoMapeo,
                CodigoOrigen,
                NombreOrigen,
                Ocurrencias,
                PrimeraVez,
                UltimaVez
            FROM dim.ValorNoMapeado
            WHERE NodoConexionId = @NodoConexionId AND Resuelto = 0
            ORDER BY Ocurrencias DESC, UltimaVez DESC",
            new { NodoConexionId = nodoConexionId });
    }

    public async Task<IEnumerable<EjecucionNodoDto>> GetEjecucionesAsync(int nodoConexionId, int limite = 10)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<EjecucionNodoDto>(@"
            SELECT TOP (@Limite)
                EjecucionNodoId,
                NodoConexionId,
                EjecucionId,
                InicioEjecucion,
                FinEjecucion,
                FechaNegocio,
                Estado,
                ModoEjecucion,
                TicketsProcesados,
                LineasProcesadas,
                WarningsCount,
                ErrorsCount,
                BatchId,
                BatchEstado,
                ApiVersionOrigen
            FROM log.EjecucionNodo
            WHERE NodoConexionId = @NodoConexionId
            ORDER BY InicioEjecucion DESC",
            new { NodoConexionId = nodoConexionId, Limite = limite });
    }

    public async Task<int> CreateNodoAsync(NodoConexionCreateDto nodo)
    {
        using var connection = _connectionFactory.CreateConnection();

        var configJson = nodo.Configuracion != null
            ? System.Text.Json.JsonSerializer.Serialize(nodo.Configuracion)
            : null;

        return await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO dim.NodoConexion (
                Codigo, Nombre, FranquiciaId, TipoConector, Modo,
                Estado, ConfiguracionJson, CronExpression,
                Timezone, Moneda, ConvencionImportes, PoliticaDevoluciones,
                ToleranciaReconciliacion
            ) VALUES (
                @Codigo, @Nombre, @FranquiciaId, @TipoConector, @Modo,
                'PENDING_CONFIG', @ConfiguracionJson, @CronExpression,
                @Timezone, @Moneda, @ConvencionImportes, @PoliticaDevoluciones,
                @ToleranciaReconciliacion
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                nodo.Codigo,
                nodo.Nombre,
                nodo.FranquiciaId,
                nodo.TipoConector,
                nodo.Modo,
                ConfiguracionJson = configJson,
                nodo.CronExpression,
                nodo.Timezone,
                nodo.Moneda,
                nodo.ConvencionImportes,
                nodo.PoliticaDevoluciones,
                nodo.ToleranciaReconciliacion
            });
    }

    public async Task UpdateNodoEstadoAsync(int nodoConexionId, string estado)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion
            SET Estado = @Estado, ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoConexionId",
            new { NodoConexionId = nodoConexionId, Estado = estado });
    }

    public async Task<int> UpsertMapeoCategoriaAsync(MapeoCategoriaDto mapeo)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(@"
            MERGE dim.MapeoCategoria AS target
            USING (SELECT @NodoConexionId AS NodoConexionId, @CodigoOrigen AS CodigoOrigen) AS source
            ON target.NodoConexionId = source.NodoConexionId AND target.CodigoOrigen = source.CodigoOrigen
            WHEN MATCHED THEN
                UPDATE SET
                    NombreOrigen = @NombreOrigen,
                    CategoriaDestino = @CategoriaDestino,
                    FamiliaDestino = @FamiliaDestino,
                    SubfamiliaDestino = @SubfamiliaDestino,
                    Verificado = @Verificado,
                    ModificadoEn = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (NodoConexionId, CodigoOrigen, NombreOrigen, CategoriaDestino, FamiliaDestino, SubfamiliaDestino, Verificado)
                VALUES (@NodoConexionId, @CodigoOrigen, @NombreOrigen, @CategoriaDestino, @FamiliaDestino, @SubfamiliaDestino, @Verificado)
            OUTPUT INSERTED.MapeoCategoriaId;",
            new
            {
                mapeo.NodoConexionId,
                mapeo.CodigoOrigen,
                mapeo.NombreOrigen,
                mapeo.CategoriaDestino,
                mapeo.FamiliaDestino,
                mapeo.SubfamiliaDestino,
                mapeo.Verificado
            });
    }

    public async Task<int> UpsertMapeoMedioPagoAsync(MapeoMedioPagoDto mapeo)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(@"
            MERGE dim.MapeoMedioPago AS target
            USING (SELECT @NodoConexionId AS NodoConexionId, @CodigoOrigen AS CodigoOrigen) AS source
            ON target.NodoConexionId = source.NodoConexionId AND target.CodigoOrigen = source.CodigoOrigen
            WHEN MATCHED THEN
                UPDATE SET
                    NombreOrigen = @NombreOrigen,
                    MedioPagoDestino = @MedioPagoDestino,
                    MarcaTarjeta = @MarcaTarjeta,
                    Verificado = @Verificado,
                    ModificadoEn = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (NodoConexionId, CodigoOrigen, NombreOrigen, MedioPagoDestino, MarcaTarjeta, Verificado)
                VALUES (@NodoConexionId, @CodigoOrigen, @NombreOrigen, @MedioPagoDestino, @MarcaTarjeta, @Verificado)
            OUTPUT INSERTED.MapeoMedioPagoId;",
            new
            {
                mapeo.NodoConexionId,
                mapeo.CodigoOrigen,
                mapeo.NombreOrigen,
                mapeo.MedioPagoDestino,
                mapeo.MarcaTarjeta,
                mapeo.Verificado
            });
    }

    public async Task<int> ResolverValoresYaMapeadosAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Marca como resueltos los valores que ya tienen mapeo de categoría o medio de pago
        return await connection.ExecuteAsync(@"
            UPDATE vnm
            SET Resuelto = 1
            FROM dim.ValorNoMapeado vnm
            WHERE vnm.NodoConexionId = @NodoConexionId
              AND vnm.Resuelto = 0
              AND (
                  (vnm.TipoMapeo = 'CATEGORIA' AND EXISTS (
                      SELECT 1 FROM dim.MapeoCategoria mc
                      WHERE mc.NodoConexionId = vnm.NodoConexionId
                        AND mc.CodigoOrigen = vnm.CodigoOrigen
                  ))
                  OR
                  (vnm.TipoMapeo = 'MEDIO_PAGO' AND EXISTS (
                      SELECT 1 FROM dim.MapeoMedioPago mmp
                      WHERE mmp.NodoConexionId = vnm.NodoConexionId
                        AND mmp.CodigoOrigen = vnm.CodigoOrigen
                  ))
              )",
            new { NodoConexionId = nodoConexionId });
    }
}
