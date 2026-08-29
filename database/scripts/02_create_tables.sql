/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 02_create_tables.sql
  Descripcion: Crea todas las tablas del modelo de datos
  Autor: Claude Code
  Fecha: 2026-07-16

  ESTRUCTURA:
  - Tablas de configuracion (cfg)
  - Tablas de dimensiones (dim)
  - Tablas de staging/ingesta (stg)
  - Tablas de API (api)
  - Tablas de hechos (fact)

  INSTRUCCIONES:
  - Ejecutar despues de 01_create_schemas.sql
  - Las tablas se crean sin FK para permitir carga en cualquier orden
  - Las FK se agregan en 03_create_constraints_indexes.sql
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- SECCION 1: TABLAS DE CONFIGURACION (cfg)
-- ============================================================================

PRINT 'Creando tablas de configuracion...'

-- Tabla de parametros del sistema
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[cfg].[Parametro]') AND type = 'U')
BEGIN
    CREATE TABLE [cfg].[Parametro]
    (
        ParametroId         INT IDENTITY(1,1) NOT NULL,
        Clave               NVARCHAR(100) NOT NULL,
        Valor               NVARCHAR(MAX) NULL,
        Descripcion         NVARCHAR(500) NULL,
        TipoDato            NVARCHAR(50) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [cfg].[Parametro] creada.'
END
GO

-- ============================================================================
-- SECCION 2: TABLAS DE DIMENSIONES (dim)
-- ============================================================================

PRINT 'Creando tablas de dimensiones...'

-- Monedas
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[Moneda]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[Moneda]
    (
        MonedaId            INT IDENTITY(1,1) NOT NULL,
        CodigoISO           CHAR(3) NOT NULL,              -- ISO 4217: USD, ARS, PYG, EUR
        Nombre              NVARCHAR(100) NOT NULL,
        Simbolo             NVARCHAR(10) NULL,
        EsMonedaBase        BIT NOT NULL DEFAULT 0,        -- Moneda base para reportes consolidados
        TasaCambioBase      DECIMAL(18,6) NULL,            -- Tasa vs moneda base
        FechaTasaCambio     DATE NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[Moneda] creada.'
END
GO

-- Grupos Economicos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[GrupoEconomico]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[GrupoEconomico]
    (
        GrupoEconomicoId    INT IDENTITY(1,1) NOT NULL,
        Codigo              NVARCHAR(50) NOT NULL,         -- GRP_PARAGUAY, GRP_MIAMI, etc.
        Nombre              NVARCHAR(200) NOT NULL,
        Pais                NVARCHAR(100) NULL,
        ContactoNombre      NVARCHAR(200) NULL,
        ContactoEmail       NVARCHAR(200) NULL,
        ContactoTelefono    NVARCHAR(50) NULL,
        Notas               NVARCHAR(MAX) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[GrupoEconomico] creada.'
END
GO

-- Franquicias
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[Franquicia]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[Franquicia]
    (
        FranquiciaId        INT IDENTITY(1,1) NOT NULL,
        Codigo              NVARCHAR(50) NOT NULL,         -- PARAGUAY_ASU01, MIAMI_01, etc.
        Nombre              NVARCHAR(200) NOT NULL,
        GrupoEconomicoId    INT NULL,
        Pais                NVARCHAR(100) NOT NULL,
        Ciudad              NVARCHAR(100) NOT NULL,
        Direccion           NVARCHAR(500) NULL,
        ZonaHoraria         NVARCHAR(100) NOT NULL,        -- America/Asuncion, etc.
        MonedaId            INT NULL,
        CantidadMesas       INT NULL,
        CapacidadMaxima     INT NULL,                      -- Capacidad total de cubiertos
        HoraApertura        TIME NULL,
        HoraCierre          TIME NULL,
        SistemaOrigen       NVARCHAR(100) NULL,            -- Nombre del POS
        VersionSistema      NVARCHAR(50) NULL,
        ContactoNombre      NVARCHAR(200) NULL,
        ContactoEmail       NVARCHAR(200) NULL,
        ContactoTelefono    NVARCHAR(50) NULL,
        FechaAltaSistema    DATE NULL,
        UltimaSincronizacion DATETIME2 NULL,
        EstadoIntegracion   NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- PENDING, ACTIVE, INACTIVE, ERROR
        Notas               NVARCHAR(MAX) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[Franquicia] creada.'
END
GO

-- Tipos de Plato / Categorias de Producto
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[TipoPlato]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[TipoPlato]
    (
        TipoPlatoId         INT IDENTITY(1,1) NOT NULL,
        Codigo              NVARCHAR(50) NOT NULL,         -- STARTER, MAIN_COURSE, DESSERT, etc.
        Nombre              NVARCHAR(100) NOT NULL,
        NombreIngles        NVARCHAR(100) NULL,
        Orden               INT NOT NULL DEFAULT 0,        -- Para ordenar en reportes
        ColorHex            NVARCHAR(7) NULL,              -- Para graficos: #FF5733
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[TipoPlato] creada.'
END
GO

-- Productos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[Producto]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[Producto]
    (
        ProductoId          INT IDENTITY(1,1) NOT NULL,
        FranquiciaId        INT NOT NULL,                  -- Producto pertenece a una franquicia
        ExternalId          NVARCHAR(100) NOT NULL,        -- Codigo del producto en sistema origen
        Codigo              NVARCHAR(50) NOT NULL,
        Nombre              NVARCHAR(200) NOT NULL,
        TipoPlatoId         INT NULL,
        Familia             NVARCHAR(100) NULL,            -- CARNES, ENTRADAS, etc.
        SubFamilia          NVARCHAR(100) NULL,            -- BIFE, EMPANADAS, etc.
        PrecioBase          DECIMAL(18,2) NULL,
        MonedaId            INT NULL,
        EsActivo            BIT NOT NULL DEFAULT 1,
        FuenteSistema       NVARCHAR(100) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[Producto] creada.'
END
GO

-- Mozos / Camareros
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[Mozo]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[Mozo]
    (
        MozoId              INT IDENTITY(1,1) NOT NULL,
        FranquiciaId        INT NOT NULL,
        ExternalId          NVARCHAR(100) NOT NULL,        -- ID en sistema origen
        Codigo              NVARCHAR(50) NULL,
        Nombre              NVARCHAR(200) NOT NULL,
        FechaIngreso        DATE NULL,
        EsActivo            BIT NOT NULL DEFAULT 1,
        FuenteSistema       NVARCHAR(100) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[Mozo] creada.'
END
GO

-- Mesas
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[Mesa]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[Mesa]
    (
        MesaId              INT IDENTITY(1,1) NOT NULL,
        FranquiciaId        INT NOT NULL,
        NumeroMesa          NVARCHAR(20) NOT NULL,
        Area                NVARCHAR(100) NULL,            -- SALON_PRINCIPAL, TERRAZA, etc.
        Capacidad           INT NULL,
        EsActiva            BIT NOT NULL DEFAULT 1,
        FuenteSistema       NVARCHAR(100) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[Mesa] creada.'
END
GO

-- Medios de Pago
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[MedioPago]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[MedioPago]
    (
        MedioPagoId         INT IDENTITY(1,1) NOT NULL,
        Codigo              NVARCHAR(50) NOT NULL,         -- CASH, CREDIT_CARD, DEBIT_CARD, etc.
        Nombre              NVARCHAR(100) NOT NULL,
        Orden               INT NOT NULL DEFAULT 0,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[MedioPago] creada.'
END
GO

-- Periodos (para facilitar reportes por franja horaria)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dim].[PeriodoComida]') AND type = 'U')
BEGIN
    CREATE TABLE [dim].[PeriodoComida]
    (
        PeriodoComidaId     INT IDENTITY(1,1) NOT NULL,
        Codigo              NVARCHAR(50) NOT NULL,         -- BREAKFAST, LUNCH, DINNER, etc.
        Nombre              NVARCHAR(100) NOT NULL,
        HoraInicio          TIME NOT NULL,
        HoraFin             TIME NOT NULL,
        Orden               INT NOT NULL DEFAULT 0,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [dim].[PeriodoComida] creada.'
END
GO

-- ============================================================================
-- SECCION 3: TABLAS DE STAGING E INGESTA (stg)
-- ============================================================================

PRINT 'Creando tablas de staging...'

-- Batch de Ingesta
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[stg].[IngestionBatch]') AND type = 'U')
BEGIN
    CREATE TABLE [stg].[IngestionBatch]
    (
        IngestionBatchId    BIGINT IDENTITY(1,1) NOT NULL,
        BatchId             NVARCHAR(200) NOT NULL,        -- ID unico del batch enviado por franquicia
        FranquiciaId        INT NOT NULL,
        FechaNegocio        DATE NOT NULL,                 -- business_date del JSON
        SchemaVersion       NVARCHAR(20) NOT NULL,
        TipoCarga           NVARCHAR(50) NOT NULL,         -- FULL_DAY, INCREMENTAL, CORRECTION
        OrigenIngesta       NVARCHAR(50) NOT NULL,         -- API, FILE
        NombreArchivo       NVARCHAR(500) NULL,            -- Si viene de archivo

        -- Totales de control recibidos
        TicketCountRecibido     INT NULL,
        ItemLineCountRecibido   INT NULL,
        GrossSalesRecibido      DECIMAL(18,2) NULL,
        DiscountRecibido        DECIMAL(18,2) NULL,
        NetSalesRecibido        DECIMAL(18,2) NULL,
        TaxRecibido             DECIMAL(18,2) NULL,
        CoversTotalRecibido     INT NULL,

        -- Totales calculados despues de procesar
        TicketCountCalculado    INT NULL,
        ItemLineCountCalculado  INT NULL,
        GrossSalesCalculado     DECIMAL(18,2) NULL,
        DiscountCalculado       DECIMAL(18,2) NULL,
        NetSalesCalculado       DECIMAL(18,2) NULL,

        -- Estado y auditoria
        Estado              NVARCHAR(50) NOT NULL DEFAULT 'RECEIVED',
        -- RECEIVED, VALIDATING, ACCEPTED, ACCEPTED_WITH_WARNINGS, REJECTED, PROCESSED, FAILED
        MensajeEstado       NVARCHAR(MAX) NULL,
        CantidadErrores     INT NOT NULL DEFAULT 0,
        CantidadWarnings    INT NOT NULL DEFAULT 0,

        FechaRecepcion      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaInicioProceso  DATETIME2 NULL,
        FechaFinProceso     DATETIME2 NULL,

        -- Sistema origen
        SistemaOrigenNombre     NVARCHAR(100) NULL,
        SistemaOrigenVersion    NVARCHAR(50) NULL,
        ExportadoPor            NVARCHAR(100) NULL,

        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER
    )
    PRINT 'Tabla [stg].[IngestionBatch] creada.'
END
GO

-- JSON Crudo del Batch
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[stg].[IngestionBatchRawJson]') AND type = 'U')
BEGIN
    CREATE TABLE [stg].[IngestionBatchRawJson]
    (
        IngestionBatchRawJsonId BIGINT IDENTITY(1,1) NOT NULL,
        IngestionBatchId        BIGINT NOT NULL,
        JsonContent             NVARCHAR(MAX) NOT NULL,
        HashJson                VARBINARY(32) NULL,        -- SHA256 para detectar duplicados
        TamanioBytes            BIGINT NULL,
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    )
    PRINT 'Tabla [stg].[IngestionBatchRawJson] creada.'
END
GO

-- Errores de Ingesta
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[stg].[IngestionError]') AND type = 'U')
BEGIN
    CREATE TABLE [stg].[IngestionError]
    (
        IngestionErrorId    BIGINT IDENTITY(1,1) NOT NULL,
        IngestionBatchId    BIGINT NOT NULL,
        Severidad           NVARCHAR(20) NOT NULL,         -- ERROR, WARNING, INFO
        CodigoError         NVARCHAR(50) NOT NULL,
        MensajeError        NVARCHAR(MAX) NOT NULL,
        CampoAfectado       NVARCHAR(200) NULL,
        ValorRecibido       NVARCHAR(MAX) NULL,
        ValorEsperado       NVARCHAR(500) NULL,
        TicketId            NVARCHAR(200) NULL,            -- Si el error es en un ticket especifico
        LineaId             NVARCHAR(100) NULL,            -- Si el error es en una linea especifica
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    )
    PRINT 'Tabla [stg].[IngestionError] creada.'
END
GO

-- Log de Llamadas API
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[stg].[ApiIngestaLog]') AND type = 'U')
BEGIN
    CREATE TABLE [stg].[ApiIngestaLog]
    (
        ApiIngestaLogId     BIGINT IDENTITY(1,1) NOT NULL,
        FranquiciaId        INT NULL,
        Endpoint            NVARCHAR(500) NOT NULL,
        MetodoHttp          NVARCHAR(10) NOT NULL,
        IpOrigen            NVARCHAR(50) NULL,
        ApiKeyUsada         NVARCHAR(100) NULL,            -- Solo los ultimos 8 caracteres
        RequestHeaders      NVARCHAR(MAX) NULL,
        RequestBodySize     BIGINT NULL,
        ResponseStatusCode  INT NULL,
        ResponseBody        NVARCHAR(MAX) NULL,
        IngestionBatchId    BIGINT NULL,
        DuracionMs          INT NULL,
        FechaRequest        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaResponse       DATETIME2 NULL
    )
    PRINT 'Tabla [stg].[ApiIngestaLog] creada.'
END
GO

-- ============================================================================
-- SECCION 4: TABLAS DE API (api)
-- ============================================================================

PRINT 'Creando tablas de API...'

-- API Keys por Franquicia
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[api].[ApiKeyFranquicia]') AND type = 'U')
BEGIN
    CREATE TABLE [api].[ApiKeyFranquicia]
    (
        ApiKeyFranquiciaId  INT IDENTITY(1,1) NOT NULL,
        FranquiciaId        INT NOT NULL,
        ApiKey              NVARCHAR(100) NOT NULL,        -- Almacenar hasheada en produccion
        Nombre              NVARCHAR(200) NULL,            -- Descripcion de la key
        FechaExpiracion     DATETIME2 NULL,
        UltimoUso           DATETIME2 NULL,
        ContadorUsos        BIGINT NOT NULL DEFAULT 0,
        IpsPermitidas       NVARCHAR(MAX) NULL,            -- Lista de IPs permitidas, JSON
        Permisos            NVARCHAR(MAX) NULL,            -- Permisos especificos, JSON
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        Activo              BIT NOT NULL DEFAULT 1
    )
    PRINT 'Tabla [api].[ApiKeyFranquicia] creada.'
END
GO

-- ============================================================================
-- SECCION 5: TABLAS DE HECHOS (fact)
-- ============================================================================

PRINT 'Creando tablas de hechos...'

-- Tickets / Comandas
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[fact].[VentaTicket]') AND type = 'U')
BEGIN
    CREATE TABLE [fact].[VentaTicket]
    (
        VentaTicketId       BIGINT IDENTITY(1,1) NOT NULL,
        IngestionBatchId    BIGINT NOT NULL,
        FranquiciaId        INT NOT NULL,

        -- Identificadores del ticket
        ExternalTicketId    NVARCHAR(200) NOT NULL,        -- ticket_id del JSON
        NumeroTicket        NVARCHAR(100) NOT NULL,
        ExternalOrderId     NVARCHAR(200) NULL,

        -- Documento fiscal
        TipoDocumentoFiscal NVARCHAR(50) NULL,
        NumeroDocumentoFiscal NVARCHAR(100) NULL,

        -- Estado y tiempos
        Estado              NVARCHAR(50) NOT NULL,         -- OPEN, CLOSED, CANCELLED, VOIDED, REFUNDED
        FechaNegocio        DATE NOT NULL,
        FechaApertura       DATETIME2 NOT NULL,
        FechaCierre         DATETIME2 NULL,
        PeriodoComida       NVARCHAR(50) NULL,             -- BREAKFAST, LUNCH, DINNER, etc.

        -- Mesa y Mozo
        MesaId              INT NULL,
        NumeroMesa          NVARCHAR(20) NULL,             -- Guardamos tambien el valor crudo
        AreaMesa            NVARCHAR(100) NULL,
        MozoId              INT NULL,
        NombreMozo          NVARCHAR(200) NULL,            -- Guardamos tambien el valor crudo

        -- Cubiertos
        CantidadCubiertos   INT NULL,

        -- Moneda
        MonedaId            INT NULL,
        CodigoMoneda        CHAR(3) NOT NULL,

        -- Importes
        ImporteBruto        DECIMAL(18,2) NOT NULL,
        ImporteDescuento    DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImporteNeto         DECIMAL(18,2) NOT NULL,
        ImporteImpuesto     DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImporteServicio     DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImportePropina      DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImporteTotalPagado  DECIMAL(18,2) NOT NULL DEFAULT 0,

        -- Flags calculados
        TieneDescuento      BIT NOT NULL DEFAULT 0,
        EstaAnulado         BIT NOT NULL DEFAULT 0,

        -- Tiempos de consumo (calculados)
        TiempoConsumoMinutos INT NULL,                     -- Diferencia entre apertura y cierre

        -- Calidad de dato
        TieneDatosCubiertos BIT NOT NULL DEFAULT 0,
        TieneDatosMozo      BIT NOT NULL DEFAULT 0,
        TieneDatosMesa      BIT NOT NULL DEFAULT 0,

        -- Auditoria
        FuenteSistema       NVARCHAR(100) NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER
    )
    PRINT 'Tabla [fact].[VentaTicket] creada.'
END
GO

-- Detalle de Tickets (Items vendidos)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[fact].[VentaTicketDetalle]') AND type = 'U')
BEGIN
    CREATE TABLE [fact].[VentaTicketDetalle]
    (
        VentaTicketDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        VentaTicketId       BIGINT NOT NULL,
        IngestionBatchId    BIGINT NOT NULL,
        FranquiciaId        INT NOT NULL,

        -- Identificador de linea
        ExternalLineId      NVARCHAR(100) NOT NULL,        -- line_id del JSON

        -- Producto
        ProductoId          INT NULL,
        CodigoProducto      NVARCHAR(100) NOT NULL,
        NombreProducto      NVARCHAR(200) NOT NULL,
        TipoPlatoId         INT NULL,
        CategoriaProducto   NVARCHAR(50) NULL,             -- Valor crudo del JSON
        FamiliaProducto     NVARCHAR(100) NULL,
        SubFamiliaProducto  NVARCHAR(100) NULL,

        -- Cantidades e importes
        Cantidad            DECIMAL(18,4) NOT NULL,
        PrecioUnitario      DECIMAL(18,2) NOT NULL,
        ImporteBruto        DECIMAL(18,2) NOT NULL,
        ImporteDescuento    DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImporteNeto         DECIMAL(18,2) NOT NULL,
        ImporteImpuesto     DECIMAL(18,2) NOT NULL DEFAULT 0,

        -- Flags
        TieneDescuento      BIT NOT NULL DEFAULT 0,
        EstaAnulado         BIT NOT NULL DEFAULT 0,
        MotivoAnulacion     NVARCHAR(500) NULL,

        -- Tiempos
        FechaPedido         DATETIME2 NULL,
        FechaServido        DATETIME2 NULL,
        TiempoPreparacionMinutos INT NULL,                 -- Diferencia entre pedido y servido

        -- Notas
        Notas               NVARCHAR(MAX) NULL,

        -- Auditoria
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER
    )
    PRINT 'Tabla [fact].[VentaTicketDetalle] creada.'
END
GO

-- Descuentos aplicados a tickets
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[fact].[VentaTicketDescuento]') AND type = 'U')
BEGIN
    CREATE TABLE [fact].[VentaTicketDescuento]
    (
        VentaTicketDescuentoId BIGINT IDENTITY(1,1) NOT NULL,
        VentaTicketId       BIGINT NOT NULL,
        IngestionBatchId    BIGINT NOT NULL,
        FranquiciaId        INT NOT NULL,

        CodigoDescuento     NVARCHAR(100) NULL,
        NombreDescuento     NVARCHAR(200) NULL,
        TipoDescuento       NVARCHAR(50) NULL,             -- PERCENTAGE, FIXED_AMOUNT, etc.
        ValorDescuento      DECIMAL(18,4) NULL,            -- Porcentaje o monto fijo
        ImporteDescuento    DECIMAL(18,2) NOT NULL,

        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    )
    PRINT 'Tabla [fact].[VentaTicketDescuento] creada.'
END
GO

-- Medios de pago utilizados en tickets
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[fact].[VentaTicketMedioPago]') AND type = 'U')
BEGIN
    CREATE TABLE [fact].[VentaTicketMedioPago]
    (
        VentaTicketMedioPagoId BIGINT IDENTITY(1,1) NOT NULL,
        VentaTicketId       BIGINT NOT NULL,
        IngestionBatchId    BIGINT NOT NULL,
        FranquiciaId        INT NOT NULL,

        MedioPagoId         INT NULL,
        CodigoMedioPago     NVARCHAR(50) NOT NULL,
        MarcaTarjeta        NVARCHAR(100) NULL,            -- VISA, MASTERCARD, etc.
        Importe             DECIMAL(18,2) NOT NULL,

        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    )
    PRINT 'Tabla [fact].[VentaTicketMedioPago] creada.'
END
GO

PRINT '============================================'
PRINT 'Todas las tablas han sido creadas.'
PRINT '============================================'
GO
