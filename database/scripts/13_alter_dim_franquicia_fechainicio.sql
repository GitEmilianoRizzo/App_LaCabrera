/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 13_alter_dim_franquicia_fechainicio.sql
  Descripcion: Agrega FechaInicioDatos a dim.Franquicia
  Autor: Claude Code
  Fecha: 2026-08-22
  Lote: MEJORAS_20260822 - Etapa 1.5

  MOTIVO:
  =======
  La columna FechaInicioDatos indica desde cuando hay datos cargados para
  una franquicia. Esto es CRITICO para los comparativos:

  - Si comparo agosto 2026 contra agosto 2025, pero la franquicia empezo
    a cargar datos en junio 2026, el resultado debe ser "SIN_DATOS_HISTORICOS",
    NO debe mostrar -100% ni cero.

  - Permite distinguir "vendio cero" (mal) de "no habia datos" (no aplica).

  REGLA DEL PROYECTO:
  ===================
  Historia de datos: desde junio 2026 en adelante. Sin backfill de POS.

  Por lo tanto, todas las franquicias activas tendran FechaInicioDatos = '2026-06-01'
  o la fecha real de su primera integracion si es posterior.

  INSTRUCCIONES:
  - Ejecutar despues de 12_create_cfg_franjas_alicuotas.sql
  - Idempotente: puede re-ejecutarse sin error
  - Incluye UPDATE para setear FechaInicioDatos de franquicias existentes
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- ALTER: dim.Franquicia - Agregar FechaInicioDatos
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dim.Franquicia') AND name = 'FechaInicioDatos'
)
BEGIN
    ALTER TABLE dim.Franquicia
    ADD FechaInicioDatos DATE NULL

    PRINT 'Columna FechaInicioDatos agregada a dim.Franquicia'
END
ELSE
    PRINT 'Columna FechaInicioDatos ya existe en dim.Franquicia'
GO

-- ============================================================================
-- UPDATE: Setear FechaInicioDatos para franquicias existentes
-- ============================================================================
PRINT 'Actualizando FechaInicioDatos de franquicias existentes...'

-- Para franquicias que ya tienen datos, usar la fecha del primer ticket
UPDATE f
SET FechaInicioDatos = (
    SELECT MIN(vt.FechaNegocio)
    FROM fact.VentaTicket vt
    WHERE vt.FranquiciaId = f.FranquiciaId
)
FROM dim.Franquicia f
WHERE f.FechaInicioDatos IS NULL
AND EXISTS (
    SELECT 1 FROM fact.VentaTicket vt
    WHERE vt.FranquiciaId = f.FranquiciaId
)

PRINT 'FechaInicioDatos actualizada para franquicias con datos existentes.'
GO

-- Para franquicias activas sin datos, usar fecha de alta o '2026-06-01'
UPDATE dim.Franquicia
SET FechaInicioDatos = COALESCE(FechaAltaSistema, '2026-06-01')
WHERE FechaInicioDatos IS NULL
AND Activo = 1

PRINT 'FechaInicioDatos seteada para franquicias activas sin datos.'
GO

-- ============================================================================
-- DOCUMENTACION: Comentario en columna
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dim.Franquicia')
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('dim.Franquicia') AND name = 'FechaInicioDatos')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Fecha desde la cual hay datos cargados para esta franquicia. Usar para evitar comparativos contra periodos sin datos.',
        @level0type = N'SCHEMA', @level0name = N'dim',
        @level1type = N'TABLE',  @level1name = N'Franquicia',
        @level2type = N'COLUMN', @level2name = N'FechaInicioDatos'

    PRINT 'Descripcion de columna FechaInicioDatos agregada.'
END
GO

-- ============================================================================
-- VERIFICACION: Mostrar estado de FechaInicioDatos
-- ============================================================================
PRINT ''
PRINT 'Estado de FechaInicioDatos por franquicia:'
PRINT '==========================================='

SELECT
    f.FranquiciaId,
    f.Codigo,
    f.Nombre,
    f.Pais,
    f.FechaInicioDatos,
    f.FechaAltaSistema,
    (SELECT MIN(FechaNegocio) FROM fact.VentaTicket WHERE FranquiciaId = f.FranquiciaId) AS PrimerTicket,
    (SELECT MAX(FechaNegocio) FROM fact.VentaTicket WHERE FranquiciaId = f.FranquiciaId) AS UltimoTicket,
    (SELECT COUNT(*) FROM fact.VentaTicket WHERE FranquiciaId = f.FranquiciaId) AS TotalTickets
FROM dim.Franquicia f
WHERE f.Activo = 1
ORDER BY f.FranquiciaId
GO

PRINT ''
PRINT '=========================================='
PRINT 'Script 13 completado: FechaInicioDatos agregada y poblada.'
PRINT '=========================================='
GO
