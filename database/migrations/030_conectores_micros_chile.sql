-- =====================================================
-- Conectores de las franquicias de Chile (Oracle MICROS por mail)
--
-- Los locales mandan el export standard.GeneralLedger por mail a sync_cabrera_chile@pillow.com.ar.
-- La sincronización diaria (ConectoresSyncJob, 10:00 ARG) lee la casilla una sola vez y reparte cada
-- archivo al local según el código de tienda del GLID:
--   CAB -> ALONSO, LC ISI -> ISIDORA, EGANIA -> EGANA, LCR -> EL_RODEO, PAQ -> AL_PASO_CHILE
--
-- - Nodos TipoConector MICROS_MAIL, Modo FILE, ACTIVE, con "micros": {codigo_tienda, auto}.
-- - Las franquicias pasan a ACTIVE (conectadas) si estaban PENDING y quedan con moneda CLP.
-- - La casilla, el dominio de remitentes y la ruta de la contraseña están en la configuración de la API
--   (MicrosMail); la contraseña vive en el servidor, nunca en el repo.
--
-- Re-ejecutable: no duplica ni pisa lo que ya exista.
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @Clp INT = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = 'CLP');

DECLARE @Locales TABLE (Franquicia NVARCHAR(50), Tienda NVARCHAR(20));
INSERT INTO @Locales VALUES
    ('ALONSO', 'CAB'),
    ('ISIDORA', 'LC ISI'),
    ('EGANA', 'EGANIA'),
    ('EL_RODEO', 'LCR'),
    ('AL_PASO_CHILE', 'PAQ');

INSERT INTO dim.NodoConexion (
    Codigo, Nombre, FranquiciaId, TipoConector, Modo, Estado, Timezone, Moneda,
    ConvencionImportes, PoliticaDevoluciones, ConfiguracionJson, Activo)
SELECT
    'NODO_' + f.Codigo + '_MICROS',
    'Conector ' + f.Nombre + ' - MICROS (mail)',
    f.FranquiciaId, 'MICROS_MAIL', 'FILE', 'ACTIVE', 'America/Santiago', 'CLP',
    'VAT_INCLUDED', 'NEGATIVE_LINES',
    N'{"micros":{"codigo_tienda":"' + l.Tienda + N'","auto":true}}',
    1
FROM @Locales l
JOIN dim.Franquicia f ON f.Codigo = l.Franquicia
WHERE NOT EXISTS (SELECT 1 FROM dim.NodoConexion n WHERE n.Codigo = 'NODO_' + f.Codigo + '_MICROS');

UPDATE f
SET MonedaId = ISNULL(f.MonedaId, @Clp),
    EstadoIntegracion = CASE WHEN f.EstadoIntegracion = 'PENDING' THEN 'ACTIVE' ELSE f.EstadoIntegracion END,
    FechaModificacion = SYSDATETIME(),
    UsuarioModificacion = 'MIGRACION_030'
FROM dim.Franquicia f
JOIN @Locales l ON l.Franquicia = f.Codigo;

COMMIT TRANSACTION;

SELECT f.Codigo, f.EstadoIntegracion, m.CodigoISO, n.NodoConexionId, n.Codigo AS Nodo, n.Estado,
       JSON_VALUE(n.ConfiguracionJson, '$.micros.codigo_tienda') AS Tienda
FROM dim.Franquicia f
JOIN @Locales l ON l.Franquicia = f.Codigo
LEFT JOIN dim.Moneda m ON m.MonedaId = f.MonedaId
LEFT JOIN dim.NodoConexion n ON n.FranquiciaId = f.FranquiciaId AND n.TipoConector = 'MICROS_MAIL';
