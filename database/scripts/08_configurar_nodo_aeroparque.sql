-- =====================================================
-- LA CABRERA - Configurar Nodo AEROPARQUE con Vinson
-- Ejecutar despues de crear las tablas base
-- =====================================================

-- =====================================================
-- 1. Crear franquicia AEROPARQUE_01 si no existe
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM dim.Franquicia WHERE Codigo = 'AEROPARQUE_01')
BEGIN
    INSERT INTO dim.Franquicia (
        Codigo, Nombre, Pais, Ciudad, ZonaHoraria,
        GrupoEconomicoCodigo, GrupoEconomicoNombre,
        Activo, EstadoIntegracion
    ) VALUES (
        'AEROPARQUE_01',
        'La Cabrera Aeroparque',
        'Argentina',
        'Buenos Aires',
        'America/Argentina/Buenos_Aires',
        'GRP_AEROPUERTOS_ARG',
        'Grupo Aeropuertos Argentina',
        1,
        'PENDING'
    );
    PRINT 'Franquicia AEROPARQUE_01 creada';
END
ELSE
BEGIN
    PRINT 'Franquicia AEROPARQUE_01 ya existe';
END
GO

-- =====================================================
-- 2. Insertar o actualizar nodo NODO_AEROPARQUE
-- =====================================================
DECLARE @FranquiciaId INT;
SELECT @FranquiciaId = FranquiciaId FROM dim.Franquicia WHERE Codigo = 'AEROPARQUE_01';

-- Configuracion JSON para Vinson
-- store_id obtenido de GET /api/Branch/GetBranchsByUser:
-- idTienda: 369 = "AEP La Cabrera" (Aeroparque)
-- idTienda: 380 = "EZE La Cabrera" (Ezeiza)
DECLARE @ConfigJson NVARCHAR(MAX) = N'{
    "connection": {
        "base_url": "https://apireportes.vinson.com.ar",
        "username": "api@cabrera.com.ar",
        "password": "i23LkbsZ",
        "store_id": "369",
        "timeout_seconds": 60,
        "retry": {
            "max_attempts": 4,
            "backoff_seconds": [5, 30, 120, 600]
        }
    },
    "include_processed": false,
    "mark_processed_after_accept": true
}';

IF NOT EXISTS (SELECT 1 FROM dim.NodoConexion WHERE Codigo = 'NODO_AEROPARQUE')
BEGIN
    INSERT INTO dim.NodoConexion (
        Codigo,
        Nombre,
        FranquiciaId,
        TipoConector,
        Modo,
        Estado,
        ConfiguracionJson,
        CronExpression,
        BusinessDateOffsetDays,
        CatchUpDays,
        Timezone,
        Moneda,
        ConvencionImportes,
        PoliticaDevoluciones,
        ToleranciaReconciliacion
    ) VALUES (
        'NODO_AEROPARQUE',
        'Conector Aeroparque - Vinson Cloud',
        @FranquiciaId,
        'VINSON',           -- Tipo conector Vinson
        'PULL',             -- Modo PULL (nosotros extraemos)
        'ACTIVE',           -- Activo con store_id=369
        @ConfigJson,
        '0 30 6 * * *',     -- 06:30 AM todos los dias (hora Argentina)
        -1,                 -- Procesar dia anterior
        7,                  -- Catch up de 7 dias
        'America/Argentina/Buenos_Aires',
        'ARS',
        'VAT_INCLUDED',     -- IVA incluido en los precios
        'NEGATIVE_LINES',   -- Devoluciones como lineas negativas
        0.05                -- 5% tolerancia reconciliacion
    );
    PRINT 'Nodo NODO_AEROPARQUE creado exitosamente';
END
ELSE
BEGIN
    -- Si ya existe, actualizar la configuracion
    UPDATE dim.NodoConexion
    SET ConfiguracionJson = @ConfigJson,
        TipoConector = 'VINSON',
        Modo = 'PULL',
        Estado = 'ACTIVE',
        Timezone = 'America/Argentina/Buenos_Aires',
        Moneda = 'ARS',
        ModificadoEn = GETUTCDATE()
    WHERE Codigo = 'NODO_AEROPARQUE';

    PRINT 'Nodo NODO_AEROPARQUE actualizado exitosamente';
END
GO

-- =====================================================
-- 3. Crear nodo NODO_EZEIZA (segunda tienda del grupo)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM dim.Franquicia WHERE Codigo = 'EZEIZA_01')
BEGIN
    INSERT INTO dim.Franquicia (
        Codigo, Nombre, Pais, Ciudad, ZonaHoraria,
        GrupoEconomicoCodigo, GrupoEconomicoNombre,
        Activo, EstadoIntegracion
    ) VALUES (
        'EZEIZA_01',
        'La Cabrera Ezeiza',
        'Argentina',
        'Buenos Aires',
        'America/Argentina/Buenos_Aires',
        'GRP_AEROPUERTOS_ARG',
        'Grupo Aeropuertos Argentina',
        1,
        'PENDING'
    );
    PRINT 'Franquicia EZEIZA_01 creada';
END
GO

DECLARE @FranquiciaIdEze INT;
SELECT @FranquiciaIdEze = FranquiciaId FROM dim.Franquicia WHERE Codigo = 'EZEIZA_01';

DECLARE @ConfigJsonEze NVARCHAR(MAX) = N'{
    "connection": {
        "base_url": "https://apireportes.vinson.com.ar",
        "username": "api@cabrera.com.ar",
        "password": "i23LkbsZ",
        "store_id": "380",
        "timeout_seconds": 60,
        "retry": {
            "max_attempts": 4,
            "backoff_seconds": [5, 30, 120, 600]
        }
    },
    "include_processed": false,
    "mark_processed_after_accept": true
}';

IF NOT EXISTS (SELECT 1 FROM dim.NodoConexion WHERE Codigo = 'NODO_EZEIZA')
BEGIN
    INSERT INTO dim.NodoConexion (
        Codigo,
        Nombre,
        FranquiciaId,
        TipoConector,
        Modo,
        Estado,
        ConfiguracionJson,
        CronExpression,
        BusinessDateOffsetDays,
        CatchUpDays,
        Timezone,
        Moneda,
        ConvencionImportes,
        PoliticaDevoluciones,
        ToleranciaReconciliacion
    ) VALUES (
        'NODO_EZEIZA',
        'Conector Ezeiza - Vinson Cloud',
        @FranquiciaIdEze,
        'VINSON',
        'PULL',
        'ACTIVE',
        @ConfigJsonEze,
        '0 35 6 * * *',     -- 06:35 AM (5 min despues de Aeroparque)
        -1,
        7,
        'America/Argentina/Buenos_Aires',
        'ARS',
        'VAT_INCLUDED',
        'NEGATIVE_LINES',
        0.05
    );
    PRINT 'Nodo NODO_EZEIZA creado exitosamente';
END
GO

-- =====================================================
-- 4. Verificar configuracion
-- =====================================================
SELECT
    n.Codigo AS NodoCodigo,
    n.Nombre,
    n.TipoConector,
    n.Modo,
    n.Estado,
    n.Timezone,
    n.Moneda,
    f.Codigo AS FranquiciaCodigo,
    JSON_VALUE(n.ConfiguracionJson, '$.connection.base_url') AS BaseUrl,
    JSON_VALUE(n.ConfiguracionJson, '$.connection.store_id') AS StoreId
FROM dim.NodoConexion n
INNER JOIN dim.Franquicia f ON n.FranquiciaId = f.FranquiciaId
WHERE n.TipoConector = 'VINSON';
GO

PRINT '';
PRINT '=== Configuracion de nodos Vinson completada ===';
PRINT '';
PRINT 'Nodos configurados:';
PRINT '  - NODO_AEROPARQUE: store_id=369 (AEP La Cabrera) - ACTIVE';
PRINT '  - NODO_EZEIZA: store_id=380 (EZE La Cabrera) - ACTIVE';
PRINT '';
PRINT 'Para probar la extraccion manualmente:';
PRINT '  POST /api/v1/conexiones/{id}/ejecutar?fechaNegocio=2026-08-07';
