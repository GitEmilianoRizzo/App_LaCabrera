/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 07_seed_realistic_sales_data.sql
  Descripcion: Genera datos de ventas realistas para 30 dias de operaciones
  Autor: Claude Code
  Fecha: 2026-07-16

  ESCENARIOS GENERADOS:
  - 30 dias de operaciones (desde 2026-06-15 hasta 2026-07-14)
  - 2 franquicias: Paraguay (Guaranies) y Miami (USD)
  - Patrones de fin de semana (mas ventas sabado/domingo)
  - Horarios pico: almuerzo (12-15hs) y cena (20-23hs)
  - ~15% de tickets con descuento
  - Variedad de medios de pago
  - Tickets tipicos de parrilla argentina

  VOLUMENES APROXIMADOS POR DIA:
  - Paraguay: 35-60 tickets/dia (Lun-Jue: 35-45, Vie: 50-60, Sab-Dom: 55-70)
  - Miami: 45-80 tickets/dia (Lun-Jue: 45-55, Vie: 60-75, Sab-Dom: 70-90)

  INSTRUCCIONES:
  - Ejecutar despues de 06_seed_demo_data.sql
  - Este script borra datos de ventas existentes y los regenera
  - Tiempo estimado de ejecucion: 2-5 minutos
================================================================================
*/

USE [LaCabreraDB]
GO

SET NOCOUNT ON
GO

PRINT '============================================'
PRINT 'Iniciando generacion de datos realistas...'
PRINT '============================================'
PRINT ''

-- ============================================================================
-- LIMPIEZA DE DATOS EXISTENTES (solo ventas, no dimensiones)
-- ============================================================================

PRINT 'Limpiando datos de ventas existentes...'

DELETE FROM [fact].[VentaTicketMedioPago]
DELETE FROM [fact].[VentaTicketDescuento]
DELETE FROM [fact].[VentaTicketDetalle]
DELETE FROM [fact].[VentaTicket]
DELETE FROM [stg].[IngestionError]
DELETE FROM [stg].[IngestionBatchRawJson]
DELETE FROM [stg].[IngestionBatch]

-- Reset identity seeds
DBCC CHECKIDENT ('[fact].[VentaTicketMedioPago]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicketDescuento]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicketDetalle]', RESEED, 0)
DBCC CHECKIDENT ('[fact].[VentaTicket]', RESEED, 0)
DBCC CHECKIDENT ('[stg].[IngestionBatch]', RESEED, 0)

PRINT 'Datos de ventas limpiados.'
PRINT ''
GO

-- ============================================================================
-- VARIABLES DE CONFIGURACION
-- ============================================================================

DECLARE @FechaInicio DATE = '2026-06-15'
DECLARE @FechaFin DATE = '2026-07-14'
DECLARE @FechaActual DATE

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
DECLARE @MonedaPYG INT, @MonedaUSD INT

SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'
SELECT @MonedaPYG = MonedaId FROM [dim].[Moneda] WHERE CodigoISO = 'PYG'
SELECT @MonedaUSD = MonedaId FROM [dim].[Moneda] WHERE CodigoISO = 'USD'

-- Verificar que existen las franquicias
IF @FranqParaguayId IS NULL OR @FranqMiamiId IS NULL
BEGIN
    RAISERROR('ERROR: Las franquicias demo no existen. Ejecutar primero 06_seed_demo_data.sql', 16, 1)
    RETURN
END

PRINT 'Franquicia Paraguay ID: ' + CAST(@FranqParaguayId AS VARCHAR(10))
PRINT 'Franquicia Miami ID: ' + CAST(@FranqMiamiId AS VARCHAR(10))
PRINT ''

-- ============================================================================
-- TABLAS TEMPORALES PARA GENERACION
-- ============================================================================

-- Tabla para productos Paraguay
CREATE TABLE #ProductosParaguay (
    Idx INT IDENTITY(1,1),
    ProductoId INT,
    Codigo NVARCHAR(50),
    Nombre NVARCHAR(200),
    Precio DECIMAL(18,2),
    TipoPlato NVARCHAR(50),
    Peso INT -- Peso para seleccion aleatoria
)

INSERT INTO #ProductosParaguay (ProductoId, Codigo, Nombre, Precio, TipoPlato, Peso)
SELECT p.ProductoId, p.Codigo, p.Nombre, p.PrecioBase, tp.Codigo,
    CASE
        WHEN tp.Codigo = 'MAIN_COURSE' THEN 40  -- Carnes mas frecuentes
        WHEN tp.Codigo = 'STARTER' THEN 20
        WHEN tp.Codigo = 'SIDE_DISH' THEN 25
        WHEN tp.Codigo = 'WINE' THEN 15
        WHEN tp.Codigo = 'BEVERAGE' THEN 30
        WHEN tp.Codigo = 'DESSERT' THEN 15
        WHEN tp.Codigo = 'COFFEE' THEN 12
        ELSE 10
    END
FROM [dim].[Producto] p
JOIN [dim].[TipoPlato] tp ON p.TipoPlatoId = tp.TipoPlatoId
WHERE p.FranquiciaId = @FranqParaguayId AND p.Activo = 1

-- Tabla para productos Miami
CREATE TABLE #ProductosMiami (
    Idx INT IDENTITY(1,1),
    ProductoId INT,
    Codigo NVARCHAR(50),
    Nombre NVARCHAR(200),
    Precio DECIMAL(18,2),
    TipoPlato NVARCHAR(50),
    Peso INT
)

INSERT INTO #ProductosMiami (ProductoId, Codigo, Nombre, Precio, TipoPlato, Peso)
SELECT p.ProductoId, p.Codigo, p.Nombre, p.PrecioBase, tp.Codigo,
    CASE
        WHEN tp.Codigo = 'MAIN_COURSE' THEN 40
        WHEN tp.Codigo = 'STARTER' THEN 20
        WHEN tp.Codigo = 'SIDE_DISH' THEN 25
        WHEN tp.Codigo = 'WINE' THEN 18
        WHEN tp.Codigo = 'BEVERAGE' THEN 25
        WHEN tp.Codigo = 'DESSERT' THEN 15
        WHEN tp.Codigo = 'COFFEE' THEN 12
        ELSE 10
    END
FROM [dim].[Producto] p
JOIN [dim].[TipoPlato] tp ON p.TipoPlatoId = tp.TipoPlatoId
WHERE p.FranquiciaId = @FranqMiamiId AND p.Activo = 1

-- Tabla para mozos
CREATE TABLE #MozosParaguay (Idx INT IDENTITY(1,1), MozoId INT, Nombre NVARCHAR(200))
CREATE TABLE #MozosMiami (Idx INT IDENTITY(1,1), MozoId INT, Nombre NVARCHAR(200))

INSERT INTO #MozosParaguay (MozoId, Nombre)
SELECT MozoId, Nombre FROM [dim].[Mozo] WHERE FranquiciaId = @FranqParaguayId AND Activo = 1

INSERT INTO #MozosMiami (MozoId, Nombre)
SELECT MozoId, Nombre FROM [dim].[Mozo] WHERE FranquiciaId = @FranqMiamiId AND Activo = 1

-- Tabla para mesas
CREATE TABLE #MesasParaguay (Idx INT IDENTITY(1,1), MesaId INT, Numero NVARCHAR(20), Area NVARCHAR(100))
CREATE TABLE #MesasMiami (Idx INT IDENTITY(1,1), MesaId INT, Numero NVARCHAR(20), Area NVARCHAR(100))

INSERT INTO #MesasParaguay (MesaId, Numero, Area)
SELECT MesaId, NumeroMesa, Area FROM [dim].[Mesa] WHERE FranquiciaId = @FranqParaguayId AND Activo = 1

INSERT INTO #MesasMiami (MesaId, Numero, Area)
SELECT MesaId, NumeroMesa, Area FROM [dim].[Mesa] WHERE FranquiciaId = @FranqMiamiId AND Activo = 1

-- Medios de pago
CREATE TABLE #MediosPago (Idx INT IDENTITY(1,1), MedioPagoId INT, Codigo NVARCHAR(50), Peso INT)
INSERT INTO #MediosPago (MedioPagoId, Codigo, Peso)
SELECT MedioPagoId, Codigo,
    CASE Codigo
        WHEN 'CREDIT_CARD' THEN 40
        WHEN 'DEBIT_CARD' THEN 25
        WHEN 'CASH' THEN 20
        WHEN 'QR' THEN 10
        ELSE 5
    END
FROM [dim].[MedioPago] WHERE Activo = 1

PRINT 'Tablas temporales creadas.'
PRINT ''

-- ============================================================================
-- FUNCION AUXILIAR: Numero aleatorio en rango
-- ============================================================================

-- Usar CHECKSUM(NEWID()) para randomizacion

-- ============================================================================
-- GENERACION DE DATOS POR DIA
-- ============================================================================

SET @FechaActual = @FechaInicio

DECLARE @TotalTicketsGenerados INT = 0
DECLARE @DiaSemana INT
DECLARE @TicketsDelDia INT
DECLARE @TicketNum INT
DECLARE @BatchId NVARCHAR(100)
DECLARE @IngestionBatchId BIGINT
DECLARE @EsDuplicado BIT

WHILE @FechaActual <= @FechaFin
BEGIN
    SET @DiaSemana = DATEPART(WEEKDAY, @FechaActual) -- 1=Dom, 2=Lun, ... 7=Sab

    PRINT 'Procesando fecha: ' + CONVERT(VARCHAR(10), @FechaActual, 120) + ' (Dia ' + CAST(@DiaSemana AS VARCHAR(2)) + ')'

    -- =============================================
    -- FRANQUICIA PARAGUAY
    -- =============================================

    -- Determinar cantidad de tickets segun dia de semana
    SET @TicketsDelDia = CASE
        WHEN @DiaSemana IN (1, 7) THEN 55 + ABS(CHECKSUM(NEWID())) % 16  -- Dom/Sab: 55-70
        WHEN @DiaSemana = 6 THEN 50 + ABS(CHECKSUM(NEWID())) % 11       -- Vie: 50-60
        ELSE 35 + ABS(CHECKSUM(NEWID())) % 11                           -- Lun-Jue: 35-45
    END

    -- Crear batch para Paraguay
    SET @BatchId = 'SEED-PY-' + REPLACE(CONVERT(VARCHAR(10), @FechaActual, 120), '-', '')

    INSERT INTO [stg].[IngestionBatch]
        (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta,
         TicketCountRecibido, Estado, SistemaOrigenNombre)
    VALUES
        (@BatchId, @FranqParaguayId, @FechaActual, '1.0', 'FULL_DAY', 'SEED',
         @TicketsDelDia, 'PROCESSED', 'Demo Seed Script')

    SET @IngestionBatchId = SCOPE_IDENTITY()

    -- Generar tickets Paraguay
    SET @TicketNum = 1
    WHILE @TicketNum <= @TicketsDelDia
    BEGIN
        -- Hora del ticket (distribucion: 30% almuerzo, 70% cena)
        DECLARE @EsAlmuerzo BIT = CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 30 THEN 1 ELSE 0 END
        DECLARE @HoraBase INT = CASE WHEN @EsAlmuerzo = 1 THEN 12 ELSE 20 END
        DECLARE @MinutoRandom INT = ABS(CHECKSUM(NEWID())) % 180  -- 0-179 minutos desde hora base
        DECLARE @FechaApertura DATETIME2 = DATEADD(MINUTE, @HoraBase * 60 + @MinutoRandom, CAST(@FechaActual AS DATETIME2))
        DECLARE @DuracionMinutos INT = 45 + ABS(CHECKSUM(NEWID())) % 60  -- 45-104 minutos
        DECLARE @FechaCierre DATETIME2 = DATEADD(MINUTE, @DuracionMinutos, @FechaApertura)

        -- Seleccionar mesa y mozo aleatorios
        DECLARE @MesaIdx INT = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MesasParaguay)
        DECLARE @MozoIdx INT = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MozosParaguay)

        DECLARE @MesaId INT, @NumeroMesa NVARCHAR(20), @AreaMesa NVARCHAR(100)
        DECLARE @MozoId INT, @NombreMozo NVARCHAR(200)

        SELECT @MesaId = MesaId, @NumeroMesa = Numero, @AreaMesa = Area FROM #MesasParaguay WHERE Idx = @MesaIdx
        SELECT @MozoId = MozoId, @NombreMozo = Nombre FROM #MozosParaguay WHERE Idx = @MozoIdx

        -- Cubiertos (2-6 personas)
        DECLARE @Cubiertos INT = 2 + ABS(CHECKSUM(NEWID())) % 5

        -- External IDs
        DECLARE @ExternalTicketId NVARCHAR(200) = 'TKT-PY-' + REPLACE(CONVERT(VARCHAR(10), @FechaActual, 120), '-', '') + '-' + RIGHT('000' + CAST(@TicketNum AS VARCHAR(4)), 4)
        DECLARE @NumeroTicketStr NVARCHAR(100) = 'A-' + RIGHT('0000' + CAST(((@TicketNum + 1000) % 10000) AS VARCHAR(5)), 5)

        -- Periodo de comida
        DECLARE @PeriodoComida NVARCHAR(50) = CASE WHEN @EsAlmuerzo = 1 THEN 'LUNCH' ELSE 'DINNER' END

        -- Importes (se calculan despues de insertar items)
        DECLARE @VentaTicketId BIGINT

        INSERT INTO [fact].[VentaTicket]
            (IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado,
             FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
             MesaId, NumeroMesa, AreaMesa, MozoId, NombreMozo, CantidadCubiertos,
             MonedaId, CodigoMoneda, ImporteBruto, ImporteDescuento, ImporteNeto,
             TieneDescuento, TiempoConsumoMinutos, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa,
             FuenteSistema)
        VALUES
            (@IngestionBatchId, @FranqParaguayId, @ExternalTicketId, @NumeroTicketStr, 'CLOSED',
             @FechaActual, @FechaApertura, @FechaCierre, @PeriodoComida,
             @MesaId, @NumeroMesa, @AreaMesa, @MozoId, @NombreMozo, @Cubiertos,
             @MonedaPYG, 'PYG', 0, 0, 0,
             0, @DuracionMinutos, 1, 1, 1,
             'Demo Seed')

        SET @VentaTicketId = SCOPE_IDENTITY()

        -- Generar items del ticket (tipico: 1 entrada, 1-2 carnes, 1-2 guarniciones, bebidas, postre/cafe)
        DECLARE @ItemNum INT = 1
        DECLARE @TotalBruto DECIMAL(18,2) = 0
        DECLARE @CantidadItems INT = 3 + ABS(CHECKSUM(NEWID())) % 5  -- 3-7 items

        -- Siempre incluir al menos una carne
        DECLARE @ProductoIdx INT
        DECLARE @ProdId INT, @ProdCodigo NVARCHAR(50), @ProdNombre NVARCHAR(200), @ProdPrecio DECIMAL(18,2), @ProdTipo NVARCHAR(50)

        -- Item 1: Carne principal
        SELECT TOP 1 @ProdId = ProductoId, @ProdCodigo = Codigo, @ProdNombre = Nombre, @ProdPrecio = Precio, @ProdTipo = TipoPlato
        FROM #ProductosParaguay WHERE TipoPlato = 'MAIN_COURSE' ORDER BY NEWID()

        INSERT INTO [fact].[VentaTicketDetalle]
            (VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId,
             ProductoId, CodigoProducto, NombreProducto, CategoriaProducto,
             Cantidad, PrecioUnitario, ImporteBruto, ImporteDescuento, ImporteNeto)
        VALUES
            (@VentaTicketId, @IngestionBatchId, @FranqParaguayId, 'L-' + CAST(@ItemNum AS VARCHAR(3)),
             @ProdId, @ProdCodigo, @ProdNombre, @ProdTipo,
             1, @ProdPrecio, @ProdPrecio, 0, @ProdPrecio)

        SET @TotalBruto = @TotalBruto + @ProdPrecio
        SET @ItemNum = @ItemNum + 1

        -- Items adicionales
        WHILE @ItemNum <= @CantidadItems
        BEGIN
            -- Seleccionar producto aleatorio con peso
            SELECT TOP 1 @ProdId = ProductoId, @ProdCodigo = Codigo, @ProdNombre = Nombre, @ProdPrecio = Precio, @ProdTipo = TipoPlato
            FROM #ProductosParaguay ORDER BY NEWID()

            DECLARE @Cantidad INT = CASE
                WHEN @ProdTipo IN ('BEVERAGE', 'COFFEE') THEN 1 + ABS(CHECKSUM(NEWID())) % 3
                ELSE 1
            END

            INSERT INTO [fact].[VentaTicketDetalle]
                (VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId,
                 ProductoId, CodigoProducto, NombreProducto, CategoriaProducto,
                 Cantidad, PrecioUnitario, ImporteBruto, ImporteDescuento, ImporteNeto)
            VALUES
                (@VentaTicketId, @IngestionBatchId, @FranqParaguayId, 'L-' + CAST(@ItemNum AS VARCHAR(3)),
                 @ProdId, @ProdCodigo, @ProdNombre, @ProdTipo,
                 @Cantidad, @ProdPrecio, @ProdPrecio * @Cantidad, 0, @ProdPrecio * @Cantidad)

            SET @TotalBruto = @TotalBruto + (@ProdPrecio * @Cantidad)
            SET @ItemNum = @ItemNum + 1
        END

        -- Descuento (15% de tickets tiene descuento)
        DECLARE @TieneDescuento BIT = CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 15 THEN 1 ELSE 0 END
        DECLARE @ImporteDescuento DECIMAL(18,2) = 0

        IF @TieneDescuento = 1
        BEGIN
            -- Descuento 10% o 15%
            DECLARE @PorcDescuento DECIMAL(5,2) = CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 0.10 ELSE 0.15 END
            SET @ImporteDescuento = ROUND(@TotalBruto * @PorcDescuento, 0)

            INSERT INTO [fact].[VentaTicketDescuento]
                (VentaTicketId, IngestionBatchId, FranquiciaId, CodigoDescuento, NombreDescuento,
                 TipoDescuento, ValorDescuento, ImporteDescuento)
            VALUES
                (@VentaTicketId, @IngestionBatchId, @FranqParaguayId,
                 CASE WHEN @PorcDescuento = 0.10 THEN 'DESC10' ELSE 'DESC15' END,
                 CASE WHEN @PorcDescuento = 0.10 THEN 'Descuento 10%' ELSE 'Descuento 15%' END,
                 'PERCENTAGE', @PorcDescuento * 100, @ImporteDescuento)
        END

        -- Actualizar totales del ticket
        UPDATE [fact].[VentaTicket]
        SET ImporteBruto = @TotalBruto,
            ImporteDescuento = @ImporteDescuento,
            ImporteNeto = @TotalBruto - @ImporteDescuento,
            ImporteTotalPagado = @TotalBruto - @ImporteDescuento,
            TieneDescuento = @TieneDescuento
        WHERE VentaTicketId = @VentaTicketId

        -- Medio de pago
        DECLARE @MedioPagoIdx INT = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MediosPago)
        DECLARE @MedioPagoId INT, @MedioPagoCodigo NVARCHAR(50)

        SELECT @MedioPagoId = MedioPagoId, @MedioPagoCodigo = Codigo FROM #MediosPago WHERE Idx = @MedioPagoIdx

        INSERT INTO [fact].[VentaTicketMedioPago]
            (VentaTicketId, IngestionBatchId, FranquiciaId, MedioPagoId, CodigoMedioPago, Importe)
        VALUES
            (@VentaTicketId, @IngestionBatchId, @FranqParaguayId, @MedioPagoId, @MedioPagoCodigo, @TotalBruto - @ImporteDescuento)

        SET @TicketNum = @TicketNum + 1
        SET @TotalTicketsGenerados = @TotalTicketsGenerados + 1
    END

    -- Actualizar totales del batch Paraguay
    UPDATE [stg].[IngestionBatch]
    SET TicketCountCalculado = (SELECT COUNT(*) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        GrossSalesCalculado = (SELECT ISNULL(SUM(ImporteBruto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        NetSalesCalculado = (SELECT ISNULL(SUM(ImporteNeto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        GrossSalesRecibido = (SELECT ISNULL(SUM(ImporteBruto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        NetSalesRecibido = (SELECT ISNULL(SUM(ImporteNeto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        FechaFinProceso = GETUTCDATE()
    WHERE IngestionBatchId = @IngestionBatchId

    -- =============================================
    -- FRANQUICIA MIAMI
    -- =============================================

    -- Determinar cantidad de tickets segun dia de semana (Miami tiene mas volumen)
    SET @TicketsDelDia = CASE
        WHEN @DiaSemana IN (1, 7) THEN 70 + ABS(CHECKSUM(NEWID())) % 21  -- Dom/Sab: 70-90
        WHEN @DiaSemana = 6 THEN 60 + ABS(CHECKSUM(NEWID())) % 16       -- Vie: 60-75
        ELSE 45 + ABS(CHECKSUM(NEWID())) % 11                           -- Lun-Jue: 45-55
    END

    -- Crear batch para Miami
    SET @BatchId = 'SEED-MI-' + REPLACE(CONVERT(VARCHAR(10), @FechaActual, 120), '-', '')

    INSERT INTO [stg].[IngestionBatch]
        (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta,
         TicketCountRecibido, Estado, SistemaOrigenNombre)
    VALUES
        (@BatchId, @FranqMiamiId, @FechaActual, '1.0', 'FULL_DAY', 'SEED',
         @TicketsDelDia, 'PROCESSED', 'Demo Seed Script')

    SET @IngestionBatchId = SCOPE_IDENTITY()

    -- Generar tickets Miami
    SET @TicketNum = 1
    WHILE @TicketNum <= @TicketsDelDia
    BEGIN
        -- Hora del ticket (Miami: mas cenas, turistas)
        SET @EsAlmuerzo = CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 25 THEN 1 ELSE 0 END
        SET @HoraBase = CASE WHEN @EsAlmuerzo = 1 THEN 12 ELSE 19 END  -- Cena mas temprano en USA
        SET @MinutoRandom = ABS(CHECKSUM(NEWID())) % 210  -- 0-209 minutos
        SET @FechaApertura = DATEADD(MINUTE, @HoraBase * 60 + @MinutoRandom, CAST(@FechaActual AS DATETIME2))
        SET @DuracionMinutos = 50 + ABS(CHECKSUM(NEWID())) % 70  -- 50-119 minutos
        SET @FechaCierre = DATEADD(MINUTE, @DuracionMinutos, @FechaApertura)

        -- Seleccionar mesa y mozo aleatorios
        SET @MesaIdx = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MesasMiami)
        SET @MozoIdx = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MozosMiami)

        SELECT @MesaId = MesaId, @NumeroMesa = Numero, @AreaMesa = Area FROM #MesasMiami WHERE Idx = @MesaIdx
        SELECT @MozoId = MozoId, @NombreMozo = Nombre FROM #MozosMiami WHERE Idx = @MozoIdx

        -- Cubiertos (2-8 personas, Miami tiene grupos mas grandes)
        SET @Cubiertos = 2 + ABS(CHECKSUM(NEWID())) % 7

        -- External IDs
        SET @ExternalTicketId = 'TKT-MI-' + REPLACE(CONVERT(VARCHAR(10), @FechaActual, 120), '-', '') + '-' + RIGHT('000' + CAST(@TicketNum AS VARCHAR(4)), 4)
        SET @NumeroTicketStr = 'M-' + RIGHT('0000' + CAST(((@TicketNum + 2000) % 10000) AS VARCHAR(5)), 5)

        -- Periodo de comida
        SET @PeriodoComida = CASE WHEN @EsAlmuerzo = 1 THEN 'LUNCH' ELSE 'DINNER' END

        INSERT INTO [fact].[VentaTicket]
            (IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado,
             FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
             MesaId, NumeroMesa, AreaMesa, MozoId, NombreMozo, CantidadCubiertos,
             MonedaId, CodigoMoneda, ImporteBruto, ImporteDescuento, ImporteNeto,
             TieneDescuento, TiempoConsumoMinutos, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa,
             FuenteSistema)
        VALUES
            (@IngestionBatchId, @FranqMiamiId, @ExternalTicketId, @NumeroTicketStr, 'CLOSED',
             @FechaActual, @FechaApertura, @FechaCierre, @PeriodoComida,
             @MesaId, @NumeroMesa, @AreaMesa, @MozoId, @NombreMozo, @Cubiertos,
             @MonedaUSD, 'USD', 0, 0, 0,
             0, @DuracionMinutos, 1, 1, 1,
             'Demo Seed')

        SET @VentaTicketId = SCOPE_IDENTITY()

        -- Generar items del ticket
        SET @ItemNum = 1
        SET @TotalBruto = 0
        SET @CantidadItems = 4 + ABS(CHECKSUM(NEWID())) % 5  -- 4-8 items (Miami tiene tickets mas grandes)

        -- Item 1: Carne principal
        SELECT TOP 1 @ProdId = ProductoId, @ProdCodigo = Codigo, @ProdNombre = Nombre, @ProdPrecio = Precio, @ProdTipo = TipoPlato
        FROM #ProductosMiami WHERE TipoPlato = 'MAIN_COURSE' ORDER BY NEWID()

        INSERT INTO [fact].[VentaTicketDetalle]
            (VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId,
             ProductoId, CodigoProducto, NombreProducto, CategoriaProducto,
             Cantidad, PrecioUnitario, ImporteBruto, ImporteDescuento, ImporteNeto)
        VALUES
            (@VentaTicketId, @IngestionBatchId, @FranqMiamiId, 'L-' + CAST(@ItemNum AS VARCHAR(3)),
             @ProdId, @ProdCodigo, @ProdNombre, @ProdTipo,
             1, @ProdPrecio, @ProdPrecio, 0, @ProdPrecio)

        SET @TotalBruto = @TotalBruto + @ProdPrecio
        SET @ItemNum = @ItemNum + 1

        -- Items adicionales
        WHILE @ItemNum <= @CantidadItems
        BEGIN
            SELECT TOP 1 @ProdId = ProductoId, @ProdCodigo = Codigo, @ProdNombre = Nombre, @ProdPrecio = Precio, @ProdTipo = TipoPlato
            FROM #ProductosMiami ORDER BY NEWID()

            SET @Cantidad = CASE
                WHEN @ProdTipo IN ('BEVERAGE', 'COFFEE') THEN 1 + ABS(CHECKSUM(NEWID())) % 3
                ELSE 1
            END

            INSERT INTO [fact].[VentaTicketDetalle]
                (VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId,
                 ProductoId, CodigoProducto, NombreProducto, CategoriaProducto,
                 Cantidad, PrecioUnitario, ImporteBruto, ImporteDescuento, ImporteNeto)
            VALUES
                (@VentaTicketId, @IngestionBatchId, @FranqMiamiId, 'L-' + CAST(@ItemNum AS VARCHAR(3)),
                 @ProdId, @ProdCodigo, @ProdNombre, @ProdTipo,
                 @Cantidad, @ProdPrecio, @ProdPrecio * @Cantidad, 0, @ProdPrecio * @Cantidad)

            SET @TotalBruto = @TotalBruto + (@ProdPrecio * @Cantidad)
            SET @ItemNum = @ItemNum + 1
        END

        -- Descuento (12% de tickets tiene descuento en Miami - menos descuentos)
        SET @TieneDescuento = CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 12 THEN 1 ELSE 0 END
        SET @ImporteDescuento = 0

        IF @TieneDescuento = 1
        BEGIN
            SET @PorcDescuento = CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 0.10 ELSE 0.15 END
            SET @ImporteDescuento = ROUND(@TotalBruto * @PorcDescuento, 2)

            INSERT INTO [fact].[VentaTicketDescuento]
                (VentaTicketId, IngestionBatchId, FranquiciaId, CodigoDescuento, NombreDescuento,
                 TipoDescuento, ValorDescuento, ImporteDescuento)
            VALUES
                (@VentaTicketId, @IngestionBatchId, @FranqMiamiId,
                 CASE WHEN @PorcDescuento = 0.10 THEN 'PROMO10' ELSE 'VIP15' END,
                 CASE WHEN @PorcDescuento = 0.10 THEN '10% Off Promotion' ELSE '15% VIP Discount' END,
                 'PERCENTAGE', @PorcDescuento * 100, @ImporteDescuento)
        END

        -- Propina (tipico en USA: 18-22%)
        DECLARE @Propina DECIMAL(18,2) = ROUND((@TotalBruto - @ImporteDescuento) * (0.18 + (ABS(CHECKSUM(NEWID())) % 5) * 0.01), 2)

        -- Actualizar totales del ticket
        UPDATE [fact].[VentaTicket]
        SET ImporteBruto = @TotalBruto,
            ImporteDescuento = @ImporteDescuento,
            ImporteNeto = @TotalBruto - @ImporteDescuento,
            ImportePropina = @Propina,
            ImporteTotalPagado = @TotalBruto - @ImporteDescuento + @Propina,
            TieneDescuento = @TieneDescuento
        WHERE VentaTicketId = @VentaTicketId

        -- Medio de pago
        SET @MedioPagoIdx = 1 + ABS(CHECKSUM(NEWID())) % (SELECT COUNT(*) FROM #MediosPago)
        SELECT @MedioPagoId = MedioPagoId, @MedioPagoCodigo = Codigo FROM #MediosPago WHERE Idx = @MedioPagoIdx

        INSERT INTO [fact].[VentaTicketMedioPago]
            (VentaTicketId, IngestionBatchId, FranquiciaId, MedioPagoId, CodigoMedioPago, Importe)
        VALUES
            (@VentaTicketId, @IngestionBatchId, @FranqMiamiId, @MedioPagoId, @MedioPagoCodigo, @TotalBruto - @ImporteDescuento + @Propina)

        SET @TicketNum = @TicketNum + 1
        SET @TotalTicketsGenerados = @TotalTicketsGenerados + 1
    END

    -- Actualizar totales del batch Miami
    UPDATE [stg].[IngestionBatch]
    SET TicketCountCalculado = (SELECT COUNT(*) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        GrossSalesCalculado = (SELECT ISNULL(SUM(ImporteBruto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        NetSalesCalculado = (SELECT ISNULL(SUM(ImporteNeto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        GrossSalesRecibido = (SELECT ISNULL(SUM(ImporteBruto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        NetSalesRecibido = (SELECT ISNULL(SUM(ImporteNeto), 0) FROM [fact].[VentaTicket] WHERE IngestionBatchId = @IngestionBatchId),
        FechaFinProceso = GETUTCDATE()
    WHERE IngestionBatchId = @IngestionBatchId

    -- Siguiente dia
    SET @FechaActual = DATEADD(DAY, 1, @FechaActual)
END

-- Actualizar ultima sincronizacion de franquicias
UPDATE [dim].[Franquicia]
SET UltimaSincronizacion = GETUTCDATE(),
    EstadoIntegracion = 'ACTIVE'
WHERE FranquiciaId IN (@FranqParaguayId, @FranqMiamiId)

-- ============================================================================
-- LIMPIEZA Y RESUMEN
-- ============================================================================

DROP TABLE #ProductosParaguay
DROP TABLE #ProductosMiami
DROP TABLE #MozosParaguay
DROP TABLE #MozosMiami
DROP TABLE #MesasParaguay
DROP TABLE #MesasMiami
DROP TABLE #MediosPago

PRINT ''
PRINT '============================================'
PRINT 'GENERACION DE DATOS COMPLETADA'
PRINT '============================================'
PRINT ''
PRINT 'Total de tickets generados: ' + CAST(@TotalTicketsGenerados AS VARCHAR(10))
PRINT ''

-- Mostrar resumen
SELECT
    'RESUMEN POR FRANQUICIA' AS [Estadisticas],
    f.Nombre AS Franquicia,
    COUNT(DISTINCT b.IngestionBatchId) AS [Batches],
    COUNT(t.VentaTicketId) AS [Tickets],
    SUM(t.CantidadCubiertos) AS [Cubiertos],
    FORMAT(SUM(t.ImporteBruto), 'N2') AS [Venta Bruta],
    FORMAT(SUM(t.ImporteDescuento), 'N2') AS [Descuentos],
    FORMAT(SUM(t.ImporteNeto), 'N2') AS [Venta Neta],
    FORMAT(AVG(t.ImporteNeto), 'N2') AS [Ticket Promedio]
FROM [fact].[VentaTicket] t
JOIN [stg].[IngestionBatch] b ON t.IngestionBatchId = b.IngestionBatchId
JOIN [dim].[Franquicia] f ON t.FranquiciaId = f.FranquiciaId
GROUP BY f.FranquiciaId, f.Nombre

SELECT
    'RESUMEN POR DIA DE SEMANA' AS [Estadisticas],
    DATENAME(WEEKDAY, t.FechaNegocio) AS [Dia],
    COUNT(t.VentaTicketId) AS [Tickets],
    FORMAT(AVG(t.ImporteNeto), 'N2') AS [Ticket Promedio]
FROM [fact].[VentaTicket] t
GROUP BY DATEPART(WEEKDAY, t.FechaNegocio), DATENAME(WEEKDAY, t.FechaNegocio)
ORDER BY DATEPART(WEEKDAY, t.FechaNegocio)

SELECT
    'TOP 10 PRODUCTOS MAS VENDIDOS' AS [Estadisticas],
    d.NombreProducto,
    f.Nombre AS Franquicia,
    SUM(d.Cantidad) AS [Cantidad],
    FORMAT(SUM(d.ImporteNeto), 'N2') AS [Venta Neta]
FROM [fact].[VentaTicketDetalle] d
JOIN [dim].[Franquicia] f ON d.FranquiciaId = f.FranquiciaId
GROUP BY d.NombreProducto, f.Nombre
ORDER BY SUM(d.ImporteNeto) DESC

PRINT ''
PRINT 'Para verificar los datos en las vistas del dashboard:'
PRINT '  SELECT * FROM [fact].[vw_HomeDashboard]'
PRINT '  SELECT * FROM [fact].[vw_VentasResumenDiario]'
PRINT '  SELECT * FROM [fact].[vw_VentasPorFranquicia]'
PRINT ''
GO
