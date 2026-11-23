-- =====================================================
-- 037 · daily-batch v1.3 (T-380, Manila / Aquarius POS)
--
-- Solo agrega: columnas nullable, una tabla de respaldo y parámetros opcionales.
-- Los llamadores existentes (v1.0-v1.2: Toast, Drive, MICROS, Lima) no cambian:
-- los parámetros nuevos tienen default y el SP calcula EstaAnulado como antes
-- salvo que se le pase @EstaAnuladoForzado (solo lo hace la ingesta v1.3).
--
-- Re-ejecutable. Reversión: 037_daily_batch_v13_manila_rollback.sql
-- Numeración: se llamó 035 hasta el 23/11/2026 (chocaba con 035_alta_neo_malaga y 036 ya era el control del
-- mapa). En la base de desarrollo ya estaba aplicada con el nombre viejo; no hay tabla de migraciones aplicadas.
-- =====================================================

-- 1. Esquema de respaldos
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'bak')
    EXEC('CREATE SCHEMA bak');
GO

-- 2. Columnas nuevas en fact.VentaTicket (v1.3)
IF COL_LENGTH('fact.VentaTicket', 'ExternalTicketIdOriginal') IS NULL
    ALTER TABLE fact.VentaTicket ADD ExternalTicketIdOriginal NVARCHAR(200) NULL;   -- original_ticket_id (REFUNDED)
IF COL_LENGTH('fact.VentaTicket', 'ImporteGravado') IS NULL
    ALTER TABLE fact.VentaTicket ADD ImporteGravado DECIMAL(18,2) NULL;             -- tax_breakdown.vatable_sales
IF COL_LENGTH('fact.VentaTicket', 'ImporteIvaGravado') IS NULL
    ALTER TABLE fact.VentaTicket ADD ImporteIvaGravado DECIMAL(18,2) NULL;          -- tax_breakdown.vat_amount
IF COL_LENGTH('fact.VentaTicket', 'ImporteExento') IS NULL
    ALTER TABLE fact.VentaTicket ADD ImporteExento DECIMAL(18,2) NULL;              -- tax_breakdown.vat_exempt_sales
IF COL_LENGTH('fact.VentaTicket', 'ImporteTasaCero') IS NULL
    ALTER TABLE fact.VentaTicket ADD ImporteTasaCero DECIMAL(18,2) NULL;            -- tax_breakdown.zero_rated_sales
GO

-- 3. Referencia del pago (payment_methods[].reference)
IF COL_LENGTH('fact.VentaTicketMedioPago', 'Referencia') IS NULL
    ALTER TABLE fact.VentaTicketMedioPago ADD Referencia NVARCHAR(200) NULL;
GO

-- 4. Respaldo de tickets reemplazados por FULL_DAY o actualizados por INCREMENTAL/CORRECTION.
--    Se guarda el ticket completo (cabecera + líneas + pagos + descuentos) en JSON, así el
--    respaldo no depende de las columnas que tenga la tabla en cada momento.
IF OBJECT_ID('bak.VentaTicketReemplazado', 'U') IS NULL
BEGIN
    CREATE TABLE bak.VentaTicketReemplazado
    (
        VentaTicketReemplazadoId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VentaTicketReemplazado PRIMARY KEY,
        IngestionBatchId BIGINT NOT NULL,            -- batch que lo reemplazó
        FranquiciaId INT NOT NULL,
        FechaNegocio DATE NOT NULL,
        VentaTicketId BIGINT NOT NULL,               -- id que tenía el ticket borrado
        ExternalTicketId NVARCHAR(200) NOT NULL,
        IngestionBatchIdOriginal BIGINT NULL,        -- batch que lo había cargado
        Motivo VARCHAR(30) NOT NULL,                 -- FULL_DAY | UPSERT
        TicketJson NVARCHAR(MAX) NOT NULL,
        FechaRespaldo DATETIME2 NOT NULL CONSTRAINT DF_VentaTicketReemplazado_Fecha DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_VentaTicketReemplazado_Franquicia_Fecha
        ON bak.VentaTicketReemplazado (FranquiciaId, FechaNegocio, ExternalTicketId);
END
GO

-- 5. SP de inserción de tickets: mismos parámetros que la 027 + opcionales v1.3
CREATE OR ALTER PROCEDURE [fact].[sp_InsertarTicketIdempotente]
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
    -- v1.2 - Eje Impuesto
    @ImporteNetoSinImpuesto DECIMAL(18,4) = NULL,
    @ImportesIncluyenImpuesto BIT = NULL,
    @CalidadImpuesto VARCHAR(20) = NULL,
    -- v1.2 - Turno
    @TurnoCodigo VARCHAR(50) = NULL,
    @TurnoNombre NVARCHAR(100) = NULL,
    @TurnoApertura DATETIME2 = NULL,
    @TurnoCierre DATETIME2 = NULL,
    @MealPeriodOrigen VARCHAR(20) = NULL,
    -- v1.3 (T-380): todos opcionales
    @EstaAnuladoForzado BIT = NULL,              -- NULL = regla histórica por estado
    @ExternalTicketIdOriginal NVARCHAR(200) = NULL,
    @ImporteGravado DECIMAL(18,2) = NULL,
    @ImporteIvaGravado DECIMAL(18,2) = NULL,
    @ImporteExento DECIMAL(18,2) = NULL,
    @ImporteTasaCero DECIMAL(18,2) = NULL,
    -- Output
    @VentaTicketId BIGINT OUTPUT,
    @EsDuplicado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Idempotencia: franquicia + ticket + fecha de negocio (027)
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

    DECLARE @TieneDescuento BIT = CASE WHEN @ImporteDescuento > 0 THEN 1 ELSE 0 END
    -- v1.3 pasa 0 para REFUNDED (importes negativos que restan) y 1 para VOIDED/CANCELLED
    DECLARE @EstaAnulado BIT = COALESCE(@EstaAnuladoForzado,
        CASE WHEN @Estado IN ('CANCELLED', 'VOIDED', 'REFUNDED') THEN 1 ELSE 0 END)
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
        ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
        TurnoCodigo, TurnoNombre, TurnoApertura, TurnoCierre, MealPeriodOrigen,
        ExternalTicketIdOriginal, ImporteGravado, ImporteIvaGravado, ImporteExento, ImporteTasaCero
    )
    VALUES
    (
        @IngestionBatchId, @FranquiciaId, @ExternalTicketId, @NumeroTicket, @ExternalOrderId,
        @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @Estado, @FechaNegocio, @FechaApertura, @FechaCierre,
        @PeriodoComida, @MesaId, @NumeroMesa, @AreaMesa, @MozoId, @NombreMozo, @CantidadCubiertos,
        @MonedaId, @CodigoMoneda, @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
        @ImporteServicio, @ImportePropina, @ImporteTotalPagado, @TieneDescuento, @EstaAnulado,
        @TiempoConsumoMinutos, @TieneDatosCubiertos, @TieneDatosMozo, @TieneDatosMesa, @FuenteSistema,
        @ImporteNetoSinImpuesto, @ImportesIncluyenImpuesto, @CalidadImpuesto,
        @TurnoCodigo, @TurnoNombre, @TurnoApertura, @TurnoCierre, @MealPeriodOrigen,
        @ExternalTicketIdOriginal, @ImporteGravado, @ImporteIvaGravado, @ImporteExento, @ImporteTasaCero
    )

    SET @VentaTicketId = SCOPE_IDENTITY()
END
GO

-- 6. Respaldar en bak.VentaTicketReemplazado y borrar tickets de una franquicia y fecha.
--    @ExternalTicketId NULL = el día completo (FULL_DAY); con valor = un ticket (UPSERT).
--    No abre transacción propia: la ingesta v1.3 lo llama dentro de la suya.
CREATE OR ALTER PROCEDURE [fact].[sp_RespaldarYEliminarTickets]
    @IngestionBatchId BIGINT,
    @FranquiciaId INT,
    @FechaNegocio DATE,
    @ExternalTicketId NVARCHAR(200) = NULL,
    @Motivo VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ids TABLE (VentaTicketId BIGINT PRIMARY KEY);

    INSERT INTO @ids (VentaTicketId)
    SELECT VentaTicketId
    FROM fact.VentaTicket
    WHERE FranquiciaId = @FranquiciaId
      AND FechaNegocio = @FechaNegocio
      AND (@ExternalTicketId IS NULL OR ExternalTicketId = @ExternalTicketId);

    INSERT INTO bak.VentaTicketReemplazado
        (IngestionBatchId, FranquiciaId, FechaNegocio, VentaTicketId, ExternalTicketId, IngestionBatchIdOriginal, Motivo, TicketJson)
    SELECT
        @IngestionBatchId, vt.FranquiciaId, vt.FechaNegocio, vt.VentaTicketId, vt.ExternalTicketId, vt.IngestionBatchId, @Motivo,
        (
            SELECT
                t.*,
                (SELECT d.* FROM fact.VentaTicketDetalle d WHERE d.VentaTicketId = t.VentaTicketId FOR JSON PATH, INCLUDE_NULL_VALUES) AS lineas,
                (SELECT p.* FROM fact.VentaTicketMedioPago p WHERE p.VentaTicketId = t.VentaTicketId FOR JSON PATH, INCLUDE_NULL_VALUES) AS pagos,
                (SELECT ds.* FROM fact.VentaTicketDescuento ds WHERE ds.VentaTicketId = t.VentaTicketId FOR JSON PATH, INCLUDE_NULL_VALUES) AS descuentos
            FROM fact.VentaTicket t
            WHERE t.VentaTicketId = vt.VentaTicketId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
        )
    FROM fact.VentaTicket vt
    JOIN @ids i ON i.VentaTicketId = vt.VentaTicketId;

    DELETE d FROM fact.VentaTicketDetalle d JOIN @ids i ON i.VentaTicketId = d.VentaTicketId;
    DELETE p FROM fact.VentaTicketMedioPago p JOIN @ids i ON i.VentaTicketId = p.VentaTicketId;
    DELETE ds FROM fact.VentaTicketDescuento ds JOIN @ids i ON i.VentaTicketId = ds.VentaTicketId;
    DELETE vt FROM fact.VentaTicket vt JOIN @ids i ON i.VentaTicketId = vt.VentaTicketId;

    SELECT ExternalTicketId FROM bak.VentaTicketReemplazado
    WHERE IngestionBatchId = @IngestionBatchId AND VentaTicketId IN (SELECT VentaTicketId FROM @ids);
END
GO
