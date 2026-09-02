-- =====================================================
-- Fix: Incluir FechaNegocio en deteccion de duplicados
-- El problema: Toast POS reutiliza IDs de ticket cada dia
-- (T1, T14, etc.) asi que tickets de dias diferentes
-- con el mismo ID eran rechazados como duplicados
--
-- IMPORTANTE: Este SP usa la version v1.2 completa con
-- parametros de eje impuesto y turno
-- =====================================================

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
    -- FIX: Incluir FechaNegocio porque Toast POS reutiliza IDs de ticket cada dia
    SELECT @VentaTicketId = VentaTicketId
    FROM [fact].[VentaTicket]
    WHERE FranquiciaId = @FranquiciaId
      AND ExternalTicketId = @ExternalTicketId
      AND FechaNegocio = @FechaNegocio

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

PRINT 'SP fact.sp_InsertarTicketIdempotente actualizado - verifica duplicados por FranquiciaId + ExternalTicketId + FechaNegocio'
GO
