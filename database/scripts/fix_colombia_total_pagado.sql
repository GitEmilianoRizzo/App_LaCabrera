/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: fix_colombia_total_pagado.sql
  Descripcion: Corregir ImporteTotalPagado para tickets de Colombia
  Fecha: 2026-09-27

  PROBLEMA:
  Los tickets de Colombia se importaron con ImporteTotalPagado = 0 porque
  el JSON no incluia el campo total_paid_amount.

  SOLUCION:
  Recalcular ImporteTotalPagado = ImporteNeto + ImportePropina + ImporteServicio
  (ImporteNeto ya incluye impuestos para Colombia donde amounts_include_tax = true)
================================================================================
*/

USE [LaCabreraDB]
GO

-- Ver datos antes de actualizar
SELECT
    f.Nombre AS Franquicia,
    f.Pais,
    COUNT(*) AS TotalTickets,
    SUM(CASE WHEN vt.ImporteTotalPagado = 0 OR vt.ImporteTotalPagado IS NULL THEN 1 ELSE 0 END) AS TicketsSinTotal,
    SUM(vt.ImporteNeto) AS TotalNeto,
    SUM(vt.ImporteTotalPagado) AS TotalPagadoActual
FROM fact.VentaTicket vt
INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
WHERE f.Pais = 'Colombia'
GROUP BY f.Nombre, f.Pais

-- Actualizar tickets de Colombia donde ImporteTotalPagado es 0
UPDATE vt
SET ImporteTotalPagado = vt.ImporteNeto + ISNULL(vt.ImportePropina, 0) + ISNULL(vt.ImporteServicio, 0)
FROM fact.VentaTicket vt
INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
WHERE f.Pais = 'Colombia'
  AND (vt.ImporteTotalPagado = 0 OR vt.ImporteTotalPagado IS NULL)

PRINT 'Tickets de Colombia actualizados: ' + CAST(@@ROWCOUNT AS VARCHAR(10))

-- Verificar despues de actualizar
SELECT
    f.Nombre AS Franquicia,
    f.Pais,
    COUNT(*) AS TotalTickets,
    SUM(CASE WHEN vt.ImporteTotalPagado = 0 OR vt.ImporteTotalPagado IS NULL THEN 1 ELSE 0 END) AS TicketsSinTotal,
    SUM(vt.ImporteNeto) AS TotalNeto,
    SUM(vt.ImporteTotalPagado) AS TotalPagadoCorregido
FROM fact.VentaTicket vt
INNER JOIN dim.Franquicia f ON vt.FranquiciaId = f.FranquiciaId
WHERE f.Pais = 'Colombia'
GROUP BY f.Nombre, f.Pais

GO

PRINT '============================================'
PRINT 'Script completado: ImporteTotalPagado corregido para Colombia'
PRINT '============================================'
GO
