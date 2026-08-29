/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 15_backfill_impuesto_turno.sql
  Descripcion: Backfill de datos existentes para eje impuesto y turno v1.2
  Autor: Claude Code
  Fecha: 2026-08-23
  Lote: MEJORAS_20260822 - Etapa 3.5

  OBJETIVO:
  Recalcular ImporteNetoSinImpuesto, CalidadImpuesto y MealPeriod derivado
  para todos los tickets ya cargados desde junio 2026.

  LOGICA DE CALIDAD IMPUESTO:
  1. Si ImporteImpuesto > 0 -> CalidadImpuesto = 'POS' (asumimos que vino del POS)
  2. Si hay alicuota en cfg.AlicuotaImpuestoFranquicia -> CalidadImpuesto = 'DERIVADO'
  3. Si no hay informacion -> CalidadImpuesto = 'DESCONOCIDO'

  LOGICA DE MEAL PERIOD:
  1. Si PeriodoComida ya tiene valor -> MealPeriodOrigen = 'POS'
  2. Si no, derivar de FechaApertura usando cfg.FranjaHorariaFranquicia -> 'DERIVADO'

  INSTRUCCIONES:
  - Ejecutar despues de 14_alter_sp_v12_impuesto_turno.sql
  - Re-ejecutable sin error
  - Reporta cantidad de filas actualizadas por cada escenario
================================================================================
*/

USE [LaCabreraDB]
GO

SET NOCOUNT ON;
PRINT '============================================'
PRINT 'Backfill de eje impuesto y turno v1.2'
PRINT 'Fecha: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '============================================'
GO

-- ============================================================================
-- PASO 1: Backfill de CalidadImpuesto e ImporteNetoSinImpuesto en TICKETS
-- ============================================================================

PRINT ''
PRINT '--- PASO 1: Backfill fact.VentaTicket ---'

-- 1a. Tickets que ya tienen ImporteImpuesto > 0 -> CalidadImpuesto = 'POS'
UPDATE fact.VentaTicket
SET
    ImporteNetoSinImpuesto = ImporteNeto - ImporteImpuesto,
    CalidadImpuesto = 'POS',
    FechaModificacion = GETDATE(),
    UsuarioModificacion = 'BACKFILL_V12'
WHERE ImporteImpuesto > 0
  AND CalidadImpuesto IS NULL

DECLARE @TicketsPOS INT = @@ROWCOUNT
PRINT 'Tickets con CalidadImpuesto = POS (tenian ImporteImpuesto): ' + CAST(@TicketsPOS AS VARCHAR)

-- 1b. Tickets sin ImporteImpuesto pero con alicuota en config -> DERIVADO
-- (Solo si existe cfg.AlicuotaImpuestoFranquicia con datos)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'cfg' AND TABLE_NAME = 'AlicuotaImpuestoFranquicia')
BEGIN
    UPDATE t
    SET
        ImporteNetoSinImpuesto = ROUND(t.ImporteNeto / (1 + a.Alicuota), 4),
        ImporteImpuesto = t.ImporteNeto - ROUND(t.ImporteNeto / (1 + a.Alicuota), 4),
        CalidadImpuesto = 'DERIVADO',
        ImportesIncluyenImpuesto = 1,  -- Asumimos IVA incluido si derivamos
        FechaModificacion = GETDATE(),
        UsuarioModificacion = 'BACKFILL_V12'
    FROM fact.VentaTicket t
    INNER JOIN cfg.AlicuotaImpuestoFranquicia a
        ON t.FranquiciaId = a.FranquiciaId
        AND a.CategoriaProducto IS NULL  -- Alicuota default de la franquicia
        AND t.FechaNegocio >= a.VigenciaDesde
        AND (a.VigenciaHasta IS NULL OR t.FechaNegocio <= a.VigenciaHasta)
    WHERE t.CalidadImpuesto IS NULL
      AND (t.ImporteImpuesto IS NULL OR t.ImporteImpuesto = 0)

    DECLARE @TicketsDerivado INT = @@ROWCOUNT
    PRINT 'Tickets con CalidadImpuesto = DERIVADO (desde alicuota config): ' + CAST(@TicketsDerivado AS VARCHAR)
END
ELSE
BEGIN
    PRINT 'Tabla cfg.AlicuotaImpuestoFranquicia no existe, saltando derivacion.'
END

-- 1c. Resto de tickets -> DESCONOCIDO
UPDATE fact.VentaTicket
SET
    CalidadImpuesto = 'DESCONOCIDO',
    FechaModificacion = GETDATE(),
    UsuarioModificacion = 'BACKFILL_V12'
WHERE CalidadImpuesto IS NULL

DECLARE @TicketsDesconocido INT = @@ROWCOUNT
PRINT 'Tickets con CalidadImpuesto = DESCONOCIDO: ' + CAST(@TicketsDesconocido AS VARCHAR)
GO

-- ============================================================================
-- PASO 2: Backfill de CalidadImpuesto e ImporteNetoSinImpuesto en DETALLES
-- ============================================================================

PRINT ''
PRINT '--- PASO 2: Backfill fact.VentaTicketDetalle ---'

-- 2a. Detalles que ya tienen ImporteImpuesto > 0 -> CalidadImpuesto = 'POS'
UPDATE fact.VentaTicketDetalle
SET
    ImporteNetoSinImpuesto = ImporteNeto - ImporteImpuesto,
    CalidadImpuesto = 'POS',
    FechaModificacion = GETDATE(),
    UsuarioModificacion = 'BACKFILL_V12'
WHERE ImporteImpuesto > 0
  AND CalidadImpuesto IS NULL

DECLARE @DetallesPOS INT = @@ROWCOUNT
PRINT 'Detalles con CalidadImpuesto = POS (tenian ImporteImpuesto): ' + CAST(@DetallesPOS AS VARCHAR)

-- 2b. Detalles con alicuota en config por categoria -> DERIVADO
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'cfg' AND TABLE_NAME = 'AlicuotaImpuestoFranquicia')
BEGIN
    -- Primero intentar con alicuota por categoria de producto
    UPDATE d
    SET
        ImporteNetoSinImpuesto = ROUND(d.ImporteNeto / (1 + a.Alicuota), 4),
        TasaImpuesto = a.Alicuota,
        CalidadImpuesto = 'DERIVADO',
        ImportesIncluyenImpuesto = 1,
        FechaModificacion = GETDATE(),
        UsuarioModificacion = 'BACKFILL_V12'
    FROM fact.VentaTicketDetalle d
    INNER JOIN fact.VentaTicket t ON d.VentaTicketId = t.VentaTicketId
    INNER JOIN cfg.AlicuotaImpuestoFranquicia a
        ON d.FranquiciaId = a.FranquiciaId
        AND a.CategoriaProducto = d.CategoriaProducto
        AND t.FechaNegocio >= a.VigenciaDesde
        AND (a.VigenciaHasta IS NULL OR t.FechaNegocio <= a.VigenciaHasta)
    WHERE d.CalidadImpuesto IS NULL
      AND (d.ImporteImpuesto IS NULL OR d.ImporteImpuesto = 0)

    DECLARE @DetallesDerivadoCat INT = @@ROWCOUNT
    PRINT 'Detalles con CalidadImpuesto = DERIVADO (por categoria): ' + CAST(@DetallesDerivadoCat AS VARCHAR)

    -- Luego con alicuota default de franquicia
    UPDATE d
    SET
        ImporteNetoSinImpuesto = ROUND(d.ImporteNeto / (1 + a.Alicuota), 4),
        TasaImpuesto = a.Alicuota,
        CalidadImpuesto = 'DERIVADO',
        ImportesIncluyenImpuesto = 1,
        FechaModificacion = GETDATE(),
        UsuarioModificacion = 'BACKFILL_V12'
    FROM fact.VentaTicketDetalle d
    INNER JOIN fact.VentaTicket t ON d.VentaTicketId = t.VentaTicketId
    INNER JOIN cfg.AlicuotaImpuestoFranquicia a
        ON d.FranquiciaId = a.FranquiciaId
        AND a.CategoriaProducto IS NULL  -- Default
        AND t.FechaNegocio >= a.VigenciaDesde
        AND (a.VigenciaHasta IS NULL OR t.FechaNegocio <= a.VigenciaHasta)
    WHERE d.CalidadImpuesto IS NULL
      AND (d.ImporteImpuesto IS NULL OR d.ImporteImpuesto = 0)

    DECLARE @DetallesDerivadoDef INT = @@ROWCOUNT
    PRINT 'Detalles con CalidadImpuesto = DERIVADO (default franquicia): ' + CAST(@DetallesDerivadoDef AS VARCHAR)
END

-- 2c. Resto de detalles -> DESCONOCIDO
UPDATE fact.VentaTicketDetalle
SET
    CalidadImpuesto = 'DESCONOCIDO',
    FechaModificacion = GETDATE(),
    UsuarioModificacion = 'BACKFILL_V12'
WHERE CalidadImpuesto IS NULL

DECLARE @DetallesDesconocido INT = @@ROWCOUNT
PRINT 'Detalles con CalidadImpuesto = DESCONOCIDO: ' + CAST(@DetallesDesconocido AS VARCHAR)
GO

-- ============================================================================
-- PASO 3: Backfill de MealPeriod y MealPeriodOrigen
-- ============================================================================

PRINT ''
PRINT '--- PASO 3: Backfill MealPeriod ---'

-- 3a. Tickets que ya tienen PeriodoComida -> MealPeriodOrigen = 'POS'
UPDATE fact.VentaTicket
SET
    MealPeriodOrigen = 'POS',
    FechaModificacion = GETDATE(),
    UsuarioModificacion = 'BACKFILL_V12'
WHERE PeriodoComida IS NOT NULL
  AND MealPeriodOrigen IS NULL

DECLARE @MealPOS INT = @@ROWCOUNT
PRINT 'Tickets con MealPeriodOrigen = POS (ya tenian PeriodoComida): ' + CAST(@MealPOS AS VARCHAR)

-- 3b. Derivar MealPeriod de FechaApertura usando cfg.FranjaHorariaFranquicia
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'cfg' AND TABLE_NAME = 'FranjaHorariaFranquicia')
BEGIN
    UPDATE t
    SET
        PeriodoComida = f.MealPeriod,
        MealPeriodOrigen = 'DERIVADO',
        FechaModificacion = GETDATE(),
        UsuarioModificacion = 'BACKFILL_V12'
    FROM fact.VentaTicket t
    INNER JOIN cfg.FranjaHorariaFranquicia f
        ON t.FranquiciaId = f.FranquiciaId
        AND f.Activo = 1
        AND CAST(t.FechaApertura AS TIME) >= f.HoraDesde
        AND CAST(t.FechaApertura AS TIME) < f.HoraHasta
    WHERE t.PeriodoComida IS NULL
      AND t.MealPeriodOrigen IS NULL
      AND t.FechaApertura IS NOT NULL

    DECLARE @MealDerivado INT = @@ROWCOUNT
    PRINT 'Tickets con MealPeriod derivado de franja horaria: ' + CAST(@MealDerivado AS VARCHAR)
END
ELSE
BEGIN
    PRINT 'Tabla cfg.FranjaHorariaFranquicia no existe, saltando derivacion de MealPeriod.'
END
GO

-- ============================================================================
-- REPORTE FINAL
-- ============================================================================

PRINT ''
PRINT '--- REPORTE FINAL ---'

SELECT
    'fact.VentaTicket' AS Tabla,
    CalidadImpuesto,
    COUNT(*) AS Cantidad
FROM fact.VentaTicket
WHERE CalidadImpuesto IS NOT NULL
GROUP BY CalidadImpuesto
ORDER BY CalidadImpuesto

SELECT
    'fact.VentaTicketDetalle' AS Tabla,
    CalidadImpuesto,
    COUNT(*) AS Cantidad
FROM fact.VentaTicketDetalle
WHERE CalidadImpuesto IS NOT NULL
GROUP BY CalidadImpuesto
ORDER BY CalidadImpuesto

SELECT
    'fact.VentaTicket - MealPeriod' AS Tabla,
    MealPeriodOrigen,
    COUNT(*) AS Cantidad
FROM fact.VentaTicket
GROUP BY MealPeriodOrigen
ORDER BY MealPeriodOrigen

PRINT ''
PRINT '============================================'
PRINT 'Script 15 completado: Backfill v1.2 finalizado.'
PRINT '============================================'
GO
