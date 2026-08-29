/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 12_create_cfg_franjas_alicuotas.sql
  Descripcion: Tablas de configuracion para franjas horarias y alicuotas
  Autor: Claude Code
  Fecha: 2026-08-22
  Lote: MEJORAS_20260822 - Etapa 1.4 (franjas) y 1.3 (alicuotas)

  TABLAS:
  - cfg.FranjaHorariaFranquicia: Horarios de almuerzo/cena por franquicia
  - cfg.AlicuotaImpuestoFranquicia: Alicuotas de impuesto por franquicia/categoria

  MOTIVO - FRANJAS HORARIAS:
  ==========================
  El horario de almuerzo de Madrid NO es el de Miami:
  - Madrid: Almuerzo 13:00-17:00, Cena 20:00-00:00
  - Miami: Lunch 11:00-15:00, Dinner 17:00-22:00
  - Buenos Aires: Almuerzo 12:00-16:00, Cena 20:00-00:00

  Por eso la franja NO puede estar hardcodeada en el codigo.
  Se usa para derivar MealPeriod cuando el POS no lo informa.

  MOTIVO - ALICUOTAS:
  ===================
  Sirve como fallback cuando el POS no informa impuesto por linea.
  Permite derivar ImporteNetoSinImpuesto a partir de ImporteNeto.

  INSTRUCCIONES:
  - Ejecutar despues de 11_alter_fact_turno.sql
  - Idempotente: puede re-ejecutarse sin error
  - Incluye seeds con franjas por defecto para franquicias existentes
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- TABLA: cfg.FranjaHorariaFranquicia
-- Horarios de cada periodo de comida por franquicia
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'cfg.FranjaHorariaFranquicia') AND type = 'U')
BEGIN
    CREATE TABLE cfg.FranjaHorariaFranquicia (
        FranjaHorariaId         INT IDENTITY(1,1) NOT NULL,

        FranquiciaId            INT NOT NULL,
        MealPeriod              VARCHAR(20) NOT NULL,       -- BREAKFAST, LUNCH, DINNER, LATE_NIGHT

        HoraDesde               TIME NOT NULL,              -- Hora de inicio (local de la franquicia)
        HoraHasta               TIME NOT NULL,              -- Hora de fin

        Activo                  BIT NOT NULL DEFAULT 1,
        Prioridad               INT NOT NULL DEFAULT 100,   -- Para resolver solapamientos

        -- Auditoria
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaModificacion       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UsuarioCreacion         NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,

        CONSTRAINT PK_FranjaHorariaFranquicia PRIMARY KEY CLUSTERED (FranjaHorariaId),

        CONSTRAINT FK_FranjaHoraria_Franquicia
            FOREIGN KEY (FranquiciaId) REFERENCES dim.Franquicia(FranquiciaId),

        CONSTRAINT CK_FranjaHoraria_MealPeriod
            CHECK (MealPeriod IN ('BREAKFAST', 'LUNCH', 'DINNER', 'LATE_NIGHT')),

        -- Una sola franja activa por franquicia+periodo
        CONSTRAINT UQ_FranjaHoraria_Franquicia_Periodo
            UNIQUE (FranquiciaId, MealPeriod, Activo)
    )

    PRINT 'Tabla cfg.FranjaHorariaFranquicia creada.'
END
ELSE
    PRINT 'Tabla cfg.FranjaHorariaFranquicia ya existe.'
GO

-- ============================================================================
-- TABLA: cfg.AlicuotaImpuestoFranquicia
-- Alicuotas de impuesto por franquicia (fallback cuando POS no informa)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'cfg.AlicuotaImpuestoFranquicia') AND type = 'U')
BEGIN
    CREATE TABLE cfg.AlicuotaImpuestoFranquicia (
        AlicuotaId              INT IDENTITY(1,1) NOT NULL,

        FranquiciaId            INT NOT NULL,
        CategoriaProducto       NVARCHAR(100) NULL,         -- NULL = alicuota default de la franquicia

        Alicuota                DECIMAL(9,6) NOT NULL,      -- Ej: 0.21 para 21%
        NombreImpuesto          NVARCHAR(100) NULL,         -- IVA, Sales Tax, etc.

        -- Vigencia
        VigenciaDesde           DATE NOT NULL,
        VigenciaHasta           DATE NULL,                  -- NULL = vigente indefinidamente

        Activo                  BIT NOT NULL DEFAULT 1,

        -- Auditoria
        FechaCreacion           DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaModificacion       DATETIME2 NOT NULL DEFAULT GETDATE(),
        UsuarioCreacion         NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        UsuarioModificacion     NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,

        CONSTRAINT PK_AlicuotaImpuestoFranquicia PRIMARY KEY CLUSTERED (AlicuotaId),

        CONSTRAINT FK_AlicuotaImpuesto_Franquicia
            FOREIGN KEY (FranquiciaId) REFERENCES dim.Franquicia(FranquiciaId),

        CONSTRAINT CK_AlicuotaImpuesto_Rango
            CHECK (Alicuota >= 0 AND Alicuota <= 1),  -- 0% a 100%

        CONSTRAINT CK_AlicuotaImpuesto_Vigencia
            CHECK (VigenciaHasta IS NULL OR VigenciaHasta >= VigenciaDesde)
    )

    PRINT 'Tabla cfg.AlicuotaImpuestoFranquicia creada.'
END
ELSE
    PRINT 'Tabla cfg.AlicuotaImpuestoFranquicia ya existe.'
GO

-- Indice para busqueda de alicuota vigente
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AlicuotaImpuesto_Franquicia_Vigencia' AND object_id = OBJECT_ID('cfg.AlicuotaImpuestoFranquicia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AlicuotaImpuesto_Franquicia_Vigencia
    ON cfg.AlicuotaImpuestoFranquicia (FranquiciaId, VigenciaDesde, VigenciaHasta)
    WHERE Activo = 1

    PRINT 'Indice IX_AlicuotaImpuesto_Franquicia_Vigencia creado.'
END
GO

-- ============================================================================
-- SEEDS: Franjas horarias por defecto para franquicias existentes
-- ============================================================================
PRINT 'Insertando franjas horarias por defecto...'

-- Obtener IDs de franquicias existentes para seed
DECLARE @FranquiciaId INT

-- Cursor para cada franquicia existente
DECLARE franquicia_cursor CURSOR FOR
    SELECT FranquiciaId FROM dim.Franquicia WHERE Activo = 1

OPEN franquicia_cursor
FETCH NEXT FROM franquicia_cursor INTO @FranquiciaId

WHILE @@FETCH_STATUS = 0
BEGIN
    -- LUNCH (default global)
    IF NOT EXISTS (SELECT 1 FROM cfg.FranjaHorariaFranquicia WHERE FranquiciaId = @FranquiciaId AND MealPeriod = 'LUNCH')
    BEGIN
        INSERT INTO cfg.FranjaHorariaFranquicia (FranquiciaId, MealPeriod, HoraDesde, HoraHasta)
        VALUES (@FranquiciaId, 'LUNCH', '11:00:00', '16:00:00')
    END

    -- DINNER (default global)
    IF NOT EXISTS (SELECT 1 FROM cfg.FranjaHorariaFranquicia WHERE FranquiciaId = @FranquiciaId AND MealPeriod = 'DINNER')
    BEGIN
        INSERT INTO cfg.FranjaHorariaFranquicia (FranquiciaId, MealPeriod, HoraDesde, HoraHasta)
        VALUES (@FranquiciaId, 'DINNER', '19:00:00', '23:59:00')
    END

    FETCH NEXT FROM franquicia_cursor INTO @FranquiciaId
END

CLOSE franquicia_cursor
DEALLOCATE franquicia_cursor

PRINT 'Franjas horarias por defecto insertadas.'
GO

-- ============================================================================
-- SEEDS: Alicuotas por defecto segun pais
-- ============================================================================
PRINT 'Insertando alicuotas por defecto...'

-- Argentina - IVA 21%
INSERT INTO cfg.AlicuotaImpuestoFranquicia (FranquiciaId, CategoriaProducto, Alicuota, NombreImpuesto, VigenciaDesde)
SELECT f.FranquiciaId, NULL, 0.21, 'IVA', '2026-01-01'
FROM dim.Franquicia f
WHERE f.Pais = 'Argentina' AND f.Activo = 1
AND NOT EXISTS (
    SELECT 1 FROM cfg.AlicuotaImpuestoFranquicia a
    WHERE a.FranquiciaId = f.FranquiciaId AND a.CategoriaProducto IS NULL
)

-- Espana - IVA 10% (restauracion)
INSERT INTO cfg.AlicuotaImpuestoFranquicia (FranquiciaId, CategoriaProducto, Alicuota, NombreImpuesto, VigenciaDesde)
SELECT f.FranquiciaId, NULL, 0.10, 'IVA', '2026-01-01'
FROM dim.Franquicia f
WHERE f.Pais = 'Espana' AND f.Activo = 1
AND NOT EXISTS (
    SELECT 1 FROM cfg.AlicuotaImpuestoFranquicia a
    WHERE a.FranquiciaId = f.FranquiciaId AND a.CategoriaProducto IS NULL
)

-- USA (Florida) - Sales Tax ~7%
INSERT INTO cfg.AlicuotaImpuestoFranquicia (FranquiciaId, CategoriaProducto, Alicuota, NombreImpuesto, VigenciaDesde)
SELECT f.FranquiciaId, NULL, 0.07, 'Sales Tax', '2026-01-01'
FROM dim.Franquicia f
WHERE f.Pais = 'USA' AND f.Activo = 1
AND NOT EXISTS (
    SELECT 1 FROM cfg.AlicuotaImpuestoFranquicia a
    WHERE a.FranquiciaId = f.FranquiciaId AND a.CategoriaProducto IS NULL
)

-- Paraguay - IVA 10%
INSERT INTO cfg.AlicuotaImpuestoFranquicia (FranquiciaId, CategoriaProducto, Alicuota, NombreImpuesto, VigenciaDesde)
SELECT f.FranquiciaId, NULL, 0.10, 'IVA', '2026-01-01'
FROM dim.Franquicia f
WHERE f.Pais = 'Paraguay' AND f.Activo = 1
AND NOT EXISTS (
    SELECT 1 FROM cfg.AlicuotaImpuestoFranquicia a
    WHERE a.FranquiciaId = f.FranquiciaId AND a.CategoriaProducto IS NULL
)

-- Colombia - IVA 19%
INSERT INTO cfg.AlicuotaImpuestoFranquicia (FranquiciaId, CategoriaProducto, Alicuota, NombreImpuesto, VigenciaDesde)
SELECT f.FranquiciaId, NULL, 0.19, 'IVA', '2026-01-01'
FROM dim.Franquicia f
WHERE f.Pais = 'Colombia' AND f.Activo = 1
AND NOT EXISTS (
    SELECT 1 FROM cfg.AlicuotaImpuestoFranquicia a
    WHERE a.FranquiciaId = f.FranquiciaId AND a.CategoriaProducto IS NULL
)

PRINT 'Alicuotas por defecto insertadas.'
GO

-- ============================================================================
-- FUNCION: Obtener MealPeriod para una hora dada
-- ============================================================================
IF OBJECT_ID('cfg.fn_GetMealPeriod', 'FN') IS NOT NULL
    DROP FUNCTION cfg.fn_GetMealPeriod
GO

CREATE FUNCTION cfg.fn_GetMealPeriod (
    @FranquiciaId INT,
    @Hora TIME
)
RETURNS VARCHAR(20)
AS
BEGIN
    DECLARE @MealPeriod VARCHAR(20)

    SELECT TOP 1 @MealPeriod = MealPeriod
    FROM cfg.FranjaHorariaFranquicia
    WHERE FranquiciaId = @FranquiciaId
      AND Activo = 1
      AND @Hora >= HoraDesde
      AND @Hora <= HoraHasta
    ORDER BY Prioridad

    RETURN ISNULL(@MealPeriod, 'UNKNOWN')
END
GO

PRINT 'Funcion cfg.fn_GetMealPeriod creada.'
GO

-- ============================================================================
-- FUNCION: Obtener alicuota vigente para una franquicia/categoria/fecha
-- ============================================================================
IF OBJECT_ID('cfg.fn_GetAlicuotaImpuesto', 'FN') IS NOT NULL
    DROP FUNCTION cfg.fn_GetAlicuotaImpuesto
GO

CREATE FUNCTION cfg.fn_GetAlicuotaImpuesto (
    @FranquiciaId INT,
    @CategoriaProducto NVARCHAR(100),
    @Fecha DATE
)
RETURNS DECIMAL(9,6)
AS
BEGIN
    DECLARE @Alicuota DECIMAL(9,6)

    -- Primero buscar por categoria especifica
    SELECT TOP 1 @Alicuota = Alicuota
    FROM cfg.AlicuotaImpuestoFranquicia
    WHERE FranquiciaId = @FranquiciaId
      AND CategoriaProducto = @CategoriaProducto
      AND Activo = 1
      AND VigenciaDesde <= @Fecha
      AND (VigenciaHasta IS NULL OR VigenciaHasta >= @Fecha)
    ORDER BY VigenciaDesde DESC

    -- Si no hay, buscar default de la franquicia
    IF @Alicuota IS NULL
    BEGIN
        SELECT TOP 1 @Alicuota = Alicuota
        FROM cfg.AlicuotaImpuestoFranquicia
        WHERE FranquiciaId = @FranquiciaId
          AND CategoriaProducto IS NULL
          AND Activo = 1
          AND VigenciaDesde <= @Fecha
          AND (VigenciaHasta IS NULL OR VigenciaHasta >= @Fecha)
        ORDER BY VigenciaDesde DESC
    END

    RETURN @Alicuota  -- NULL si no hay configuracion
END
GO

PRINT 'Funcion cfg.fn_GetAlicuotaImpuesto creada.'
GO

PRINT '=========================================='
PRINT 'Script 12 completado: Tablas de configuracion creadas.'
PRINT '- cfg.FranjaHorariaFranquicia con seeds'
PRINT '- cfg.AlicuotaImpuestoFranquicia con seeds por pais'
PRINT '- Funciones auxiliares fn_GetMealPeriod y fn_GetAlicuotaImpuesto'
PRINT '=========================================='
GO
