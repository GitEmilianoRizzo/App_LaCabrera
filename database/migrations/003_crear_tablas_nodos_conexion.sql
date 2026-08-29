-- =====================================================
-- LA CABRERA - Tablas para Nodos de Conexion
-- Gestion de integraciones con sistemas POS
-- =====================================================

-- Crear schema log si no existe
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'log')
BEGIN
    EXEC('CREATE SCHEMA log')
END
GO

-- =====================================================
-- 1. Tabla principal de Nodos de Conexion
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NodoConexion' AND schema_id = SCHEMA_ID('dim'))
BEGIN
    CREATE TABLE dim.NodoConexion (
        NodoConexionId INT IDENTITY(1,1) PRIMARY KEY,
        Codigo NVARCHAR(50) NOT NULL UNIQUE,
        Nombre NVARCHAR(200) NOT NULL,
        FranquiciaId INT NOT NULL,

        -- Tipo de conector
        TipoConector NVARCHAR(50) NOT NULL, -- AGORA_HTTP, PUSH_API, FILE, AGENT
        Modo NVARCHAR(20) NOT NULL CHECK (Modo IN ('PULL', 'PUSH', 'AGENT', 'FILE')),

        -- Estado
        Estado NVARCHAR(20) NOT NULL DEFAULT 'PENDING_CONFIG'
            CHECK (Estado IN ('ACTIVE', 'PAUSED', 'ERROR', 'PENDING_CONFIG')),

        -- Configuracion de conexion (JSON cifrado para datos sensibles)
        ConfiguracionJson NVARCHAR(MAX) NULL,

        -- Scheduling (para modo PULL)
        CronExpression NVARCHAR(100) NULL, -- ej: '0 30 6 * * *'
        BusinessDateOffsetDays INT DEFAULT -1, -- Generalmente -1 (dia anterior)
        CatchUpDays INT DEFAULT 7,

        -- Metadata de franquicia (cache, viene de dim.Franquicia)
        Timezone NVARCHAR(100) NOT NULL DEFAULT 'UTC',
        Moneda NVARCHAR(3) NOT NULL DEFAULT 'USD',

        -- Politicas de mapeo
        ConvencionImportes NVARCHAR(20) DEFAULT 'VAT_INCLUDED'
            CHECK (ConvencionImportes IN ('VAT_INCLUDED', 'VAT_EXCLUDED')),
        PoliticaDevoluciones NVARCHAR(20) DEFAULT 'NEGATIVE_LINES'
            CHECK (PoliticaDevoluciones IN ('NEGATIVE_LINES', 'SEPARATE_TICKET')),

        -- Tolerancias
        ToleranciaReconciliacion DECIMAL(10,2) DEFAULT 0.05,

        -- Auditoria
        UltimaSincronizacion DATETIME2 NULL,
        UltimoEstado NVARCHAR(20) NULL CHECK (UltimoEstado IN ('SUCCESS', 'ERROR', 'WARNING')),
        UltimoBatchId NVARCHAR(200) NULL,

        Activo BIT NOT NULL DEFAULT 1,
        CreadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ModificadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_NodoConexion_Franquicia
            FOREIGN KEY (FranquiciaId) REFERENCES dim.Franquicia(FranquiciaId)
    );

    CREATE INDEX IX_NodoConexion_Franquicia ON dim.NodoConexion(FranquiciaId);
    CREATE INDEX IX_NodoConexion_Estado ON dim.NodoConexion(Estado);

    PRINT 'Tabla dim.NodoConexion creada exitosamente';
END
GO

-- =====================================================
-- 2. Tabla de Mapeo de Categorias de Producto
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MapeoCategoria' AND schema_id = SCHEMA_ID('dim'))
BEGIN
    CREATE TABLE dim.MapeoCategoria (
        MapeoCategoriaId INT IDENTITY(1,1) PRIMARY KEY,
        NodoConexionId INT NOT NULL,

        -- Valor en el sistema origen
        CodigoOrigen NVARCHAR(100) NOT NULL,
        NombreOrigen NVARCHAR(200) NULL,

        -- Valor en nuestro sistema (categorias estandar)
        CategoriaDestino NVARCHAR(50) NOT NULL CHECK (CategoriaDestino IN (
            'STARTER', 'MAIN_COURSE', 'SIDE_DISH', 'DESSERT',
            'COFFEE', 'BEVERAGE', 'WINE', 'COCKTAIL', 'OTHER'
        )),

        -- Familia/Subfamilia destino (opcional)
        FamiliaDestino NVARCHAR(100) NULL,
        SubfamiliaDestino NVARCHAR(100) NULL,

        -- Estado del mapeo
        Verificado BIT DEFAULT 0, -- 0 = sugerido, 1 = confirmado por usuario

        Activo BIT NOT NULL DEFAULT 1,
        CreadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ModificadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_MapeoCategoria_Nodo
            FOREIGN KEY (NodoConexionId) REFERENCES dim.NodoConexion(NodoConexionId),
        CONSTRAINT UQ_MapeoCategoria_Nodo_Codigo
            UNIQUE (NodoConexionId, CodigoOrigen)
    );

    CREATE INDEX IX_MapeoCategoria_Nodo ON dim.MapeoCategoria(NodoConexionId);

    PRINT 'Tabla dim.MapeoCategoria creada exitosamente';
END
GO

-- =====================================================
-- 3. Tabla de Mapeo de Medios de Pago
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MapeoMedioPago' AND schema_id = SCHEMA_ID('dim'))
BEGIN
    CREATE TABLE dim.MapeoMedioPago (
        MapeoMedioPagoId INT IDENTITY(1,1) PRIMARY KEY,
        NodoConexionId INT NOT NULL,

        -- Valor en el sistema origen
        CodigoOrigen NVARCHAR(100) NOT NULL,
        NombreOrigen NVARCHAR(200) NULL,

        -- Valor en nuestro sistema (medios estandar)
        MedioPagoDestino NVARCHAR(50) NOT NULL CHECK (MedioPagoDestino IN (
            'CASH', 'DEBIT_CARD', 'CREDIT_CARD', 'BANK_TRANSFER',
            'QR', 'MERCADO_PAGO', 'DELIVERY_APP', 'OTHER'
        )),

        -- Marca de tarjeta (opcional)
        MarcaTarjeta NVARCHAR(50) NULL, -- VISA, MASTERCARD, AMEX, etc.

        -- Estado del mapeo
        Verificado BIT DEFAULT 0,

        Activo BIT NOT NULL DEFAULT 1,
        CreadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ModificadoEn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_MapeoMedioPago_Nodo
            FOREIGN KEY (NodoConexionId) REFERENCES dim.NodoConexion(NodoConexionId),
        CONSTRAINT UQ_MapeoMedioPago_Nodo_Codigo
            UNIQUE (NodoConexionId, CodigoOrigen)
    );

    CREATE INDEX IX_MapeoMedioPago_Nodo ON dim.MapeoMedioPago(NodoConexionId);

    PRINT 'Tabla dim.MapeoMedioPago creada exitosamente';
END
GO

-- =====================================================
-- 4. Tabla de Valores No Mapeados (cola de pendientes)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ValorNoMapeado' AND schema_id = SCHEMA_ID('dim'))
BEGIN
    CREATE TABLE dim.ValorNoMapeado (
        ValorNoMapeadoId INT IDENTITY(1,1) PRIMARY KEY,
        NodoConexionId INT NOT NULL,

        TipoMapeo NVARCHAR(50) NOT NULL CHECK (TipoMapeo IN (
            'CATEGORIA', 'MEDIO_PAGO', 'CENTRO_VENTA', 'USUARIO'
        )),

        CodigoOrigen NVARCHAR(100) NOT NULL,
        NombreOrigen NVARCHAR(200) NULL,

        -- Cuantas veces se ha visto (para priorizar)
        Ocurrencias INT DEFAULT 1,
        PrimeraVez DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UltimaVez DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Estado
        Resuelto BIT DEFAULT 0,
        ResueltoEn DATETIME2 NULL,

        CONSTRAINT FK_ValorNoMapeado_Nodo
            FOREIGN KEY (NodoConexionId) REFERENCES dim.NodoConexion(NodoConexionId),
        CONSTRAINT UQ_ValorNoMapeado
            UNIQUE (NodoConexionId, TipoMapeo, CodigoOrigen)
    );

    CREATE INDEX IX_ValorNoMapeado_Pendientes
        ON dim.ValorNoMapeado(NodoConexionId, TipoMapeo, Resuelto);

    PRINT 'Tabla dim.ValorNoMapeado creada exitosamente';
END
GO

-- =====================================================
-- 5. Tabla de Log de Ejecuciones
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EjecucionNodo' AND schema_id = SCHEMA_ID('log'))
BEGIN
    CREATE TABLE log.EjecucionNodo (
        EjecucionNodoId BIGINT IDENTITY(1,1) PRIMARY KEY,
        NodoConexionId INT NOT NULL,

        -- Identificador de ejecucion
        EjecucionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

        -- Fechas
        InicioEjecucion DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FinEjecucion DATETIME2 NULL,
        FechaNegocio DATE NOT NULL,

        -- Estado
        Estado NVARCHAR(20) NOT NULL DEFAULT 'RUNNING' CHECK (Estado IN (
            'RUNNING', 'SUCCESS', 'ERROR', 'WARNING', 'CANCELLED'
        )),

        -- Modo de ejecucion
        ModoEjecucion NVARCHAR(20) DEFAULT 'SCHEDULED' CHECK (ModoEjecucion IN (
            'SCHEDULED', 'MANUAL', 'CATCHUP', 'DRY_RUN'
        )),

        -- Metricas
        TicketsProcesados INT DEFAULT 0,
        LineasProcesadas INT DEFAULT 0,
        WarningsCount INT DEFAULT 0,
        ErrorsCount INT DEFAULT 0,

        -- Resultado
        BatchId NVARCHAR(200) NULL,
        BatchEstado NVARCHAR(20) NULL,

        -- Version del API origen (importante para detectar cambios)
        ApiVersionOrigen NVARCHAR(50) NULL,

        -- Detalles de error/warning
        Mensajes NVARCHAR(MAX) NULL, -- JSON array de mensajes

        -- Crudo guardado (referencia a archivo o blob)
        RutaCrudoGuardado NVARCHAR(500) NULL,

        CONSTRAINT FK_EjecucionNodo_Nodo
            FOREIGN KEY (NodoConexionId) REFERENCES dim.NodoConexion(NodoConexionId)
    );

    CREATE INDEX IX_EjecucionNodo_Nodo ON log.EjecucionNodo(NodoConexionId);
    CREATE INDEX IX_EjecucionNodo_Fecha ON log.EjecucionNodo(FechaNegocio);
    CREATE INDEX IX_EjecucionNodo_Estado ON log.EjecucionNodo(Estado);

    PRINT 'Tabla log.EjecucionNodo creada exitosamente';
END
GO

-- =====================================================
-- 6. Insertar Nodo de Marbella (ejemplo inicial)
-- =====================================================
-- Primero verificar que existe la franquicia
IF NOT EXISTS (SELECT 1 FROM dim.Franquicia WHERE Codigo = 'MARBELLA_01')
BEGIN
    INSERT INTO dim.Franquicia (
        Codigo, Nombre, Pais, Ciudad, ZonaHoraria,
        Activo, EstadoIntegracion
    ) VALUES (
        'MARBELLA_01', 'La Cabrera Marbella', 'Spain', 'Marbella', 'Europe/Madrid',
        1, 'PENDING'
    );
    PRINT 'Franquicia MARBELLA_01 creada';
END
GO

-- Insertar el nodo de conexion de Marbella
IF NOT EXISTS (SELECT 1 FROM dim.NodoConexion WHERE Codigo = 'MARBELLA_AGORA')
BEGIN
    DECLARE @FranquiciaId INT;
    SELECT @FranquiciaId = FranquiciaId FROM dim.Franquicia WHERE Codigo = 'MARBELLA_01';

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
        'MARBELLA_AGORA',
        'Conector Marbella - Agora POS',
        @FranquiciaId,
        'AGORA_HTTP',
        'PULL',
        'PENDING_CONFIG',
        '{
            "connection": {
                "base_url": "https://cabrera.eccicloud.es",
                "api_token": "PENDIENTE_CONFIGURAR",
                "timeout_seconds": 60,
                "retry": { "max_attempts": 4, "backoff_seconds": [5, 30, 120, 600] }
            },
            "export_filter": "Invoices",
            "include_processed": false,
            "mark_processed_after_accept": true,
            "workplace_ids": []
        }',
        '0 30 6 * * *', -- 06:30 AM todos los dias
        -1, -- Procesar dia anterior
        7,  -- Catch up de 7 dias
        'Europe/Madrid',
        'EUR',
        'VAT_INCLUDED',
        'NEGATIVE_LINES',
        0.05
    );

    PRINT 'Nodo MARBELLA_AGORA creado exitosamente';
END
GO

-- =====================================================
-- 7. Vista para consultar estado de conexiones
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_EstadoNodosConexion' AND schema_id = SCHEMA_ID('dim'))
    DROP VIEW dim.vw_EstadoNodosConexion;
GO

CREATE VIEW dim.vw_EstadoNodosConexion AS
SELECT
    n.NodoConexionId,
    n.Codigo AS NodoCodigo,
    n.Nombre AS NodoNombre,
    n.TipoConector,
    n.Modo,
    n.Estado,
    n.UltimaSincronizacion,
    n.UltimoEstado,
    n.UltimoBatchId,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    f.Pais,
    f.Ciudad,
    n.Timezone,
    n.Moneda,
    n.CronExpression,
    n.ConvencionImportes,
    n.Activo,
    -- Conteo de mapeos
    (SELECT COUNT(*) FROM dim.MapeoCategoria mc WHERE mc.NodoConexionId = n.NodoConexionId AND mc.Activo = 1) AS MapeosCategoria,
    (SELECT COUNT(*) FROM dim.MapeoMedioPago mp WHERE mp.NodoConexionId = n.NodoConexionId AND mp.Activo = 1) AS MapeosMedioPago,
    -- Valores pendientes de mapear
    (SELECT COUNT(*) FROM dim.ValorNoMapeado v WHERE v.NodoConexionId = n.NodoConexionId AND v.Resuelto = 0) AS ValoresPendientes,
    -- Ultima ejecucion
    (SELECT TOP 1 e.TicketsProcesados
     FROM log.EjecucionNodo e
     WHERE e.NodoConexionId = n.NodoConexionId AND e.Estado = 'SUCCESS'
     ORDER BY e.FinEjecucion DESC) AS TicketsUltimaEjecucion
FROM dim.NodoConexion n
INNER JOIN dim.Franquicia f ON n.FranquiciaId = f.FranquiciaId
WHERE n.Activo = 1;
GO

PRINT 'Vista dim.vw_EstadoNodosConexion creada exitosamente';
GO

PRINT '=== Migracion de tablas de Nodos de Conexion completada ===';
