using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

public interface IDashboardService
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

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<IEnumerable<HomeDashboardDto>> GetHomeDashboardAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetHomeDashboardAsync(filters);
    }

    public async Task<IEnumerable<VentasResumenDiarioDto>> GetVentasResumenDiarioAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasResumenDiarioAsync(filters);
    }

    public async Task<IEnumerable<VentasPorFranquiciaDto>> GetVentasPorFranquiciaAsync(DashboardFilters? filters = null)
    {
        return await _dashboardRepository.GetVentasPorFranquiciaAsync(filters);
    }

    public async Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasPorProductoAsync(filters);
    }

    public async Task<IEnumerable<VentasPorMozoDto>> GetVentasPorMozoAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasPorMozoAsync(filters);
    }

    public async Task<IEnumerable<VentasPorTipoPlatoDto>> GetVentasPorTipoPlatoAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasPorTipoPlatoAsync(filters);
    }

    public async Task<IEnumerable<OcupacionMesasDto>> GetOcupacionMesasAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetOcupacionMesasAsync(filters);
    }

    public async Task<IEnumerable<EstadoIntegracionDto>> GetEstadoIntegracionAsync()
    {
        return await _dashboardRepository.GetEstadoIntegracionAsync();
    }

    #region v1.2 Methods

    public async Task<IEnumerable<VentasConsolidadasDto>> GetVentasConsolidadasAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasConsolidadasAsync(filters);
    }

    public async Task<IEnumerable<VentasPorProductoConPesoDto>> GetVentasPorProductoConPesoAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasPorProductoConPesoAsync(filters);
    }

    public async Task<IEnumerable<VentasPorMealPeriodDto>> GetVentasPorMealPeriodAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasPorMealPeriodAsync(filters);
    }

    public async Task<IEnumerable<DiaRankingDto>> GetDiaRankingAsync(DashboardFilters filters, bool mejores = true, int top = 10)
    {
        return await _dashboardRepository.GetDiaRankingAsync(filters, mejores, top);
    }

    public async Task<IEnumerable<VentasDelDiaDto>> GetVentasDelDiaAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetVentasDelDiaAsync(filters);
    }

    public async Task<IEnumerable<ComparativoMensualDto>> GetComparativoMensualAsync(DashboardFilters filters)
    {
        return await _dashboardRepository.GetComparativoMensualAsync(filters);
    }

    #endregion

    #region Tickets / Transacciones

    public async Task<IEnumerable<TransaccionDto>> GetTransaccionesByFranquiciaAsync(int franquiciaId, DateTime fechaDesde, DateTime fechaHasta)
    {
        return await _dashboardRepository.GetTransaccionesByFranquiciaAsync(franquiciaId, fechaDesde, fechaHasta);
    }

    public async Task<IEnumerable<TransaccionDetalleDto>> GetTransaccionDetalleAsync(long ticketId)
    {
        return await _dashboardRepository.GetTransaccionDetalleAsync(ticketId);
    }

    #endregion
}
