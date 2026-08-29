using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Repositories;

public interface IDashboardRepository
{
    Task<IEnumerable<HomeDashboardDto>> GetHomeDashboardAsync(DashboardFilters filters);
    Task<IEnumerable<VentasResumenDiarioDto>> GetVentasResumenDiarioAsync(DashboardFilters filters);
    Task<IEnumerable<VentasPorFranquiciaDto>> GetVentasPorFranquiciaAsync(DashboardFilters? filters = null);
    Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DashboardFilters filters);
    Task<IEnumerable<VentasPorMozoDto>> GetVentasPorMozoAsync(DashboardFilters filters);
    Task<IEnumerable<VentasPorTipoPlatoDto>> GetVentasPorTipoPlatoAsync(DashboardFilters filters);
    Task<IEnumerable<OcupacionMesasDto>> GetOcupacionMesasAsync(DashboardFilters filters);
    Task<IEnumerable<EstadoIntegracionDto>> GetEstadoIntegracionAsync();

    // v1.2: New consolidated views with USD and tax separation
    Task<IEnumerable<VentasConsolidadasDto>> GetVentasConsolidadasAsync(DashboardFilters filters);
    Task<IEnumerable<VentasPorProductoConPesoDto>> GetVentasPorProductoConPesoAsync(DashboardFilters filters);
    Task<IEnumerable<VentasPorMealPeriodDto>> GetVentasPorMealPeriodAsync(DashboardFilters filters);
    Task<IEnumerable<DiaRankingDto>> GetDiaRankingAsync(DashboardFilters filters, bool mejores = true, int top = 10);
    Task<IEnumerable<VentasDelDiaDto>> GetVentasDelDiaAsync(DashboardFilters filters);
    Task<IEnumerable<ComparativoMensualDto>> GetComparativoMensualAsync(DashboardFilters filters);

    // Tickets / Transacciones
    Task<IEnumerable<TransaccionDto>> GetTransaccionesByFranquiciaAsync(int franquiciaId, DateTime fechaDesde, DateTime fechaHasta);
    Task<IEnumerable<TransaccionDetalleDto>> GetTransaccionDetalleAsync(long ticketId);
}

public class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DashboardRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<HomeDashboardDto>> GetHomeDashboardAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Por defecto: último mes hasta hoy
        var fechaHasta = filters.FechaHasta ?? DateTime.Today;
        var fechaDesde = filters.FechaDesde ?? fechaHasta.AddMonths(-1);

        var parameters = new DynamicParameters();
        parameters.Add("FechaDesde", fechaDesde);
        parameters.Add("FechaHasta", fechaHasta);

        // Query con conversión a USD y cálculo de períodos adicionales
        // Usamos @FechaHasta como referencia para calcular los períodos (no GETDATE)
        var sql = @"
            -- Variables de período basadas en el rango de fechas del filtro (no en GETDATE)
            DECLARE @FechaRef DATE = @FechaHasta;  -- Usar fecha del filtro como referencia
            DECLARE @MesActual INT = MONTH(@FechaRef);
            DECLARE @AnioActual INT = YEAR(@FechaRef);
            DECLARE @DiaActual INT = DAY(@FechaRef);
            DECLARE @MesAnterior INT = CASE WHEN @MesActual = 1 THEN 12 ELSE @MesActual - 1 END;
            DECLARE @AnioMesAnterior INT = CASE WHEN @MesActual = 1 THEN @AnioActual - 1 ELSE @AnioActual END;
            DECLARE @AnioPrevio INT = @AnioActual - 1;

            -- Inicio de cada período
            DECLARE @InicioMesActual DATE = DATEFROMPARTS(@AnioActual, @MesActual, 1);
            DECLARE @InicioMesAnterior DATE = DATEFROMPARTS(@AnioMesAnterior, @MesAnterior, 1);
            DECLARE @FinMesAnterior DATE = DATEADD(DAY, -1, @InicioMesActual);
            DECLARE @InicioAnioActual DATE = DATEFROMPARTS(@AnioActual, 1, 1);
            DECLARE @InicioAnioPrevio DATE = DATEFROMPARTS(@AnioPrevio, 1, 1);
            -- Para YTD previo: usar el menor entre el día actual y el último día del mes en el año anterior
            DECLARE @UltimoDiaMesPrevio INT = DAY(EOMONTH(DATEFROMPARTS(@AnioPrevio, @MesActual, 1)));
            DECLARE @DiaYTDPrevio INT = CASE WHEN @DiaActual > @UltimoDiaMesPrevio THEN @UltimoDiaMesPrevio ELSE @DiaActual END;
            DECLARE @FinAnioPrevioYTD DATE = DATEFROMPARTS(@AnioPrevio, @MesActual, @DiaYTDPrevio);

            WITH VentasPorDia AS (
                -- Ventas agrupadas por franquicia y fecha (para el período seleccionado)
                SELECT
                    vt.FranquiciaId,
                    vt.FechaNegocio,
                    SUM(vt.ImporteNeto) AS VentaNetaLocal,
                    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
                    COUNT(*) AS TotalTickets
                FROM fact.VentaTicket vt
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @FechaDesde
                  AND vt.FechaNegocio <= @FechaHasta
                GROUP BY vt.FranquiciaId, vt.FechaNegocio
            ),
            VentasConTC AS (
                -- Agregar tipo de cambio (el de la fecha o el más cercano anterior)
                SELECT
                    vpd.FranquiciaId,
                    vpd.FechaNegocio,
                    vpd.VentaNetaLocal,
                    vpd.TotalCubiertos,
                    vpd.TotalTickets,
                    m.CodigoISO AS MonedaCodigo,
                    tc.UnidadesPorUsd,
                    tc.Fecha AS FechaTasa,
                    CASE
                        WHEN m.CodigoISO = 'USD' THEN 1
                        WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.Fecha = vpd.FechaNegocio THEN 1
                        WHEN tc.UnidadesPorUsd IS NOT NULL THEN 0
                        ELSE 0
                    END AS TasaDirecta,
                    CASE
                        WHEN m.CodigoISO = 'USD' THEN 0
                        WHEN tc.UnidadesPorUsd IS NULL THEN 1
                        WHEN tc.Fecha < vpd.FechaNegocio THEN 1
                        ELSE 0
                    END AS TasaArrastrada
                FROM VentasPorDia vpd
                INNER JOIN dim.Franquicia f ON vpd.FranquiciaId = f.FranquiciaId
                INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
                OUTER APPLY (
                    SELECT TOP 1 tc2.UnidadesPorUsd, tc2.Fecha
                    FROM dim.TipoCambio tc2
                    WHERE tc2.CodigoMoneda = m.CodigoISO
                      AND tc2.Fecha <= vpd.FechaNegocio
                    ORDER BY tc2.Fecha DESC
                ) tc
            ),
            VentasAgregadas AS (
                -- Agregar por franquicia y convertir a USD
                SELECT
                    FranquiciaId,
                    SUM(VentaNetaLocal) AS VentaNetaLocal,
                    SUM(
                        CASE
                            WHEN MonedaCodigo = 'USD' THEN VentaNetaLocal
                            WHEN UnidadesPorUsd IS NOT NULL AND UnidadesPorUsd > 0 THEN VentaNetaLocal / UnidadesPorUsd
                            ELSE 0
                        END
                    ) AS VentaNetaUsd,
                    SUM(TotalCubiertos) AS TotalCubiertos,
                    SUM(TotalTickets) AS TotalTickets,
                    SUM(TasaDirecta) AS DiasTasaDirecta,
                    SUM(TasaArrastrada) AS DiasTasaArrastrada
                FROM VentasConTC
                GROUP BY FranquiciaId
            ),
            -- Ventas del Mes Actual
            VentasMesActual AS (
                SELECT
                    vt.FranquiciaId,
                    SUM(
                        CASE
                            WHEN m.CodigoISO = 'USD' THEN vt.ImporteNeto
                            WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd
                            ELSE 0
                        END
                    ) AS VentaUsd,
                    SUM(vt.CantidadCubiertos) AS Cubiertos,
                    COUNT(*) AS Tickets
                FROM fact.VentaTicket vt
                INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
                INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
                OUTER APPLY (
                    SELECT TOP 1 tc2.UnidadesPorUsd
                    FROM dim.TipoCambio tc2
                    WHERE tc2.CodigoMoneda = m.CodigoISO AND tc2.Fecha <= vt.FechaNegocio
                    ORDER BY tc2.Fecha DESC
                ) tc
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @InicioMesActual
                  AND vt.FechaNegocio <= @FechaRef
                GROUP BY vt.FranquiciaId
            ),
            -- Ventas del Mes Anterior
            VentasMesAnterior AS (
                SELECT
                    vt.FranquiciaId,
                    SUM(
                        CASE
                            WHEN m.CodigoISO = 'USD' THEN vt.ImporteNeto
                            WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd
                            ELSE 0
                        END
                    ) AS VentaUsd,
                    SUM(vt.CantidadCubiertos) AS Cubiertos,
                    COUNT(*) AS Tickets
                FROM fact.VentaTicket vt
                INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
                INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
                OUTER APPLY (
                    SELECT TOP 1 tc2.UnidadesPorUsd
                    FROM dim.TipoCambio tc2
                    WHERE tc2.CodigoMoneda = m.CodigoISO AND tc2.Fecha <= vt.FechaNegocio
                    ORDER BY tc2.Fecha DESC
                ) tc
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @InicioMesAnterior
                  AND vt.FechaNegocio <= @FinMesAnterior
                GROUP BY vt.FranquiciaId
            ),
            -- Acumulado Año Actual (YTD)
            VentasYTDActual AS (
                SELECT
                    vt.FranquiciaId,
                    SUM(
                        CASE
                            WHEN m.CodigoISO = 'USD' THEN vt.ImporteNeto
                            WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd
                            ELSE 0
                        END
                    ) AS VentaUsd,
                    SUM(vt.CantidadCubiertos) AS Cubiertos,
                    COUNT(*) AS Tickets
                FROM fact.VentaTicket vt
                INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
                INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
                OUTER APPLY (
                    SELECT TOP 1 tc2.UnidadesPorUsd
                    FROM dim.TipoCambio tc2
                    WHERE tc2.CodigoMoneda = m.CodigoISO AND tc2.Fecha <= vt.FechaNegocio
                    ORDER BY tc2.Fecha DESC
                ) tc
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @InicioAnioActual
                  AND vt.FechaNegocio <= @FechaRef
                GROUP BY vt.FranquiciaId
            ),
            -- Acumulado Año Previo (YTD del año anterior hasta el mismo mes/día)
            VentasYTDPrevio AS (
                SELECT
                    vt.FranquiciaId,
                    SUM(
                        CASE
                            WHEN m.CodigoISO = 'USD' THEN vt.ImporteNeto
                            WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd
                            ELSE 0
                        END
                    ) AS VentaUsd,
                    SUM(vt.CantidadCubiertos) AS Cubiertos,
                    COUNT(*) AS Tickets
                FROM fact.VentaTicket vt
                INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
                INNER JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
                OUTER APPLY (
                    SELECT TOP 1 tc2.UnidadesPorUsd
                    FROM dim.TipoCambio tc2
                    WHERE tc2.CodigoMoneda = m.CodigoISO AND tc2.Fecha <= vt.FechaNegocio
                    ORDER BY tc2.Fecha DESC
                ) tc
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @InicioAnioPrevio
                  AND vt.FechaNegocio <= @FinAnioPrevioYTD
                GROUP BY vt.FranquiciaId
            )
            SELECT
                f.FranquiciaId,
                f.Codigo AS FranquiciaCodigo,
                f.Nombre AS FranquiciaNombre,
                ge.Codigo AS GrupoEconomicoCodigo,
                ge.Nombre AS GrupoEconomicoNombre,
                f.Pais,
                f.Ciudad,
                m.CodigoISO AS MonedaCodigo,
                f.UltimaSincronizacion,
                f.EstadoIntegracion,
                DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) AS DiasSinSincronizar,
                CAST(CASE WHEN DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) > 1 THEN 1 ELSE 0 END AS BIT) AS AlertaSincronizacion,
                -- Período seleccionado
                ISNULL(va.VentaNetaLocal, 0) AS VentaNetaLocal,
                ISNULL(va.VentaNetaUsd, 0) AS VentaNeta,
                ISNULL(va.TotalCubiertos, 0) AS TotalCubiertos,
                ISNULL(va.TotalTickets, 0) AS TotalTickets,
                ISNULL(va.DiasTasaDirecta, 0) AS DiasTasaDirecta,
                ISNULL(va.DiasTasaArrastrada, 0) AS DiasTasaArrastrada,
                -- Mes Actual
                ISNULL(vma.VentaUsd, 0) AS VentaMesActual,
                ISNULL(vma.Tickets, 0) AS TicketsMesActual,
                ISNULL(vma.Cubiertos, 0) AS CubiertosMesActual,
                -- Mes Anterior
                ISNULL(vmant.VentaUsd, 0) AS VentaMesAnterior,
                ISNULL(vmant.Tickets, 0) AS TicketsMesAnterior,
                ISNULL(vmant.Cubiertos, 0) AS CubiertosMesAnterior,
                -- Año Previo YTD
                ISNULL(vytdp.VentaUsd, 0) AS VentaAcumAnioPrevio,
                ISNULL(vytdp.Tickets, 0) AS TicketsAcumAnioPrevio,
                ISNULL(vytdp.Cubiertos, 0) AS CubiertosAcumAnioPrevio,
                -- Año Actual YTD
                ISNULL(vytda.VentaUsd, 0) AS VentaAcumAnioActual,
                ISNULL(vytda.Tickets, 0) AS TicketsAcumAnioActual,
                ISNULL(vytda.Cubiertos, 0) AS CubiertosAcumAnioActual
            FROM dim.Franquicia f
            LEFT JOIN dim.GrupoEconomico ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
            LEFT JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
            LEFT JOIN VentasAgregadas va ON f.FranquiciaId = va.FranquiciaId
            LEFT JOIN VentasMesActual vma ON f.FranquiciaId = vma.FranquiciaId
            LEFT JOIN VentasMesAnterior vmant ON f.FranquiciaId = vmant.FranquiciaId
            LEFT JOIN VentasYTDActual vytda ON f.FranquiciaId = vytda.FranquiciaId
            LEFT JOIN VentasYTDPrevio vytdp ON f.FranquiciaId = vytdp.FranquiciaId
            WHERE f.Activo = 1";

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND f.FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }

        if (!string.IsNullOrEmpty(filters.Pais))
        {
            sql += " AND f.Pais = @Pais";
            parameters.Add("Pais", filters.Pais);
        }

        sql += " ORDER BY f.Nombre";

        return await connection.QueryAsync<HomeDashboardDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasResumenDiarioDto>> GetVentasResumenDiarioAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                v.FechaNegocio,
                v.FranquiciaId,
                v.FranquiciaCodigo,
                v.FranquiciaNombre,
                v.GrupoEconomicoNombre,
                v.Pais,
                v.MonedaCodigo,
                v.CantidadTickets,
                v.TotalCubiertos,
                v.VentaBruta,
                v.TotalDescuentos,
                v.VentaNeta,
                -- Conversion a USD
                CASE
                    WHEN v.MonedaCodigo = 'USD' THEN v.VentaNeta
                    WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 THEN v.VentaNeta / tc.UnidadesPorUsd
                    ELSE NULL
                END AS VentaNetaUsd,
                tc.UnidadesPorUsd AS TipoCambio,
                v.TicketPromedio,
                v.VentaPorCubierto
            FROM fact.vw_VentasResumenDiario v
            OUTER APPLY (
                SELECT TOP 1 tc2.UnidadesPorUsd
                FROM dim.TipoCambio tc2
                WHERE tc2.CodigoMoneda = v.MonedaCodigo
                  AND tc2.Fecha <= v.FechaNegocio
                ORDER BY tc2.Fecha DESC
            ) tc
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FechaDesde.HasValue)
        {
            sql += " AND v.FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND v.FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }
        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND v.FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.GrupoEconomicoId.HasValue)
        {
            sql += " AND v.GrupoEconomicoId = @GrupoEconomicoId";
            parameters.Add("GrupoEconomicoId", filters.GrupoEconomicoId.Value);
        }

        sql += " ORDER BY v.FechaNegocio DESC, v.FranquiciaNombre";

        return await connection.QueryAsync<VentasResumenDiarioDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasPorFranquiciaDto>> GetVentasPorFranquiciaAsync(DashboardFilters? filters = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Si hay filtros de fecha, usamos la vista de resumen diario para filtrar
        if (filters?.FechaDesde.HasValue == true || filters?.FechaHasta.HasValue == true)
        {
            var sql = @"
                SELECT
                    r.FranquiciaId,
                    r.FranquiciaCodigo,
                    r.FranquiciaNombre,
                    r.GrupoEconomicoNombre,
                    r.Pais,
                    f.Ciudad,
                    r.MonedaCodigo,
                    f.UltimaSincronizacion,
                    f.EstadoIntegracion,
                    SUM(r.VentaNeta) AS VentaNetaYtd,
                    SUM(r.TotalCubiertos) AS CubiertosYtd,
                    SUM(r.CantidadTickets) AS TicketsYtd,
                    SUM(r.VentaNeta) AS VentaNetaMtd,
                    SUM(r.TotalCubiertos) AS CubiertosMtd,
                    SUM(r.CantidadTickets) AS TicketsMtd,
                    SUM(r.VentaNeta) AS VentaNetaTotal,
                    MIN(r.FechaNegocio) AS PrimeraVenta,
                    MAX(r.FechaNegocio) AS UltimaVenta
                FROM fact.vw_VentasResumenDiario r
                INNER JOIN dim.Franquicia f ON r.FranquiciaId = f.FranquiciaId
                WHERE 1=1";

            var parameters = new DynamicParameters();

            if (filters.FechaDesde.HasValue)
            {
                sql += " AND r.FechaNegocio >= @FechaDesde";
                parameters.Add("FechaDesde", filters.FechaDesde.Value);
            }
            if (filters.FechaHasta.HasValue)
            {
                sql += " AND r.FechaNegocio <= @FechaHasta";
                parameters.Add("FechaHasta", filters.FechaHasta.Value);
            }
            if (filters.FranquiciaId.HasValue)
            {
                sql += " AND r.FranquiciaId = @FranquiciaId";
                parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
            }

            sql += @" GROUP BY r.FranquiciaId, r.FranquiciaCodigo, r.FranquiciaNombre,
                      r.GrupoEconomicoNombre, r.Pais, f.Ciudad, r.MonedaCodigo,
                      f.UltimaSincronizacion, f.EstadoIntegracion
                      ORDER BY SUM(r.VentaNeta) DESC";

            return await connection.QueryAsync<VentasPorFranquiciaDto>(sql, parameters);
        }

        // Sin filtros, usamos la vista original con YTD/MTD pre-calculados
        return await connection.QueryAsync<VentasPorFranquiciaDto>(@"
            SELECT
                FranquiciaId,
                FranquiciaCodigo,
                FranquiciaNombre,
                GrupoEconomicoNombre,
                Pais,
                Ciudad,
                MonedaCodigo,
                UltimaSincronizacion,
                EstadoIntegracion,
                VentaNetaYTD AS VentaNetaYtd,
                CubiertosYTD AS CubiertosYtd,
                TicketsYTD AS TicketsYtd,
                VentaNetaMTD AS VentaNetaMtd,
                CubiertosMTD AS CubiertosMtd,
                TicketsMTD AS TicketsMtd,
                VentaNetaTotal,
                PrimeraVenta,
                UltimaVenta
            FROM fact.vw_VentasPorFranquicia
            ORDER BY VentaNetaYTD DESC");
    }

    public async Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Consulta directa optimizada (10x más rápida que usar la vista)
        // Usa COALESCE para obtener categoría de TipoPlato o del valor crudo del JSON
        var sql = @"
            SELECT
                vtd.CodigoProducto AS ProductoCodigo,
                vtd.NombreProducto AS ProductoNombre,
                COALESCE(tp.Nombre, vtd.CategoriaProducto, 'SIN_CATEGORIA') AS Categoria,
                vtd.FamiliaProducto AS Familia,
                f.Nombre AS FranquiciaNombre,
                SUM(vtd.Cantidad) AS CantidadVendida,
                SUM(vtd.ImporteBruto) AS VentaBruta,
                SUM(vtd.ImporteNeto) AS VentaNeta,
                COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets
            FROM fact.VentaTicketDetalle vtd
            INNER JOIN fact.VentaTicket vt ON vtd.VentaTicketId = vt.VentaTicketId
            INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
            LEFT JOIN dim.TipoPlato tp ON vtd.TipoPlatoId = tp.TipoPlatoId
            WHERE vtd.EstaAnulado = 0";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND vt.FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND vt.FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND vt.FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }

        sql += @" GROUP BY vtd.CodigoProducto, vtd.NombreProducto, tp.Nombre, vtd.CategoriaProducto, vtd.FamiliaProducto, f.Nombre
                  ORDER BY SUM(vtd.ImporteNeto) DESC";

        return await connection.QueryAsync<VentasPorProductoDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasPorMozoDto>> GetVentasPorMozoAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                MozoId,
                MozoNombre,
                FranquiciaNombre,
                SUM(CantidadTickets) AS CantidadTickets,
                SUM(TotalCubiertos) AS TotalCubiertos,
                SUM(VentaNeta) AS VentaNeta,
                SUM(TotalPropinas) AS TotalPropinas,
                AVG(TicketPromedio) AS TicketPromedio
            FROM fact.vw_VentasPorMozo
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }

        sql += @" GROUP BY MozoId, MozoNombre, FranquiciaNombre
                  ORDER BY SUM(VentaNeta) DESC";

        return await connection.QueryAsync<VentasPorMozoDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasPorTipoPlatoDto>> GetVentasPorTipoPlatoAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                TipoPlatoCodigo,
                TipoPlatoNombre,
                FranquiciaNombre,
                SUM(CantidadVendida) AS CantidadVendida,
                SUM(VentaNeta) AS VentaNeta,
                SUM(CantidadTickets) AS CantidadTickets
            FROM fact.vw_VentasPorTipoPlato
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }

        sql += @" GROUP BY TipoPlatoCodigo, TipoPlatoNombre, FranquiciaNombre
                  ORDER BY SUM(VentaNeta) DESC";

        return await connection.QueryAsync<VentasPorTipoPlatoDto>(sql, parameters);
    }

    public async Task<IEnumerable<OcupacionMesasDto>> GetOcupacionMesasAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                FranquiciaId,
                FranquiciaNombre,
                FechaNegocio,
                MesasTotales,
                MesasUtilizadas,
                RotacionMesas,
                PorcentajeOcupacion,
                TiempoPromedioMinutos,
                TotalTickets,
                TotalCubiertos
            FROM fact.vw_OcupacionMesas
            WHERE FechaNegocio IS NOT NULL";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }

        sql += " ORDER BY FechaNegocio DESC";

        return await connection.QueryAsync<OcupacionMesasDto>(sql, parameters);
    }

    public async Task<IEnumerable<EstadoIntegracionDto>> GetEstadoIntegracionAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryAsync<EstadoIntegracionDto>(@"
            SELECT
                FranquiciaId,
                FranquiciaCodigo,
                FranquiciaNombre,
                GrupoEconomicoNombre,
                Pais,
                Ciudad,
                EstadoIntegracion,
                SistemaOrigen,
                ContactoNombre,
                ContactoEmail,
                ContactoTelefono,
                UltimaSincronizacion,
                DiasSinSincronizar,
                CAST(AlertaSincronizacion AS BIT) AS AlertaSincronizacion,
                UltimoBatchId,
                UltimoBatchEstado,
                BatchesUltimoMes,
                BatchesConErrorUltimoMes
            FROM fact.vw_EstadoIntegracionFranquicias
            ORDER BY AlertaSincronizacion DESC, FranquiciaNombre");
    }

    #region v1.2 Methods

    public async Task<IEnumerable<VentasConsolidadasDto>> GetVentasConsolidadasAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                FechaNegocio,
                FranquiciaId,
                FranquiciaNombre,
                Pais,
                MonedaLocal,
                CantidadTickets,
                TotalCubiertos,
                VentaBrutaLocal,
                VentaNetaLocal,
                VentaNetaSinImpLocal,
                ImpuestoLocal,
                TipoCambio,
                VentaBrutaUsd,
                VentaNetaUsd,
                VentaNetaSinImpUsd,
                CalidadImpuesto,
                CalidadTipoCambio,
                CASE WHEN CantidadTickets > 0 THEN VentaNetaLocal / CantidadTickets ELSE NULL END AS TicketPromedio,
                CASE WHEN TotalCubiertos > 0 THEN VentaNetaLocal / TotalCubiertos ELSE NULL END AS VentaPorCubierto
            FROM fact.vw_VentasConsolidadasUsd
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }
        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (!string.IsNullOrEmpty(filters.Pais))
        {
            sql += " AND Pais = @Pais";
            parameters.Add("Pais", filters.Pais);
        }

        sql += " ORDER BY FechaNegocio DESC, FranquiciaNombre";

        return await connection.QueryAsync<VentasConsolidadasDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasPorProductoConPesoDto>> GetVentasPorProductoConPesoAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                FranquiciaId,
                FranquiciaNombre,
                CodigoProducto AS ProductoCodigo,
                NombreProducto AS ProductoNombre,
                Categoria,
                CantidadVendida,
                VentaNeta,
                VentaNetaUsd,
                PctSobreTotalFranquicia,
                PctSobreCategoria,
                PctCategoriaSobreTotal,
                RankingFranquicia,
                RankingCategoria
            FROM fact.vw_VentasPorProductoConPeso
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }

        sql += " ORDER BY RankingFranquicia";

        return await connection.QueryAsync<VentasPorProductoConPesoDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasPorMealPeriodDto>> GetVentasPorMealPeriodAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                FranquiciaId,
                FranquiciaNombre,
                MealPeriod,
                MealPeriodOrigen,
                CantidadTickets,
                TotalCubiertos,
                VentaNeta,
                VentaNetaUsd,
                VentaNetaSinImp,
                CASE WHEN CantidadTickets > 0 THEN VentaNeta / CantidadTickets ELSE NULL END AS TicketPromedio,
                PctSobreTotal
            FROM fact.vw_VentasPorMealPeriod
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }
        if (!string.IsNullOrEmpty(filters.MealPeriod))
        {
            sql += " AND MealPeriod = @MealPeriod";
            parameters.Add("MealPeriod", filters.MealPeriod);
        }

        sql += " ORDER BY FranquiciaNombre, MealPeriod";

        return await connection.QueryAsync<VentasPorMealPeriodDto>(sql, parameters);
    }

    public async Task<IEnumerable<DiaRankingDto>> GetDiaRankingAsync(DashboardFilters filters, bool mejores = true, int top = 10)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderColumn = mejores ? "RankingMejor" : "RankingPeor";

        var sql = $@"
            SELECT TOP (@Top)
                FechaNegocio,
                FranquiciaId,
                FranquiciaNombre,
                DiaSemana,
                VentaNeta,
                VentaNetaUsd,
                CantidadTickets,
                TotalCubiertos,
                RankingMejor,
                RankingPeor
            FROM fact.vw_VentasPorDiaRanking
            WHERE 1=1";

        var parameters = new DynamicParameters();
        parameters.Add("Top", top);

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }
        if (filters.FechaDesde.HasValue)
        {
            sql += " AND FechaNegocio >= @FechaDesde";
            parameters.Add("FechaDesde", filters.FechaDesde.Value);
        }
        if (filters.FechaHasta.HasValue)
        {
            sql += " AND FechaNegocio <= @FechaHasta";
            parameters.Add("FechaHasta", filters.FechaHasta.Value);
        }

        sql += $" ORDER BY {orderColumn}";

        return await connection.QueryAsync<DiaRankingDto>(sql, parameters);
    }

    public async Task<IEnumerable<VentasDelDiaDto>> GetVentasDelDiaAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                FranquiciaId,
                FranquiciaNombre,
                FechaHoy,
                VentaHoy,
                VentaHoyUsd,
                TicketsHoy,
                CubiertosHoy,
                VentaMismoDiaSemPasada,
                VarVsSemPasadaPct,
                PromedioMensual,
                VarVsPromedioPct
            FROM fact.vw_VentasDelDia
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.FranquiciaId.HasValue)
        {
            sql += " AND FranquiciaId = @FranquiciaId";
            parameters.Add("FranquiciaId", filters.FranquiciaId.Value);
        }

        sql += " ORDER BY FranquiciaNombre";

        return await connection.QueryAsync<VentasDelDiaDto>(sql, parameters);
    }

    public async Task<IEnumerable<ComparativoMensualDto>> GetComparativoMensualAsync(DashboardFilters filters)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Use the table-valued function
        var sql = @"
            SELECT
                FranquiciaId,
                FranquiciaNombre,
                Anio,
                Mes,
                VentaNeta,
                VentaNetaUsd,
                CantidadTickets,
                TotalCubiertos,
                VentaMesAnterior,
                VarVsMesAnteriorPct,
                VentaMismoMesAnioAnterior,
                VarVsAnioAnteriorPct
            FROM fact.fn_ComparativoMensual(@FranquiciaId, @Anio, @Mes)
            ORDER BY FranquiciaNombre";

        var parameters = new DynamicParameters();
        parameters.Add("FranquiciaId", filters.FranquiciaId);
        parameters.Add("Anio", filters.Anio ?? DateTime.Today.Year);
        parameters.Add("Mes", filters.Mes ?? DateTime.Today.Month);

        return await connection.QueryAsync<ComparativoMensualDto>(sql, parameters);
    }

    #endregion

    #region Tickets / Transacciones

    public async Task<IEnumerable<TransaccionDto>> GetTransaccionesByFranquiciaAsync(int franquiciaId, DateTime fechaDesde, DateTime fechaHasta)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                vt.VentaTicketId AS TicketId,
                vt.NumeroTicket,
                vt.FechaNegocio,
                vt.FechaApertura,
                vt.FechaCierre,
                vt.Estado,
                vt.PeriodoComida,
                vt.NumeroMesa,
                vt.AreaMesa,
                vt.NombreMozo,
                vt.CantidadCubiertos,
                vt.CodigoMoneda AS MonedaCodigo,
                vt.ImporteBruto,
                vt.ImporteDescuento,
                vt.ImporteNeto,
                vt.ImporteImpuesto,
                vt.ImportePropina,
                vt.ImporteTotalPagado,
                vt.TiempoConsumoMinutos,
                (SELECT COUNT(*) FROM fact.VentaTicketDetalle vtd WHERE vtd.VentaTicketId = vt.VentaTicketId AND vtd.EstaAnulado = 0) AS CantidadItems
            FROM fact.VentaTicket vt
            WHERE vt.FranquiciaId = @FranquiciaId
              AND vt.FechaNegocio >= @FechaDesde
              AND vt.FechaNegocio <= @FechaHasta
              AND vt.EstaAnulado = 0
            ORDER BY vt.FechaApertura DESC";

        return await connection.QueryAsync<TransaccionDto>(sql, new { FranquiciaId = franquiciaId, FechaDesde = fechaDesde, FechaHasta = fechaHasta });
    }

    public async Task<IEnumerable<TransaccionDetalleDto>> GetTransaccionDetalleAsync(long ticketId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                vtd.VentaTicketDetalleId AS DetalleId,
                vtd.VentaTicketId AS TicketId,
                vtd.CodigoProducto,
                vtd.NombreProducto,
                COALESCE(tp.Nombre, vtd.CategoriaProducto) AS Categoria,
                vtd.FamiliaProducto AS Familia,
                vtd.Cantidad,
                vtd.PrecioUnitario,
                vtd.ImporteBruto,
                vtd.ImporteDescuento,
                vtd.ImporteNeto,
                vtd.EstaAnulado,
                vtd.Notas
            FROM fact.VentaTicketDetalle vtd
            LEFT JOIN dim.TipoPlato tp ON vtd.TipoPlatoId = tp.TipoPlatoId
            WHERE vtd.VentaTicketId = @TicketId
            ORDER BY vtd.VentaTicketDetalleId";

        return await connection.QueryAsync<TransaccionDetalleDto>(sql, new { TicketId = ticketId });
    }

    #endregion
}
