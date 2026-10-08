using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Repositories;

/// <summary>
/// Fila diaria por franquicia para el Informe Ejecutivo.
/// Importes en moneda local; la conversión a USD usa el tipo de cambio del día (igual que el dashboard).
/// </summary>
public class InformeVentaDiaRow
{
    public int FranquiciaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string Moneda { get; set; } = "USD";
    public DateTime FechaNegocio { get; set; }
    public decimal NetoLocal { get; set; }
    public decimal BrutoLocal { get; set; }
    public decimal DescuentoLocal { get; set; }
    public decimal PropinaLocal { get; set; }
    public int Cubiertos { get; set; }
    public int Tickets { get; set; }
    public int Reembolsos { get; set; }
    public decimal? UnidadesPorUsd { get; set; }
    public string? TipoTasa { get; set; }
}

public interface IInformeEjecutivoRepository
{
    Task<IReadOnlyList<InformeVentaDiaRow>> GetVentasDiariasAsync(DateTime fechaDesde, DateTime fechaHasta);
}

public class InformeEjecutivoRepository : IInformeEjecutivoRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InformeEjecutivoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<InformeVentaDiaRow>> GetVentasDiariasAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Neto sin impuestos ni propinas, mismo criterio que GetHomeDashboardAsync.
        // Los reembolsos (Estado = REFUNDED, EstaAnulado = 0) ya traen importes negativos;
        // sus cubiertos se restan y no cuentan como ticket.
        const string sql = @"
            WITH dia AS (
                SELECT
                    vt.FranquiciaId,
                    vt.FechaNegocio,
                    SUM(COALESCE(vt.ImporteNetoSinImpuesto, vt.ImporteNeto) - ISNULL(vt.ImporteServicio, 0)) AS NetoLocal,
                    SUM(ISNULL(vt.ImporteBruto, 0)) AS BrutoLocal,
                    SUM(ISNULL(vt.ImporteDescuento, 0)) AS DescuentoLocal,
                    SUM(ISNULL(vt.ImportePropina, 0)) AS PropinaLocal,
                    SUM(CASE WHEN vt.Estado = 'REFUNDED' THEN -ISNULL(vt.CantidadCubiertos, 0)
                             ELSE ISNULL(vt.CantidadCubiertos, 0) END) AS Cubiertos,
                    SUM(CASE WHEN vt.Estado = 'REFUNDED' THEN 0 ELSE 1 END) AS Tickets,
                    SUM(CASE WHEN vt.Estado = 'REFUNDED' THEN 1 ELSE 0 END) AS Reembolsos
                FROM fact.VentaTicket vt
                WHERE vt.EstaAnulado = 0
                  AND vt.FechaNegocio >= @FechaDesde
                  AND vt.FechaNegocio <= @FechaHasta
                GROUP BY vt.FranquiciaId, vt.FechaNegocio
            )
            SELECT
                d.FranquiciaId,
                f.Codigo,
                f.Nombre,
                f.Pais,
                ISNULL(m.CodigoISO, 'USD') AS Moneda,
                d.FechaNegocio,
                d.NetoLocal,
                d.BrutoLocal,
                d.DescuentoLocal,
                d.PropinaLocal,
                d.Cubiertos,
                d.Tickets,
                d.Reembolsos,
                CASE WHEN ISNULL(m.CodigoISO, 'USD') = 'USD' THEN 1 ELSE tc.UnidadesPorUsd END AS UnidadesPorUsd,
                tc.TipoTasa
            FROM dia d
            INNER JOIN dim.Franquicia f ON f.FranquiciaId = d.FranquiciaId
            LEFT JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
            OUTER APPLY (
                SELECT TOP 1 tc2.UnidadesPorUsd, tc2.TipoTasa
                FROM dim.TipoCambio tc2
                WHERE tc2.CodigoMoneda = m.CodigoISO
                  AND tc2.Fecha <= d.FechaNegocio
                ORDER BY tc2.Fecha DESC
            ) tc
            ORDER BY f.Nombre, d.FechaNegocio";

        var rows = await connection.QueryAsync<InformeVentaDiaRow>(sql, new
        {
            FechaDesde = fechaDesde.Date,
            FechaHasta = fechaHasta.Date
        });
        return rows.ToList();
    }
}
