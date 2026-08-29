/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 05_create_stored_procedures_ingestion.sql
  Descripcion: Stored procedures para ingesta y procesamiento de datos
  Autor: Claude Code
  Fecha: 2026-07-16

  STORED PROCEDURES:
  - sp_RegistrarBatchIngesta: Registra un nuevo batch
  - sp_GuardarJsonCrudo: Guarda el JSON original
  - sp_ValidarBatch: Valida el batch recibido
  - sp_InsertarActualizarMozo: Inserta o actualiza mozo
  - sp_InsertarActualizarProducto: Inserta o actualiza producto
  - sp_InsertarTicketIdempotente: Inserta ticket de forma idempotente
  - sp_ActualizarEstadoBatch: Actualiza estado del batch
  - sp_RegistrarErrorIngesta: Registra error de validacion
  - sp_ObtenerEstadoBatch: Consulta estado de un batch
  - sp_ActualizarUltimaSincronizacion: Actualiza fecha de sincro

  INSTRUCCIONES:
  - Ejecutar despues de 04_create_views_dashboard.sql
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- SP: Registrar Batch de Ingesta
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_RegistrarBatchIngesta')
    DROP PROCEDURE [stg].[sp_RegistrarBatchIngesta]
GO

CREATE PROCEDURE [stg].[sp_RegistrarBatchIngesta]
    @BatchId NVARCHAR(200),
    @FranquiciaId INT,
    @FechaNegocio DATE,
    @SchemaVersion NVARCHAR(20),
    @TipoCarga NVARCHAR(50),
    @OrigenIngesta NVARCHAR(50),
    @NombreArchivo NVARCHAR(500) = NULL,
    @TicketCountRecibido INT = NULL,
    @ItemLineCountRecibido INT = NULL,
    @GrossSalesRecibido DECIMAL(18,2) = NULL,
    @DiscountRecibido DECIMAL(18,2) = NULL,
    @NetSalesRecibido DECIMAL(18,2) = NULL,
    @TaxRecibido DECIMAL(18,2) = NULL,
    @CoversTotalRecibido INT = NULL,
    @SistemaOrigenNombre NVARCHAR(100) = NULL,
    @SistemaOrigenVersion NVARCHAR(50) = NULL,
    @ExportadoPor NVARCHAR(100) = NULL,
    @IngestionBatchId BIGINT OUTPUT,
    @EsDuplicado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar si ya existe el batch (idempotencia)
    SELECT @IngestionBatchId = IngestionBatchId
    FROM [stg].[IngestionBatch]
    WHERE FranquiciaId = @FranquiciaId AND BatchId = @BatchId

    IF @IngestionBatchId IS NOT NULL
    BEGIN
        SET @EsDuplicado = 1
        RETURN
    END

    SET @EsDuplicado = 0

    INSERT INTO [stg].[IngestionBatch]
    (
        BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga,
        OrigenIngesta, NombreArchivo, TicketCountRecibido, ItemLineCountRecibido,
        GrossSalesRecibido, DiscountRecibido, NetSalesRecibido, TaxRecibido,
        CoversTotalRecibido, Estado, SistemaOrigenNombre, SistemaOrigenVersion,
        ExportadoPor, FechaRecepcion
    )
    VALUES
    (
        @BatchId, @FranquiciaId, @FechaNegocio, @SchemaVersion, @TipoCarga,
        @OrigenIngesta, @NombreArchivo, @TicketCountRecibido, @ItemLineCountRecibido,
        @GrossSalesRecibido, @DiscountRecibido, @NetSalesRecibido, @TaxRecibido,
        @CoversTotalRecibido, 'RECEIVED', @SistemaOrigenNombre, @SistemaOrigenVersion,
        @ExportadoPor, GETUTCDATE()
    )

    SET @IngestionBatchId = SCOPE_IDENTITY()
END
GO

-- ============================================================================
-- SP: Guardar JSON Crudo
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GuardarJsonCrudo')
    DROP PROCEDURE [stg].[sp_GuardarJsonCrudo]
GO

CREATE PROCEDURE [stg].[sp_GuardarJsonCrudo]
    @IngestionBatchId BIGINT,
    @JsonContent NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HashJson VARBINARY(32) = HASHBYTES('SHA2_256', @JsonContent)
    DECLARE @TamanioBytes BIGINT = DATALENGTH(@JsonContent)

    INSERT INTO [stg].[IngestionBatchRawJson]
    (IngestionBatchId, JsonContent, HashJson, TamanioBytes)
    VALUES
    (@IngestionBatchId, @JsonContent, @HashJson, @TamanioBytes)
END
GO

-- ============================================================================
-- SP: Actualizar Estado del Batch
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_ActualizarEstadoBatch')
    DROP PROCEDURE [stg].[sp_ActualizarEstadoBatch]
GO

CREATE PROCEDURE [stg].[sp_ActualizarEstadoBatch]
    @IngestionBatchId BIGINT,
    @Estado NVARCHAR(50),
    @MensajeEstado NVARCHAR(MAX) = NULL,
    @CantidadErrores INT = NULL,
    @CantidadWarnings INT = NULL,
    @TicketCountCalculado INT = NULL,
    @ItemLineCountCalculado INT = NULL,
    @GrossSalesCalculado DECIMAL(18,2) = NULL,
    @DiscountCalculado DECIMAL(18,2) = NULL,
    @NetSalesCalculado DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [stg].[IngestionBatch]
    SET
        Estado = @Estado,
        MensajeEstado = ISNULL(@MensajeEstado, MensajeEstado),
        CantidadErrores = ISNULL(@CantidadErrores, CantidadErrores),
        CantidadWarnings = ISNULL(@CantidadWarnings, CantidadWarnings),
        TicketCountCalculado = ISNULL(@TicketCountCalculado, TicketCountCalculado),
        ItemLineCountCalculado = ISNULL(@ItemLineCountCalculado, ItemLineCountCalculado),
        GrossSalesCalculado = ISNULL(@GrossSalesCalculado, GrossSalesCalculado),
        DiscountCalculado = ISNULL(@DiscountCalculado, DiscountCalculado),
        NetSalesCalculado = ISNULL(@NetSalesCalculado, NetSalesCalculado),
        FechaInicioProceso = CASE WHEN @Estado = 'VALIDATING' THEN GETUTCDATE() ELSE FechaInicioProceso END,
        FechaFinProceso = CASE WHEN @Estado IN ('PROCESSED', 'FAILED', 'REJECTED') THEN GETUTCDATE() ELSE FechaFinProceso END,
        FechaModificacion = GETUTCDATE()
    WHERE IngestionBatchId = @IngestionBatchId
END
GO

-- ============================================================================
-- SP: Registrar Error de Ingesta
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_RegistrarErrorIngesta')
    DROP PROCEDURE [stg].[sp_RegistrarErrorIngesta]
GO

CREATE PROCEDURE [stg].[sp_RegistrarErrorIngesta]
    @IngestionBatchId BIGINT,
    @Severidad NVARCHAR(20),
    @CodigoError NVARCHAR(50),
    @MensajeError NVARCHAR(MAX),
    @CampoAfectado NVARCHAR(200) = NULL,
    @ValorRecibido NVARCHAR(MAX) = NULL,
    @ValorEsperado NVARCHAR(500) = NULL,
    @TicketId NVARCHAR(200) = NULL,
    @LineaId NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [stg].[IngestionError]
    (
        IngestionBatchId, Severidad, CodigoError, MensajeError,
        CampoAfectado, ValorRecibido, ValorEsperado, TicketId, LineaId
    )
    VALUES
    (
        @IngestionBatchId, @Severidad, @CodigoError, @MensajeError,
        @CampoAfectado, @ValorRecibido, @ValorEsperado, @TicketId, @LineaId
    )

    -- Actualizar contador de errores/warnings
    IF @Severidad = 'ERROR'
        UPDATE [stg].[IngestionBatch]
        SET CantidadErrores = CantidadErrores + 1
        WHERE IngestionBatchId = @IngestionBatchId
    ELSE IF @Severidad = 'WARNING'
        UPDATE [stg].[IngestionBatch]
        SET CantidadWarnings = CantidadWarnings + 1
        WHERE IngestionBatchId = @IngestionBatchId
END
GO

-- ============================================================================
-- SP: Insertar o Actualizar Mozo
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarActualizarMozo')
    DROP PROCEDURE [dim].[sp_InsertarActualizarMozo]
GO

CREATE PROCEDURE [dim].[sp_InsertarActualizarMozo]
    @FranquiciaId INT,
    @ExternalId NVARCHAR(100),
    @Nombre NVARCHAR(200),
    @FuenteSistema NVARCHAR(100) = NULL,
    @MozoId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Buscar si ya existe
    SELECT @MozoId = MozoId
    FROM [dim].[Mozo]
    WHERE FranquiciaId = @FranquiciaId AND ExternalId = @ExternalId

    IF @MozoId IS NULL
    BEGIN
        -- Insertar nuevo
        INSERT INTO [dim].[Mozo]
        (FranquiciaId, ExternalId, Nombre, FuenteSistema, EsActivo)
        VALUES
        (@FranquiciaId, @ExternalId, @Nombre, @FuenteSistema, 1)

        SET @MozoId = SCOPE_IDENTITY()
    END
    ELSE
    BEGIN
        -- Actualizar existente
        UPDATE [dim].[Mozo]
        SET
            Nombre = @Nombre,
            FuenteSistema = ISNULL(@FuenteSistema, FuenteSistema),
            FechaModificacion = GETUTCDATE()
        WHERE MozoId = @MozoId
    END
END
GO

-- ============================================================================
-- SP: Insertar o Actualizar Producto
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarActualizarProducto')
    DROP PROCEDURE [dim].[sp_InsertarActualizarProducto]
GO

CREATE PROCEDURE [dim].[sp_InsertarActualizarProducto]
    @FranquiciaId INT,
    @ExternalId NVARCHAR(100),
    @Codigo NVARCHAR(50),
    @Nombre NVARCHAR(200),
    @CategoriaProducto NVARCHAR(50) = NULL,
    @Familia NVARCHAR(100) = NULL,
    @SubFamilia NVARCHAR(100) = NULL,
    @FuenteSistema NVARCHAR(100) = NULL,
    @ProductoId INT OUTPUT,
    @TipoPlatoId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Buscar TipoPlato por categoria
    SELECT @TipoPlatoId = TipoPlatoId
    FROM [dim].[TipoPlato]
    WHERE Codigo = @CategoriaProducto

    -- Buscar si ya existe el producto
    SELECT @ProductoId = ProductoId
    FROM [dim].[Producto]
    WHERE FranquiciaId = @FranquiciaId AND ExternalId = @ExternalId

    IF @ProductoId IS NULL
    BEGIN
        -- Insertar nuevo
        INSERT INTO [dim].[Producto]
        (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, FuenteSistema, EsActivo)
        VALUES
        (@FranquiciaId, @ExternalId, @Codigo, @Nombre, @TipoPlatoId, @Familia, @SubFamilia, @FuenteSistema, 1)

        SET @ProductoId = SCOPE_IDENTITY()
    END
    ELSE
    BEGIN
        -- Actualizar existente
        UPDATE [dim].[Producto]
        SET
            Codigo = @Codigo,
            Nombre = @Nombre,
            TipoPlatoId = ISNULL(@TipoPlatoId, TipoPlatoId),
            Familia = ISNULL(@Familia, Familia),
            SubFamilia = ISNULL(@SubFamilia, SubFamilia),
            FuenteSistema = ISNULL(@FuenteSistema, FuenteSistema),
            FechaModificacion = GETUTCDATE()
        WHERE ProductoId = @ProductoId
    END
END
GO

-- ============================================================================
-- SP: Insertar o Actualizar Mesa
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarActualizarMesa')
    DROP PROCEDURE [dim].[sp_InsertarActualizarMesa]
GO

CREATE PROCEDURE [dim].[sp_InsertarActualizarMesa]
    @FranquiciaId INT,
    @NumeroMesa NVARCHAR(20),
    @Area NVARCHAR(100) = NULL,
    @FuenteSistema NVARCHAR(100) = NULL,
    @MesaId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Buscar si ya existe
    SELECT @MesaId = MesaId
    FROM [dim].[Mesa]
    WHERE FranquiciaId = @FranquiciaId AND NumeroMesa = @NumeroMesa

    IF @MesaId IS NULL
    BEGIN
        -- Insertar nueva
        INSERT INTO [dim].[Mesa]
        (FranquiciaId, NumeroMesa, Area, FuenteSistema, EsActiva)
        VALUES
        (@FranquiciaId, @NumeroMesa, @Area, @FuenteSistema, 1)

        SET @MesaId = SCOPE_IDENTITY()
    END
    ELSE
    BEGIN
        -- Actualizar existente
        UPDATE [dim].[Mesa]
        SET
            Area = ISNULL(@Area, Area),
            FuenteSistema = ISNULL(@FuenteSistema, FuenteSistema),
            FechaModificacion = GETUTCDATE()
        WHERE MesaId = @MesaId
    END
END
GO

-- ============================================================================
-- SP: Insertar Ticket de forma Idempotente
-- Retorna el ID si ya existia o el nuevo ID si se inserto
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarTicketIdempotente')
    DROP PROCEDURE [fact].[sp_InsertarTicketIdempotente]
GO

CREATE PROCEDURE [fact].[sp_InsertarTicketIdempotente]
    @IngestionBatchId BIGINT,
    @FranquiciaId INT,
    @ExternalTicketId NVARCHAR(200),
    @NumeroTicket NVARCHAR(100),
    @ExternalOrderId NVARCHAR(200) = NULL,
    @TipoDocumentoFiscal NVARCHAR(50) = NULL,
    @NumeroDocumentoFiscal NVARCHAR(100) = NULL,
    @Estado NVARCHAR(50),
    @FechaNegocio DATE,
    @FechaApertura DATETIME2,
    @FechaCierre DATETIME2 = NULL,
    @PeriodoComida NVARCHAR(50) = NULL,
    @MesaId INT = NULL,
    @NumeroMesa NVARCHAR(20) = NULL,
    @AreaMesa NVARCHAR(100) = NULL,
    @MozoId INT = NULL,
    @NombreMozo NVARCHAR(200) = NULL,
    @CantidadCubiertos INT = NULL,
    @MonedaId INT = NULL,
    @CodigoMoneda CHAR(3),
    @ImporteBruto DECIMAL(18,2),
    @ImporteDescuento DECIMAL(18,2),
    @ImporteNeto DECIMAL(18,2),
    @ImporteImpuesto DECIMAL(18,2) = 0,
    @ImporteServicio DECIMAL(18,2) = 0,
    @ImportePropina DECIMAL(18,2) = 0,
    @ImporteTotalPagado DECIMAL(18,2) = 0,
    @FuenteSistema NVARCHAR(100) = NULL,
    @VentaTicketId BIGINT OUTPUT,
    @EsDuplicado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar si ya existe (idempotencia)
    SELECT @VentaTicketId = VentaTicketId
    FROM [fact].[VentaTicket]
    WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalTicketId

    IF @VentaTicketId IS NOT NULL
    BEGIN
        SET @EsDuplicado = 1
        RETURN
    END

    SET @EsDuplicado = 0

    -- Calcular campos derivados
    DECLARE @TieneDescuento BIT = CASE WHEN @ImporteDescuento > 0 THEN 1 ELSE 0 END
    DECLARE @EstaAnulado BIT = CASE WHEN @Estado IN ('CANCELLED', 'VOIDED', 'REFUNDED') THEN 1 ELSE 0 END
    DECLARE @TiempoConsumoMinutos INT = DATEDIFF(MINUTE, @FechaApertura, @FechaCierre)
    DECLARE @TieneDatosCubiertos BIT = CASE WHEN @CantidadCubiertos IS NOT NULL AND @CantidadCubiertos > 0 THEN 1 ELSE 0 END
    DECLARE @TieneDatosMozo BIT = CASE WHEN @MozoId IS NOT NULL OR @NombreMozo IS NOT NULL THEN 1 ELSE 0 END
    DECLARE @TieneDatosMesa BIT = CASE WHEN @MesaId IS NOT NULL OR @NumeroMesa IS NOT NULL THEN 1 ELSE 0 END

    INSERT INTO [fact].[VentaTicket]
    (
        IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, ExternalOrderId,
        TipoDocumentoFiscal, NumeroDocumentoFiscal, Estado, FechaNegocio, FechaApertura, FechaCierre,
        PeriodoComida, MesaId, NumeroMesa, AreaMesa, MozoId, NombreMozo, CantidadCubiertos,
        MonedaId, CodigoMoneda, ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
        ImporteServicio, ImportePropina, ImporteTotalPagado, TieneDescuento, EstaAnulado,
        TiempoConsumoMinutos, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa, FuenteSistema
    )
    VALUES
    (
        @IngestionBatchId, @FranquiciaId, @ExternalTicketId, @NumeroTicket, @ExternalOrderId,
        @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @Estado, @FechaNegocio, @FechaApertura, @FechaCierre,
        @PeriodoComida, @MesaId, @NumeroMesa, @AreaMesa, @MozoId, @NombreMozo, @CantidadCubiertos,
        @MonedaId, @CodigoMoneda, @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
        @ImporteServicio, @ImportePropina, @ImporteTotalPagado, @TieneDescuento, @EstaAnulado,
        @TiempoConsumoMinutos, @TieneDatosCubiertos, @TieneDatosMozo, @TieneDatosMesa, @FuenteSistema
    )

    SET @VentaTicketId = SCOPE_IDENTITY()
END
GO

-- ============================================================================
-- SP: Insertar Detalle de Ticket
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarTicketDetalle')
    DROP PROCEDURE [fact].[sp_InsertarTicketDetalle]
GO

CREATE PROCEDURE [fact].[sp_InsertarTicketDetalle]
    @VentaTicketId BIGINT,
    @IngestionBatchId BIGINT,
    @FranquiciaId INT,
    @ExternalLineId NVARCHAR(100),
    @ProductoId INT = NULL,
    @CodigoProducto NVARCHAR(100),
    @NombreProducto NVARCHAR(200),
    @TipoPlatoId INT = NULL,
    @CategoriaProducto NVARCHAR(50) = NULL,
    @FamiliaProducto NVARCHAR(100) = NULL,
    @SubFamiliaProducto NVARCHAR(100) = NULL,
    @Cantidad DECIMAL(18,4),
    @PrecioUnitario DECIMAL(18,2),
    @ImporteBruto DECIMAL(18,2),
    @ImporteDescuento DECIMAL(18,2),
    @ImporteNeto DECIMAL(18,2),
    @ImporteImpuesto DECIMAL(18,2) = 0,
    @EstaAnulado BIT = 0,
    @MotivoAnulacion NVARCHAR(500) = NULL,
    @FechaPedido DATETIME2 = NULL,
    @FechaServido DATETIME2 = NULL,
    @Notas NVARCHAR(MAX) = NULL,
    @VentaTicketDetalleId BIGINT OUTPUT,
    @EsDuplicado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar si ya existe (idempotencia por ticket + linea)
    SELECT @VentaTicketDetalleId = VentaTicketDetalleId
    FROM [fact].[VentaTicketDetalle]
    WHERE VentaTicketId = @VentaTicketId AND ExternalLineId = @ExternalLineId

    IF @VentaTicketDetalleId IS NOT NULL
    BEGIN
        SET @EsDuplicado = 1
        RETURN
    END

    SET @EsDuplicado = 0

    DECLARE @TieneDescuento BIT = CASE WHEN @ImporteDescuento > 0 THEN 1 ELSE 0 END
    DECLARE @TiempoPreparacionMinutos INT = DATEDIFF(MINUTE, @FechaPedido, @FechaServido)

    INSERT INTO [fact].[VentaTicketDetalle]
    (
        VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId, ProductoId,
        CodigoProducto, NombreProducto, TipoPlatoId, CategoriaProducto, FamiliaProducto,
        SubFamiliaProducto, Cantidad, PrecioUnitario, ImporteBruto, ImporteDescuento,
        ImporteNeto, ImporteImpuesto, TieneDescuento, EstaAnulado, MotivoAnulacion,
        FechaPedido, FechaServido, TiempoPreparacionMinutos, Notas
    )
    VALUES
    (
        @VentaTicketId, @IngestionBatchId, @FranquiciaId, @ExternalLineId, @ProductoId,
        @CodigoProducto, @NombreProducto, @TipoPlatoId, @CategoriaProducto, @FamiliaProducto,
        @SubFamiliaProducto, @Cantidad, @PrecioUnitario, @ImporteBruto, @ImporteDescuento,
        @ImporteNeto, @ImporteImpuesto, @TieneDescuento, @EstaAnulado, @MotivoAnulacion,
        @FechaPedido, @FechaServido, @TiempoPreparacionMinutos, @Notas
    )

    SET @VentaTicketDetalleId = SCOPE_IDENTITY()
END
GO

-- ============================================================================
-- SP: Obtener Estado de Batch
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_ObtenerEstadoBatch')
    DROP PROCEDURE [stg].[sp_ObtenerEstadoBatch]
GO

CREATE PROCEDURE [stg].[sp_ObtenerEstadoBatch]
    @BatchId NVARCHAR(200) = NULL,
    @IngestionBatchId BIGINT = NULL,
    @FranquiciaId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ib.IngestionBatchId,
        ib.BatchId,
        ib.FranquiciaId,
        f.Codigo AS FranquiciaCodigo,
        f.Nombre AS FranquiciaNombre,
        ib.FechaNegocio,
        ib.SchemaVersion,
        ib.TipoCarga,
        ib.OrigenIngesta,
        ib.Estado,
        ib.MensajeEstado,
        ib.CantidadErrores,
        ib.CantidadWarnings,
        ib.TicketCountRecibido,
        ib.TicketCountCalculado,
        ib.ItemLineCountRecibido,
        ib.ItemLineCountCalculado,
        ib.GrossSalesRecibido,
        ib.GrossSalesCalculado,
        ib.NetSalesRecibido,
        ib.NetSalesCalculado,
        ib.FechaRecepcion,
        ib.FechaInicioProceso,
        ib.FechaFinProceso,
        DATEDIFF(SECOND, ib.FechaInicioProceso, ib.FechaFinProceso) AS DuracionProcesoSegundos
    FROM [stg].[IngestionBatch] ib
    INNER JOIN [dim].[Franquicia] f ON ib.FranquiciaId = f.FranquiciaId
    WHERE (@IngestionBatchId IS NULL OR ib.IngestionBatchId = @IngestionBatchId)
      AND (@BatchId IS NULL OR ib.BatchId = @BatchId)
      AND (@FranquiciaId IS NULL OR ib.FranquiciaId = @FranquiciaId)
    ORDER BY ib.FechaRecepcion DESC
END
GO

-- ============================================================================
-- SP: Actualizar Ultima Sincronizacion de Franquicia
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_ActualizarUltimaSincronizacion')
    DROP PROCEDURE [dim].[sp_ActualizarUltimaSincronizacion]
GO

CREATE PROCEDURE [dim].[sp_ActualizarUltimaSincronizacion]
    @FranquiciaId INT,
    @EstadoIntegracion NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dim].[Franquicia]
    SET
        UltimaSincronizacion = GETUTCDATE(),
        EstadoIntegracion = ISNULL(@EstadoIntegracion, 'ACTIVE'),
        FechaModificacion = GETUTCDATE()
    WHERE FranquiciaId = @FranquiciaId
END
GO

-- ============================================================================
-- SP: Obtener Franquicia por API Key
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_ObtenerFranquiciaPorApiKey')
    DROP PROCEDURE [api].[sp_ObtenerFranquiciaPorApiKey]
GO

CREATE PROCEDURE [api].[sp_ObtenerFranquiciaPorApiKey]
    @ApiKey NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        f.FranquiciaId,
        f.Codigo,
        f.Nombre,
        f.GrupoEconomicoId,
        ge.Codigo AS GrupoEconomicoCodigo,
        f.Pais,
        f.Ciudad,
        f.ZonaHoraria,
        f.MonedaId,
        m.CodigoISO AS MonedaCodigo,
        ak.ApiKeyFranquiciaId,
        ak.Nombre AS ApiKeyNombre,
        ak.FechaExpiracion
    FROM [api].[ApiKeyFranquicia] ak
    INNER JOIN [dim].[Franquicia] f ON ak.FranquiciaId = f.FranquiciaId
    LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
    LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
    WHERE ak.ApiKey = @ApiKey
      AND ak.Activo = 1
      AND f.Activo = 1
      AND (ak.FechaExpiracion IS NULL OR ak.FechaExpiracion > GETUTCDATE())

    -- Actualizar ultimo uso
    UPDATE [api].[ApiKeyFranquicia]
    SET UltimoUso = GETUTCDATE(), ContadorUsos = ContadorUsos + 1
    WHERE ApiKey = @ApiKey
END
GO

-- ============================================================================
-- SP: Registrar Log de API
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_RegistrarApiLog')
    DROP PROCEDURE [stg].[sp_RegistrarApiLog]
GO

CREATE PROCEDURE [stg].[sp_RegistrarApiLog]
    @FranquiciaId INT = NULL,
    @Endpoint NVARCHAR(500),
    @MetodoHttp NVARCHAR(10),
    @IpOrigen NVARCHAR(50) = NULL,
    @ApiKeyUsada NVARCHAR(100) = NULL,
    @RequestBodySize BIGINT = NULL,
    @ResponseStatusCode INT = NULL,
    @ResponseBody NVARCHAR(MAX) = NULL,
    @IngestionBatchId BIGINT = NULL,
    @DuracionMs INT = NULL,
    @ApiIngestaLogId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Solo guardar ultimos 8 caracteres de la API Key por seguridad
    DECLARE @ApiKeyMasked NVARCHAR(100) = NULL
    IF @ApiKeyUsada IS NOT NULL
        SET @ApiKeyMasked = '...' + RIGHT(@ApiKeyUsada, 8)

    INSERT INTO [stg].[ApiIngestaLog]
    (
        FranquiciaId, Endpoint, MetodoHttp, IpOrigen, ApiKeyUsada,
        RequestBodySize, ResponseStatusCode, ResponseBody, IngestionBatchId, DuracionMs,
        FechaRequest, FechaResponse
    )
    VALUES
    (
        @FranquiciaId, @Endpoint, @MetodoHttp, @IpOrigen, @ApiKeyMasked,
        @RequestBodySize, @ResponseStatusCode, @ResponseBody, @IngestionBatchId, @DuracionMs,
        GETUTCDATE(), CASE WHEN @ResponseStatusCode IS NOT NULL THEN GETUTCDATE() ELSE NULL END
    )

    SET @ApiIngestaLogId = SCOPE_IDENTITY()
END
GO

PRINT '============================================'
PRINT 'Todos los stored procedures han sido creados.'
PRINT '============================================'
GO
