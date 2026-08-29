import { useState, useEffect, useCallback } from 'react'
import { conexionesApi } from '@/services/api'
import type {
  NodoConexion,
  NodoDetalle,
} from '@/types/conexiones'

interface UseDataResult<T> {
  data: T | null
  isLoading: boolean
  error: Error | null
  refetch: () => Promise<void>
}

interface UseMutationResult<TData, TVariables> {
  mutate: (variables: TVariables) => void
  mutateAsync: (variables: TVariables) => Promise<TData>
  isPending: boolean
  error: Error | null
}

function useApiData<T>(
  fetchFn: () => Promise<T>,
  dependencies: unknown[] = [],
  enabled: boolean = true
): UseDataResult<T> {
  const [data, setData] = useState<T | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  const fetchData = useCallback(async () => {
    if (!enabled) {
      setIsLoading(false)
      return
    }
    try {
      setIsLoading(true)
      setError(null)
      const result = await fetchFn()
      setData(result)
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Error al cargar datos'))
    } finally {
      setIsLoading(false)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, ...dependencies])

  useEffect(() => {
    fetchData()
  }, [fetchData])

  return { data, isLoading, error, refetch: fetchData }
}

function useMutation<TData, TVariables>(
  mutationFn: (variables: TVariables) => Promise<TData>,
  onSuccess?: () => void
): UseMutationResult<TData, TVariables> {
  const [isPending, setIsPending] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const mutateAsync = async (variables: TVariables): Promise<TData> => {
    try {
      setIsPending(true)
      setError(null)
      const result = await mutationFn(variables)
      onSuccess?.()
      return result
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Error en operacion')
      setError(error)
      throw error
    } finally {
      setIsPending(false)
    }
  }

  const mutate = (variables: TVariables) => {
    mutateAsync(variables).catch(() => {})
  }

  return { mutate, mutateAsync, isPending, error }
}

// Get all nodos
export function useConexiones(): UseDataResult<NodoConexion[]> {
  return useApiData(() => conexionesApi.getAllNodos())
}

// Get nodo by ID
export function useConexion(id: number | null): UseDataResult<NodoConexion> {
  return useApiData(
    () => conexionesApi.getNodoById(id!),
    [id],
    !!id
  )
}

// Get nodo detail (with mapeos and executions)
export function useConexionDetalle(id: number | null): UseDataResult<NodoDetalle> {
  return useApiData(
    () => conexionesApi.getNodoDetalle(id!),
    [id],
    !!id
  )
}

// Get catalogos
export function useCategorias(): UseDataResult<string[]> {
  return useApiData(() => conexionesApi.getCategorias())
}

export function useMediosPago(): UseDataResult<string[]> {
  return useApiData(() => conexionesApi.getMediosPago())
}

// Mutations
export function useUpdateNodoEstado(onSuccess?: () => void): UseMutationResult<void, { id: number; estado: string }> {
  return useMutation(
    ({ id, estado }) => conexionesApi.updateNodoEstado(id, estado),
    onSuccess
  )
}
