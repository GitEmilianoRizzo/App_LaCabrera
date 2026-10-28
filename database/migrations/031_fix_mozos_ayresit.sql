-- =====================================================
-- Fix: tickets de Ayres IT (Palermo Norte y Sur) asignados a mozos de otra franquicia
--
-- El conector de Ayres guardaba el código de vendedor de Ayres directamente en fact.VentaTicket.MozoId.
-- Cuando ese número coincidía con un mozo existente, el ticket quedaba asignado a un mozo de OTRA
-- franquicia (p. ej. de Miami); cuando no coincidía, la FK rechazaba la venta.
--
-- Como el MozoId guardado es el código de vendedor de Ayres, se crea (si falta) el mozo de la franquicia
-- con ese código ("Vendedor {código}") y se reasigna el ticket. El conector ya resuelve el mozo así.
--
-- Re-ejecutable: solo toca tickets AYRESIT cuyo mozo es de otra franquicia o no existe.
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @Afectados TABLE (FranquiciaId INT, CodigoVendedor NVARCHAR(100));

INSERT INTO @Afectados (FranquiciaId, CodigoVendedor)
SELECT DISTINCT v.FranquiciaId, CAST(v.MozoId AS NVARCHAR(100))
FROM fact.VentaTicket v
LEFT JOIN dim.Mozo m ON m.MozoId = v.MozoId
WHERE v.FuenteSistema = 'AYRESIT' AND v.MozoId IS NOT NULL
  AND (m.MozoId IS NULL OR m.FranquiciaId <> v.FranquiciaId);

INSERT INTO dim.Mozo (FranquiciaId, ExternalId, Nombre, FuenteSistema, EsActivo)
SELECT a.FranquiciaId, a.CodigoVendedor, 'Vendedor ' + a.CodigoVendedor, 'AYRESIT', 1
FROM @Afectados a
WHERE NOT EXISTS (SELECT 1 FROM dim.Mozo x WHERE x.FranquiciaId = a.FranquiciaId AND x.ExternalId = a.CodigoVendedor);

PRINT 'Mozos de Ayres creados: ' + CAST(@@ROWCOUNT AS VARCHAR);

UPDATE v
SET MozoId = nuevo.MozoId,
    FechaModificacion = SYSDATETIME(),
    UsuarioModificacion = 'FIX_MOZOS_AYRESIT'
FROM fact.VentaTicket v
LEFT JOIN dim.Mozo actual ON actual.MozoId = v.MozoId
JOIN dim.Mozo nuevo ON nuevo.FranquiciaId = v.FranquiciaId AND nuevo.ExternalId = CAST(v.MozoId AS NVARCHAR(100))
WHERE v.FuenteSistema = 'AYRESIT' AND v.MozoId IS NOT NULL
  AND (actual.MozoId IS NULL OR actual.FranquiciaId <> v.FranquiciaId);

PRINT 'Tickets de Ayres reasignados: ' + CAST(@@ROWCOUNT AS VARCHAR);

COMMIT TRANSACTION;

SELECT v.FranquiciaId, COUNT(*) AS Tickets,
       SUM(CASE WHEN m.FranquiciaId = v.FranquiciaId THEN 1 ELSE 0 END) AS ConMozoDeLaFranquicia,
       SUM(CASE WHEN m.FranquiciaId <> v.FranquiciaId THEN 1 ELSE 0 END) AS ConMozoDeOtraFranquicia
FROM fact.VentaTicket v
LEFT JOIN dim.Mozo m ON m.MozoId = v.MozoId
WHERE v.FuenteSistema = 'AYRESIT'
GROUP BY v.FranquiciaId;
