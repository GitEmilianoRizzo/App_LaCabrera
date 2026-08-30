import { useState, useEffect, useCallback } from 'react'
import { dashboardApi } from '@/services/api'
import type {
  HomeDashboard,
  VentasResumenDiario,
  VentasPorFranquicia,
  VentasPorProducto,
  VentasPorMozo,
  VentasPorTipoPlato,
  OcupacionMesas,
  EstadoIntegracion,
  DashboardFilters,
} from '@/types/dashboard'

interface UseDataResult<T> {
  data: T | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

function useApiData<T>(
  fetchFn: () => Promise<T>,
  dependencies: unknown[] = []
): UseDataResult<T> {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchData = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const result = await fetchFn()
      setData(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al cargar datos')
    } finally {
      setLoading(false)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, dependencies)

  useEffect(() => {
    fetchData()
  }, [fetchData])

  return { data, loading, error, refetch: fetchData }
}

export function useHomeDashboard(filters?: DashboardFilters): UseDataResult<HomeDashboard[]> {
  return useApiData(
    () => dashboardApi.getHomeDashboard(filters),
    [filters?.fechaDesde, filters?.fechaHasta, filters?.franquiciaId]
  )
}

export function useVentasResumen(filters?: DashboardFilters): UseDataResult<VentasResumenDiario[]> {
  return useApiData(
    () => dashboardApi.getVentasResumen(filters),
    [filters?.fechaDesde, filters?.fechaHasta, filters?.franquiciaId]
  )
}

export function useVentasPorFranquicia(filters?: DashboardFilters): UseDataResult<VentasPorFranquicia[]> {
  return useApiData(
    () => dashboardApi.getVentasPorFranquicia(filters),
    [filters?.fechaDesde, filters?.fechaHasta, filters?.franquiciaId]
  )
}

export function useVentasPorProducto(filters?: DashboardFilters): UseDataResult<VentasPorProducto[]> {
  return useApiData(
    () => dashboardApi.getVentasPorProducto(filters),
    [filters?.franquiciaId, filters?.fechaDesde, filters?.fechaHasta]
  )
}

export function useVentasPorMozo(filters?: DashboardFilters): UseDataResult<VentasPorMozo[]> {
  return useApiData(
    () => dashboardApi.getVentasPorMozo(filters),
    [filters?.franquiciaId, filters?.fechaDesde]
  )
}

export function useVentasPorTipoPlato(filters?: DashboardFilters): UseDataResult<VentasPorTipoPlato[]> {
  return useApiData(
    () => dashboardApi.getVentasPorTipoPlato(filters),
    [filters?.franquiciaId]
  )
}

export function useOcupacionMesas(filters?: DashboardFilters): UseDataResult<OcupacionMesas[]> {
  return useApiData(
    () => dashboardApi.getOcupacionMesas(filters),
    [filters?.franquiciaId, filters?.fechaDesde]
  )
}

export function useEstadoIntegracion(): UseDataResult<EstadoIntegracion[]> {
  return useApiData(() => dashboardApi.getEstadoIntegracion())
}

// v1.2 Hooks - USD Consolidation and Tax Axis
import { dashboardApiV2 } from '@/services/api'
import type {
  VentasConsolidadas,
  VentasPorProductoConPeso,
  VentasPorMealPeriod,
  DiaRanking,
  VentasDelDia,
  ComparativoMensual,
  VentasPorHora,
} from '@/types/dashboard'

export function useVentasConsolidadas(filters?: DashboardFilters): UseDataResult<VentasConsolidadas[]> {
  return useApiData(
    () => dashboardApiV2.getVentasConsolidadas(filters),
    [filters?.fechaDesde, filters?.fechaHasta, filters?.franquiciaId, filters?.currencyMode, filters?.taxMode]
  )
}

export function useVentasPorProductoConPeso(filters?: DashboardFilters): UseDataResult<VentasPorProductoConPeso[]> {
  return useApiData(
    () => dashboardApiV2.getVentasPorProductoConPeso(filters),
    [filters?.franquiciaId, filters?.fechaDesde, filters?.fechaHasta]
  )
}

export function useVentasPorMealPeriod(filters?: DashboardFilters): UseDataResult<VentasPorMealPeriod[]> {
  return useApiData(
    () => dashboardApiV2.getVentasPorMealPeriod(filters),
    [filters?.franquiciaId, filters?.fechaDesde, filters?.fechaHasta, filters?.mealPeriod]
  )
}

export function useDiaRanking(filters?: DashboardFilters, mejores = true, top = 10): UseDataResult<DiaRanking[]> {
  return useApiData(
    () => dashboardApiV2.getDiaRanking(filters, mejores, top),
    [filters?.franquiciaId, filters?.fechaDesde, filters?.fechaHasta, mejores, top]
  )
}

export function useVentasDelDia(filters?: DashboardFilters): UseDataResult<VentasDelDia[]> {
  return useApiData(
    () => dashboardApiV2.getVentasDelDia(filters),
    [filters?.franquiciaId]
  )
}

export function useComparativoMensual(filters?: DashboardFilters): UseDataResult<ComparativoMensual[]> {
  return useApiData(
    () => dashboardApiV2.getComparativoMensual(filters),
    [filters?.franquiciaId, filters?.anio, filters?.mes]
  )
}

// v1.3: ClockChart hourly consumption
export function useVentasPorHora(filters?: DashboardFilters): UseDataResult<VentasPorHora[]> {
  return useApiData(
    () => dashboardApiV2.getVentasPorHora(filters),
    [filters?.franquiciaId, filters?.fechaDesde, filters?.fechaHasta, filters?.productoId, filters?.hora]
  )
}
