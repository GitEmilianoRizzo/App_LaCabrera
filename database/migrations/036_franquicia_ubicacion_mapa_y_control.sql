-- =====================================================
-- Regla del Mapa de Consumo: toda franquicia tiene que poder ubicarse en el mapa
--
-- 1. dim.Franquicia: PaisIso (ISO 3166 numérico, el código del mapa del mundo), ZonaMapa (la zona en la que se
--    agrupa en el mapa; p. ej. Ezeiza y Aeroparque -> Buenos Aires), Latitud y Longitud del marcador.
--    Sin estos datos el nodo de la franquicia no se puede activar (ConexionService) y el control diario la marca en error.
-- 2. log.ControlMapaConsumo: resultado del control diario (ControlMapaConsumoJob) por franquicia, para auditar
--    desde cuándo una franquicia está fuera de regla.
--
-- Re-ejecutable y no destructiva: agrega columnas y completa solo lo que está en NULL.
-- =====================================================

SET NOCOUNT ON;

IF COL_LENGTH('dim.Franquicia', 'PaisIso') IS NULL ALTER TABLE dim.Franquicia ADD PaisIso CHAR(3) NULL;
IF COL_LENGTH('dim.Franquicia', 'ZonaMapa') IS NULL ALTER TABLE dim.Franquicia ADD ZonaMapa NVARCHAR(100) NULL;
IF COL_LENGTH('dim.Franquicia', 'Latitud') IS NULL ALTER TABLE dim.Franquicia ADD Latitud DECIMAL(9,5) NULL;
IF COL_LENGTH('dim.Franquicia', 'Longitud') IS NULL ALTER TABLE dim.Franquicia ADD Longitud DECIMAL(9,5) NULL;
GO

UPDATE f SET
    PaisIso = COALESCE(f.PaisIso, u.PaisIso),
    ZonaMapa = COALESCE(f.ZonaMapa, u.ZonaMapa),
    Latitud = COALESCE(f.Latitud, u.Latitud),
    Longitud = COALESCE(f.Longitud, u.Longitud)
FROM dim.Franquicia f
JOIN (VALUES
    ('MARBELLA_01',           '724', N'Marbella',              36.51000,   -4.88000),
    ('EZEIZA',                '032', N'Buenos Aires',         -34.60000,  -58.38000),
    ('AEROPARQUE',            '032', N'Buenos Aires',         -34.60000,  -58.38000),
    ('PALERMO_NORTE',         '032', N'Buenos Aires',         -34.60000,  -58.38000),
    ('PALERMO_SUR',           '032', N'Buenos Aires',         -34.60000,  -58.38000),
    ('BARCELONA',             '724', N'Barcelona',             41.39000,    2.17000),
    ('MADRID',                '724', N'Madrid',                40.42000,   -3.70000),
    ('MALAGA',                '724', N'Málaga',                36.72000,   -4.42000),
    ('MIAMI_SUNNY',           '840', N'Miami',                 25.76000,  -80.19000),
    ('MIAMI_COCONUT',         '840', N'Miami',                 25.76000,  -80.19000),
    ('MIAMI_MIDTOWN',         '840', N'Miami',                 25.76000,  -80.19000),
    ('AEROPUERTOS_CHILE',     '152', N'Santiago',             -33.45000,  -70.67000),
    ('AL_PASO_CHILE',         '152', N'Santiago',             -33.45000,  -70.67000),
    ('EGANA',                 '152', N'Santiago',             -33.45000,  -70.67000),
    ('ALONSO',                '152', N'Santiago',             -33.45000,  -70.67000),
    ('ISIDORA',               '152', N'Santiago',             -33.45000,  -70.67000),
    ('EL_RODEO',              '152', N'Santiago',             -33.45000,  -70.67000),
    ('CARILO',                '032', N'Cariló',               -37.16000,  -56.90000),
    ('PILAR',                 '032', N'Pilar',                -34.46000,  -58.91000),
    ('MENDOZA',               '032', N'Mendoza',              -32.89000,  -68.83000),
    ('CITY_BELL',             '032', N'City Bell',            -34.87000,  -58.05000),
    ('LA_PLATA',              '032', N'La Plata',             -34.92000,  -57.95000),
    ('JUJUY',                 '032', N'San Salvador de Jujuy',-24.19000,  -65.30000),
    ('SALTA',                 '032', N'Salta',                -24.79000,  -65.41000),
    ('IGUAZU',                '032', N'Puerto Iguazú',        -25.60000,  -54.57000),
    ('BOGOTA_COLCABRERA',     '170', N'Bogotá',                 4.71000,  -74.07000),
    ('BOGOTA_FOODIFY',        '170', N'Bogotá',                 4.71000,  -74.07000),
    ('CARTAGENA',             '170', N'Cartagena',             10.39000,  -75.51000),
    ('LIMA_MIRAFLORES',       '604', N'Lima',                 -12.05000,  -77.04000),
    ('LIMA_POLO',             '604', N'Lima',                 -12.05000,  -77.04000),
    ('MEXICO',                '484', N'Ciudad de México',      19.43000,  -99.13000),
    ('MANILA_SHANGRILA',      '608', N'Manila',                14.60000,  120.98000),
    ('MANILA_AYALA',          '608', N'Manila',                14.60000,  120.98000),
    ('ASUNCION_SANTA_TERESA', '600', N'Asunción',             -25.26000,  -57.58000),
    ('ASUNCION_SOL',          '600', N'Asunción',             -25.26000,  -57.58000),
    ('ASUNCION_GALERIA',      '600', N'Asunción',             -25.26000,  -57.58000),
    ('PORTO_ALEGRE',          '076', N'Porto Alegre',         -30.03000,  -51.23000)
) AS u (Codigo, PaisIso, ZonaMapa, Latitud, Longitud) ON u.Codigo = f.Codigo
WHERE f.PaisIso IS NULL OR f.ZonaMapa IS NULL OR f.Latitud IS NULL OR f.Longitud IS NULL;
GO

IF OBJECT_ID('log.ControlMapaConsumo') IS NULL
BEGIN
    CREATE TABLE log.ControlMapaConsumo (
        ControlMapaConsumoId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ControlMapaConsumo PRIMARY KEY,
        FechaControl DATETIME2 NOT NULL CONSTRAINT DF_ControlMapaConsumo_Fecha DEFAULT SYSUTCDATETIME(),
        FranquiciaId INT NOT NULL,
        Estado VARCHAR(20) NOT NULL,          -- OK | ATENCION | ERROR | SIN_DATOS
        Problemas NVARCHAR(MAX) NULL,         -- JSON con los chequeos que fallaron
        PctClasificado DECIMAL(6,2) NULL
    );
    CREATE INDEX IX_ControlMapaConsumo_Franquicia ON log.ControlMapaConsumo (FranquiciaId, FechaControl DESC);
END
GO

SELECT Codigo, PaisIso, ZonaMapa, Latitud, Longitud FROM dim.Franquicia
WHERE PaisIso IS NULL OR ZonaMapa IS NULL OR Latitud IS NULL OR Longitud IS NULL;
