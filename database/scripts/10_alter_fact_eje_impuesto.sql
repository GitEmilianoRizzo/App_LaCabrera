/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 10_alter_fact_eje_impuesto.sql
  Descripcion: Agrega columnas para el eje de impuesto en tablas de hechos
  Autor: Claude Code
  Fecha: 2026-08-22
  Lote: MEJORAS_20260822 - Etapa 1.3

  PROBLEMA QUE RESUELVE:
  ======================
  El contrato actual define net_amount = gross_amount - discount_amount, o sea
  el eje DESCUENTO. No dice nada sobre si esos importes incluyen o no impuesto.

  Eso es el eje IMPUESTO, que es ORTOGONAL al eje descuento:
  - En EEUU el precio de carta es SIN tax, el tax se suma al final
  - En Espana y Argentina el IVA va INCLUIDO en el precio

  Sin resolverlo, comparar Miami contra Marbella compara cosas distintas.
  El KPI comparable entre franquicias es NETO SIN IMPUESTO.

  COLUMNAS NUEVAS:
  ================
  - ImporteNetoSinImpuesto: El importe neto despues de descuentos Y sin impuesto
  - ImportesIncluyenImpuesto: Flag que indica si los importes origen incluian impuesto
  - CalidadImpuesto: 'POS' | 'DERIVADO' | 'DESCONOCIDO'

  CASCADA DE CALCULO (en ingesta):
  ================================
  1. Si viene net_amount_excl_tax en el JSON    -> CalidadImpuesto = 'POS'
  2. Si viene tax_amount por linea              -> CalidadImpuesto = 'POS'
  3. Si viene tax_rate                          -> CalidadImpuesto = 'POS'
  4. Si hay alicuota en cfg.AlicuotaImpuestoFranquicia -> CalidadImpuesto = 'DERIVADO'
  5. Si no hay nada                             -> CalidadImpuesto = 'DESCONOCIDO', NULL

  REGLA: Nunca inventar impuesto. Si no se puede determinar, DESCONOCIDO.

  NOTA: La columna ImporteImpuesto ya existe en las tablas, pero se renombra
  el uso conceptual para que sea consistente.

  INSTRUCCIONES:
  - Ejecutar despues de 09_create_tipocambio_tables.sql
  - Idempotente: puede re-ejecutarse sin error
  - NO hace backfill de datos existentes (eso es script 14)
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- ALTER: fact.VentaTicket - Agregar columnas de eje impuesto
-- ============================================================================

-- ImporteNetoSinImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'ImporteNetoSinImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD ImporteNetoSinImpuesto DECIMAL(18,4) NULL

    PRINT 'Columna ImporteNetoSinImpuesto agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna ImporteNetoSinImpuesto ya existe en fact.VentaTicket'
GO

-- ImportesIncluyenImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'ImportesIncluyenImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD ImportesIncluyenImpuesto BIT NULL

    PRINT 'Columna ImportesIncluyenImpuesto agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna ImportesIncluyenImpuesto ya existe en fact.VentaTicket'
GO

-- CalidadImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicket') AND name = 'CalidadImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD CalidadImpuesto VARCHAR(20) NULL

    PRINT 'Columna CalidadImpuesto agregada a fact.VentaTicket'
END
ELSE
    PRINT 'Columna CalidadImpuesto ya existe en fact.VentaTicket'
GO

-- Constraint para validar valores de CalidadImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_VentaTicket_CalidadImpuesto' AND parent_object_id = OBJECT_ID('fact.VentaTicket')
)
BEGIN
    ALTER TABLE fact.VentaTicket
    ADD CONSTRAINT CK_VentaTicket_CalidadImpuesto
    CHECK (CalidadImpuesto IS NULL OR CalidadImpuesto IN ('POS', 'DERIVADO', 'DESCONOCIDO'))

    PRINT 'Constraint CK_VentaTicket_CalidadImpuesto creado'
END
GO

-- ============================================================================
-- ALTER: fact.VentaTicketDetalle - Agregar columnas de eje impuesto
-- ============================================================================

-- ImporteNetoSinImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicketDetalle') AND name = 'ImporteNetoSinImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicketDetalle
    ADD ImporteNetoSinImpuesto DECIMAL(18,4) NULL

    PRINT 'Columna ImporteNetoSinImpuesto agregada a fact.VentaTicketDetalle'
END
ELSE
    PRINT 'Columna ImporteNetoSinImpuesto ya existe en fact.VentaTicketDetalle'
GO

-- ImportesIncluyenImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicketDetalle') AND name = 'ImportesIncluyenImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicketDetalle
    ADD ImportesIncluyenImpuesto BIT NULL

    PRINT 'Columna ImportesIncluyenImpuesto agregada a fact.VentaTicketDetalle'
END
ELSE
    PRINT 'Columna ImportesIncluyenImpuesto ya existe en fact.VentaTicketDetalle'
GO

-- CalidadImpuesto
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicketDetalle') AND name = 'CalidadImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicketDetalle
    ADD CalidadImpuesto VARCHAR(20) NULL

    PRINT 'Columna CalidadImpuesto agregada a fact.VentaTicketDetalle'
END
ELSE
    PRINT 'Columna CalidadImpuesto ya existe en fact.VentaTicketDetalle'
GO

-- TasaImpuesto (alicuota usada para el calculo)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('fact.VentaTicketDetalle') AND name = 'TasaImpuesto'
)
BEGIN
    ALTER TABLE fact.VentaTicketDetalle
    ADD TasaImpuesto DECIMAL(9,6) NULL  -- Ej: 0.21 para 21%

    PRINT 'Columna TasaImpuesto agregada a fact.VentaTicketDetalle'
END
ELSE
    PRINT 'Columna TasaImpuesto ya existe en fact.VentaTicketDetalle'
GO

-- Constraint para validar valores de CalidadImpuesto en Detalle
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_VentaTicketDetalle_CalidadImpuesto' AND parent_object_id = OBJECT_ID('fact.VentaTicketDetalle')
)
BEGIN
    ALTER TABLE fact.VentaTicketDetalle
    ADD CONSTRAINT CK_VentaTicketDetalle_CalidadImpuesto
    CHECK (CalidadImpuesto IS NULL OR CalidadImpuesto IN ('POS', 'DERIVADO', 'DESCONOCIDO'))

    PRINT 'Constraint CK_VentaTicketDetalle_CalidadImpuesto creado'
END
GO

-- ============================================================================
-- DOCUMENTACION: Comentarios en columnas
-- ============================================================================
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Importe neto despues de descuentos Y sin impuesto. Este es el KPI comparable entre franquicias.',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'ImporteNetoSinImpuesto'
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Indica si los importes originales del POS incluian impuesto (1=si, 0=no, NULL=desconocido)',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'ImportesIncluyenImpuesto'
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Origen del dato de impuesto: POS=informado por el sistema, DERIVADO=calculado con alicuota configurada, DESCONOCIDO=no se pudo determinar',
    @level0type = N'SCHEMA', @level0name = N'fact',
    @level1type = N'TABLE',  @level1name = N'VentaTicket',
    @level2type = N'COLUMN', @level2name = N'CalidadImpuesto'
GO

PRINT '=========================================='
PRINT 'Script 10 completado: Columnas de eje impuesto agregadas.'
PRINT 'NOTA: Los datos existentes quedan con NULL. El backfill es script 14.'
PRINT '=========================================='
GO
