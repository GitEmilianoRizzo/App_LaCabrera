/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: setup_colombia_production.sql
  Descripcion: Configurar Colombia en produccion (conexiones y tipo de cambio)
  Fecha: 2026-09-27

  EJECUTAR EN PRODUCCION para habilitar franquicias de Colombia
================================================================================
*/

USE [LaCabreraDB]
GO

PRINT '============================================'
PRINT 'CONFIGURANDO COLOMBIA EN PRODUCCION'
PRINT '============================================'

-- ============================================
-- 1. VERIFICAR/CREAR MONEDA COP
-- ============================================
PRINT ''
PRINT '1. Verificando moneda COP...'

IF NOT EXISTS (SELECT 1 FROM dim.Moneda WHERE CodigoISO = 'COP')
BEGIN
    INSERT INTO dim.Moneda (CodigoISO, Nombre, Simbolo, EsActiva)
    VALUES ('COP', 'Peso Colombiano', '$', 1)
    PRINT '   Moneda COP creada'
END
ELSE
BEGIN
    PRINT '   Moneda COP ya existe'
END

-- ============================================
-- 2. CONFIGURAR PROVEEDOR DE TIPO DE CAMBIO COP
-- ============================================
PRINT ''
PRINT '2. Configurando proveedor de tipo de cambio para COP...'

-- El COP usa override manual porque BCRA no lo tiene
DECLARE @MonedaCopId INT = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = 'COP')

IF NOT EXISTS (SELECT 1 FROM dim.MonedaTipoCambioProveedor WHERE MonedaId = @MonedaCopId)
BEGIN
    INSERT INTO dim.MonedaTipoCambioProveedor (MonedaId, ProveedorCodigo, Prioridad, EsActivo)
    VALUES (@MonedaCopId, 'MANUAL_OVERRIDE', 1, 1)
    PRINT '   Proveedor MANUAL_OVERRIDE configurado para COP'
END

-- ============================================
-- 3. INSERTAR TIPO DE CAMBIO COP (si no existe)
-- ============================================
PRINT ''
PRINT '3. Insertando tipos de cambio COP...'

-- Tipo de cambio aproximado: 1 USD = 4200 COP (septiembre 2026)
IF NOT EXISTS (SELECT 1 FROM fact.TipoCambio WHERE MonedaId = @MonedaCopId AND Fecha = '2026-08-01')
BEGIN
    INSERT INTO fact.TipoCambio (MonedaId, Fecha, UnidadesPorUsd, Fuente)
    VALUES (@MonedaCopId, '2026-08-01', 4200.00, 'MANUAL_OVERRIDE')
    PRINT '   Tipo de cambio COP 2026-08-01: 4200 COP/USD'
END

IF NOT EXISTS (SELECT 1 FROM fact.TipoCambio WHERE MonedaId = @MonedaCopId AND Fecha = '2026-09-01')
BEGIN
    INSERT INTO fact.TipoCambio (MonedaId, Fecha, UnidadesPorUsd, Fuente)
    VALUES (@MonedaCopId, '2026-09-01', 4200.00, 'MANUAL_OVERRIDE')
    PRINT '   Tipo de cambio COP 2026-09-01: 4200 COP/USD'
END

-- ============================================
-- 4. VERIFICAR FRANQUICIAS DE COLOMBIA
-- ============================================
PRINT ''
PRINT '4. Verificando franquicias de Colombia...'

SELECT
    f.FranquiciaId,
    f.Codigo,
    f.Nombre,
    f.Pais,
    m.CodigoISO AS Moneda,
    f.EstaActiva
FROM dim.Franquicia f
LEFT JOIN dim.Moneda m ON f.MonedaId = m.MonedaId
WHERE f.Pais = 'Colombia'

-- ============================================
-- 5. VERIFICAR NODOS DE CONEXION COLOMBIA
-- ============================================
PRINT ''
PRINT '5. Verificando nodos de conexion de Colombia...'

SELECT
    nc.NodoConexionId,
    f.Nombre AS Franquicia,
    nc.NombreNodo,
    nc.TipoConector,
    nc.EstaActivo
FROM dim.NodoConexion nc
INNER JOIN dim.Franquicia f ON nc.FranquiciaId = f.FranquiciaId
WHERE f.Pais = 'Colombia'

-- ============================================
-- 6. VERIFICAR TIPOS DE CAMBIO DISPONIBLES
-- ============================================
PRINT ''
PRINT '6. Tipos de cambio COP disponibles:'

SELECT
    tc.Fecha,
    tc.UnidadesPorUsd,
    tc.Fuente
FROM fact.TipoCambio tc
WHERE tc.MonedaId = @MonedaCopId
ORDER BY tc.Fecha DESC

GO

PRINT ''
PRINT '============================================'
PRINT 'CONFIGURACION DE COLOMBIA COMPLETADA'
PRINT '============================================'
PRINT ''
PRINT 'NOTA: Si las franquicias de Colombia no aparecen,'
PRINT 'deben crearse manualmente en dim.Franquicia con:'
PRINT '  - Pais = Colombia'
PRINT '  - MonedaId = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = ''COP'')'
PRINT ''
GO
