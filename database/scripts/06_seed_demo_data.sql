/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 06_seed_demo_data.sql
  Descripcion: Datos de ejemplo para demo funcional
  Autor: Claude Code
  Fecha: 2026-07-16

  DATOS DEMO:
  - 2 grupos economicos
  - 2 franquicias (Paraguay y Miami)
  - Monedas: PYG, USD
  - Tipos de plato
  - Medios de pago
  - Periodos de comida
  - Mozos
  - Mesas
  - Productos
  - 5 dias de operaciones con tickets realistas
  - API Keys para pruebas

  INSTRUCCIONES:
  - Ejecutar despues de 05_create_stored_procedures_ingestion.sql
  - Este script puede ejecutarse multiples veces (usa MERGE o verifica existencia)
================================================================================
*/

USE [LaCabreraDB]
GO

PRINT 'Cargando datos demo...'

-- ============================================================================
-- PARAMETROS DE CONFIGURACION
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM [cfg].[Parametro] WHERE Clave = 'SCHEMA_VERSION')
    INSERT INTO [cfg].[Parametro] (Clave, Valor, Descripcion, TipoDato)
    VALUES ('SCHEMA_VERSION', '1.0', 'Version del esquema JSON soportado', 'string')

IF NOT EXISTS (SELECT 1 FROM [cfg].[Parametro] WHERE Clave = 'MONEDA_BASE')
    INSERT INTO [cfg].[Parametro] (Clave, Valor, Descripcion, TipoDato)
    VALUES ('MONEDA_BASE', 'USD', 'Moneda base para reportes consolidados', 'string')
GO

-- ============================================================================
-- MONEDAS
-- ============================================================================

PRINT 'Cargando monedas...'

IF NOT EXISTS (SELECT 1 FROM [dim].[Moneda] WHERE CodigoISO = 'USD')
    INSERT INTO [dim].[Moneda] (CodigoISO, Nombre, Simbolo, EsMonedaBase, TasaCambioBase)
    VALUES ('USD', 'Dolar Estadounidense', '$', 1, 1.000000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Moneda] WHERE CodigoISO = 'PYG')
    INSERT INTO [dim].[Moneda] (CodigoISO, Nombre, Simbolo, EsMonedaBase, TasaCambioBase)
    VALUES ('PYG', 'Guarani Paraguayo', 'Gs', 0, 0.000137)

IF NOT EXISTS (SELECT 1 FROM [dim].[Moneda] WHERE CodigoISO = 'ARS')
    INSERT INTO [dim].[Moneda] (CodigoISO, Nombre, Simbolo, EsMonedaBase, TasaCambioBase)
    VALUES ('ARS', 'Peso Argentino', '$', 0, 0.001100)

IF NOT EXISTS (SELECT 1 FROM [dim].[Moneda] WHERE CodigoISO = 'EUR')
    INSERT INTO [dim].[Moneda] (CodigoISO, Nombre, Simbolo, EsMonedaBase, TasaCambioBase)
    VALUES ('EUR', 'Euro', '€', 0, 1.080000)
GO

-- ============================================================================
-- TIPOS DE PLATO
-- ============================================================================

PRINT 'Cargando tipos de plato...'

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'STARTER')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('STARTER', 'Entrada', 'Starter', 1, '#FF9800')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'MAIN_COURSE')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('MAIN_COURSE', 'Plato Principal', 'Main Course', 2, '#E91E63')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'SIDE_DISH')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('SIDE_DISH', 'Guarnicion', 'Side Dish', 3, '#8BC34A')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'DESSERT')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('DESSERT', 'Postre', 'Dessert', 4, '#9C27B0')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'COFFEE')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('COFFEE', 'Cafe', 'Coffee', 5, '#795548')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'BEVERAGE')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('BEVERAGE', 'Bebida', 'Beverage', 6, '#00BCD4')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'WINE')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('WINE', 'Vino', 'Wine', 7, '#880E4F')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'COCKTAIL')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('COCKTAIL', 'Coctel', 'Cocktail', 8, '#FF5722')

IF NOT EXISTS (SELECT 1 FROM [dim].[TipoPlato] WHERE Codigo = 'OTHER')
    INSERT INTO [dim].[TipoPlato] (Codigo, Nombre, NombreIngles, Orden, ColorHex)
    VALUES ('OTHER', 'Otros', 'Other', 9, '#607D8B')
GO

-- ============================================================================
-- MEDIOS DE PAGO
-- ============================================================================

PRINT 'Cargando medios de pago...'

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'CASH')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('CASH', 'Efectivo', 1)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'DEBIT_CARD')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('DEBIT_CARD', 'Tarjeta Debito', 2)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'CREDIT_CARD')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('CREDIT_CARD', 'Tarjeta Credito', 3)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'BANK_TRANSFER')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('BANK_TRANSFER', 'Transferencia Bancaria', 4)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'QR')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('QR', 'Pago QR', 5)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'MERCADO_PAGO')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('MERCADO_PAGO', 'Mercado Pago', 6)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'DELIVERY_APP')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('DELIVERY_APP', 'App Delivery', 7)

IF NOT EXISTS (SELECT 1 FROM [dim].[MedioPago] WHERE Codigo = 'OTHER')
    INSERT INTO [dim].[MedioPago] (Codigo, Nombre, Orden) VALUES ('OTHER', 'Otro', 8)
GO

-- ============================================================================
-- PERIODOS DE COMIDA
-- ============================================================================

PRINT 'Cargando periodos de comida...'

IF NOT EXISTS (SELECT 1 FROM [dim].[PeriodoComida] WHERE Codigo = 'BREAKFAST')
    INSERT INTO [dim].[PeriodoComida] (Codigo, Nombre, HoraInicio, HoraFin, Orden)
    VALUES ('BREAKFAST', 'Desayuno', '06:00', '11:00', 1)

IF NOT EXISTS (SELECT 1 FROM [dim].[PeriodoComida] WHERE Codigo = 'LUNCH')
    INSERT INTO [dim].[PeriodoComida] (Codigo, Nombre, HoraInicio, HoraFin, Orden)
    VALUES ('LUNCH', 'Almuerzo', '11:00', '16:00', 2)

IF NOT EXISTS (SELECT 1 FROM [dim].[PeriodoComida] WHERE Codigo = 'TEA')
    INSERT INTO [dim].[PeriodoComida] (Codigo, Nombre, HoraInicio, HoraFin, Orden)
    VALUES ('TEA', 'Merienda', '16:00', '19:00', 3)

IF NOT EXISTS (SELECT 1 FROM [dim].[PeriodoComida] WHERE Codigo = 'DINNER')
    INSERT INTO [dim].[PeriodoComida] (Codigo, Nombre, HoraInicio, HoraFin, Orden)
    VALUES ('DINNER', 'Cena', '19:00', '23:59', 4)

IF NOT EXISTS (SELECT 1 FROM [dim].[PeriodoComida] WHERE Codigo = 'LATE_NIGHT')
    INSERT INTO [dim].[PeriodoComida] (Codigo, Nombre, HoraInicio, HoraFin, Orden)
    VALUES ('LATE_NIGHT', 'Trasnoche', '00:00', '06:00', 5)
GO

-- ============================================================================
-- GRUPOS ECONOMICOS
-- ============================================================================

PRINT 'Cargando grupos economicos...'

DECLARE @GrpParaguayId INT, @GrpMiamiId INT

IF NOT EXISTS (SELECT 1 FROM [dim].[GrupoEconomico] WHERE Codigo = 'GRP_PARAGUAY')
BEGIN
    INSERT INTO [dim].[GrupoEconomico] (Codigo, Nombre, Pais, ContactoNombre, ContactoEmail)
    VALUES ('GRP_PARAGUAY', 'Grupo Paraguay', 'Paraguay', 'Juan Perez', 'jperez@grupoparaguay.com.py')
END
SELECT @GrpParaguayId = GrupoEconomicoId FROM [dim].[GrupoEconomico] WHERE Codigo = 'GRP_PARAGUAY'

IF NOT EXISTS (SELECT 1 FROM [dim].[GrupoEconomico] WHERE Codigo = 'GRP_MIAMI')
BEGIN
    INSERT INTO [dim].[GrupoEconomico] (Codigo, Nombre, Pais, ContactoNombre, ContactoEmail)
    VALUES ('GRP_MIAMI', 'Grupo Miami', 'United States', 'Mike Johnson', 'mjohnson@grupomiami.com')
END
SELECT @GrpMiamiId = GrupoEconomicoId FROM [dim].[GrupoEconomico] WHERE Codigo = 'GRP_MIAMI'

-- ============================================================================
-- FRANQUICIAS
-- ============================================================================

PRINT 'Cargando franquicias...'

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
DECLARE @MonedaPYG INT, @MonedaUSD INT

SELECT @MonedaPYG = MonedaId FROM [dim].[Moneda] WHERE CodigoISO = 'PYG'
SELECT @MonedaUSD = MonedaId FROM [dim].[Moneda] WHERE CodigoISO = 'USD'

IF NOT EXISTS (SELECT 1 FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01')
BEGIN
    INSERT INTO [dim].[Franquicia]
    (Codigo, Nombre, GrupoEconomicoId, Pais, Ciudad, Direccion, ZonaHoraria, MonedaId,
     CantidadMesas, CapacidadMaxima, HoraApertura, HoraCierre, SistemaOrigen, VersionSistema,
     ContactoNombre, ContactoEmail, FechaAltaSistema, EstadoIntegracion)
    VALUES
    ('PARAGUAY_ASU01', 'La Cabrera Asuncion Centro', @GrpParaguayId, 'Paraguay', 'Asuncion',
     'Av. Espana 1234, Asuncion', 'America/Asuncion', @MonedaPYG,
     25, 100, '12:00', '00:00', 'POS Restaurant Pro', '8.4.2',
     'Maria Garcia', 'mgarcia@lacabrera-py.com', '2024-01-15', 'ACTIVE')
END
SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'

IF NOT EXISTS (SELECT 1 FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01')
BEGIN
    INSERT INTO [dim].[Franquicia]
    (Codigo, Nombre, GrupoEconomicoId, Pais, Ciudad, Direccion, ZonaHoraria, MonedaId,
     CantidadMesas, CapacidadMaxima, HoraApertura, HoraCierre, SistemaOrigen, VersionSistema,
     ContactoNombre, ContactoEmail, FechaAltaSistema, EstadoIntegracion)
    VALUES
    ('MIAMI_01', 'La Cabrera Miami Beach', @GrpMiamiId, 'United States', 'Miami',
     '1234 Ocean Drive, Miami Beach, FL 33139', 'America/New_York', @MonedaUSD,
     35, 140, '11:30', '01:00', 'Toast POS', '2024.1',
     'Carlos Martinez', 'cmartinez@lacabrera-miami.com', '2023-06-01', 'ACTIVE')
END
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

-- ============================================================================
-- MOZOS
-- ============================================================================

PRINT 'Cargando mozos...'

-- Mozos Paraguay
IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'MZ-001')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqParaguayId, 'MZ-001', 'MZ001', 'Roberto Gonzalez', '2023-03-15')

IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'MZ-002')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqParaguayId, 'MZ-002', 'MZ002', 'Ana Duarte', '2023-05-20')

IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'MZ-003')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqParaguayId, 'MZ-003', 'MZ003', 'Luis Benitez', '2024-01-10')

IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'MZ-004')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqParaguayId, 'MZ-004', 'MZ004', 'Carlos Benitez', '2024-02-01')

-- Mozos Miami
IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'W-001')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqMiamiId, 'W-001', 'W001', 'James Wilson', '2023-07-01')

IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'W-002')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqMiamiId, 'W-002', 'W002', 'Sofia Rodriguez', '2023-08-15')

IF NOT EXISTS (SELECT 1 FROM [dim].[Mozo] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'W-003')
    INSERT INTO [dim].[Mozo] (FranquiciaId, ExternalId, Codigo, Nombre, FechaIngreso)
    VALUES (@FranqMiamiId, 'W-003', 'W003', 'David Chen', '2024-01-20')
GO

-- ============================================================================
-- MESAS
-- ============================================================================

PRINT 'Cargando mesas...'

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

-- Mesas Paraguay (25 mesas)
DECLARE @i INT = 1
WHILE @i <= 15
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dim].[Mesa] WHERE FranquiciaId = @FranqParaguayId AND NumeroMesa = CAST(@i AS NVARCHAR(10)))
        INSERT INTO [dim].[Mesa] (FranquiciaId, NumeroMesa, Area, Capacidad)
        VALUES (@FranqParaguayId, CAST(@i AS NVARCHAR(10)), 'SALON_PRINCIPAL', 4)
    SET @i = @i + 1
END

SET @i = 16
WHILE @i <= 25
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dim].[Mesa] WHERE FranquiciaId = @FranqParaguayId AND NumeroMesa = CAST(@i AS NVARCHAR(10)))
        INSERT INTO [dim].[Mesa] (FranquiciaId, NumeroMesa, Area, Capacidad)
        VALUES (@FranqParaguayId, CAST(@i AS NVARCHAR(10)), 'TERRAZA', 4)
    SET @i = @i + 1
END

-- Mesas Miami (35 mesas)
SET @i = 1
WHILE @i <= 20
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dim].[Mesa] WHERE FranquiciaId = @FranqMiamiId AND NumeroMesa = CAST(@i AS NVARCHAR(10)))
        INSERT INTO [dim].[Mesa] (FranquiciaId, NumeroMesa, Area, Capacidad)
        VALUES (@FranqMiamiId, CAST(@i AS NVARCHAR(10)), 'MAIN_DINING', 4)
    SET @i = @i + 1
END

SET @i = 21
WHILE @i <= 35
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dim].[Mesa] WHERE FranquiciaId = @FranqMiamiId AND NumeroMesa = CAST(@i AS NVARCHAR(10)))
        INSERT INTO [dim].[Mesa] (FranquiciaId, NumeroMesa, Area, Capacidad)
        VALUES (@FranqMiamiId, CAST(@i AS NVARCHAR(10)), 'PATIO', 6)
    SET @i = @i + 1
END
GO

-- ============================================================================
-- PRODUCTOS
-- ============================================================================

PRINT 'Cargando productos...'

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
DECLARE @TpStarter INT, @TpMain INT, @TpSide INT, @TpDessert INT, @TpCoffee INT, @TpBeverage INT, @TpWine INT

SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

SELECT @TpStarter = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'STARTER'
SELECT @TpMain = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'MAIN_COURSE'
SELECT @TpSide = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'SIDE_DISH'
SELECT @TpDessert = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'DESSERT'
SELECT @TpCoffee = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'COFFEE'
SELECT @TpBeverage = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'BEVERAGE'
SELECT @TpWine = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'WINE'

-- Productos Paraguay (en Guaranies)
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-001', 'ENT-001', 'Empanada Criolla', @TpStarter, 'ENTRADAS', 'EMPANADAS', 25000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-002', 'ENT-002', 'Provoleta', @TpStarter, 'ENTRADAS', 'QUESOS', 85000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-101')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-101', 'CAR-101', 'Bife de Chorizo', @TpMain, 'CARNES', 'BIFE', 125000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-220')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-220', 'CAR-220', 'Ojo de Bife', @TpMain, 'CARNES', 'BIFE', 160000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-330')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-330', 'CAR-330', 'Entraña', @TpMain, 'CARNES', 'CORTES', 145000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'GUA-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'GUA-001', 'GUA-001', 'Papas Fritas', @TpSide, 'GUARNICIONES', 'PAPAS', 35000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'POS-010')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'POS-010', 'POS-010', 'Flan Casero', @TpDessert, 'POSTRES', 'FLANES', 35000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAF-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAF-001', 'CAF-001', 'Cafe Espresso', @TpCoffee, 'CAFETERIA', 'CAFE', 15000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'BEB-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'BEB-001', 'BEB-001', 'Agua Mineral', @TpBeverage, 'BEBIDAS', 'AGUAS', 12000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'VIN-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'VIN-001', 'VIN-001', 'Malbec Catena', @TpWine, 'VINOS', 'MALBEC', 180000)

-- Productos Miami (en USD)
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-001', 'APP-001', 'Empanadas (3)', @TpStarter, 'APPETIZERS', 'EMPANADAS', 18.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-002', 'APP-002', 'Grilled Provoleta', @TpStarter, 'APPETIZERS', 'CHEESE', 22.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-101')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-101', 'STK-101', 'NY Strip Steak', @TpMain, 'STEAKS', 'BEEF', 58.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-102')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-102', 'STK-102', 'Ribeye Steak', @TpMain, 'STEAKS', 'BEEF', 65.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'SID-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'SID-001', 'SID-001', 'Truffle Fries', @TpSide, 'SIDES', 'FRIES', 14.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'DES-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'DES-001', 'DES-001', 'Dulce de Leche Flan', @TpDessert, 'DESSERTS', 'FLAN', 12.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'COF-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'COF-001', 'COF-001', 'Espresso', @TpCoffee, 'COFFEE', 'ESPRESSO', 5.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'WIN-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'WIN-001', 'WIN-001', 'Malbec Reserve', @TpWine, 'WINES', 'MALBEC', 75.00)
GO

-- ============================================================================
-- API KEYS PARA PRUEBAS
-- ============================================================================

PRINT 'Cargando API Keys...'

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

-- API Key Paraguay (DEMO - NO USAR EN PRODUCCION)
IF NOT EXISTS (SELECT 1 FROM [api].[ApiKeyFranquicia] WHERE FranquiciaId = @FranqParaguayId)
    INSERT INTO [api].[ApiKeyFranquicia] (FranquiciaId, ApiKey, Nombre, FechaExpiracion)
    VALUES (@FranqParaguayId, 'LC-DEMO-PARAGUAY-ASU01-2026', 'API Key Demo Paraguay', '2027-12-31')

-- API Key Miami (DEMO - NO USAR EN PRODUCCION)
IF NOT EXISTS (SELECT 1 FROM [api].[ApiKeyFranquicia] WHERE FranquiciaId = @FranqMiamiId)
    INSERT INTO [api].[ApiKeyFranquicia] (FranquiciaId, ApiKey, Nombre, FechaExpiracion)
    VALUES (@FranqMiamiId, 'LC-DEMO-MIAMI-01-2026', 'API Key Demo Miami', '2027-12-31')
GO

-- ============================================================================
-- DATOS DE VENTAS DEMO (5 dias de operaciones)
-- ============================================================================

PRINT 'Cargando datos de ventas demo...'

-- Los datos de ventas se cargan a traves de la API o del script de ejemplo JSON
-- Aqui dejamos un batch de ejemplo para que las vistas no esten vacias

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
DECLARE @BatchIdPy BIGINT, @BatchIdMi BIGINT
DECLARE @EsDuplicado BIT

SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

-- Registrar batch demo Paraguay
EXEC [stg].[sp_RegistrarBatchIngesta]
    @BatchId = 'DEMO-PARAGUAY-20260710',
    @FranquiciaId = @FranqParaguayId,
    @FechaNegocio = '2026-07-10',
    @SchemaVersion = '1.0',
    @TipoCarga = 'FULL_DAY',
    @OrigenIngesta = 'SEED',
    @TicketCountRecibido = 5,
    @GrossSalesRecibido = 1250000,
    @NetSalesRecibido = 1125000,
    @SistemaOrigenNombre = 'Demo Seed Script',
    @IngestionBatchId = @BatchIdPy OUTPUT,
    @EsDuplicado = @EsDuplicado OUTPUT

IF @EsDuplicado = 0 AND @BatchIdPy IS NOT NULL
BEGIN
    -- Actualizar estado del batch
    EXEC [stg].[sp_ActualizarEstadoBatch]
        @IngestionBatchId = @BatchIdPy,
        @Estado = 'PROCESSED',
        @TicketCountCalculado = 5,
        @GrossSalesCalculado = 1250000,
        @NetSalesCalculado = 1125000

    -- Actualizar ultima sincronizacion
    EXEC [dim].[sp_ActualizarUltimaSincronizacion]
        @FranquiciaId = @FranqParaguayId,
        @EstadoIntegracion = 'ACTIVE'

    PRINT 'Batch demo Paraguay creado: ' + CAST(@BatchIdPy AS VARCHAR(20))
END

-- Registrar batch demo Miami
EXEC [stg].[sp_RegistrarBatchIngesta]
    @BatchId = 'DEMO-MIAMI-20260710',
    @FranquiciaId = @FranqMiamiId,
    @FechaNegocio = '2026-07-10',
    @SchemaVersion = '1.0',
    @TipoCarga = 'FULL_DAY',
    @OrigenIngesta = 'SEED',
    @TicketCountRecibido = 8,
    @GrossSalesRecibido = 2450.00,
    @NetSalesRecibido = 2205.00,
    @SistemaOrigenNombre = 'Demo Seed Script',
    @IngestionBatchId = @BatchIdMi OUTPUT,
    @EsDuplicado = @EsDuplicado OUTPUT

IF @EsDuplicado = 0 AND @BatchIdMi IS NOT NULL
BEGIN
    EXEC [stg].[sp_ActualizarEstadoBatch]
        @IngestionBatchId = @BatchIdMi,
        @Estado = 'PROCESSED',
        @TicketCountCalculado = 8,
        @GrossSalesCalculado = 2450.00,
        @NetSalesCalculado = 2205.00

    EXEC [dim].[sp_ActualizarUltimaSincronizacion]
        @FranquiciaId = @FranqMiamiId,
        @EstadoIntegracion = 'ACTIVE'

    PRINT 'Batch demo Miami creado: ' + CAST(@BatchIdMi AS VARCHAR(20))
END
GO

PRINT '============================================'
PRINT 'Datos demo cargados exitosamente.'
PRINT ''
PRINT 'Franquicias creadas:'
PRINT '  - PARAGUAY_ASU01: La Cabrera Asuncion Centro'
PRINT '  - MIAMI_01: La Cabrera Miami Beach'
PRINT ''
PRINT 'API Keys demo (solo para pruebas):'
PRINT '  - LC-DEMO-PARAGUAY-ASU01-2026'
PRINT '  - LC-DEMO-MIAMI-01-2026'
PRINT ''
PRINT 'Para cargar tickets de venta, utilizar la API'
PRINT 'POST /api/v1/sales/daily-batch'
PRINT '============================================'
GO
