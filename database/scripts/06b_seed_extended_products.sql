/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 06b_seed_extended_products.sql
  Descripcion: Catalogo extendido de productos para demo realista
  Autor: Claude Code
  Fecha: 2026-07-16

  PRODUCTOS AGREGADOS:
  - Paraguay: 30+ productos tipicos de parrilla argentina
  - Miami: 25+ productos con nombres en ingles

  INSTRUCCIONES:
  - Ejecutar despues de 06_seed_demo_data.sql
  - Ejecutar antes de 07_seed_realistic_sales_data.sql
================================================================================
*/

USE [LaCabreraDB]
GO

PRINT 'Cargando catalogo extendido de productos...'
PRINT ''

DECLARE @FranqParaguayId INT, @FranqMiamiId INT
DECLARE @TpStarter INT, @TpMain INT, @TpSide INT, @TpDessert INT, @TpCoffee INT, @TpBeverage INT, @TpWine INT, @TpCocktail INT

SELECT @FranqParaguayId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'PARAGUAY_ASU01'
SELECT @FranqMiamiId = FranquiciaId FROM [dim].[Franquicia] WHERE Codigo = 'MIAMI_01'

SELECT @TpStarter = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'STARTER'
SELECT @TpMain = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'MAIN_COURSE'
SELECT @TpSide = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'SIDE_DISH'
SELECT @TpDessert = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'DESSERT'
SELECT @TpCoffee = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'COFFEE'
SELECT @TpBeverage = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'BEVERAGE'
SELECT @TpWine = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'WINE'
SELECT @TpCocktail = TipoPlatoId FROM [dim].[TipoPlato] WHERE Codigo = 'COCKTAIL'

-- ============================================================================
-- PRODUCTOS ADICIONALES PARAGUAY (en Guaranies)
-- ============================================================================

PRINT 'Cargando productos Paraguay...'

-- ENTRADAS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-003', 'ENT-003', 'Chorizo Criollo', @TpStarter, 'ENTRADAS', 'EMBUTIDOS', 45000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-004', 'ENT-004', 'Morcilla', @TpStarter, 'ENTRADAS', 'EMBUTIDOS', 35000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-005')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-005', 'ENT-005', 'Tabla de Achuras', @TpStarter, 'ENTRADAS', 'ACHURAS', 95000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-006')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-006', 'ENT-006', 'Ensalada Mixta', @TpStarter, 'ENTRADAS', 'ENSALADAS', 38000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'ENT-007')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'ENT-007', 'ENT-007', 'Mollejas a la Parrilla', @TpStarter, 'ENTRADAS', 'ACHURAS', 75000)

-- CARNES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-440')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-440', 'CAR-440', 'Vacio', @TpMain, 'CARNES', 'CORTES', 135000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-550')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-550', 'CAR-550', 'Asado de Tira', @TpMain, 'CARNES', 'CORTES', 155000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-660')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-660', 'CAR-660', 'Lomo', @TpMain, 'CARNES', 'BIFE', 185000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-770')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-770', 'CAR-770', 'Parrillada Completa (2 pers)', @TpMain, 'CARNES', 'PARRILLADA', 320000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-880')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-880', 'CAR-880', 'Cuadril', @TpMain, 'CARNES', 'CORTES', 115000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAR-990')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAR-990', 'CAR-990', 'Colita de Cuadril', @TpMain, 'CARNES', 'CORTES', 128000)

-- GUARNICIONES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'GUA-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'GUA-002', 'GUA-002', 'Pure de Papa', @TpSide, 'GUARNICIONES', 'PURE', 32000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'GUA-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'GUA-003', 'GUA-003', 'Ensalada Cesar', @TpSide, 'GUARNICIONES', 'ENSALADAS', 42000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'GUA-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'GUA-004', 'GUA-004', 'Vegetales Grillados', @TpSide, 'GUARNICIONES', 'VEGETALES', 38000)

-- POSTRES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'POS-011')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'POS-011', 'POS-011', 'Tiramisu', @TpDessert, 'POSTRES', 'ESPECIALES', 42000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'POS-012')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'POS-012', 'POS-012', 'Panqueque con Dulce de Leche', @TpDessert, 'POSTRES', 'CLASICOS', 38000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'POS-013')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'POS-013', 'POS-013', 'Helado Artesanal', @TpDessert, 'POSTRES', 'HELADOS', 28000)

-- CAFETERIA
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAF-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAF-002', 'CAF-002', 'Cafe Cortado', @TpCoffee, 'CAFETERIA', 'CAFE', 18000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'CAF-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'CAF-003', 'CAF-003', 'Te', @TpCoffee, 'CAFETERIA', 'INFUSIONES', 12000)

-- BEBIDAS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'BEB-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'BEB-002', 'BEB-002', 'Gaseosa', @TpBeverage, 'BEBIDAS', 'GASEOSAS', 15000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'BEB-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'BEB-003', 'BEB-003', 'Cerveza Pilsen', @TpBeverage, 'BEBIDAS', 'CERVEZAS', 22000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'BEB-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'BEB-004', 'BEB-004', 'Cerveza Artesanal', @TpBeverage, 'BEBIDAS', 'CERVEZAS', 35000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'BEB-005')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'BEB-005', 'BEB-005', 'Limonada', @TpBeverage, 'BEBIDAS', 'JUGOS', 18000)

-- VINOS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'VIN-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'VIN-002', 'VIN-002', 'Malbec Rutini', @TpWine, 'VINOS', 'MALBEC', 280000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'VIN-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'VIN-003', 'VIN-003', 'Cabernet Sauvignon', @TpWine, 'VINOS', 'CABERNET', 160000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'VIN-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'VIN-004', 'VIN-004', 'Copa de Vino Tinto', @TpWine, 'VINOS', 'COPAS', 45000)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqParaguayId AND ExternalId = 'VIN-005')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqParaguayId, 'VIN-005', 'VIN-005', 'Copa de Vino Blanco', @TpWine, 'VINOS', 'COPAS', 42000)

-- ============================================================================
-- PRODUCTOS ADICIONALES MIAMI (en USD)
-- ============================================================================

PRINT 'Cargando productos Miami...'

-- APPETIZERS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-003', 'APP-003', 'Chorizo Argentino', @TpStarter, 'APPETIZERS', 'SAUSAGE', 16.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-004', 'APP-004', 'Sweetbreads', @TpStarter, 'APPETIZERS', 'OFFAL', 24.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-005')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-005', 'APP-005', 'Mixed Salad', @TpStarter, 'APPETIZERS', 'SALADS', 14.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'APP-006')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'APP-006', 'APP-006', 'Burrata & Tomato', @TpStarter, 'APPETIZERS', 'CHEESE', 19.00)

-- STEAKS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-103')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-103', 'STK-103', 'Filet Mignon', @TpMain, 'STEAKS', 'BEEF', 72.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-104')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-104', 'STK-104', 'Porterhouse for Two', @TpMain, 'STEAKS', 'BEEF', 135.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-105')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-105', 'STK-105', 'Wagyu Ribeye', @TpMain, 'STEAKS', 'PREMIUM', 145.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-106')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-106', 'STK-106', 'Skirt Steak', @TpMain, 'STEAKS', 'BEEF', 48.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'STK-107')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'STK-107', 'STK-107', 'Mixed Grill Parrillada', @TpMain, 'STEAKS', 'GRILL', 95.00)

-- SIDES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'SID-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'SID-002', 'SID-002', 'Mashed Potatoes', @TpSide, 'SIDES', 'POTATOES', 12.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'SID-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'SID-003', 'SID-003', 'Grilled Vegetables', @TpSide, 'SIDES', 'VEGETABLES', 14.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'SID-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'SID-004', 'SID-004', 'Caesar Salad', @TpSide, 'SIDES', 'SALADS', 16.00)

-- DESSERTS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'DES-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'DES-002', 'DES-002', 'Tiramisu', @TpDessert, 'DESSERTS', 'ITALIAN', 14.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'DES-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'DES-003', 'DES-003', 'Chocolate Lava Cake', @TpDessert, 'DESSERTS', 'CHOCOLATE', 15.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'DES-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'DES-004', 'DES-004', 'Gelato Selection', @TpDessert, 'DESSERTS', 'ICE CREAM', 10.00)

-- COFFEE
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'COF-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'COF-002', 'COF-002', 'Cappuccino', @TpCoffee, 'COFFEE', 'SPECIALTY', 6.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'COF-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'COF-003', 'COF-003', 'Cortado', @TpCoffee, 'COFFEE', 'SPECIALTY', 5.50)

-- BEVERAGES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'BEV-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'BEV-001', 'BEV-001', 'Sparkling Water', @TpBeverage, 'BEVERAGES', 'WATER', 8.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'BEV-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'BEV-002', 'BEV-002', 'Soft Drink', @TpBeverage, 'BEVERAGES', 'SODA', 5.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'BEV-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'BEV-003', 'BEV-003', 'Craft Beer', @TpBeverage, 'BEVERAGES', 'BEER', 12.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'BEV-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'BEV-004', 'BEV-004', 'Fresh Lemonade', @TpBeverage, 'BEVERAGES', 'JUICE', 8.00)

-- WINES
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'WIN-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'WIN-002', 'WIN-002', 'Malbec Premium', @TpWine, 'WINES', 'MALBEC', 95.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'WIN-003')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'WIN-003', 'WIN-003', 'Cabernet Sauvignon', @TpWine, 'WINES', 'CABERNET', 68.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'WIN-004')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'WIN-004', 'WIN-004', 'Glass of Red Wine', @TpWine, 'WINES', 'BY GLASS', 18.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'WIN-005')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'WIN-005', 'WIN-005', 'Glass of White Wine', @TpWine, 'WINES', 'BY GLASS', 16.00)

-- COCKTAILS
IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'CKT-001')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'CKT-001', 'CKT-001', 'Aperol Spritz', @TpCocktail, 'COCKTAILS', 'APERITIVO', 16.00)

IF NOT EXISTS (SELECT 1 FROM [dim].[Producto] WHERE FranquiciaId = @FranqMiamiId AND ExternalId = 'CKT-002')
    INSERT INTO [dim].[Producto] (FranquiciaId, ExternalId, Codigo, Nombre, TipoPlatoId, Familia, SubFamilia, PrecioBase)
    VALUES (@FranqMiamiId, 'CKT-002', 'CKT-002', 'Negroni', @TpCocktail, 'COCKTAILS', 'CLASSIC', 18.00)

PRINT ''
PRINT 'Catalogo de productos extendido cargado.'
PRINT ''

-- Mostrar resumen
SELECT
    f.Nombre AS Franquicia,
    COUNT(*) AS [Total Productos],
    SUM(CASE WHEN tp.Codigo = 'STARTER' THEN 1 ELSE 0 END) AS Entradas,
    SUM(CASE WHEN tp.Codigo = 'MAIN_COURSE' THEN 1 ELSE 0 END) AS [Platos Principales],
    SUM(CASE WHEN tp.Codigo = 'SIDE_DISH' THEN 1 ELSE 0 END) AS Guarniciones,
    SUM(CASE WHEN tp.Codigo = 'DESSERT' THEN 1 ELSE 0 END) AS Postres,
    SUM(CASE WHEN tp.Codigo IN ('BEVERAGE', 'WINE', 'COCKTAIL', 'COFFEE') THEN 1 ELSE 0 END) AS Bebidas
FROM [dim].[Producto] p
JOIN [dim].[Franquicia] f ON p.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[TipoPlato] tp ON p.TipoPlatoId = tp.TipoPlatoId
WHERE p.Activo = 1
GROUP BY f.Nombre, f.FranquiciaId
GO
