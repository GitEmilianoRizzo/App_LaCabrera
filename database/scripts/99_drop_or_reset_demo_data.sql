/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 99_drop_or_reset_demo_data.sql
  Descripcion: Limpia datos de prueba sin eliminar estructura
  Autor: Claude Code
  Fecha: 2026-07-16

  OPCIONES:
  1. Limpiar solo datos demo (mantiene estructura)
  2. Limpiar todos los datos (mantiene estructura)
  3. Eliminar todo (incluye estructura) - PELIGROSO

  INSTRUCCIONES:
  - Seleccionar la opcion deseada descomentando el bloque correspondiente
  - Por defecto, solo limpia datos demo
================================================================================
*/

USE [LaCabreraDB]
GO

PRINT '============================================'
PRINT 'Script de limpieza de datos demo'
PRINT '============================================'

-- ============================================================================
-- OPCION 1: LIMPIAR SOLO DATOS DEMO (Por defecto)
-- Elimina batches con OrigenIngesta = 'SEED' y datos asociados
-- ============================================================================

PRINT 'Opcion 1: Limpiando datos demo (OrigenIngesta = SEED)...'

-- Obtener IDs de batches demo
DECLARE @BatchesDemo TABLE (IngestionBatchId BIGINT)
INSERT INTO @BatchesDemo
SELECT IngestionBatchId FROM [stg].[IngestionBatch] WHERE OrigenIngesta = 'SEED'

-- Eliminar en orden de dependencias
DELETE FROM [fact].[VentaTicketMedioPago]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - VentaTicketMedioPago limpiada'

DELETE FROM [fact].[VentaTicketDescuento]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - VentaTicketDescuento limpiada'

DELETE FROM [fact].[VentaTicketDetalle]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - VentaTicketDetalle limpiada'

DELETE FROM [fact].[VentaTicket]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - VentaTicket limpiada'

DELETE FROM [stg].[IngestionError]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - IngestionError limpiada'

DELETE FROM [stg].[IngestionBatchRawJson]
WHERE IngestionBatchId IN (SELECT IngestionBatchId FROM @BatchesDemo)
PRINT '  - IngestionBatchRawJson limpiada'

DELETE FROM [stg].[IngestionBatch]
WHERE OrigenIngesta = 'SEED'
PRINT '  - IngestionBatch (SEED) limpiada'

PRINT 'Datos demo eliminados. Estructura y datos maestros intactos.'
GO

/*
-- ============================================================================
-- OPCION 2: LIMPIAR TODOS LOS DATOS TRANSACCIONALES
-- Mantiene datos maestros (franquicias, productos, etc.)
-- DESCOMENTAR PARA USAR
-- ============================================================================

PRINT 'Opcion 2: Limpiando TODOS los datos transaccionales...'

-- Desactivar constraints temporalmente
ALTER TABLE [fact].[VentaTicketMedioPago] NOCHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicketDescuento] NOCHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicketDetalle] NOCHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicket] NOCHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionError] NOCHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionBatchRawJson] NOCHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionBatch] NOCHECK CONSTRAINT ALL
ALTER TABLE [stg].[ApiIngestaLog] NOCHECK CONSTRAINT ALL

-- Truncar tablas de hechos
TRUNCATE TABLE [fact].[VentaTicketMedioPago]
TRUNCATE TABLE [fact].[VentaTicketDescuento]
TRUNCATE TABLE [fact].[VentaTicketDetalle]
TRUNCATE TABLE [fact].[VentaTicket]

-- Truncar tablas de staging
TRUNCATE TABLE [stg].[IngestionError]
TRUNCATE TABLE [stg].[IngestionBatchRawJson]
TRUNCATE TABLE [stg].[IngestionBatch]
TRUNCATE TABLE [stg].[ApiIngestaLog]

-- Reactivar constraints
ALTER TABLE [fact].[VentaTicketMedioPago] CHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicketDescuento] CHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicketDetalle] CHECK CONSTRAINT ALL
ALTER TABLE [fact].[VentaTicket] CHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionError] CHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionBatchRawJson] CHECK CONSTRAINT ALL
ALTER TABLE [stg].[IngestionBatch] CHECK CONSTRAINT ALL
ALTER TABLE [stg].[ApiIngestaLog] CHECK CONSTRAINT ALL

-- Resetear ultima sincronizacion en franquicias
UPDATE [dim].[Franquicia] SET UltimaSincronizacion = NULL, EstadoIntegracion = 'PENDING'

PRINT 'Todos los datos transaccionales eliminados.'
GO
*/

/*
-- ============================================================================
-- OPCION 3: ELIMINAR TODO INCLUYENDO DATOS MAESTROS
-- PELIGROSO - Solo para resetear completamente la demo
-- DESCOMENTAR PARA USAR
-- ============================================================================

PRINT 'Opcion 3: Eliminando TODOS los datos (incluyendo maestros)...'

-- Desactivar constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'

-- Eliminar en orden
DELETE FROM [fact].[VentaTicketMedioPago]
DELETE FROM [fact].[VentaTicketDescuento]
DELETE FROM [fact].[VentaTicketDetalle]
DELETE FROM [fact].[VentaTicket]
DELETE FROM [stg].[IngestionError]
DELETE FROM [stg].[IngestionBatchRawJson]
DELETE FROM [stg].[IngestionBatch]
DELETE FROM [stg].[ApiIngestaLog]
DELETE FROM [api].[ApiKeyFranquicia]
DELETE FROM [dim].[Producto]
DELETE FROM [dim].[Mozo]
DELETE FROM [dim].[Mesa]
DELETE FROM [dim].[Franquicia]
DELETE FROM [dim].[GrupoEconomico]
DELETE FROM [dim].[TipoPlato]
DELETE FROM [dim].[MedioPago]
DELETE FROM [dim].[PeriodoComida]
DELETE FROM [dim].[Moneda]
DELETE FROM [cfg].[Parametro]

-- Reactivar constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL'

-- Resetear identity seeds
DBCC CHECKIDENT ('[fact].[VentaTicketMedioPago]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicketDescuento]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicketDetalle]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicket]', RESEED, 0)
DBCC CHECKIDENT ('[stg].[IngestionError]', RESEED, 0)
DBCC CHECKIDENT ('[stg].[IngestionBatchRawJson]', RESEED, 0)
DBCC CHECKIDENT ('[stg].[IngestionBatch]', RESEED, 0)
DBCC CHECKIDENT ('[stg].[ApiIngestaLog]', RESEED, 0)
DBCC CHECKIDENT ('[api].[ApiKeyFranquicia]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[Producto]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[Mozo]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[Mesa]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[Franquicia]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[GrupoEconomico]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[TipoPlato]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[MedioPago]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[PeriodoComida]', RESEED, 0)
DBCC CHECKIDENT ('[dim].[Moneda]', RESEED, 0)
DBCC CHECKIDENT ('[cfg].[Parametro]', RESEED, 0)

PRINT 'Base de datos completamente limpia. Ejecutar 06_seed_demo_data.sql para recargar.'
GO
*/

/*
-- ============================================================================
-- OPCION 4: ELIMINAR BASE DE DATOS COMPLETA
-- MUY PELIGROSO - Elimina la base de datos entera
-- DESCOMENTAR PARA USAR
-- ============================================================================

USE [master]
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'LaCabreraDB')
BEGIN
    ALTER DATABASE [LaCabreraDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
    DROP DATABASE [LaCabreraDB]
    PRINT 'Base de datos LaCabreraDB eliminada.'
END
GO
*/

PRINT '============================================'
PRINT 'Limpieza completada.'
PRINT '============================================'
GO
