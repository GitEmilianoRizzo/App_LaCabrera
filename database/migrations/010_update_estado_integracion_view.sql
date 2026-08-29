/*
================================================================================
  LA CABRERA - Migration 010
  Descripcion: Agregar ContactoNombre a la vista de Estado Integracion
  Fecha: 2026-08-17
================================================================================
*/

USE [LaCabreraDB]
GO

-- Actualizar vista de Estado de Integracion
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_EstadoIntegracionFranquicias]'))
    DROP VIEW [fact].[vw_EstadoIntegracionFranquicias]
GO

CREATE VIEW [fact].[vw_EstadoIntegracionFranquicias]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    f.Pais,
    f.Ciudad,
    f.EstadoIntegracion,
    f.SistemaOrigen,
    f.VersionSistema,
    f.ContactoNombre,
    f.ContactoEmail,
    f.ContactoTelefono,
    f.UltimaSincronizacion,

    -- Dias sin sincronizar
    DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) AS DiasSinSincronizar,

    -- Alerta: mas de 1 dia sin sincro
    CASE WHEN DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) > 1 THEN 1 ELSE 0 END AS AlertaSincronizacion,

    -- Ultimo batch
    ub.BatchId AS UltimoBatchId,
    ub.FechaNegocio AS UltimaFechaNegocio,
    ub.Estado AS UltimoBatchEstado,
    ub.FechaRecepcion AS UltimoBatchFechaRecepcion,

    -- Batches del ultimo mes
    (SELECT COUNT(*) FROM [stg].[IngestionBatch] ib
     WHERE ib.FranquiciaId = f.FranquiciaId
       AND ib.FechaRecepcion >= DATEADD(MONTH, -1, GETDATE())) AS BatchesUltimoMes,

    -- Errores del ultimo mes
    (SELECT COUNT(*) FROM [stg].[IngestionBatch] ib
     WHERE ib.FranquiciaId = f.FranquiciaId
       AND ib.FechaRecepcion >= DATEADD(MONTH, -1, GETDATE())
       AND ib.Estado IN ('REJECTED', 'FAILED')) AS BatchesConErrorUltimoMes

FROM [dim].[Franquicia] f
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
OUTER APPLY (
    SELECT TOP 1 *
    FROM [stg].[IngestionBatch] ib
    WHERE ib.FranquiciaId = f.FranquiciaId
    ORDER BY ib.FechaRecepcion DESC
) ub
WHERE f.Activo = 1
GO

PRINT 'Vista vw_EstadoIntegracionFranquicias actualizada con ContactoNombre'
GO
