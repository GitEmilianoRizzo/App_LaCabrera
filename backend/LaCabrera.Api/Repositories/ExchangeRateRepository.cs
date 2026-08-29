using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models;

namespace LaCabrera.Api.Repositories;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetRateAsync(string codigoMoneda, DateTime fecha, string tipoTasa = "OFICIAL");
    Task<IEnumerable<ExchangeRate>> GetRatesForDateRangeAsync(DateTime fechaDesde, DateTime fechaHasta, string? codigoMoneda = null);
    Task<DateTime?> GetLastRateDateAsync(string codigoMoneda);
    Task UpsertRateAsync(ExchangeRate rate);
    Task UpsertRatesAsync(IEnumerable<ExchangeRate> rates);
    Task<int> LogIngestAsync(ExchangeRateIngestLog log);
    Task<IEnumerable<ExchangeRateIngestLog>> GetRecentLogsAsync(int count = 10);
    Task<IEnumerable<string>> GetActiveCurrenciesAsync();

    // Currency CRUD
    Task<IEnumerable<Currency>> GetCurrenciesAsync();
    Task<Currency?> GetCurrencyAsync(string codigoISO);
    Task<int> AddCurrencyAsync(Currency currency);
    Task UpdateCurrencyAsync(Currency currency);
    Task DeleteCurrencyAsync(string codigoISO);
    Task DeleteRatesForCurrencyAsync(string codigoISO);

    // Providers
    Task<IEnumerable<ExchangeRateProvider>> GetProvidersAsync();
    Task<ExchangeRateProvider?> GetProviderAsync(int proveedorId);
    Task<int?> GetPreferredProviderIdAsync(string codigoMoneda);
}

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ExchangeRateRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ExchangeRate?> GetRateAsync(string codigoMoneda, DateTime fecha, string tipoTasa = "OFICIAL")
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT TipoCambioId, CodigoMoneda, Fecha, FechaCotizacionOrigen,
                   UnidadesPorUsd, TipoTasa, Proveedor, EsArrastrada,
                   EsOverrideManual, ObservacionOverride, FechaCreacion, FechaModificacion
            FROM dim.TipoCambio
            WHERE CodigoMoneda = @CodigoMoneda
              AND Fecha = @Fecha
              AND TipoTasa = @TipoTasa";

        return await connection.QueryFirstOrDefaultAsync<ExchangeRate>(sql, new
        {
            CodigoMoneda = codigoMoneda,
            Fecha = fecha.Date,
            TipoTasa = tipoTasa
        });
    }

    public async Task<IEnumerable<ExchangeRate>> GetRatesForDateRangeAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        string? codigoMoneda = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT TipoCambioId, CodigoMoneda, Fecha, FechaCotizacionOrigen,
                   UnidadesPorUsd, TipoTasa, Proveedor, EsArrastrada,
                   EsOverrideManual, ObservacionOverride, FechaCreacion, FechaModificacion
            FROM dim.TipoCambio
            WHERE Fecha >= @FechaDesde AND Fecha <= @FechaHasta";

        if (!string.IsNullOrEmpty(codigoMoneda))
        {
            sql += " AND CodigoMoneda = @CodigoMoneda";
        }

        sql += " ORDER BY Fecha, CodigoMoneda";

        return await connection.QueryAsync<ExchangeRate>(sql, new
        {
            FechaDesde = fechaDesde.Date,
            FechaHasta = fechaHasta.Date,
            CodigoMoneda = codigoMoneda
        });
    }

    public async Task<DateTime?> GetLastRateDateAsync(string codigoMoneda)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT MAX(Fecha)
            FROM dim.TipoCambio
            WHERE CodigoMoneda = @CodigoMoneda
              AND EsArrastrada = 0";

        return await connection.QueryFirstOrDefaultAsync<DateTime?>(sql, new { CodigoMoneda = codigoMoneda });
    }

    public async Task UpsertRateAsync(ExchangeRate rate)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            MERGE dim.TipoCambio AS target
            USING (SELECT @CodigoMoneda AS CodigoMoneda, @Fecha AS Fecha, @TipoTasa AS TipoTasa) AS source
            ON target.CodigoMoneda = source.CodigoMoneda
               AND target.Fecha = source.Fecha
               AND target.TipoTasa = source.TipoTasa
            WHEN MATCHED THEN
                UPDATE SET
                    FechaCotizacionOrigen = @FechaCotizacionOrigen,
                    UnidadesPorUsd = @UnidadesPorUsd,
                    Proveedor = @Proveedor,
                    EsArrastrada = @EsArrastrada,
                    EsOverrideManual = @EsOverrideManual,
                    ObservacionOverride = @ObservacionOverride,
                    FechaModificacion = GETDATE(),
                    UsuarioModificacion = SYSTEM_USER
            WHEN NOT MATCHED THEN
                INSERT (CodigoMoneda, Fecha, FechaCotizacionOrigen, UnidadesPorUsd,
                        TipoTasa, Proveedor, EsArrastrada, EsOverrideManual, ObservacionOverride)
                VALUES (@CodigoMoneda, @Fecha, @FechaCotizacionOrigen, @UnidadesPorUsd,
                        @TipoTasa, @Proveedor, @EsArrastrada, @EsOverrideManual, @ObservacionOverride);";

        await connection.ExecuteAsync(sql, new
        {
            rate.CodigoMoneda,
            Fecha = rate.Fecha.Date,
            FechaCotizacionOrigen = rate.FechaCotizacionOrigen.Date,
            rate.UnidadesPorUsd,
            rate.TipoTasa,
            rate.Proveedor,
            rate.EsArrastrada,
            rate.EsOverrideManual,
            rate.ObservacionOverride
        });
    }

    public async Task UpsertRatesAsync(IEnumerable<ExchangeRate> rates)
    {
        foreach (var rate in rates)
        {
            await UpsertRateAsync(rate);
        }
    }

    public async Task<int> LogIngestAsync(ExchangeRateIngestLog log)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO stg.TipoCambioIngestaLog
                (FechaEjecucion, Proveedor, FechaDesde, FechaHasta, MonedasConsultadas,
                 Estado, MonedasObtenidas, DiasObtenidos, DiasArrastrados,
                 MensajeError, MonedasFaltantes, DuracionMs, RequestsRealizados)
            VALUES
                (@FechaEjecucion, @Proveedor, @FechaDesde, @FechaHasta, @MonedasConsultadas,
                 @Estado, @MonedasObtenidas, @DiasObtenidos, @DiasArrastrados,
                 @MensajeError, @MonedasFaltantes, @DuracionMs, @RequestsRealizados);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.QuerySingleAsync<int>(sql, log);
    }

    public async Task<IEnumerable<ExchangeRateIngestLog>> GetRecentLogsAsync(int count = 10)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT TOP (@Count)
                IngestaLogId, FechaEjecucion, Proveedor, FechaDesde, FechaHasta,
                MonedasConsultadas, Estado, MonedasObtenidas, DiasObtenidos,
                DiasArrastrados, MensajeError, MonedasFaltantes, DuracionMs, RequestsRealizados
            FROM stg.TipoCambioIngestaLog
            ORDER BY FechaEjecucion DESC";

        return await connection.QueryAsync<ExchangeRateIngestLog>(sql, new { Count = count });
    }

    public async Task<IEnumerable<string>> GetActiveCurrenciesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        // Obtener monedas de franquicias activas
        const string sql = @"
            SELECT DISTINCT m.CodigoISO
            FROM dim.Franquicia f
            INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
            WHERE f.Activo = 1
            UNION
            SELECT 'USD'  -- Siempre incluir USD como base";

        return await connection.QueryAsync<string>(sql);
    }

    #region Currency CRUD

    public async Task<IEnumerable<Currency>> GetCurrenciesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT m.MonedaId, m.CodigoISO, m.Nombre, m.Simbolo, m.EsMonedaBase,
                   m.Activo, m.ProveedorPreferidoId, p.Codigo AS ProveedorPreferidoCodigo
            FROM dim.Moneda m
            LEFT JOIN cfg.TipoCambioProveedor p ON m.ProveedorPreferidoId = p.ProveedorId
            ORDER BY m.EsMonedaBase DESC, m.CodigoISO";

        return await connection.QueryAsync<Currency>(sql);
    }

    public async Task<Currency?> GetCurrencyAsync(string codigoISO)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT m.MonedaId, m.CodigoISO, m.Nombre, m.Simbolo, m.EsMonedaBase,
                   m.Activo, m.ProveedorPreferidoId, p.Codigo AS ProveedorPreferidoCodigo
            FROM dim.Moneda m
            LEFT JOIN cfg.TipoCambioProveedor p ON m.ProveedorPreferidoId = p.ProveedorId
            WHERE m.CodigoISO = @CodigoISO";

        return await connection.QueryFirstOrDefaultAsync<Currency>(sql, new { CodigoISO = codigoISO.ToUpperInvariant() });
    }

    public async Task<int> AddCurrencyAsync(Currency currency)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO dim.Moneda (CodigoISO, Nombre, Simbolo, EsMonedaBase, Activo, ProveedorPreferidoId,
                                    FechaCreacion, FechaModificacion, UsuarioCreacion, UsuarioModificacion)
            VALUES (@CodigoISO, @Nombre, @Simbolo, 0, @Activo, @ProveedorPreferidoId,
                    GETDATE(), GETDATE(), SYSTEM_USER, SYSTEM_USER);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.QuerySingleAsync<int>(sql, new
        {
            CodigoISO = currency.CodigoISO.ToUpperInvariant(),
            currency.Nombre,
            currency.Simbolo,
            Activo = currency.Activo ? 1 : 0,
            currency.ProveedorPreferidoId
        });
    }

    public async Task UpdateCurrencyAsync(Currency currency)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dim.Moneda
            SET Nombre = @Nombre,
                Simbolo = @Simbolo,
                Activo = @Activo,
                ProveedorPreferidoId = @ProveedorPreferidoId,
                FechaModificacion = GETDATE(),
                UsuarioModificacion = SYSTEM_USER
            WHERE CodigoISO = @CodigoISO";

        await connection.ExecuteAsync(sql, new
        {
            CodigoISO = currency.CodigoISO.ToUpperInvariant(),
            currency.Nombre,
            currency.Simbolo,
            Activo = currency.Activo ? 1 : 0,
            currency.ProveedorPreferidoId
        });
    }

    public async Task DeleteCurrencyAsync(string codigoISO)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = "DELETE FROM dim.Moneda WHERE CodigoISO = @CodigoISO AND EsMonedaBase = 0";
        await connection.ExecuteAsync(sql, new { CodigoISO = codigoISO.ToUpperInvariant() });
    }

    public async Task DeleteRatesForCurrencyAsync(string codigoISO)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = "DELETE FROM dim.TipoCambio WHERE CodigoMoneda = @CodigoISO";
        await connection.ExecuteAsync(sql, new { CodigoISO = codigoISO.ToUpperInvariant() });
    }

    #endregion

    #region Providers

    public async Task<IEnumerable<ExchangeRateProvider>> GetProvidersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProveedorId, Codigo, Nombre, BaseUrl, Activo, Prioridad,
                   MonedasSoportadas, RequiereApiKey
            FROM cfg.TipoCambioProveedor
            WHERE Activo = 1
            ORDER BY Prioridad";

        return await connection.QueryAsync<ExchangeRateProvider>(sql);
    }

    public async Task<ExchangeRateProvider?> GetProviderAsync(int proveedorId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProveedorId, Codigo, Nombre, BaseUrl, Activo, Prioridad,
                   MonedasSoportadas, RequiereApiKey
            FROM cfg.TipoCambioProveedor
            WHERE ProveedorId = @ProveedorId";

        return await connection.QueryFirstOrDefaultAsync<ExchangeRateProvider>(sql, new { ProveedorId = proveedorId });
    }

    public async Task<int?> GetPreferredProviderIdAsync(string codigoMoneda)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProveedorPreferidoId
            FROM dim.Moneda
            WHERE CodigoISO = @CodigoMoneda";

        return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { CodigoMoneda = codigoMoneda.ToUpperInvariant() });
    }

    #endregion
}
