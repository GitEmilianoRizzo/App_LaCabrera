-- =====================================================
-- Porto Alegre (GCOM): cubiertos a partir del ítem "EXPERIENCIA LA CABRERA"
--
-- GCOM no informa personas por venta. En Porto Alegre se carga un ítem "EXPERIENCIA LA CABRERA" (R$ 0,01)
-- por comensal. Regla definida por el cliente el 04/11/2026:
--   cubiertos = cantidad de ítems "EXPERIENCIA LA CABRERA" no anulados de la venta; si no tiene ninguno, 1.
-- El conector GCOM aplica la misma regla en las cargas nuevas (GcomImportes.CalcularCubiertos).
--
-- Re-ejecutable: recalcula los tickets GCOM cuyo valor no coincide con la regla.
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

;WITH calculo AS (
    SELECT v.VentaTicketId,
           CASE WHEN e.Experiencias >= 1 THEN CAST(ROUND(e.Experiencias, 0) AS INT) ELSE 1 END AS Cubiertos
    FROM fact.VentaTicket v
    JOIN dim.Franquicia f ON f.FranquiciaId = v.FranquiciaId
    OUTER APPLY (
        SELECT SUM(d.Cantidad) AS Experiencias
        FROM fact.VentaTicketDetalle d
        WHERE d.VentaTicketId = v.VentaTicketId
          AND d.NombreProducto LIKE 'EXPERIENCIA LA CABRERA%'
          AND (d.EstaAnulado = 0 OR v.EstaAnulado = 1)
    ) e
    WHERE f.Codigo = 'PORTO_ALEGRE' AND v.FuenteSistema = 'GCOM'
)
UPDATE v
SET CantidadCubiertos = c.Cubiertos,
    TieneDatosCubiertos = 1,
    FechaModificacion = SYSDATETIME(),
    UsuarioModificacion = 'MIGRACION_034'
FROM fact.VentaTicket v
JOIN calculo c ON c.VentaTicketId = v.VentaTicketId
WHERE ISNULL(v.CantidadCubiertos, 0) <> c.Cubiertos OR ISNULL(v.TieneDatosCubiertos, 0) = 0;

PRINT 'Tickets de Porto Alegre con cubiertos: ' + CAST(@@ROWCOUNT AS VARCHAR);

COMMIT TRANSACTION;

-- Control por mes (tickets válidos)
SELECT FORMAT(v.FechaNegocio, 'yyyy-MM') AS Mes, COUNT(*) AS Tickets, SUM(v.CantidadCubiertos) AS Cubiertos
FROM fact.VentaTicket v
JOIN dim.Franquicia f ON f.FranquiciaId = v.FranquiciaId
WHERE f.Codigo = 'PORTO_ALEGRE' AND v.EstaAnulado = 0
GROUP BY FORMAT(v.FechaNegocio, 'yyyy-MM')
ORDER BY Mes;
