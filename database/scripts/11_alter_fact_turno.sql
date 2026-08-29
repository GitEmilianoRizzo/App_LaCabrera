/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 11_alter_fact_turno.sql
  Descripcion: Agrega columnas para turno y franja horaria en fact.VentaTicket
  Autor: Claude Code
  Fecha: 2026-08-22
  Lote: MEJORAS_20260822 - Etapa 1.4

  CONCEPTOS DISTINTOS - NO MEZCLAR:
  ==================================
  1. TURNO (shift): Turno operativo de caja. Un cajero abre turno, trabaja, cierra.
     - TurnoCodigo: ID del turno en el POS
     - TurnoNombre: Nombre descriptivo
     - TurnoApertura / TurnoCierre: Horarios del turno

  2. MEAL PERIOD (periodo de comida): Almuerzo / Cena / Desayuno
     - MealPeriod: BREAKFAST | LUNCH | DINNER | LATE_NIGHT
     - MealPeriodOrigen: 'POS' (informado) | 'DERIVADO' (calculado por hora)

  El turno es dato operativo de caja. El meal period es concepto de negocio
  que permite analizar comportamiento de almuerzo vs cena.

  DERIVACION DE MEAL PERIOD:
  ==========================
  Si el POS no informa meal_period, se deriva de la hora de apertura del ticket
  usando cfg.FranjaHorariaFranquicia (script 12).
  El horario de almuerzo de Madrid NO es el de Miami, por eso es configurable
  por franquicia.

  INSTRUCCIONES:
  - Ejecutar despues de 10_alter_fact_eje_impuesto.sql
  - Idempotente: puede re-ejecutarse sin error
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- ALTER: fact.VentaTicket - Agregar columnas de turno
-- ============================================================================

-- TurnoCodigo
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'TurnoCodigo'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD TurnoCodigo VARCHAR(50) NULL

    PRINT 'Columna TurnoCodigo agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna TurnoCodigo ya existe en fact.VentaTicket'
GO

-- TurnoNombre
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'TurnoNombre'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD TurnoNombre NVARCHAR(100) NULL

    PRINT 'Columna TurnoNombre agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna TurnoNombre ya existe en fact.VentaTicket'
GO

-- TurnoApertura
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'TurnoApertura'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD TurnoApertura DATETIME2 NULL

    PRINT 'Columna TurnoApertura agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna TurnoApertura ya existe en fact.VentaTicket'
GO

-- TurnoCierre
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'TurnoCierre'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD TurnoCierre DATETIME2 NULL

    PRINT 'Columna TurnoCierre agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna TurnoCierre ya existe en fact.VentaTicket'
GO

-- MealPeriod (BREAKFAST, LUNCH, DINNER, LATE_NIGHT, UNKNOWN)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'MealPeriod'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD MealPeriod VARCHAR(20) NULL

    PRINT 'Columna MealPeriod agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna MealPeriod ya existe en fact.VentaTicket'
GO

-- MealPeriodOrigen (POS, DERIVADO)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'MealPeriodOrigen'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD MealPeriodOrigen VARCHAR(20) NULL

    PRINT 'Columna MealPeriodOrigen agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna MealPeriodOrigen ya existe en fact.VentaTicket'
GO

-- ============================================================================
-- CONSTRAINTS de validacion
-- ============================================================================

-- Constraint para MealPeriod
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_VentaTicket_MealPeriod' AND parent_object_id = OBJECT_ID('fact.VentaTicket')
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD CONSTRAINT CK_VentaTicket_MealPeriod
    CHECK (MealPeriod IS NULL OR MealPeriod IN ('BREAKFAST', 'LUNCH', 'DINNER', 'LATE_NIGHT', 'UNKNOWN'))

    PRINT 'Constraint CK_VentaTicket_MealPeriod creado'
END
GO

-- Constraint para MealPeriodOrigen
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_VentaTicket_MealPeriodOrigen' AND parent_object_id = OBJECT_ID('fact.VentaTicket')
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD CONSTRAINT CK_VentaTicket_MealPeriodOrigen
    CHECK (MealPeriodOrigen IS NULL OR MealPeriodOrigen IN ('POS', 'DERIVADO'))

    PRINT 'Constraint CK_VentaTicket_MealPeriodOrigen creado'
END
GO

-- ============================================================================
-- INDICE para consultas por turno/meal period
-- ============================================================================
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'IX_VentaTicket_MealPeriod' AND object_id = OBJECT_ID('fact.VentaTicket')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_VentaTicket_MealPeriod
    ON fact.VentaTicket (FranquiciaId, FechaNegocio, MealPeriod)
    INCLUDE (ImporteNeto, ImporteNetoSinImpuesto, CantidadCubiertos)

    PRINT 'Indice IX_VentaTicket_MealPeriod creado'
END
GO

-- ============================================================================
-- DOCUMENTACION: Comentarios en columnas
-- ============================================================================
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Codigo del turno operativo de caja en el POS origen',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'TurnoCodigo'
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Periodo de comida: BREAKFAST, LUNCH, DINNER, LATE_NIGHT. Para analisis de comportamiento por franja horaria.',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'MealPeriod'
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Origen del MealPeriod: POS=informado por el sistema, DERIVADO=calculado por hora usando cfg.FranjaHorariaFranquicia',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'MealPeriodOrigen'
GO

PRINT '=========================================='
PRINT 'Script 11 completado: Columnas de turno agregadas.'
PRINT 'NOTA: Los datos existentes quedan con NULL. El backfill es script 14.'
PRINT '=========================================='
GO
