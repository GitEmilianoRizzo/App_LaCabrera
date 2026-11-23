-- =====================================================
-- Alta de la franquicia Corrientes (Argentina), del grupo Orion
--
-- - Grupo económico GRUPO_ORION (nuevo), contacto sas.gruporion@gmail.com.
-- - Franquicia CORRIENTES en ARS y America/Buenos_Aires, como el resto de las argentinas.
--   Estado PENDING, sin POS ni conector todavía.
-- - Regla del Mapa de Consumo: PaisIso 032, zona Corrientes y coordenadas de la ciudad, así el nodo
--   se puede activar cuando se defina el conector.
--
-- Re-ejecutable: no duplica ni pisa lo que ya exista (solo completa lo que esté en NULL).
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dim.GrupoEconomico WHERE Codigo = 'GRUPO_ORION')
    INSERT INTO dim.GrupoEconomico (Codigo, Nombre, Pais, ContactoEmail, Activo)
    VALUES ('GRUPO_ORION', 'Grupo Orion', 'Argentina', 'sas.gruporion@gmail.com', 1);

DECLARE @GrupoId INT = (SELECT GrupoEconomicoId FROM dim.GrupoEconomico WHERE Codigo = 'GRUPO_ORION');
DECLARE @MonedaId INT = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = 'ARS');

IF NOT EXISTS (SELECT 1 FROM dim.Franquicia WHERE Codigo = 'CORRIENTES')
    INSERT INTO dim.Franquicia (Codigo, Nombre, GrupoEconomicoId, Pais, Ciudad, ZonaHoraria, MonedaId,
        ContactoEmail, EstadoIntegracion, Activo, PaisIso, ZonaMapa, Latitud, Longitud)
    VALUES ('CORRIENTES', 'Corrientes', @GrupoId, 'Argentina', 'Corrientes', 'America/Buenos_Aires', @MonedaId,
        'sas.gruporion@gmail.com', 'PENDING', 1, '032', N'Corrientes', -27.47000, -58.83000);
ELSE
    UPDATE dim.Franquicia SET
        GrupoEconomicoId = COALESCE(GrupoEconomicoId, @GrupoId),
        ContactoEmail = COALESCE(ContactoEmail, 'sas.gruporion@gmail.com'),
        PaisIso = COALESCE(PaisIso, '032'),
        ZonaMapa = COALESCE(ZonaMapa, N'Corrientes'),
        Latitud = COALESCE(Latitud, -27.47000),
        Longitud = COALESCE(Longitud, -58.83000)
    WHERE Codigo = 'CORRIENTES';

COMMIT TRANSACTION;

SELECT f.FranquiciaId, f.Codigo, f.Nombre, ge.Nombre AS Grupo, f.Pais, f.ZonaHoraria, m.CodigoISO AS Moneda,
       f.ContactoEmail, f.EstadoIntegracion, f.PaisIso, f.ZonaMapa, f.Latitud, f.Longitud
FROM dim.Franquicia f
LEFT JOIN dim.GrupoEconomico ge ON ge.GrupoEconomicoId = f.GrupoEconomicoId
LEFT JOIN dim.Moneda m ON m.MonedaId = f.MonedaId
WHERE f.Codigo = 'CORRIENTES';
