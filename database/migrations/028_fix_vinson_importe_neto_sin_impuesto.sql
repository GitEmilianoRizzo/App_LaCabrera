-- =====================================================
-- Fix T-165: ImporteNetoSinImpuesto con IVA descontado dos veces (Vinson)
--
-- El backfill 15_backfill_impuesto_turno.sql (UsuarioModificacion = 'BACKFILL_V12')
-- calculó ImporteNetoSinImpuesto = ImporteNeto - ImporteImpuesto suponiendo que
-- ImporteNeto incluía IVA. En Vinson ImporteNeto ya viene SIN IVA (montoNeto),
-- así que el IVA quedó restado dos veces.
--
-- Se reconoce que el neto ya es sin impuesto cuando Bruto = Neto + Impuesto.
-- Solo toca filas de Vinson. Re-ejecutable: una fila corregida no vuelve a cambiar.
--
-- Nota: esto corrige el campo de IVA. Los importes de tickets y líneas de Vinson
-- (precio unitario sin multiplicar por cantidad) se corrigen con el reproceso:
-- POST /api/v1/conexiones/{id}/reprocesar-rango
-- =====================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

UPDATE vt
SET ImporteNetoSinImpuesto = vt.ImporteNeto,
    ImportesIncluyenImpuesto = 0,
    FechaModificacion = SYSDATETIME(),
    UsuarioModificacion = 'FIX_T165'
FROM fact.VentaTicket vt
WHERE vt.FuenteSistema = 'VINSON'
  AND vt.CalidadImpuesto = 'POS'
  AND vt.ImporteNetoSinImpuesto IS NOT NULL
  AND vt.ImporteNetoSinImpuesto <> vt.ImporteNeto
  AND ABS(vt.ImporteBruto - vt.ImporteNeto - vt.ImporteImpuesto) <= 0.05;

PRINT 'Tickets Vinson corregidos: ' + CAST(@@ROWCOUNT AS VARCHAR);

UPDATE d
SET ImporteNetoSinImpuesto = d.ImporteNeto,
    ImportesIncluyenImpuesto = 0,
    FechaModificacion = SYSDATETIME(),
    UsuarioModificacion = 'FIX_T165'
FROM fact.VentaTicketDetalle d
INNER JOIN fact.VentaTicket vt ON vt.VentaTicketId = d.VentaTicketId
WHERE vt.FuenteSistema = 'VINSON'
  AND d.CalidadImpuesto = 'POS'
  AND d.ImporteNetoSinImpuesto IS NOT NULL
  AND d.ImporteNetoSinImpuesto <> d.ImporteNeto
  AND ABS(d.ImporteBruto - d.ImporteNeto - d.ImporteImpuesto) <= 0.05;

PRINT 'Lineas Vinson corregidas: ' + CAST(@@ROWCOUNT AS VARCHAR);

COMMIT TRANSACTION;
GO
