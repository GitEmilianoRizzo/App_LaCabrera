-- =====================================================
-- Alta de la franquicia Porto Alegre (Brasil) con conector GCOM (T-170)
--
-- - Moneda BRL (el tipo de cambio lo trae DolarApi, que ya soporta BRL).
-- - Franquicia PORTO_ALEGRE, datos desde el 01/08/2026.
-- - Nodo NODO_PORTO_ALEGRE (TipoConector GCOM) en PENDING_CONFIG y SIN token:
--   el token se carga a mano en ConfiguracionJson ($.connection.token), nunca en el repo.
--   El token tiene '#': no pasarlo por .env ni por la línea de comandos.
-- - Mapeo de los medios de pago vistos en GCOM. Los que aparezcan nuevos quedan en dim.ValorNoMapeado.
--
-- Cupo de GCOM: 5 requests por día para la tienda 8346, compartido por todos los ambientes que usen
-- el mismo token. sync.max_requests_dia es lo que puede usar ESTE ambiente.
--
-- Re-ejecutable: no duplica ni pisa lo que ya exista.
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dim.Moneda WHERE CodigoISO = 'BRL')
    INSERT INTO dim.Moneda (CodigoISO, Nombre, Simbolo, Activo)
    VALUES ('BRL', 'Real Brasileño', 'R$', 1);

DECLARE @MonedaId INT = (SELECT MonedaId FROM dim.Moneda WHERE CodigoISO = 'BRL');

IF NOT EXISTS (SELECT 1 FROM dim.Franquicia WHERE Codigo = 'PORTO_ALEGRE')
    INSERT INTO dim.Franquicia (Codigo, Nombre, Pais, Ciudad, ZonaHoraria, MonedaId, SistemaOrigen, EstadoIntegracion, FechaInicioDatos, Activo)
    VALUES ('PORTO_ALEGRE', 'Porto Alegre', 'Brasil', 'Porto Alegre', 'America/Sao_Paulo', @MonedaId, 'GCOM', 'PENDING', '2026-08-01', 1);

DECLARE @FranquiciaId INT = (SELECT FranquiciaId FROM dim.Franquicia WHERE Codigo = 'PORTO_ALEGRE');

IF NOT EXISTS (SELECT 1 FROM dim.NodoConexion WHERE Codigo = 'NODO_PORTO_ALEGRE')
    INSERT INTO dim.NodoConexion (
        Codigo, Nombre, FranquiciaId, TipoConector, Modo, Estado, Timezone, Moneda,
        ConvencionImportes, PoliticaDevoluciones, BusinessDateOffsetDays, CatchUpDays, ConfiguracionJson, Activo)
    VALUES (
        'NODO_PORTO_ALEGRE', 'Conector Porto Alegre - GCOM', @FranquiciaId, 'GCOM', 'PULL', 'PENDING_CONFIG',
        'America/Sao_Paulo', 'BRL', 'VAT_INCLUDED', 'NEGATIVE_LINES', -1, 0,
        N'{
  "connection": {
    "base_url": "https://servicos.gcom.com.br/wsrest/api",
    "token": "",
    "lista_lojas": "8346",
    "id_integracao": 50,
    "timeout_seconds": 120
  },
  "sync": {
    "fecha_inicio": "2026-08-01",
    "max_requests_dia": 5,
    "zona_horaria": "America/Sao_Paulo",
    "auto": false
  }
}', 1);

DECLARE @NodoId INT = (SELECT NodoConexionId FROM dim.NodoConexion WHERE Codigo = 'NODO_PORTO_ALEGRE');

INSERT INTO dim.MapeoMedioPago (NodoConexionId, CodigoOrigen, NombreOrigen, MedioPagoDestino, Verificado, Activo)
SELECT @NodoId, m.CodigoOrigen, m.CodigoOrigen, m.Destino, 1, 1
FROM (VALUES
    ('CARTAO DE CREDITO', 'CREDIT_CARD'),
    ('CARTAO DEBITO', 'DEBIT_CARD'),
    ('DINHEIRO', 'CASH'),
    ('OUTRAS FORMAS PAGTO EMPRESA', 'OTHER')
) AS m (CodigoOrigen, Destino)
WHERE NOT EXISTS (
    SELECT 1 FROM dim.MapeoMedioPago x WHERE x.NodoConexionId = @NodoId AND x.CodigoOrigen = m.CodigoOrigen);

COMMIT TRANSACTION;

SELECT f.FranquiciaId, f.Codigo, n.NodoConexionId, n.Codigo AS Nodo, n.Estado, mo.CodigoISO
FROM dim.Franquicia f
JOIN dim.NodoConexion n ON n.FranquiciaId = f.FranquiciaId
JOIN dim.Moneda mo ON mo.MonedaId = f.MonedaId
WHERE f.Codigo = 'PORTO_ALEGRE';
