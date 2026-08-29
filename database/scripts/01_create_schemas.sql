/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 01_create_schemas.sql
  Descripcion: Crea los esquemas de la base de datos
  Autor: Claude Code
  Fecha: 2026-07-16

  ESQUEMAS:
  - cfg: Configuracion del sistema
  - dim: Dimensiones (datos maestros)
  - fact: Hechos (transacciones de venta)
  - stg: Staging e ingesta cruda
  - api: Control de integraciones y API keys

  INSTRUCCIONES:
  - Ejecutar despues de 00_create_database.sql
  - Requiere permisos de creacion de esquemas
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- ESQUEMA: cfg (Configuracion)
-- Contiene parametros y configuraciones del sistema
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'cfg')
BEGIN
    EXEC('CREATE SCHEMA [cfg] AUTHORIZATION [dbo]')
    PRINT 'Esquema [cfg] creado.'
END
ELSE
    PRINT 'Esquema [cfg] ya existe.'
GO

-- ============================================================================
-- ESQUEMA: dim (Dimensiones)
-- Contiene datos maestros: franquicias, productos, mozos, etc.
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'dim')
BEGIN
    EXEC('CREATE SCHEMA [dim] AUTHORIZATION [dbo]')
    PRINT 'Esquema [dim] creado.'
END
ELSE
    PRINT 'Esquema [dim] ya existe.'
GO

-- ============================================================================
-- ESQUEMA: fact (Hechos)
-- Contiene transacciones: tickets, detalle de ventas, pagos
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'fact')
BEGIN
    EXEC('CREATE SCHEMA [fact] AUTHORIZATION [dbo]')
    PRINT 'Esquema [fact] creado.'
END
ELSE
    PRINT 'Esquema [fact] ya existe.'
GO

-- ============================================================================
-- ESQUEMA: stg (Staging)
-- Contiene datos de ingesta cruda, logs y errores
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'stg')
BEGIN
    EXEC('CREATE SCHEMA [stg] AUTHORIZATION [dbo]')
    PRINT 'Esquema [stg] creado.'
END
ELSE
    PRINT 'Esquema [stg] ya existe.'
GO

-- ============================================================================
-- ESQUEMA: api (API Control)
-- Contiene API keys, logs de integracion y control de acceso
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'api')
BEGIN
    EXEC('CREATE SCHEMA [api] AUTHORIZATION [dbo]')
    PRINT 'Esquema [api] creado.'
END
ELSE
    PRINT 'Esquema [api] ya existe.'
GO

PRINT 'Todos los esquemas han sido verificados/creados.'
GO
