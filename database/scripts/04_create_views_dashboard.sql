/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 04_create_views_dashboard.sql
  Descripcion: Vistas para el dashboard de indicadores
  Autor: Claude Code
  Fecha: 2026-07-16

  VISTAS:
  - Resumen diario de ventas
  - Ventas por franquicia
  - Ventas por grupo economico
  - Ventas por mozo
  - Ventas por producto
  - Ventas por tipo de plato
  - Ocupacion de mesas
  - Indicadores por cubierto
  - Estado de integracion
  - Ventas por hora y dia de semana

  INSTRUCCIONES:
  - Ejecutar despues de 03_create_constraints_indexes.sql
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- VISTA: Resumen Diario de Ventas
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasResumenDiario]'))
    DROP VIEW [fact].[vw_VentasResumenDiario]
GO

CREATE VIEW [fact].[vw_VentasResumenDiario]
AS
SELECT
    vt.FechaNegocio,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.GrupoEconomicoId,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    f.Pais,
    f.Ciudad,
    m.CodigoISO AS MonedaCodigo,

    -- Metricas de venta
    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
    SUM(vt.ImporteBruto) AS VentaBruta,
    SUM(vt.ImporteDescuento) AS TotalDescuentos,
    SUM(vt.ImporteNeto) AS VentaNeta,
    SUM(vt.ImporteImpuesto) AS TotalImpuestos,
    SUM(vt.ImportePropina) AS TotalPropinas,

    -- Tickets con y sin descuento
    SUM(CASE WHEN vt.TieneDescuento = 1 THEN 1 ELSE 0 END) AS TicketsConDescuento,
    SUM(CASE WHEN vt.TieneDescuento = 0 THEN 1 ELSE 0 END) AS TicketsSinDescuento,
    SUM(CASE WHEN vt.TieneDescuento = 1 THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaConDescuento,
    SUM(CASE WHEN vt.TieneDescuento = 0 THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaSinDescuento,

    -- Tickets anulados
    SUM(CASE WHEN vt.EstaAnulado = 1 THEN 1 ELSE 0 END) AS TicketsAnulados,

    -- Promedios
    AVG(vt.ImporteNeto) AS TicketPromedio,
    CASE WHEN SUM(vt.CantidadCubiertos) > 0
         THEN SUM(vt.ImporteNeto) / NULLIF(SUM(vt.CantidadCubiertos), 0)
         ELSE NULL END AS VentaPorCubierto,

    -- Tiempo de consumo
    AVG(vt.TiempoConsumoMinutos) AS TiempoConsumoPromedioMinutos,

    -- Calidad de datos
    SUM(CASE WHEN vt.TieneDatosCubiertos = 1 THEN 1 ELSE 0 END) AS TicketsConDatosCubiertos,
    SUM(CASE WHEN vt.TieneDatosMozo = 1 THEN 1 ELSE 0 END) AS TicketsConDatosMozo,
    SUM(CASE WHEN vt.TieneDatosMesa = 1 THEN 1 ELSE 0 END) AS TicketsConDatosMesa

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
WHERE vt.EstaAnulado = 0  -- Excluir anulados del resumen principal
GROUP BY
    vt.FechaNegocio,
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Codigo, ge.Nombre,
    f.Pais, f.Ciudad, m.CodigoISO
GO

-- ============================================================================
-- VISTA: Ventas por Franquicia (Acumulado)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorFranquicia]'))
    DROP VIEW [fact].[vw_VentasPorFranquicia]
GO

CREATE VIEW [fact].[vw_VentasPorFranquicia]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.GrupoEconomicoId,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    f.Pais,
    f.Ciudad,
    m.CodigoISO AS MonedaCodigo,
    f.UltimaSincronizacion,
    f.EstadoIntegracion,

    -- YTD (Year to Date)
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaYTD,
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosYTD,
    COUNT(DISTINCT CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.VentaTicketId END) AS TicketsYTD,

    -- MTD (Month to Date)
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaMTD,
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosMTD,
    COUNT(DISTINCT CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.VentaTicketId END) AS TicketsMTD,

    -- Hoy
    SUM(CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaHoy,
    SUM(CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosHoy,
    COUNT(DISTINCT CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.VentaTicketId END) AS TicketsHoy,

    -- Total historico
    SUM(vt.ImporteNeto) AS VentaNetaTotal,
    SUM(vt.CantidadCubiertos) AS CubiertosTotal,
    COUNT(DISTINCT vt.VentaTicketId) AS TicketsTotal,

    -- Fecha de datos
    MIN(vt.FechaNegocio) AS PrimeraVenta,
    MAX(vt.FechaNegocio) AS UltimaVenta

FROM [dim].[Franquicia] f
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
LEFT JOIN [fact].[VentaTicket] vt ON f.FranquiciaId = vt.FranquiciaId AND vt.EstaAnulado = 0
WHERE f.Activo = 1
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Codigo, ge.Nombre,
    f.Pais, f.Ciudad, m.CodigoISO,
    f.UltimaSincronizacion, f.EstadoIntegracion
GO

-- ============================================================================
-- VISTA: Ventas por Grupo Economico
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorGrupoEconomico]'))
    DROP VIEW [fact].[vw_VentasPorGrupoEconomico]
GO

CREATE VIEW [fact].[vw_VentasPorGrupoEconomico]
AS
SELECT
    ge.GrupoEconomicoId,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    ge.Pais,
    COUNT(DISTINCT f.FranquiciaId) AS CantidadFranquicias,

    -- YTD
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaYTD,
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosYTD,

    -- MTD
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.ImporteNeto ELSE 0 END) AS VentaNetaMTD,
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosMTD,

    -- Total
    SUM(vt.ImporteNeto) AS VentaNetaTotal,
    SUM(vt.CantidadCubiertos) AS CubiertosTotal,
    COUNT(DISTINCT vt.VentaTicketId) AS TicketsTotal

FROM [dim].[GrupoEconomico] ge
LEFT JOIN [dim].[Franquicia] f ON ge.GrupoEconomicoId = f.GrupoEconomicoId AND f.Activo = 1
LEFT JOIN [fact].[VentaTicket] vt ON f.FranquiciaId = vt.FranquiciaId AND vt.EstaAnulado = 0
WHERE ge.Activo = 1
GROUP BY
    ge.GrupoEconomicoId, ge.Codigo, ge.Nombre, ge.Pais
GO

-- ============================================================================
-- VISTA: Ventas por Mozo
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorMozo]'))
    DROP VIEW [fact].[vw_VentasPorMozo]
GO

CREATE VIEW [fact].[vw_VentasPorMozo]
AS
SELECT
    mz.MozoId,
    mz.Nombre AS MozoNombre,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    vt.FechaNegocio,

    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
    SUM(vt.ImporteNeto) AS VentaNeta,
    SUM(vt.ImportePropina) AS TotalPropinas,
    AVG(vt.ImporteNeto) AS TicketPromedio,
    AVG(vt.TiempoConsumoMinutos) AS TiempoConsumoPromedio

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Mozo] mz ON vt.MozoId = mz.MozoId
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
WHERE vt.EstaAnulado = 0
  AND vt.MozoId IS NOT NULL
GROUP BY
    mz.MozoId, mz.Nombre,
    f.FranquiciaId, f.Codigo, f.Nombre,
    vt.FechaNegocio
GO

-- ============================================================================
-- VISTA: Ventas por Producto
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorProducto]'))
    DROP VIEW [fact].[vw_VentasPorProducto]
GO

CREATE VIEW [fact].[vw_VentasPorProducto]
AS
SELECT
    vtd.CodigoProducto,
    vtd.NombreProducto,
    vtd.CategoriaProducto,
    vtd.FamiliaProducto,
    vtd.SubFamiliaProducto,
    tp.Codigo AS TipoPlatoCodigo,
    tp.Nombre AS TipoPlatoNombre,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    vt.FechaNegocio,

    SUM(vtd.Cantidad) AS CantidadVendida,
    SUM(vtd.ImporteBruto) AS VentaBruta,
    SUM(vtd.ImporteDescuento) AS TotalDescuentos,
    SUM(vtd.ImporteNeto) AS VentaNeta,
    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets

FROM [fact].[VentaTicketDetalle] vtd
INNER JOIN [fact].[VentaTicket] vt ON vtd.VentaTicketId = vt.VentaTicketId
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[TipoPlato] tp ON vtd.TipoPlatoId = tp.TipoPlatoId
WHERE vtd.EstaAnulado = 0
GROUP BY
    vtd.CodigoProducto, vtd.NombreProducto,
    vtd.CategoriaProducto, vtd.FamiliaProducto, vtd.SubFamiliaProducto,
    tp.Codigo, tp.Nombre,
    f.FranquiciaId, f.Codigo, f.Nombre,
    vt.FechaNegocio
GO

-- ============================================================================
-- VISTA: Ventas por Tipo de Plato
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorTipoPlato]'))
    DROP VIEW [fact].[vw_VentasPorTipoPlato]
GO

CREATE VIEW [fact].[vw_VentasPorTipoPlato]
AS
SELECT
    COALESCE(tp.TipoPlatoId, 0) AS TipoPlatoId,
    COALESCE(tp.Codigo, vtd.CategoriaProducto, 'SIN_CATEGORIA') AS TipoPlatoCodigo,
    COALESCE(tp.Nombre, vtd.CategoriaProducto, 'Sin Categoria') AS TipoPlatoNombre,
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.GrupoEconomicoId,
    ge.Nombre AS GrupoEconomicoNombre,
    vt.FechaNegocio,

    SUM(vtd.Cantidad) AS CantidadVendida,
    SUM(vtd.ImporteNeto) AS VentaNeta,
    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,

    -- Porcentaje sobre el total del dia/franquicia (se calcula en el reporte)
    SUM(vtd.ImporteNeto) AS ImporteParaPorcentaje

FROM [fact].[VentaTicketDetalle] vtd
INNER JOIN [fact].[VentaTicket] vt ON vtd.VentaTicketId = vt.VentaTicketId
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[TipoPlato] tp ON vtd.TipoPlatoId = tp.TipoPlatoId
WHERE vtd.EstaAnulado = 0
GROUP BY
    tp.TipoPlatoId, tp.Codigo, tp.Nombre, vtd.CategoriaProducto,
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Nombre,
    vt.FechaNegocio
GO

-- ============================================================================
-- VISTA: Ocupacion de Mesas
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_OcupacionMesas]'))
    DROP VIEW [fact].[vw_OcupacionMesas]
GO

CREATE VIEW [fact].[vw_OcupacionMesas]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    f.CantidadMesas AS MesasTotales,
    vt.FechaNegocio,

    -- Mesas utilizadas en el dia
    COUNT(DISTINCT vt.NumeroMesa) AS MesasUtilizadas,

    -- Rotacion: cantidad de tickets por mesa
    CASE WHEN COUNT(DISTINCT vt.NumeroMesa) > 0
         THEN CAST(COUNT(DISTINCT vt.VentaTicketId) AS DECIMAL(10,2)) / COUNT(DISTINCT vt.NumeroMesa)
         ELSE 0 END AS RotacionMesas,

    -- Ocupacion porcentual (requiere CantidadMesas configurada)
    CASE WHEN f.CantidadMesas > 0
         THEN CAST(COUNT(DISTINCT vt.NumeroMesa) AS DECIMAL(10,2)) / f.CantidadMesas * 100
         ELSE NULL END AS PorcentajeOcupacion,

    -- Tiempo promedio de ocupacion
    AVG(vt.TiempoConsumoMinutos) AS TiempoPromedioMinutos,

    -- Tickets totales
    COUNT(DISTINCT vt.VentaTicketId) AS TotalTickets,

    -- Cubiertos totales
    SUM(vt.CantidadCubiertos) AS TotalCubiertos

FROM [dim].[Franquicia] f
LEFT JOIN [fact].[VentaTicket] vt ON f.FranquiciaId = vt.FranquiciaId
    AND vt.EstaAnulado = 0
    AND vt.TieneDatosMesa = 1
WHERE f.Activo = 1
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre, f.CantidadMesas,
    vt.FechaNegocio
GO

-- ============================================================================
-- VISTA: Indicadores por Cubierto
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_IndicadoresPorCubierto]'))
    DROP VIEW [fact].[vw_IndicadoresPorCubierto]
GO

CREATE VIEW [fact].[vw_IndicadoresPorCubierto]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.GrupoEconomicoId,
    ge.Nombre AS GrupoEconomicoNombre,
    vt.FechaNegocio,
    m.CodigoISO AS MonedaCodigo,

    -- Solo tickets con datos de cubiertos
    COUNT(DISTINCT vt.VentaTicketId) AS TicketsConCubiertos,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
    SUM(vt.ImporteNeto) AS VentaNeta,

    -- Venta por cubierto
    CASE WHEN SUM(vt.CantidadCubiertos) > 0
         THEN SUM(vt.ImporteNeto) / SUM(vt.CantidadCubiertos)
         ELSE NULL END AS VentaPorCubierto,

    -- Cubiertos promedio por ticket
    CASE WHEN COUNT(DISTINCT vt.VentaTicketId) > 0
         THEN CAST(SUM(vt.CantidadCubiertos) AS DECIMAL(10,2)) / COUNT(DISTINCT vt.VentaTicketId)
         ELSE NULL END AS CubiertosPromedioPorTicket

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
WHERE vt.EstaAnulado = 0
  AND vt.TieneDatosCubiertos = 1
  AND vt.CantidadCubiertos > 0
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Nombre,
    vt.FechaNegocio,
    m.CodigoISO
GO

-- ============================================================================
-- VISTA: Estado de Integracion de Franquicias
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_EstadoIntegracionFranquicias]'))
    DROP VIEW [fact].[vw_EstadoIntegracionFranquicias]
GO

CREATE VIEW [fact].[vw_EstadoIntegracionFranquicias]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    f.Pais,
    f.Ciudad,
    f.EstadoIntegracion,
    f.SistemaOrigen,
    f.VersionSistema,
    f.UltimaSincronizacion,

    -- Dias sin sincronizar
    DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) AS DiasSinSincronizar,

    -- Alerta: mas de 1 dia sin sincro
    CASE WHEN DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) > 1 THEN 1 ELSE 0 END AS AlertaSincronizacion,

    -- Ultimo batch
    ub.BatchId AS UltimoBatchId,
    ub.FechaNegocio AS UltimaFechaNegocio,
    ub.Estado AS UltimoBatchEstado,
    ub.FechaRecepcion AS UltimoBatchFechaRecepcion,

    -- Batches del ultimo mes
    (SELECT COUNT(*) FROM [stg].[IngestionBatch] ib
     WHERE ib.FranquiciaId = f.FranquiciaId
       AND ib.FechaRecepcion >= DATEADD(MONTH, -1, GETDATE())) AS BatchesUltimoMes,

    -- Errores del ultimo mes
    (SELECT COUNT(*) FROM [stg].[IngestionBatch] ib
     WHERE ib.FranquiciaId = f.FranquiciaId
       AND ib.FechaRecepcion >= DATEADD(MONTH, -1, GETDATE())
       AND ib.Estado IN ('REJECTED', 'FAILED')) AS BatchesConErrorUltimoMes

FROM [dim].[Franquicia] f
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
OUTER APPLY (
    SELECT TOP 1 *
    FROM [stg].[IngestionBatch] ib
    WHERE ib.FranquiciaId = f.FranquiciaId
    ORDER BY ib.FechaRecepcion DESC
) ub
WHERE f.Activo = 1
GO

-- ============================================================================
-- VISTA: Ventas por Hora y Dia de Semana
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorHoraDiaSemana]'))
    DROP VIEW [fact].[vw_VentasPorHoraDiaSemana]
GO

CREATE VIEW [fact].[vw_VentasPorHoraDiaSemana]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    DATEPART(WEEKDAY, vt.FechaNegocio) AS DiaSemanaNumero,
    DATENAME(WEEKDAY, vt.FechaNegocio) AS DiaSemana,
    DATEPART(HOUR, vt.FechaApertura) AS HoraApertura,
    vt.PeriodoComida,

    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
    SUM(vt.ImporteNeto) AS VentaNeta,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
    AVG(vt.ImporteNeto) AS TicketPromedio

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
WHERE vt.EstaAnulado = 0
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre,
    DATEPART(WEEKDAY, vt.FechaNegocio),
    DATENAME(WEEKDAY, vt.FechaNegocio),
    DATEPART(HOUR, vt.FechaApertura),
    vt.PeriodoComida
GO

-- ============================================================================
-- VISTA: Home Dashboard - Matriz Principal
-- Vista consolidada para el home del dashboard
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_HomeDashboard]'))
    DROP VIEW [fact].[vw_HomeDashboard]
GO

CREATE VIEW [fact].[vw_HomeDashboard]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    ge.Codigo AS GrupoEconomicoCodigo,
    ge.Nombre AS GrupoEconomicoNombre,
    f.Pais,
    f.Ciudad,
    m.CodigoISO AS MonedaCodigo,
    f.UltimaSincronizacion,
    f.EstadoIntegracion,

    -- Alerta sincronizacion
    DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) AS DiasSinSincronizar,
    CASE WHEN DATEDIFF(DAY, f.UltimaSincronizacion, GETDATE()) > 1 THEN 1 ELSE 0 END AS AlertaSincronizacion,

    -- Venta YTD
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.ImporteNeto ELSE 0 END) AS VentaYTD,

    -- Venta Mes
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.ImporteNeto ELSE 0 END) AS VentaMes,

    -- Venta Dia (hoy)
    SUM(CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.ImporteNeto ELSE 0 END) AS VentaDia,

    -- Cubiertos YTD
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosYTD,

    -- Cubiertos Mes
    SUM(CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
             THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosMes,

    -- Cubiertos Dia
    SUM(CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.CantidadCubiertos ELSE 0 END) AS CubiertosDia,

    -- Tickets
    COUNT(DISTINCT CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) THEN vt.VentaTicketId END) AS TicketsYTD,
    COUNT(DISTINCT CASE WHEN YEAR(vt.FechaNegocio) = YEAR(GETDATE()) AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
                        THEN vt.VentaTicketId END) AS TicketsMes,
    COUNT(DISTINCT CASE WHEN vt.FechaNegocio = CAST(GETDATE() AS DATE) THEN vt.VentaTicketId END) AS TicketsDia

FROM [dim].[Franquicia] f
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
LEFT JOIN [fact].[VentaTicket] vt ON f.FranquiciaId = vt.FranquiciaId AND vt.EstaAnulado = 0
WHERE f.Activo = 1
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.Codigo, ge.Nombre,
    f.Pais, f.Ciudad,
    m.CodigoISO,
    f.UltimaSincronizacion, f.EstadoIntegracion
GO

PRINT '============================================'
PRINT 'Todas las vistas han sido creadas.'
PRINT '============================================'
GO
