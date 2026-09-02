/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 025_crear_tabla_parsers.sql
  Descripcion: Crea tabla cfg.Parser para registro de parsers de archivos
  Autor: Claude Code
  Fecha: 2026-08-30

  TABLAS:
  - cfg.Parser: Registro de parsers disponibles (toast_parser, etc.)

  MODIFICACIONES:
  - Agrega columna ParserId a dim.NodoConexion para asociar parser a nodos TXT_PARSER

================================================================================
*/

USE [LaCabreraDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ============================================================================
-- TABLA: cfg.Parser (si no existe)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[cfg].[Parser]') AND type = 'U')
BEGIN
    CREATE TABLE [cfg].[Parser] (
        ParserId                INT IDENTITY(1,1) PRIMARY KEY,
        Codigo                  NVARCHAR(50) NOT NULL UNIQUE,         -- 'TOAST_PARSER'
        Nombre                  NVARCHAR(200) NOT NULL,
        Descripcion             NVARCHAR(500) NULL,
        ArchivoScript           NVARCHAR(200) NOT NULL,               -- 'toast_parser.py'
        ExtensionesPermitidas   NVARCHAR(100) NOT NULL DEFAULT '.txt',
        PaisesAplica            NVARCHAR(200) NULL,                   -- NULL = todos, o 'US,MX'
        MonedaDefault           NVARCHAR(3) NOT NULL DEFAULT 'USD',
        TimezoneDefault         NVARCHAR(100) NOT NULL DEFAULT 'America/New_York',
        Activo                  BIT NOT NULL DEFAULT 1,
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion       DATETIME2 NULL
    )

    PRINT 'Tabla [cfg].[Parser] creada.'
END
GO

-- Crear indices por separado con SET options correctos
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Parser_Codigo' AND object_id = OBJECT_ID(N'[cfg].[Parser]'))
BEGIN
    CREATE INDEX IX_Parser_Codigo ON [cfg].[Parser](Codigo)
    PRINT 'Indice IX_Parser_Codigo creado.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Parser_Activo' AND object_id = OBJECT_ID(N'[cfg].[Parser]'))
BEGIN
    CREATE INDEX IX_Parser_Activo ON [cfg].[Parser](Activo)
    PRINT 'Indice IX_Parser_Activo creado.'
END
GO

-- ============================================================================
-- DATOS INICIALES: Toast Parser
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [cfg].[Parser] WHERE Codigo = 'TOAST_PARSER')
BEGIN
    INSERT INTO [cfg].[Parser] (Codigo, Nombre, Descripcion, ArchivoScript, ExtensionesPermitidas, PaisesAplica, MonedaDefault, TimezoneDefault)
    VALUES (
        'TOAST_PARSER',
        'Toast POS Parser',
        'Convierte reportes de Toast POS (copiados desde la web) al formato JSON de ingesta',
        'toast_parser.py',
        '.txt',
        'US',
        'USD',
        'America/New_York'
    )

    PRINT 'Parser TOAST_PARSER insertado.'
END
GO

-- ============================================================================
-- MODIFICAR dim.NodoConexion para agregar ParserId (opcional)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dim].[NodoConexion]') AND name = 'ParserId')
BEGIN
    ALTER TABLE [dim].[NodoConexion]
    ADD ParserId INT NULL

    PRINT 'Columna ParserId agregada a dim.NodoConexion.'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_NodoConexion_Parser')
BEGIN
    ALTER TABLE [dim].[NodoConexion]
    ADD CONSTRAINT FK_NodoConexion_Parser FOREIGN KEY (ParserId) REFERENCES [cfg].[Parser](ParserId)

    PRINT 'FK a cfg.Parser creada.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NodoConexion_ParserId' AND object_id = OBJECT_ID(N'[dim].[NodoConexion]'))
BEGIN
    CREATE INDEX IX_NodoConexion_ParserId ON [dim].[NodoConexion](ParserId)
    PRINT 'Indice IX_NodoConexion_ParserId creado.'
END
GO

PRINT '========================================'
PRINT 'Migracion 025_crear_tabla_parsers completada.'
PRINT '========================================'
GO
