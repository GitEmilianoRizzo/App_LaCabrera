/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 09_create_tipocambio_tables.sql
  Descripcion: Tablas para manejo de tipos de cambio diarios
  Autor: Claude Code
  Fecha: 2026-08-22
  Lote: MEJORAS_20260822 - Etapa 1.1, 1.2

  TABLAS:
  - dim.TipoCambio: Tasas de cambio diarias por moneda
  - cfg.TipoCambioProveedor: Configuracion de proveedores FX (BCRA, etc.)
  - stg.TipoCambioIngestaLog: Log de ejecuciones del job FX

  DEFINICION CRITICA - LEER ANTES DE USAR:
  =========================================
  UnidadesPorUsd = cuantas unidades de esa moneda equivalen a 1 USD

  Por lo tanto:
    ImporteUsd = ImporteLocal / UnidadesPorUsd

  Ejemplos:
    - ARS: Si 1 USD = 950 ARS, entonces UnidadesPorUsd = 950
           100.000 ARS / 950 = 105.26 USD
    - EUR: Si 1 USD = 0.92 EUR, entonces UnidadesPorUsd = 0.92
           100 EUR / 0.92 = 108.70 USD
    - USD: Siempre UnidadesPorUsd = 1
           100 USD / 1 = 100 USD

  DECISION DE DISENO - FILAS POR CADA DIA:
  ========================================
  La tabla tiene una fila por CADA dia calendario y cada moneda, incluidos
  sabados, domingos y feriados. Los dias sin cotizacion publicada se completan
  con la ultima cotizacion anterior, marcando EsArrastrada = 1 y dejando
  FechaCotizacionOrigen apuntando al dia real de publicacion.

  Motivo: que todas las vistas resuelvan la conversion con un JOIN simple por
  (Moneda, Fecha) en lugar de tener que buscar "la ultima tasa <= fecha" en
  cada consulta.

  INSTRUCCIONES:
  - Ejecutar despues de 08_configurar_nodo_aeroparque.sql
  - Idempotente: puede re-ejecutarse sin error
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- TABLA: dim.TipoCambio
-- Tasas de cambio diarias. Una fila por moneda por dia.
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dim.TipoCambio') AND type = 'U')
BEGIN
    CREATE TABLE dim.TipoCambio (
        TipoCambioId            INT IDENTITY(1,1) NOT NULL,

        -- Identificacion
        CodigoMoneda            CHAR(3) NOT NULL,           -- ISO 4217: USD, ARS, EUR, COP, PYG, PHP
        Fecha                   DATE NOT NULL,              -- Fecha de negocio a la que APLICA esta tasa
        FechaCotizacionOrigen   DATE NOT NULL,              -- Fecha real de la cotizacion publicada

        -- Tasa (ver definicion critica arriba)
        UnidadesPorUsd          DECIMAL(18,8) NOT NULL,     -- Cuantas unidades de esta moneda = 1 USD

        -- Metadata
        TipoTasa                VARCHAR(20) NOT NULL DEFAULT 'OFICIAL',  -- OFICIAL, BLUE, MEP, CCL
        Proveedor               VARCHAR(50) NOT NULL,       -- BCRA, OPENEXCHANGE, MANUAL
        EsArrastrada            BIT NOT NULL DEFAULT 0,     -- 1 = copiada de dia anterior (finde/feriado)
        EsOverrideManual        BIT NOT NULL DEFAULT 0,     -- 1 = cargada manualmente
        ObservacionOverride     NVARCHAR(500) NULL,         -- Motivo del override manual

        -- Auditoria
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaModificacion       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UsuarioCreacion         NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,

        CONSTRAINT PK_TipoCambio PRIMARY KEY CLUSTERED (TipoCambioId),

        -- Una sola tasa por moneda+fecha+tipo
        CONSTRAINT UQ_TipoCambio_Moneda_Fecha_Tipo
            UNIQUE (CodigoMoneda, Fecha, TipoTasa)
    )

    PRINT 'Tabla dim.TipoCambio creada.'
END
ELSE
    PRINT 'Tabla dim.TipoCambio ya existe.'
GO

-- Indice para el JOIN de las vistas (Fecha, Moneda)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TipoCambio_Fecha_Moneda' AND object_id = OBJECT_ID('dim.TipoCambio'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TipoCambio_Fecha_Moneda
    ON dim.TipoCambio (Fecha, CodigoMoneda)
    INCLUDE (UnidadesPorUsd, FechaCotizacionOrigen, EsArrastrada)

    PRINT 'Indice IX_TipoCambio_Fecha_Moneda creado.'
END
GO

-- Seed: USD siempre tiene tasa 1 (se inserta una fila base)
IF NOT EXISTS (SELECT 1 FROM dim.TipoCambio WHERE CodigoMoneda = 'USD' AND Fecha = '2026-06-01')
BEGIN
    INSERT INTO dim.TipoCambio (CodigoMoneda, Fecha, FechaCotizacionOrigen, UnidadesPorUsd, TipoTasa, Proveedor)
    VALUES ('USD', '2026-06-01', '2026-06-01', 1.00000000, 'OFICIAL', 'SISTEMA')

    PRINT 'Seed: Tasa USD base insertada.'
END
GO

-- ============================================================================
-- TABLA: cfg.TipoCambioProveedor
-- Configuracion de proveedores de tipo de cambio
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'cfg.TipoCambioProveedor') AND type = 'U')
BEGIN
    CREATE TABLE cfg.TipoCambioProveedor (
        ProveedorId             INT IDENTITY(1,1) NOT NULL,

        Codigo                  VARCHAR(50) NOT NULL,       -- BCRA, OPENEXCHANGE, etc.
        Nombre                  NVARCHAR(200) NOT NULL,
        BaseUrl                 NVARCHAR(500) NOT NULL,

        -- Configuracion
        Activo                  BIT NOT NULL DEFAULT 1,
        Prioridad               INT NOT NULL DEFAULT 100,   -- Menor = mayor prioridad
        MonedasSoportadas       NVARCHAR(500) NULL,         -- Lista CSV: ARS,USD,EUR o NULL = todas
        RequiereApiKey          BIT NOT NULL DEFAULT 0,
        ApiKeyConfigName        VARCHAR(100) NULL,          -- Nombre en appsettings

        -- Rate limiting
        MaxRequestsPorMinuto    INT NULL,
        DelayEntreRequestsMs    INT NULL,

        -- Auditoria
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaModificacion       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UsuarioCreacion         NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,

        CONSTRAINT PK_TipoCambioProveedor PRIMARY KEY CLUSTERED (ProveedorId),
        CONSTRAINT UQ_TipoCambioProveedor_Codigo UNIQUE (Codigo)
    )

    PRINT 'Tabla cfg.TipoCambioProveedor creada.'
END
ELSE
    PRINT 'Tabla cfg.TipoCambioProveedor ya existe.'
GO

-- Seed: Proveedor BCRA (primario para ARS, USD, EUR, COP, PYG)
IF NOT EXISTS (SELECT 1 FROM cfg.TipoCambioProveedor WHERE Codigo = 'BCRA')
BEGIN
    INSERT INTO cfg.TipoCambioProveedor (Codigo, Nombre, BaseUrl, Activo, Prioridad, MonedasSoportadas, RequiereApiKey)
    VALUES (
        'BCRA',
        'Banco Central de la Republica Argentina - API Estadisticas Cambiarias',
        'https://api.bcra.gob.ar/estadisticascambiarias/v1.0',
        1,
        10,
        'ARS,USD,EUR,COP,PYG',  -- Confirmado en discovery 2026-08-22
        0                        -- No requiere API key
    )

    PRINT 'Seed: Proveedor BCRA insertado.'
END
GO

-- Seed: Proveedor secundario para PHP (Peso Filipino - Manila)
IF NOT EXISTS (SELECT 1 FROM cfg.TipoCambioProveedor WHERE Codigo = 'EXCHANGERATE_HOST')
BEGIN
    INSERT INTO cfg.TipoCambioProveedor (Codigo, Nombre, BaseUrl, Activo, Prioridad, MonedasSoportadas, RequiereApiKey, ApiKeyConfigName)
    VALUES (
        'EXCHANGERATE_HOST',
        'ExchangeRate.host - API de tipos de cambio',
        'https://api.exchangerate.host',
        1,
        20,                      -- Menor prioridad que BCRA
        'PHP',                   -- Solo para monedas no cubiertas por BCRA
        0,                       -- Tier gratuito sin API key
        'ExchangeRate:ApiKey'    -- Por si se necesita tier pago en el futuro
    )

    PRINT 'Seed: Proveedor EXCHANGERATE_HOST insertado (para PHP).'
END
GO

-- ============================================================================
-- TABLA: stg.TipoCambioIngestaLog
-- Log de ejecuciones del job de tipo de cambio
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'stg.TipoCambioIngestaLog') AND type = 'U')
BEGIN
    CREATE TABLE stg.TipoCambioIngestaLog (
        IngestaLogId            INT IDENTITY(1,1) NOT NULL,

        -- Ejecucion
        FechaEjecucion          DATETIME2 NOT NULL DEFAULT GETDATE(),
        Proveedor               VARCHAR(50) NOT NULL,

        -- Rango consultado
        FechaDesde              DATE NOT NULL,
        FechaHasta              DATE NOT NULL,
        MonedasConsultadas      NVARCHAR(500) NULL,         -- CSV de monedas

        -- Resultado
        Estado                  VARCHAR(20) NOT NULL,       -- OK, PARCIAL, ERROR
        MonedasObtenidas        INT NOT NULL DEFAULT 0,
        DiasObtenidos           INT NOT NULL DEFAULT 0,
        DiasArrastrados         INT NOT NULL DEFAULT 0,     -- Fines de semana/feriados

        -- Errores
        MensajeError            NVARCHAR(MAX) NULL,
        MonedasFaltantes        NVARCHAR(500) NULL,         -- Monedas que no se pudieron obtener

        -- Performance
        DuracionMs              INT NULL,
        RequestsRealizados      INT NOT NULL DEFAULT 0,

        CONSTRAINT PK_TipoCambioIngestaLog PRIMARY KEY CLUSTERED (IngestaLogId)
    )

    PRINT 'Tabla stg.TipoCambioIngestaLog creada.'
END
ELSE
    PRINT 'Tabla stg.TipoCambioIngestaLog ya existe.'
GO

-- Indice para consulta por fecha
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TipoCambioIngestaLog_Fecha' AND object_id = OBJECT_ID('stg.TipoCambioIngestaLog'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TipoCambioIngestaLog_Fecha
    ON stg.TipoCambioIngestaLog (FechaEjecucion DESC)

    PRINT 'Indice IX_TipoCambioIngestaLog_Fecha creado.'
END
GO

PRINT '=========================================='
PRINT 'Script 09 completado: Tablas de tipo de cambio creadas.'
PRINT '=========================================='
GO
