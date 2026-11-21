-- =====================================================
-- Conector NEO (NeoKernel API) para La Cabrera Málaga (T-381)
--
-- - La franquicia MALAGA ya existe (EUR, Europe/Madrid): se marca SistemaOrigen = 'NEO'.
-- - Nodo NODO_MALAGA (TipoConector NEO) en PENDING_CONFIG y con sync.auto = false.
--   La API key NO va en la base: el conector la lee de la variable de entorno NEO_MALAGA_API_KEY
--   (connection.api_key_env). En producción va en el .env del servidor.
-- - Solo GET a NEO. Nunca los POST workingdaydone / workingdayticketdone.
-- - Medios de pago vistos en NEO (jornada 20261031): TCR = tarjeta, CG = efectivo (el vuelto viene negativo).
--   Los nuevos que aparezcan quedan en dim.ValorNoMapeado.
--
-- Activación (una vez cargada la key): ver docs/integration/neo-malaga.md
--
-- Re-ejecutable: no duplica ni pisa lo que ya exista.
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @FranquiciaId INT = (SELECT FranquiciaId FROM dim.Franquicia WHERE Codigo = 'MALAGA');

IF @FranquiciaId IS NULL
BEGIN
    DECLARE @MonedaId INT = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = 'EUR');
    INSERT INTO dim.Franquicia (Codigo, Nombre, Pais, Ciudad, ZonaHoraria, MonedaId, SistemaOrigen, EstadoIntegracion, FechaInicioDatos, Activo)
    VALUES ('MALAGA', 'Malaga', 'Espana', 'Malaga', 'Europe/Madrid', @MonedaId, 'NEO', 'PENDING', '2026-09-01', 1);
    SET @FranquiciaId = SCOPE_IDENTITY();
END
ELSE
    UPDATE dim.Franquicia SET SistemaOrigen = 'NEO'
    WHERE FranquiciaId = @FranquiciaId AND (SistemaOrigen IS NULL OR SistemaOrigen <> 'NEO');

IF NOT EXISTS (SELECT 1 FROM dim.NodoConexion WHERE Codigo = 'NODO_MALAGA')
    INSERT INTO dim.NodoConexion (
        Codigo, Nombre, FranquiciaId, TipoConector, Modo, Estado, Timezone, Moneda,
        ConvencionImportes, PoliticaDevoluciones, BusinessDateOffsetDays, CatchUpDays, ConfiguracionJson, Activo)
    VALUES (
        'NODO_MALAGA', 'Conector Málaga - NEO', @FranquiciaId, 'NEO', 'PULL', 'PENDING_CONFIG',
        'Europe/Madrid', 'EUR', 'VAT_INCLUDED', 'NEGATIVE_LINES', 0, 7,
        N'{
  "connection": {
    "base_url": "http://restaurantelacabrera.webreports.cloud:3001",
    "shop": "0001",
    "api_key_env": "NEO_MALAGA_API_KEY",
    "timeout_seconds": 120,
    "pausa_ms": 1000,
    "reintentos": 3
  },
  "sync": {
    "fecha_inicio": "2026-09-01",
    "dias_releer": 7,
    "max_dias_por_corrida": 62,
    "zona_horaria": "Europe/Madrid",
    "auto": false
  }
}', 1);

DECLARE @NodoId INT = (SELECT NodoConexionId FROM dim.NodoConexion WHERE Codigo = 'NODO_MALAGA');

INSERT INTO dim.MapeoMedioPago (NodoConexionId, CodigoOrigen, NombreOrigen, MedioPagoDestino, Verificado, Activo)
SELECT @NodoId, m.CodigoOrigen, m.Nombre, m.Destino, 1, 1
FROM (VALUES
    ('TCR', 'Tarjeta de crédito', 'CREDIT_CARD'),
    ('CG', 'Efectivo', 'CASH')
) AS m (CodigoOrigen, Nombre, Destino)
WHERE NOT EXISTS (
    SELECT 1 FROM dim.MapeoMedioPago x WHERE x.NodoConexionId = @NodoId AND x.CodigoOrigen = m.CodigoOrigen);

COMMIT TRANSACTION;

SELECT f.FranquiciaId, f.Codigo, f.SistemaOrigen, n.NodoConexionId, n.Codigo AS Nodo, n.TipoConector, n.Estado
FROM dim.Franquicia f
JOIN dim.NodoConexion n ON n.FranquiciaId = f.FranquiciaId
WHERE f.Codigo = 'MALAGA';
