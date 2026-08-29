/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 14_alter_sp_v12_impuesto_turno.sql
  Descripcion: Actualiza stored procedures para soportar contrato v1.2
               (eje impuesto y turno)
  Autor: Claude Code
  Fecha: 2026-08-23
  Lote: MEJORAS_20260822 - Etapa 3.4

  CAMBIOS:
  - fact.sp_InsertarTicketIdempotente: nuevos parametros de impuesto y turno
  - fact.sp_InsertarTicketDetalle: nuevos parametros de impuesto

  PARAMETROS NUEVOS PARA TICKET:
  - @ImporteNetoSinImpuesto: Neto sin impuesto (KPI comparable)
  - @ImportesIncluyenImpuesto: Si los precios incluyen tax
  - @CalidadImpuesto: 'POS' | 'DERIVADO' | 'DESCONOCIDO'
  - @TurnoCodigo, @TurnoNombre, @TurnoApertura, @TurnoCierre: Info de turno
  - @MealPeriod ya existia, se agrega @MealPeriodOrigen

  PARAMETROS NUEVOS PARA DETALLE:
  - @ImporteNetoSinImpuesto: Neto sin impuesto por linea
  - @TasaImpuesto: Alicuota aplicada (decimal, ej 0.21)
  - @ImportesIncluyenImpuesto: Si el precio incluye tax
  - @CalidadImpuesto: Calidad del dato

  INSTRUCCIONES:
  - Ejecutar despues de 13_alter_dim_franquicia_fechainicio.sql
  - Idempotente: puede re-ejecutarse sin error
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- SP: Insertar Ticket de forma Idempotente (v1.2)
-- Actualizado para soportar eje impuesto y turno
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarTicketIdempotente' AND SCHEMA_NAME(schema_id) = 'fact')
    DROP PROCEDURE [fact].[sp_InsertarTicketIdempotente]
GO

CREATE PROCEDURE [fact].[sp_InsertarTicketIdempotente]
    -- Parametros originales v1.0
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

    -- Parametros nuevos v1.2 - Eje Impuesto
    @ImporteNetoSinImpuesto DECIMAL(18,4) = NULL,
    @ImportesIncluyenImpuesto BIT = NULL,
    @CalidadImpuesto VARCHAR(20) = NULL,   -- 'POS' | 'DERIVADO' | 'DESCONOCIDO'

    -- Parametros nuevos v1.2 - Turno
    @TurnoCodigo VARCHAR(50) = NULL,
    @TurnoNombre NVARCHAR(100) = NULL,
    @TurnoApertura DATETIME2 = NULL,
    @TurnoCierre DATETIME2 = NULL,
    @MealPeriodOrigen VARCHAR(20) = NULL,  -- 'POS' | 'DERIVADO'

    -- Output
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
        TiempoConsumoMinutos, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa, FuenteSistema,
        -- Columnas v1.2
        ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
        TurnoCodigo, TurnoNombre, TurnoApertura, TurnoCierre, MealPeriodOrigen
    )
    VALUES
    (
        @IngestionBatchId, @FranquiciaId, @ExternalTicketId, @NumeroTicket, @ExternalOrderId,
        @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @Estado, @FechaNegocio, @FechaApertura, @FechaCierre,
        @PeriodoComida, @MesaId, @NumeroMesa, @AreaMesa, @MozoId, @NombreMozo, @CantidadCubiertos,
        @MonedaId, @CodigoMoneda, @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
        @ImporteServicio, @ImportePropina, @ImporteTotalPagado, @TieneDescuento, @EstaAnulado,
        @TiempoConsumoMinutos, @TieneDatosCubiertos, @TieneDatosMozo, @TieneDatosMesa, @FuenteSistema,
        -- Valores v1.2
        @ImporteNetoSinImpuesto, @ImportesIncluyenImpuesto, @CalidadImpuesto,
        @TurnoCodigo, @TurnoNombre, @TurnoApertura, @TurnoCierre, @MealPeriodOrigen
    )

    SET @VentaTicketId = SCOPE_IDENTITY()
END
GO

PRINT 'SP fact.sp_InsertarTicketIdempotente actualizado para v1.2.'
GO

-- ============================================================================
-- SP: Insertar Detalle de Ticket (v1.2)
-- Actualizado para soportar eje impuesto
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_InsertarTicketDetalle' AND SCHEMA_NAME(schema_id) = 'fact')
    DROP PROCEDURE [fact].[sp_InsertarTicketDetalle]
GO

CREATE PROCEDURE [fact].[sp_InsertarTicketDetalle]
    -- Parametros originales v1.0
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

    -- Parametros nuevos v1.2 - Eje Impuesto
    @ImporteNetoSinImpuesto DECIMAL(18,4) = NULL,
    @TasaImpuesto DECIMAL(9,6) = NULL,
    @ImportesIncluyenImpuesto BIT = NULL,
    @CalidadImpuesto VARCHAR(20) = NULL,  -- 'POS' | 'DERIVADO' | 'DESCONOCIDO'

    -- Output
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
        FechaPedido, FechaServido, TiempoPreparacionMinutos, Notas,
        -- Columnas v1.2
        ImporteNetoSinImpuesto, TasaImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto
    )
    VALUES
    (
        @VentaTicketId, @IngestionBatchId, @FranquiciaId, @ExternalLineId, @ProductoId,
        @CodigoProducto, @NombreProducto, @TipoPlatoId, @CategoriaProducto, @FamiliaProducto,
        @SubFamiliaProducto, @Cantidad, @PrecioUnitario, @ImporteBruto, @ImporteDescuento,
        @ImporteNeto, @ImporteImpuesto, @TieneDescuento, @EstaAnulado, @MotivoAnulacion,
        @FechaPedido, @FechaServido, @TiempoPreparacionMinutos, @Notas,
        -- Valores v1.2
        @ImporteNetoSinImpuesto, @TasaImpuesto, @ImportesIncluyenImpuesto, @CalidadImpuesto
    )

    SET @VentaTicketDetalleId = SCOPE_IDENTITY()
END
GO

PRINT 'SP fact.sp_InsertarTicketDetalle actualizado para v1.2.'
GO

PRINT '============================================'
PRINT 'Script 14 completado: SPs actualizados para v1.2.'
PRINT '============================================'
GO
