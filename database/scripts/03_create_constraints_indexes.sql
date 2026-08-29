/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 03_create_constraints_indexes.sql
  Descripcion: Crea PKs, FKs, constraints unicos e indices
  Autor: Claude Code
  Fecha: 2026-07-16

  INSTRUCCIONES:
  - Ejecutar despues de 02_create_tables.sql
  - Los constraints de unicidad garantizan idempotencia
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- SECCION 1: CLAVES PRIMARIAS
-- ============================================================================

PRINT 'Creando claves primarias...'

-- cfg.Parametro
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Parametro')
    ALTER TABLE [cfg].[Parametro] ADD CONSTRAINT PK_Parametro PRIMARY KEY (ParametroId)
GO

-- dim.Moneda
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Moneda')
    ALTER TABLE [dim].[Moneda] ADD CONSTRAINT PK_Moneda PRIMARY KEY (MonedaId)
GO

-- dim.GrupoEconomico
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_GrupoEconomico')
    ALTER TABLE [dim].[GrupoEconomico] ADD CONSTRAINT PK_GrupoEconomico PRIMARY KEY (GrupoEconomicoId)
GO

-- dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Franquicia')
    ALTER TABLE [dim].[Franquicia] ADD CONSTRAINT PK_Franquicia PRIMARY KEY (FranquiciaId)
GO

-- dim.TipoPlato
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_TipoPlato')
    ALTER TABLE [dim].[TipoPlato] ADD CONSTRAINT PK_TipoPlato PRIMARY KEY (TipoPlatoId)
GO

-- dim.Producto
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Producto')
    ALTER TABLE [dim].[Producto] ADD CONSTRAINT PK_Producto PRIMARY KEY (ProductoId)
GO

-- dim.Mozo
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Mozo')
    ALTER TABLE [dim].[Mozo] ADD CONSTRAINT PK_Mozo PRIMARY KEY (MozoId)
GO

-- dim.Mesa
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_Mesa')
    ALTER TABLE [dim].[Mesa] ADD CONSTRAINT PK_Mesa PRIMARY KEY (MesaId)
GO

-- dim.MedioPago
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_MedioPago')
    ALTER TABLE [dim].[MedioPago] ADD CONSTRAINT PK_MedioPago PRIMARY KEY (MedioPagoId)
GO

-- dim.PeriodoComida
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_PeriodoComida')
    ALTER TABLE [dim].[PeriodoComida] ADD CONSTRAINT PK_PeriodoComida PRIMARY KEY (PeriodoComidaId)
GO

-- stg.IngestionBatch
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_IngestionBatch')
    ALTER TABLE [stg].[IngestionBatch] ADD CONSTRAINT PK_IngestionBatch PRIMARY KEY (IngestionBatchId)
GO

-- stg.IngestionBatchRawJson
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_IngestionBatchRawJson')
    ALTER TABLE [stg].[IngestionBatchRawJson] ADD CONSTRAINT PK_IngestionBatchRawJson PRIMARY KEY (IngestionBatchRawJsonId)
GO

-- stg.IngestionError
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_IngestionError')
    ALTER TABLE [stg].[IngestionError] ADD CONSTRAINT PK_IngestionError PRIMARY KEY (IngestionErrorId)
GO

-- stg.ApiIngestaLog
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_ApiIngestaLog')
    ALTER TABLE [stg].[ApiIngestaLog] ADD CONSTRAINT PK_ApiIngestaLog PRIMARY KEY (ApiIngestaLogId)
GO

-- api.ApiKeyFranquicia
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_ApiKeyFranquicia')
    ALTER TABLE [api].[ApiKeyFranquicia] ADD CONSTRAINT PK_ApiKeyFranquicia PRIMARY KEY (ApiKeyFranquiciaId)
GO

-- fact.VentaTicket
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_VentaTicket')
    ALTER TABLE [fact].[VentaTicket] ADD CONSTRAINT PK_VentaTicket PRIMARY KEY (VentaTicketId)
GO

-- fact.VentaTicketDetalle
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_VentaTicketDetalle')
    ALTER TABLE [fact].[VentaTicketDetalle] ADD CONSTRAINT PK_VentaTicketDetalle PRIMARY KEY (VentaTicketDetalleId)
GO

-- fact.VentaTicketDescuento
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_VentaTicketDescuento')
    ALTER TABLE [fact].[VentaTicketDescuento] ADD CONSTRAINT PK_VentaTicketDescuento PRIMARY KEY (VentaTicketDescuentoId)
GO

-- fact.VentaTicketMedioPago
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_VentaTicketMedioPago')
    ALTER TABLE [fact].[VentaTicketMedioPago] ADD CONSTRAINT PK_VentaTicketMedioPago PRIMARY KEY (VentaTicketMedioPagoId)
GO

-- ============================================================================
-- SECCION 2: CONSTRAINTS UNICOS (Idempotencia)
-- ============================================================================

PRINT 'Creando constraints unicos para idempotencia...'

-- Moneda: codigo ISO unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Moneda_CodigoISO')
    ALTER TABLE [dim].[Moneda] ADD CONSTRAINT UQ_Moneda_CodigoISO UNIQUE (CodigoISO)
GO

-- GrupoEconomico: codigo unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_GrupoEconomico_Codigo')
    ALTER TABLE [dim].[GrupoEconomico] ADD CONSTRAINT UQ_GrupoEconomico_Codigo UNIQUE (Codigo)
GO

-- Franquicia: codigo unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Franquicia_Codigo')
    ALTER TABLE [dim].[Franquicia] ADD CONSTRAINT UQ_Franquicia_Codigo UNIQUE (Codigo)
GO

-- TipoPlato: codigo unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_TipoPlato_Codigo')
    ALTER TABLE [dim].[TipoPlato] ADD CONSTRAINT UQ_TipoPlato_Codigo UNIQUE (Codigo)
GO

-- Producto: ExternalId unico por franquicia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Producto_Franquicia_ExternalId')
    ALTER TABLE [dim].[Producto] ADD CONSTRAINT UQ_Producto_Franquicia_ExternalId UNIQUE (FranquiciaId, ExternalId)
GO

-- Mozo: ExternalId unico por franquicia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Mozo_Franquicia_ExternalId')
    ALTER TABLE [dim].[Mozo] ADD CONSTRAINT UQ_Mozo_Franquicia_ExternalId UNIQUE (FranquiciaId, ExternalId)
GO

-- Mesa: numero unico por franquicia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Mesa_Franquicia_Numero')
    ALTER TABLE [dim].[Mesa] ADD CONSTRAINT UQ_Mesa_Franquicia_Numero UNIQUE (FranquiciaId, NumeroMesa)
GO

-- MedioPago: codigo unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_MedioPago_Codigo')
    ALTER TABLE [dim].[MedioPago] ADD CONSTRAINT UQ_MedioPago_Codigo UNIQUE (Codigo)
GO

-- PeriodoComida: codigo unico
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_PeriodoComida_Codigo')
    ALTER TABLE [dim].[PeriodoComida] ADD CONSTRAINT UQ_PeriodoComida_Codigo UNIQUE (Codigo)
GO

-- IngestionBatch: BatchId unico por franquicia (IDEMPOTENCIA CLAVE)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_IngestionBatch_Franquicia_BatchId')
    ALTER TABLE [stg].[IngestionBatch] ADD CONSTRAINT UQ_IngestionBatch_Franquicia_BatchId UNIQUE (FranquiciaId, BatchId)
GO

-- ApiKey: unica
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_ApiKeyFranquicia_ApiKey')
    ALTER TABLE [api].[ApiKeyFranquicia] ADD CONSTRAINT UQ_ApiKeyFranquicia_ApiKey UNIQUE (ApiKey)
GO

-- VentaTicket: ExternalTicketId unico por franquicia (IDEMPOTENCIA CLAVE)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_VentaTicket_Franquicia_ExternalTicketId')
    ALTER TABLE [fact].[VentaTicket] ADD CONSTRAINT UQ_VentaTicket_Franquicia_ExternalTicketId UNIQUE (FranquiciaId, ExternalTicketId)
GO

-- VentaTicketDetalle: LineId unico por ticket (IDEMPOTENCIA CLAVE)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_VentaTicketDetalle_Ticket_LineId')
    ALTER TABLE [fact].[VentaTicketDetalle] ADD CONSTRAINT UQ_VentaTicketDetalle_Ticket_LineId UNIQUE (VentaTicketId, ExternalLineId)
GO

-- cfg.Parametro: clave unica
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_Parametro_Clave')
    ALTER TABLE [cfg].[Parametro] ADD CONSTRAINT UQ_Parametro_Clave UNIQUE (Clave)
GO

-- ============================================================================
-- SECCION 3: CLAVES FORANEAS
-- ============================================================================

PRINT 'Creando claves foraneas...'

-- dim.Franquicia -> dim.GrupoEconomico
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Franquicia_GrupoEconomico')
    ALTER TABLE [dim].[Franquicia]
    ADD CONSTRAINT FK_Franquicia_GrupoEconomico
    FOREIGN KEY (GrupoEconomicoId) REFERENCES [dim].[GrupoEconomico](GrupoEconomicoId)
GO

-- dim.Franquicia -> dim.Moneda
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Franquicia_Moneda')
    ALTER TABLE [dim].[Franquicia]
    ADD CONSTRAINT FK_Franquicia_Moneda
    FOREIGN KEY (MonedaId) REFERENCES [dim].[Moneda](MonedaId)
GO

-- dim.Producto -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Producto_Franquicia')
    ALTER TABLE [dim].[Producto]
    ADD CONSTRAINT FK_Producto_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- dim.Producto -> dim.TipoPlato
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Producto_TipoPlato')
    ALTER TABLE [dim].[Producto]
    ADD CONSTRAINT FK_Producto_TipoPlato
    FOREIGN KEY (TipoPlatoId) REFERENCES [dim].[TipoPlato](TipoPlatoId)
GO

-- dim.Mozo -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Mozo_Franquicia')
    ALTER TABLE [dim].[Mozo]
    ADD CONSTRAINT FK_Mozo_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- dim.Mesa -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Mesa_Franquicia')
    ALTER TABLE [dim].[Mesa]
    ADD CONSTRAINT FK_Mesa_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- stg.IngestionBatch -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IngestionBatch_Franquicia')
    ALTER TABLE [stg].[IngestionBatch]
    ADD CONSTRAINT FK_IngestionBatch_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- stg.IngestionBatchRawJson -> stg.IngestionBatch
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IngestionBatchRawJson_Batch')
    ALTER TABLE [stg].[IngestionBatchRawJson]
    ADD CONSTRAINT FK_IngestionBatchRawJson_Batch
    FOREIGN KEY (IngestionBatchId) REFERENCES [stg].[IngestionBatch](IngestionBatchId)
GO

-- stg.IngestionError -> stg.IngestionBatch
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IngestionError_Batch')
    ALTER TABLE [stg].[IngestionError]
    ADD CONSTRAINT FK_IngestionError_Batch
    FOREIGN KEY (IngestionBatchId) REFERENCES [stg].[IngestionBatch](IngestionBatchId)
GO

-- api.ApiKeyFranquicia -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ApiKeyFranquicia_Franquicia')
    ALTER TABLE [api].[ApiKeyFranquicia]
    ADD CONSTRAINT FK_ApiKeyFranquicia_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- fact.VentaTicket -> stg.IngestionBatch
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicket_IngestionBatch')
    ALTER TABLE [fact].[VentaTicket]
    ADD CONSTRAINT FK_VentaTicket_IngestionBatch
    FOREIGN KEY (IngestionBatchId) REFERENCES [stg].[IngestionBatch](IngestionBatchId)
GO

-- fact.VentaTicket -> dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicket_Franquicia')
    ALTER TABLE [fact].[VentaTicket]
    ADD CONSTRAINT FK_VentaTicket_Franquicia
    FOREIGN KEY (FranquiciaId) REFERENCES [dim].[Franquicia](FranquiciaId)
GO

-- fact.VentaTicket -> dim.Mesa
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicket_Mesa')
    ALTER TABLE [fact].[VentaTicket]
    ADD CONSTRAINT FK_VentaTicket_Mesa
    FOREIGN KEY (MesaId) REFERENCES [dim].[Mesa](MesaId)
GO

-- fact.VentaTicket -> dim.Mozo
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicket_Mozo')
    ALTER TABLE [fact].[VentaTicket]
    ADD CONSTRAINT FK_VentaTicket_Mozo
    FOREIGN KEY (MozoId) REFERENCES [dim].[Mozo](MozoId)
GO

-- fact.VentaTicket -> dim.Moneda
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicket_Moneda')
    ALTER TABLE [fact].[VentaTicket]
    ADD CONSTRAINT FK_VentaTicket_Moneda
    FOREIGN KEY (MonedaId) REFERENCES [dim].[Moneda](MonedaId)
GO

-- fact.VentaTicketDetalle -> fact.VentaTicket
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketDetalle_Ticket')
    ALTER TABLE [fact].[VentaTicketDetalle]
    ADD CONSTRAINT FK_VentaTicketDetalle_Ticket
    FOREIGN KEY (VentaTicketId) REFERENCES [fact].[VentaTicket](VentaTicketId)
GO

-- fact.VentaTicketDetalle -> dim.Producto
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketDetalle_Producto')
    ALTER TABLE [fact].[VentaTicketDetalle]
    ADD CONSTRAINT FK_VentaTicketDetalle_Producto
    FOREIGN KEY (ProductoId) REFERENCES [dim].[Producto](ProductoId)
GO

-- fact.VentaTicketDetalle -> dim.TipoPlato
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketDetalle_TipoPlato')
    ALTER TABLE [fact].[VentaTicketDetalle]
    ADD CONSTRAINT FK_VentaTicketDetalle_TipoPlato
    FOREIGN KEY (TipoPlatoId) REFERENCES [dim].[TipoPlato](TipoPlatoId)
GO

-- fact.VentaTicketDescuento -> fact.VentaTicket
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketDescuento_Ticket')
    ALTER TABLE [fact].[VentaTicketDescuento]
    ADD CONSTRAINT FK_VentaTicketDescuento_Ticket
    FOREIGN KEY (VentaTicketId) REFERENCES [fact].[VentaTicket](VentaTicketId)
GO

-- fact.VentaTicketMedioPago -> fact.VentaTicket
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketMedioPago_Ticket')
    ALTER TABLE [fact].[VentaTicketMedioPago]
    ADD CONSTRAINT FK_VentaTicketMedioPago_Ticket
    FOREIGN KEY (VentaTicketId) REFERENCES [fact].[VentaTicket](VentaTicketId)
GO

-- fact.VentaTicketMedioPago -> dim.MedioPago
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VentaTicketMedioPago_MedioPago')
    ALTER TABLE [fact].[VentaTicketMedioPago]
    ADD CONSTRAINT FK_VentaTicketMedioPago_MedioPago
    FOREIGN KEY (MedioPagoId) REFERENCES [dim].[MedioPago](MedioPagoId)
GO

-- ============================================================================
-- SECCION 4: INDICES PARA RENDIMIENTO
-- ============================================================================

PRINT 'Creando indices para rendimiento...'

-- Indices en fact.VentaTicket
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicket_FechaNegocio')
    CREATE NONCLUSTERED INDEX IX_VentaTicket_FechaNegocio
    ON [fact].[VentaTicket] (FechaNegocio)
    INCLUDE (FranquiciaId, ImporteBruto, ImporteDescuento, ImporteNeto, CantidadCubiertos)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicket_Franquicia_FechaNegocio')
    CREATE NONCLUSTERED INDEX IX_VentaTicket_Franquicia_FechaNegocio
    ON [fact].[VentaTicket] (FranquiciaId, FechaNegocio)
    INCLUDE (ImporteBruto, ImporteDescuento, ImporteNeto, CantidadCubiertos, Estado)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicket_MozoId')
    CREATE NONCLUSTERED INDEX IX_VentaTicket_MozoId
    ON [fact].[VentaTicket] (MozoId)
    INCLUDE (FechaNegocio, ImporteNeto)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicket_MesaId')
    CREATE NONCLUSTERED INDEX IX_VentaTicket_MesaId
    ON [fact].[VentaTicket] (MesaId)
    INCLUDE (FechaNegocio, FechaApertura, FechaCierre, TiempoConsumoMinutos)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicket_IngestionBatchId')
    CREATE NONCLUSTERED INDEX IX_VentaTicket_IngestionBatchId
    ON [fact].[VentaTicket] (IngestionBatchId)
GO

-- Indices en fact.VentaTicketDetalle
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicketDetalle_VentaTicketId')
    CREATE NONCLUSTERED INDEX IX_VentaTicketDetalle_VentaTicketId
    ON [fact].[VentaTicketDetalle] (VentaTicketId)
    INCLUDE (ProductoId, Cantidad, ImporteNeto)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicketDetalle_ProductoId')
    CREATE NONCLUSTERED INDEX IX_VentaTicketDetalle_ProductoId
    ON [fact].[VentaTicketDetalle] (ProductoId)
    INCLUDE (Cantidad, ImporteNeto)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VentaTicketDetalle_TipoPlatoId')
    CREATE NONCLUSTERED INDEX IX_VentaTicketDetalle_TipoPlatoId
    ON [fact].[VentaTicketDetalle] (TipoPlatoId)
    INCLUDE (FranquiciaId, Cantidad, ImporteNeto)
GO

-- Indices en stg.IngestionBatch
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IngestionBatch_Estado')
    CREATE NONCLUSTERED INDEX IX_IngestionBatch_Estado
    ON [stg].[IngestionBatch] (Estado)
    INCLUDE (FranquiciaId, FechaNegocio, FechaRecepcion)
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IngestionBatch_Franquicia_FechaNegocio')
    CREATE NONCLUSTERED INDEX IX_IngestionBatch_Franquicia_FechaNegocio
    ON [stg].[IngestionBatch] (FranquiciaId, FechaNegocio)
GO

-- Indices en dim.Franquicia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Franquicia_GrupoEconomico')
    CREATE NONCLUSTERED INDEX IX_Franquicia_GrupoEconomico
    ON [dim].[Franquicia] (GrupoEconomicoId)
    INCLUDE (Codigo, Nombre, Pais, Ciudad)
GO

-- Indices en api.ApiKeyFranquicia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ApiKeyFranquicia_FranquiciaId')
    CREATE NONCLUSTERED INDEX IX_ApiKeyFranquicia_FranquiciaId
    ON [api].[ApiKeyFranquicia] (FranquiciaId)
    WHERE Activo = 1
GO

PRINT '============================================'
PRINT 'Todos los constraints e indices han sido creados.'
PRINT '============================================'
GO
