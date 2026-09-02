-- =====================================================
-- Fix: Obtener UltimaSincronizacion desde log.EjecucionNodo
-- en lugar del campo estatico en dim.NodoConexion
-- =====================================================

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_EstadoNodosConexion' AND schema_id = SCHEMA_ID('dim'))
    DROP VIEW dim.vw_EstadoNodosConexion;
GO

CREATE VIEW dim.vw_EstadoNodosConexion AS
SELECT
    n.NodoConexionId,
    n.Codigo AS NodoCodigo,
    n.Nombre AS NodoNombre,
    n.TipoConector,
    n.Modo,
    n.Estado,
    -- UltimaSincronizacion: preferir la fecha de la ultima ejecucion exitosa,
    -- si no existe, usar el campo de la tabla (para conexiones antiguas)
    COALESCE(ue.UltimaEjecucionExitosa, n.UltimaSincronizacion) AS UltimaSincronizacion,
    -- UltimoEstado: preferir el estado de la ultima ejecucion
    COALESCE(ue.UltimoEstadoEjecucion, n.UltimoEstado) AS UltimoEstado,
    COALESCE(ue.UltimoBatch, n.UltimoBatchId) AS UltimoBatchId,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    f.Pais,
    f.Ciudad,
    n.Timezone,
    n.Moneda,
    n.CronExpression,
    n.ConvencionImportes,
    n.Activo,
    -- Conteo de mapeos
    (SELECT COUNT(*) FROM dim.MapeoCategoria mc WHERE mc.NodoConexionId = n.NodoConexionId AND mc.Activo = 1) AS MapeosCategoria,
    (SELECT COUNT(*) FROM dim.MapeoMedioPago mp WHERE mp.NodoConexionId = n.NodoConexionId AND mp.Activo = 1) AS MapeosMedioPago,
    -- Valores pendientes de mapear
    (SELECT COUNT(*) FROM dim.ValorNoMapeado v WHERE v.NodoConexionId = n.NodoConexionId AND v.Resuelto = 0) AS ValoresPendientes,
    -- Tickets de ultima ejecucion exitosa
    ue.TicketsUltimaEjecucion
FROM dim.NodoConexion n
INNER JOIN dim.Franquicia f ON n.FranquiciaId = f.FranquiciaId
-- Subquery para obtener datos de la ultima ejecucion exitosa
OUTER APPLY (
    SELECT TOP 1
        e.FinEjecucion AS UltimaEjecucionExitosa,
        e.Estado AS UltimoEstadoEjecucion,
        e.BatchId AS UltimoBatch,
        e.TicketsProcesados AS TicketsUltimaEjecucion
    FROM log.EjecucionNodo e
    WHERE e.NodoConexionId = n.NodoConexionId
      AND e.Estado IN ('SUCCESS', 'WARNING')  -- Incluir WARNING porque los datos se procesaron
    ORDER BY e.FinEjecucion DESC
) ue
WHERE n.Activo = 1;
GO

PRINT 'Vista dim.vw_EstadoNodosConexion actualizada - ahora usa log.EjecucionNodo para UltimaSincronizacion';
GO
