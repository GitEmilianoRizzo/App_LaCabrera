/*
================================================================================
  LA CABRERA - Migration 011
  Descripcion: Insertar franquicias desde Excel de seguimiento
  Fecha: 2026-08-17

  IMPORTANTE: No tocar Marbella que ya existe
================================================================================
*/

USE [LaCabreraDB]
GO

-- Insertar Grupos Economicos (Razones Sociales) que no existen
PRINT 'Insertando Grupos Economicos...'

-- Funcion para evitar duplicados
INSERT INTO dim.GrupoEconomico (Codigo, Nombre, Pais, ContactoNombre)
SELECT Codigo, Nombre, Pais, Contacto
FROM (VALUES
    ('EEP_SA', 'EEP SA', 'Argentina', 'Andres Latella'),
    ('ARRIBOS_SA', 'Arribos SA', 'Argentina', 'Daniel Carrieri'),
    ('2006_SA', '2006 SA', 'Argentina', 'Daniel Carrieri'),
    ('FUEGOSARG_SL', 'Fuegosarg SL', 'Espana', 'Diego Batica'),
    ('COLCABRERA', 'Colcabrera', 'Colombia', 'Diego Pinto'),
    ('FOODIFY_BOGOTA', 'Foodify Bogota', 'Colombia', 'Diego Pinto'),
    ('FOODIFY_CARTAGENA', 'Foodify Cartagena', 'Colombia', 'Diego Pinto'),
    ('HUENTALA_WINES', 'Huentala Wines SA', 'Argentina', 'Eduardo Peralta'),
    ('6_DEL_SUR_SL', '6 del SUR SL', 'Espana', 'Felipe Cafferatta'),
    ('PARILA_INC', 'Parila La Cabrera INC', 'Filipinas', 'Jane Chuajap'),
    ('LC_MERCADO_SA', 'La Cabrera del Mercado SA', 'Argentina', 'Jorge Russmann / Grupo Leuzzi'),
    ('YAJOLA_SA', 'Yajola SA', 'Argentina', 'Jose Alvarenga'),
    ('INV_BONARDA', 'Inversiones Bonarda', 'Chile', 'Jose Ansoleaga'),
    ('INV_CALADOC', 'Inversiones Caladoc', 'Chile', 'Jose Ansoleaga'),
    ('INV_CALADOC_SL', 'Inversiones Caladoc SL', 'Espana', 'Jose Ansoleaga'),
    ('NORTE_EMPRENDIMIENTOS', 'Norte Emprendimientos', 'Argentina', 'Juan Carlos Abud'),
    ('OBREGON_ASADOR', 'Obregon Asador', 'Mexico', 'Marcelino'),
    ('APUNTO_GRUPO_LLC', 'Apunto Grupo LLC', 'USA', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('MARCA_COCONUT_LLC', 'Marca Coconut LLC', 'USA', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('MARCCA_MIDTOWN_LLC', 'Marcca Midtown LLC', 'USA', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('LC_MIRAFLORES', 'La Cabrera Miraflores', 'Peru', 'Nicolas Ghia'),
    ('AREAS_CHILE', 'Areas de Chile', 'Chile', 'Raul Mancila Sarno'),
    ('CB_ASADORES_SA', 'C.B. Asadores S.A.', 'Argentina', 'Rocio Lescano Grupo Leuzzi'),
    ('LC_CB_SA', 'La Cabrera C.B. S.A.', 'Argentina', 'Rosana Kuziw / Rocio Lescano Grupo Leuzzi'),
    ('SIN_DATOS', 'Sin datos', 'Paraguay', NULL)
) AS Source (Codigo, Nombre, Pais, Contacto)
WHERE NOT EXISTS (
    SELECT 1 FROM dim.GrupoEconomico ge WHERE ge.Codigo = Source.Codigo
)
GO

PRINT 'Grupos Economicos insertados.'

-- Ahora insertar las franquicias
PRINT 'Insertando Franquicias...'

-- Determinar el pais y timezone basado en la ubicacion
DECLARE @Franquicias TABLE (
    Codigo NVARCHAR(50),
    Nombre NVARCHAR(200),
    GrupoCodigo NVARCHAR(50),
    Pais NVARCHAR(100),
    Ciudad NVARCHAR(100),
    Timezone NVARCHAR(100),
    SistemaOrigen NVARCHAR(100),
    ContactoNombre NVARCHAR(200)
)

INSERT INTO @Franquicias VALUES
    ('PILAR', 'Pilar', 'EEP_SA', 'Argentina', 'Pilar', 'America/Buenos_Aires', NULL, 'Andres Latella'),
    ('AEROPARQUE', 'Aeroparque', 'ARRIBOS_SA', 'Argentina', 'Buenos Aires', 'America/Buenos_Aires', 'VINSON', 'Daniel Carrieri'),
    ('EZEIZA', 'Ezeiza', '2006_SA', 'Argentina', 'Ezeiza', 'America/Buenos_Aires', 'VINSON', 'Daniel Carrieri'),
    ('MALAGA', 'Malaga', 'FUEGOSARG_SL', 'Espana', 'Malaga', 'Europe/Madrid', 'AGORA', 'Diego Batica'),
    ('BOGOTA_COLCABRERA', 'Bogota (Colcabrera)', 'COLCABRERA', 'Colombia', 'Bogota', 'America/Bogota', NULL, 'Diego Pinto'),
    ('BOGOTA_FOODIFY', 'Bogota (Foodify)', 'FOODIFY_BOGOTA', 'Colombia', 'Bogota', 'America/Bogota', NULL, 'Diego Pinto'),
    ('CARTAGENA', 'Cartagena', 'FOODIFY_CARTAGENA', 'Colombia', 'Cartagena', 'America/Bogota', NULL, 'Diego Pinto'),
    ('MENDOZA', 'Mendoza', 'HUENTALA_WINES', 'Argentina', 'Mendoza', 'America/Buenos_Aires', NULL, 'Eduardo Peralta'),
    ('BARCELONA', 'Barcelona', '6_DEL_SUR_SL', 'Espana', 'Barcelona', 'Europe/Madrid', NULL, 'Felipe Cafferatta'),
    ('MANILA_SHANGRILA', 'Manila Shangrila', 'PARILA_INC', 'Filipinas', 'Manila', 'Asia/Manila', NULL, 'Jane Chuajap'),
    ('MANILA_AYALA', 'Manila Ayala', 'PARILA_INC', 'Filipinas', 'Manila', 'Asia/Manila', NULL, 'Jane Chuajap'),
    ('LA_PLATA', 'La Plata', 'LC_MERCADO_SA', 'Argentina', 'La Plata', 'America/Buenos_Aires', NULL, 'Jorge Russmann / Grupo Leuzzi'),
    ('IGUAZU', 'Iguazu', 'YAJOLA_SA', 'Argentina', 'Iguazu', 'America/Buenos_Aires', NULL, 'Jose Alvarenga'),
    ('AL_PASO_CHILE', 'Al Paso Chile', 'INV_BONARDA', 'Chile', 'Santiago', 'America/Santiago', NULL, 'Jose Ansoleaga'),
    ('EGANA', 'Egana', 'INV_BONARDA', 'Chile', 'Santiago', 'America/Santiago', NULL, 'Jose Ansoleaga'),
    ('ALONSO', 'Alonso', 'INV_CALADOC', 'Chile', 'Santiago', 'America/Santiago', NULL, 'Jose Ansoleaga'),
    ('ISIDORA', 'Isidora', 'INV_CALADOC', 'Chile', 'Santiago', 'America/Santiago', NULL, 'Jose Ansoleaga'),
    ('MADRID', 'Madrid', 'INV_CALADOC_SL', 'Espana', 'Madrid', 'Europe/Madrid', NULL, 'Jose Ansoleaga'),
    ('JUJUY', 'Jujuy', 'NORTE_EMPRENDIMIENTOS', 'Argentina', 'San Salvador de Jujuy', 'America/Buenos_Aires', NULL, 'Juan Carlos Abud'),
    ('SALTA', 'Salta', 'NORTE_EMPRENDIMIENTOS', 'Argentina', 'Salta', 'America/Buenos_Aires', NULL, 'Juan Carlos Abud'),
    ('MEXICO', 'Mexico', 'OBREGON_ASADOR', 'Mexico', 'Ciudad de Mexico', 'America/Mexico_City', NULL, 'Marcelino'),
    ('MIAMI_SUNNY', 'Miami Sunny', 'APUNTO_GRUPO_LLC', 'USA', 'Miami', 'America/New_York', 'TOAST', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('MIAMI_COCONUT', 'Miami Coconut', 'MARCA_COCONUT_LLC', 'USA', 'Miami', 'America/New_York', 'TOAST', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('MIAMI_MIDTOWN', 'Miami Midtown', 'MARCCA_MIDTOWN_LLC', 'USA', 'Miami', 'America/New_York', 'TOAST', 'Marcelo Ferreiros / Grupo Leuzzi'),
    ('LIMA_MIRAFLORES', 'Lima Miraflores', 'LC_MIRAFLORES', 'Peru', 'Lima', 'America/Lima', NULL, 'Nicolas Ghia'),
    ('LIMA_POLO', 'Lima Polo', 'LC_MIRAFLORES', 'Peru', 'Lima', 'America/Lima', NULL, 'Nicolas Ghia'),
    ('AEROPUERTOS_CHILE', 'Aeropuertos de Chile', 'AREAS_CHILE', 'Chile', 'Santiago', 'America/Santiago', NULL, 'Raul Mancila Sarno'),
    ('CARILO', 'Carilo', 'CB_ASADORES_SA', 'Argentina', 'Carilo', 'America/Buenos_Aires', NULL, 'Rocio Lescano Grupo Leuzzi'),
    ('CITY_BELL', 'City Bell', 'LC_CB_SA', 'Argentina', 'City Bell', 'America/Buenos_Aires', NULL, 'Rosana Kuziw / Rocio Lescano Grupo Leuzzi'),
    ('ASUNCION_SANTA_TERESA', 'Asuncion Santa Teresa', 'SIN_DATOS', 'Paraguay', 'Asuncion', 'America/Asuncion', NULL, NULL),
    ('ASUNCION_SOL', 'Asuncion Sol', 'SIN_DATOS', 'Paraguay', 'Asuncion', 'America/Asuncion', NULL, NULL),
    ('ASUNCION_GALERIA', 'Asuncion Galeria', 'SIN_DATOS', 'Paraguay', 'Asuncion', 'America/Asuncion', NULL, NULL)

-- Insertar franquicias que no existen
INSERT INTO dim.Franquicia (
    Codigo,
    Nombre,
    GrupoEconomicoId,
    Pais,
    Ciudad,
    ZonaHoraria,
    SistemaOrigen,
    ContactoNombre,
    EstadoIntegracion
)
SELECT
    f.Codigo,
    f.Nombre,
    ge.GrupoEconomicoId,
    f.Pais,
    f.Ciudad,
    f.Timezone,
    f.SistemaOrigen,
    f.ContactoNombre,
    'PENDING'
FROM @Franquicias f
LEFT JOIN dim.GrupoEconomico ge ON ge.Codigo = f.GrupoCodigo
WHERE NOT EXISTS (
    SELECT 1 FROM dim.Franquicia fr WHERE fr.Codigo = f.Codigo
)
GO

-- Actualizar Marbella con datos del Excel (solo si no tiene contacto)
UPDATE dim.Franquicia
SET ContactoNombre = 'Diego Batica',
    SistemaOrigen = 'AGORA'
WHERE Codigo = 'MARBELLA_01'
  AND (ContactoNombre IS NULL OR ContactoNombre = '')
GO

PRINT 'Franquicias insertadas.'

-- Mostrar resumen
SELECT
    'Grupos Economicos' AS Tabla,
    COUNT(*) AS Total
FROM dim.GrupoEconomico
UNION ALL
SELECT
    'Franquicias' AS Tabla,
    COUNT(*) AS Total
FROM dim.Franquicia
WHERE Activo = 1
GO

PRINT '============================================'
PRINT 'Migracion completada.'
PRINT '============================================'
GO
