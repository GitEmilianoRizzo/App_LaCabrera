/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 16_create_views_dashboard_v12.sql
  Descripcion: Vistas de dashboard v1.2 con conversion USD y eje impuesto
  Autor: Claude Code
  Fecha: 2026-08-23
  Lote: MEJORAS_20260822 - Etapa 4

  NOVEDADES v1.2:
  - Todas las vistas exponen importes en moneda local Y USD
  - Se expone ImporteNetoSinImpuesto como KPI comparable
  - Se incluye CalidadImpuesto para auditoria
  - Se incluyen datos de tipo de cambio usados
  - Vistas de ranking por dia
  - Vista de venta por producto con peso relativo

  REGLAS DE CONVERSION:
  - ImporteUsd = ImporteLocal / tc.UnidadesPorUsd
  - Siempre usar la tasa del business_date del ticket
  - Si falta la tasa, devolver NULL (nunca cero)

  INSTRUCCIONES:
  - Ejecutar despues de 15_backfill_impuesto_turno.sql
  - Idempotente: puede re-ejecutarse sin error
================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- VISTA: Resumen Diario Consolidado (con USD y eje impuesto)
-- Vista principal para el dashboard home
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasConsolidadasUsd]'))
    DROP VIEW [fact].[vw_VentasConsolidadasUsd]
GO

CREATE VIEW [fact].[vw_VentasConsolidadasUsd]
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
    m.CodigoISO AS MonedaLocal,

    -- Metricas en moneda local
    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,
    SUM(vt.ImporteBruto) AS VentaBrutaLocal,
    SUM(vt.ImporteDescuento) AS TotalDescuentosLocal,
    SUM(vt.ImporteNeto) AS VentaNetaLocal,
    SUM(vt.ImporteNetoSinImpuesto) AS VentaNetaSinImpuestoLocal,
    SUM(vt.ImporteImpuesto) AS TotalImpuestosLocal,

    -- Metricas en USD (conversion por ticket usando tasa del business_date)
    SUM(CASE WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0
             THEN vt.ImporteBruto / tc.UnidadesPorUsd
             ELSE NULL END) AS VentaBrutaUsd,
    SUM(CASE WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0
             THEN vt.ImporteDescuento / tc.UnidadesPorUsd
             ELSE NULL END) AS TotalDescuentosUsd,
    SUM(CASE WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0
             THEN vt.ImporteNeto / tc.UnidadesPorUsd
             ELSE NULL END) AS VentaNetaUsd,
    SUM(CASE WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0 AND vt.ImporteNetoSinImpuesto IS NOT NULL
             THEN vt.ImporteNetoSinImpuesto / tc.UnidadesPorUsd
             ELSE NULL END) AS VentaNetaSinImpuestoUsd,

    -- Calidad de datos de impuesto (peor caso del conjunto)
    CASE
        WHEN SUM(CASE WHEN vt.CalidadImpuesto = 'DESCONOCIDO' THEN 1 ELSE 0 END) > 0 THEN 'DESCONOCIDO'
        WHEN SUM(CASE WHEN vt.CalidadImpuesto = 'DERIVADO' THEN 1 ELSE 0 END) > 0 THEN 'MIXTO'
        ELSE 'POS'
    END AS CalidadImpuestoAgregada,

    -- Tickets sin dato de impuesto
    SUM(CASE WHEN vt.ImporteNetoSinImpuesto IS NULL THEN 1 ELSE 0 END) AS TicketsSinDatoImpuesto,

    -- Info de tipo de cambio (para auditoria)
    AVG(tc.UnidadesPorUsd) AS TasaCambioPromedio,
    SUM(CASE WHEN tc.UnidadesPorUsd IS NULL THEN 1 ELSE 0 END) AS TicketsSinTasaCambio,
    MAX(CASE WHEN tc.EsArrastrada = 1 THEN 1 ELSE 0 END) AS TieneTasaArrastrada,

    -- Ticket promedio
    AVG(vt.ImporteNeto) AS TicketPromedioLocal,
    AVG(CASE WHEN tc.UnidadesPorUsd IS NOT NULL AND tc.UnidadesPorUsd > 0
             THEN vt.ImporteNeto / tc.UnidadesPorUsd
             ELSE NULL END) AS TicketPromedioUsd

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[GrupoEconomico] ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
LEFT JOIN [dim].[TipoCambio] tc
    ON m.CodigoISO = tc.CodigoMoneda
    AND vt.FechaNegocio = tc.Fecha
    AND tc.TipoTasa = 'OFICIAL'
WHERE vt.EstaAnulado = 0
GROUP BY
    vt.FechaNegocio,
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Codigo, ge.Nombre,
    f.Pais, f.Ciudad, m.CodigoISO
GO

PRINT 'Vista fact.vw_VentasConsolidadasUsd creada.'
GO

-- ============================================================================
-- VISTA: Venta por Producto con Peso Relativo (intra-franquicia)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorProductoConPeso]'))
    DROP VIEW [fact].[vw_VentasPorProductoConPeso]
GO

CREATE VIEW [fact].[vw_VentasPorProductoConPeso]
AS
WITH VentasPorProducto AS (
    SELECT
        vtd.FranquiciaId,
        vt.FechaNegocio,
        vtd.CodigoProducto,
        vtd.NombreProducto,
        vtd.CategoriaProducto,
        vtd.FamiliaProducto,
        SUM(vtd.Cantidad) AS CantidadVendida,
        SUM(vtd.ImporteNeto) AS VentaNetaLocal,
        SUM(vtd.ImporteNetoSinImpuesto) AS VentaSinImpuestoLocal,
        SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vtd.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaNetaUsd,
        COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets
    FROM [fact].[VentaTicketDetalle] vtd
    INNER JOIN [fact].[VentaTicket] vt ON vtd.VentaTicketId = vt.VentaTicketId
    LEFT JOIN [dim].[Moneda] m ON vt.MonedaId = m.MonedaId
    LEFT JOIN [dim].[TipoCambio] tc
        ON m.CodigoISO = tc.CodigoMoneda
        AND vt.FechaNegocio = tc.Fecha
        AND tc.TipoTasa = 'OFICIAL'
    WHERE vtd.EstaAnulado = 0 AND vt.EstaAnulado = 0
    GROUP BY vtd.FranquiciaId, vt.FechaNegocio, vtd.CodigoProducto, vtd.NombreProducto,
             vtd.CategoriaProducto, vtd.FamiliaProducto
),
TotalesPorFranquicia AS (
    SELECT
        FranquiciaId,
        FechaNegocio,
        SUM(VentaNetaLocal) AS TotalVentaFranquicia,
        SUM(CantidadVendida) AS TotalUnidadesFranquicia
    FROM VentasPorProducto
    GROUP BY FranquiciaId, FechaNegocio
),
TotalesPorCategoria AS (
    SELECT
        FranquiciaId,
        FechaNegocio,
        CategoriaProducto,
        SUM(VentaNetaLocal) AS TotalVentaCategoria,
        SUM(CantidadVendida) AS TotalUnidadesCategoria
    FROM VentasPorProducto
    GROUP BY FranquiciaId, FechaNegocio, CategoriaProducto
)
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    vp.FechaNegocio,
    vp.CodigoProducto,
    vp.NombreProducto,
    vp.CategoriaProducto,
    vp.FamiliaProducto,

    -- Volumenes
    vp.CantidadVendida,
    vp.CantidadTickets,
    vp.VentaNetaLocal,
    vp.VentaSinImpuestoLocal,
    vp.VentaNetaUsd,

    -- Peso relativo por importe
    CASE WHEN tf.TotalVentaFranquicia > 0
         THEN ROUND(vp.VentaNetaLocal * 100.0 / tf.TotalVentaFranquicia, 2)
         ELSE 0 END AS PctSobreTotalFranquicia,

    CASE WHEN tc.TotalVentaCategoria > 0
         THEN ROUND(vp.VentaNetaLocal * 100.0 / tc.TotalVentaCategoria, 2)
         ELSE 0 END AS PctSobreCategoria,

    CASE WHEN tf.TotalVentaFranquicia > 0
         THEN ROUND(tc.TotalVentaCategoria * 100.0 / tf.TotalVentaFranquicia, 2)
         ELSE 0 END AS PctCategoriaSobreTotal,

    -- Peso relativo por unidades
    CASE WHEN tf.TotalUnidadesFranquicia > 0
         THEN ROUND(vp.CantidadVendida * 100.0 / tf.TotalUnidadesFranquicia, 2)
         ELSE 0 END AS PctUnidadesSobreTotal,

    CASE WHEN tc.TotalUnidadesCategoria > 0
         THEN ROUND(vp.CantidadVendida * 100.0 / tc.TotalUnidadesCategoria, 2)
         ELSE 0 END AS PctUnidadesSobreCategoria,

    -- Totales de referencia
    tf.TotalVentaFranquicia,
    tf.TotalUnidadesFranquicia

FROM VentasPorProducto vp
INNER JOIN [dim].[Franquicia] f ON vp.FranquiciaId = f.FranquiciaId
INNER JOIN TotalesPorFranquicia tf
    ON vp.FranquiciaId = tf.FranquiciaId AND vp.FechaNegocio = tf.FechaNegocio
LEFT JOIN TotalesPorCategoria tc
    ON vp.FranquiciaId = tc.FranquiciaId
    AND vp.FechaNegocio = tc.FechaNegocio
    AND vp.CategoriaProducto = tc.CategoriaProducto
GO

PRINT 'Vista fact.vw_VentasPorProductoConPeso creada.'
GO

-- ============================================================================
-- VISTA: Ranking de Dias (mayor y menor venta)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorDiaRanking]'))
    DROP VIEW [fact].[vw_VentasPorDiaRanking]
GO

CREATE VIEW [fact].[vw_VentasPorDiaRanking]
AS
WITH VentasPorDia AS (
    SELECT
        f.FranquiciaId,
        f.Codigo AS FranquiciaCodigo,
        f.Nombre AS FranquiciaNombre,
        vt.FechaNegocio,
        DATEPART(WEEKDAY, vt.FechaNegocio) AS DiaSemanaNumero,
        DATENAME(WEEKDAY, vt.FechaNegocio) AS DiaSemana,
        m.CodigoISO AS MonedaLocal,
        SUM(vt.ImporteNeto) AS VentaNetaLocal,
        SUM(vt.ImporteNetoSinImpuesto) AS VentaSinImpuestoLocal,
        SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaNetaUsd,
        COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
        SUM(vt.CantidadCubiertos) AS TotalCubiertos
    FROM [fact].[VentaTicket] vt
    INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
    LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
    LEFT JOIN [dim].[TipoCambio] tc
        ON m.CodigoISO = tc.CodigoMoneda
        AND vt.FechaNegocio = tc.Fecha
        AND tc.TipoTasa = 'OFICIAL'
    WHERE vt.EstaAnulado = 0
    GROUP BY f.FranquiciaId, f.Codigo, f.Nombre, vt.FechaNegocio, m.CodigoISO
)
SELECT
    FranquiciaId,
    FranquiciaCodigo,
    FranquiciaNombre,
    FechaNegocio,
    DiaSemanaNumero,
    DiaSemana,
    MonedaLocal,
    VentaNetaLocal,
    VentaSinImpuestoLocal,
    VentaNetaUsd,
    CantidadTickets,
    TotalCubiertos,

    -- Ranking descendente (1 = mejor dia)
    ROW_NUMBER() OVER (PARTITION BY FranquiciaId ORDER BY VentaNetaLocal DESC) AS RankingDescendente,

    -- Ranking ascendente (1 = peor dia)
    ROW_NUMBER() OVER (PARTITION BY FranquiciaId ORDER BY VentaNetaLocal ASC) AS RankingAscendente

FROM VentasPorDia
GO

PRINT 'Vista fact.vw_VentasPorDiaRanking creada.'
GO

-- ============================================================================
-- VISTA: Venta del Dia vs Historico
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasDelDia]'))
    DROP VIEW [fact].[vw_VentasDelDia]
GO

CREATE VIEW [fact].[vw_VentasDelDia]
AS
WITH VentaHoy AS (
    SELECT
        f.FranquiciaId,
        SUM(vt.ImporteNeto) AS VentaHoy,
        SUM(vt.ImporteNetoSinImpuesto) AS VentaSinImpuestoHoy,
        SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaHoyUsd,
        COUNT(DISTINCT vt.VentaTicketId) AS TicketsHoy,
        SUM(vt.CantidadCubiertos) AS CubiertosHoy
    FROM [fact].[VentaTicket] vt
    INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
    LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
    LEFT JOIN [dim].[TipoCambio] tc
        ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
    WHERE vt.EstaAnulado = 0
      AND vt.FechaNegocio = CAST(GETDATE() AS DATE)
    GROUP BY f.FranquiciaId
),
VentaMismoDiaSemanaAnterior AS (
    SELECT
        f.FranquiciaId,
        SUM(vt.ImporteNeto) AS VentaSemanaAnterior,
        SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaSemanaAnteriorUsd
    FROM [fact].[VentaTicket] vt
    INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
    LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
    LEFT JOIN [dim].[TipoCambio] tc
        ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
    WHERE vt.EstaAnulado = 0
      AND vt.FechaNegocio = DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
    GROUP BY f.FranquiciaId
),
PromedioMes AS (
    SELECT
        f.FranquiciaId,
        AVG(DailyVenta) AS PromedioVentaDiaMes,
        AVG(DailyVentaUsd) AS PromedioVentaDiaMesUsd
    FROM (
        SELECT
            vt.FranquiciaId,
            vt.FechaNegocio,
            SUM(vt.ImporteNeto) AS DailyVenta,
            SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS DailyVentaUsd
        FROM [fact].[VentaTicket] vt
        LEFT JOIN [dim].[Franquicia] ff ON vt.FranquiciaId = ff.FranquiciaId
        LEFT JOIN [dim].[Moneda] m ON ff.MonedaId = m.MonedaId
        LEFT JOIN [dim].[TipoCambio] tc
            ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
        WHERE vt.EstaAnulado = 0
          AND YEAR(vt.FechaNegocio) = YEAR(GETDATE())
          AND MONTH(vt.FechaNegocio) = MONTH(GETDATE())
          AND vt.FechaNegocio < CAST(GETDATE() AS DATE)
        GROUP BY vt.FranquiciaId, vt.FechaNegocio
    ) sub
    INNER JOIN [dim].[Franquicia] f ON sub.FranquiciaId = f.FranquiciaId
    GROUP BY f.FranquiciaId
)
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    m.CodigoISO AS MonedaLocal,

    -- Venta de hoy
    ISNULL(vh.VentaHoy, 0) AS VentaHoyLocal,
    vh.VentaSinImpuestoHoy,
    vh.VentaHoyUsd,
    ISNULL(vh.TicketsHoy, 0) AS TicketsHoy,
    ISNULL(vh.CubiertosHoy, 0) AS CubiertosHoy,

    -- Comparativo semana anterior
    ISNULL(vsa.VentaSemanaAnterior, 0) AS VentaSemanaAnteriorLocal,
    vsa.VentaSemanaAnteriorUsd,
    CASE WHEN vsa.VentaSemanaAnterior > 0
         THEN ROUND((vh.VentaHoy - vsa.VentaSemanaAnterior) * 100.0 / vsa.VentaSemanaAnterior, 2)
         ELSE NULL END AS VariacionVsSemanaAnteriorPct,

    -- Comparativo promedio mes
    pm.PromedioVentaDiaMes AS PromedioVentaDiaMesLocal,
    pm.PromedioVentaDiaMesUsd,
    CASE WHEN pm.PromedioVentaDiaMes > 0
         THEN ROUND((vh.VentaHoy - pm.PromedioVentaDiaMes) * 100.0 / pm.PromedioVentaDiaMes, 2)
         ELSE NULL END AS VariacionVsPromedioMesPct

FROM [dim].[Franquicia] f
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
LEFT JOIN VentaHoy vh ON f.FranquiciaId = vh.FranquiciaId
LEFT JOIN VentaMismoDiaSemanaAnterior vsa ON f.FranquiciaId = vsa.FranquiciaId
LEFT JOIN PromedioMes pm ON f.FranquiciaId = pm.FranquiciaId
WHERE f.Activo = 1
GO

PRINT 'Vista fact.vw_VentasDelDia creada.'
GO

-- ============================================================================
-- VISTA: Venta por MealPeriod con USD
-- ============================================================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[fact].[vw_VentasPorMealPeriod]'))
    DROP VIEW [fact].[vw_VentasPorMealPeriod]
GO

CREATE VIEW [fact].[vw_VentasPorMealPeriod]
AS
SELECT
    f.FranquiciaId,
    f.Codigo AS FranquiciaCodigo,
    f.Nombre AS FranquiciaNombre,
    vt.FechaNegocio,
    COALESCE(vt.PeriodoComida, 'SIN_DEFINIR') AS MealPeriod,
    vt.MealPeriodOrigen,
    m.CodigoISO AS MonedaLocal,

    -- Metricas locales
    COUNT(DISTINCT vt.VentaTicketId) AS CantidadTickets,
    SUM(vt.ImporteNeto) AS VentaNetaLocal,
    SUM(vt.ImporteNetoSinImpuesto) AS VentaSinImpuestoLocal,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,

    -- Metricas USD
    SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaNetaUsd,
    SUM(CASE WHEN tc.UnidadesPorUsd > 0 AND vt.ImporteNetoSinImpuesto IS NOT NULL
             THEN vt.ImporteNetoSinImpuesto / tc.UnidadesPorUsd ELSE NULL END) AS VentaSinImpuestoUsd,

    -- Calidad de MealPeriod
    SUM(CASE WHEN vt.MealPeriodOrigen = 'POS' THEN 1 ELSE 0 END) AS TicketsMealPeriodPOS,
    SUM(CASE WHEN vt.MealPeriodOrigen = 'DERIVADO' THEN 1 ELSE 0 END) AS TicketsMealPeriodDerivado,
    SUM(CASE WHEN vt.MealPeriodOrigen IS NULL THEN 1 ELSE 0 END) AS TicketsSinMealPeriod

FROM [fact].[VentaTicket] vt
INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
LEFT JOIN [dim].[TipoCambio] tc
    ON m.CodigoISO = tc.CodigoMoneda
    AND vt.FechaNegocio = tc.Fecha
    AND tc.TipoTasa = 'OFICIAL'
WHERE vt.EstaAnulado = 0
GROUP BY
    f.FranquiciaId, f.Codigo, f.Nombre,
    vt.FechaNegocio,
    vt.PeriodoComida, vt.MealPeriodOrigen,
    m.CodigoISO
GO

PRINT 'Vista fact.vw_VentasPorMealPeriod creada.'
GO

-- ============================================================================
-- FUNCION: Comparativo Mensual (actual vs anterior vs interanual)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'IF' AND name = 'fn_ComparativoMensual')
    DROP FUNCTION [fact].[fn_ComparativoMensual]
GO

CREATE FUNCTION [fact].[fn_ComparativoMensual]
(
    @FranquiciaId INT,
    @Anio INT,
    @Mes INT
)
RETURNS TABLE
AS
RETURN
(
    WITH PeriodoActual AS (
        SELECT
            SUM(vt.ImporteNeto) AS VentaLocal,
            SUM(vt.ImporteNetoSinImpuesto) AS VentaSinImpuestoLocal,
            SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaUsd,
            COUNT(DISTINCT vt.VentaTicketId) AS Tickets,
            SUM(vt.CantidadCubiertos) AS Cubiertos,
            COUNT(DISTINCT vt.FechaNegocio) AS DiasConVenta
        FROM [fact].[VentaTicket] vt
        INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
        LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
        LEFT JOIN [dim].[TipoCambio] tc
            ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
        WHERE vt.FranquiciaId = @FranquiciaId
          AND vt.EstaAnulado = 0
          AND YEAR(vt.FechaNegocio) = @Anio
          AND MONTH(vt.FechaNegocio) = @Mes
    ),
    MesAnterior AS (
        SELECT
            SUM(vt.ImporteNeto) AS VentaLocal,
            SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaUsd,
            COUNT(DISTINCT vt.VentaTicketId) AS Tickets,
            COUNT(DISTINCT vt.FechaNegocio) AS DiasConVenta
        FROM [fact].[VentaTicket] vt
        INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
        LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
        LEFT JOIN [dim].[TipoCambio] tc
            ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
        WHERE vt.FranquiciaId = @FranquiciaId
          AND vt.EstaAnulado = 0
          AND vt.FechaNegocio >= DATEADD(MONTH, -1, DATEFROMPARTS(@Anio, @Mes, 1))
          AND vt.FechaNegocio < DATEFROMPARTS(@Anio, @Mes, 1)
    ),
    Interanual AS (
        SELECT
            SUM(vt.ImporteNeto) AS VentaLocal,
            SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteNeto / tc.UnidadesPorUsd ELSE NULL END) AS VentaUsd,
            COUNT(DISTINCT vt.VentaTicketId) AS Tickets,
            COUNT(DISTINCT vt.FechaNegocio) AS DiasConVenta
        FROM [fact].[VentaTicket] vt
        INNER JOIN [dim].[Franquicia] f ON vt.FranquiciaId = f.FranquiciaId
        LEFT JOIN [dim].[Moneda] m ON f.MonedaId = m.MonedaId
        LEFT JOIN [dim].[TipoCambio] tc
            ON m.CodigoISO = tc.CodigoMoneda AND vt.FechaNegocio = tc.Fecha AND tc.TipoTasa = 'OFICIAL'
        WHERE vt.FranquiciaId = @FranquiciaId
          AND vt.EstaAnulado = 0
          AND YEAR(vt.FechaNegocio) = @Anio - 1
          AND MONTH(vt.FechaNegocio) = @Mes
    ),
    FechaInicio AS (
        SELECT FechaInicioDatos FROM [dim].[Franquicia] WHERE FranquiciaId = @FranquiciaId
    )
    SELECT
        @FranquiciaId AS FranquiciaId,
        @Anio AS Anio,
        @Mes AS Mes,

        -- Periodo actual
        pa.VentaLocal AS VentaActualLocal,
        pa.VentaSinImpuestoLocal AS VentaSinImpuestoActualLocal,
        pa.VentaUsd AS VentaActualUsd,
        pa.Tickets AS TicketsActual,
        pa.Cubiertos AS CubiertosActual,
        pa.DiasConVenta AS DiasActual,

        -- Mes anterior
        ma.VentaLocal AS VentaMesAnteriorLocal,
        ma.VentaUsd AS VentaMesAnteriorUsd,
        ma.Tickets AS TicketsMesAnterior,
        ma.DiasConVenta AS DiasMesAnterior,
        CASE WHEN ma.VentaLocal > 0
             THEN ROUND((pa.VentaLocal - ma.VentaLocal) * 100.0 / ma.VentaLocal, 2)
             ELSE NULL END AS VariacionVsMesAnteriorPct,

        -- Interanual
        ia.VentaLocal AS VentaInteranualLocal,
        ia.VentaUsd AS VentaInteranualUsd,
        ia.Tickets AS TicketsInteranual,
        ia.DiasConVenta AS DiasInteranual,
        CASE WHEN ia.VentaLocal > 0
             THEN ROUND((pa.VentaLocal - ia.VentaLocal) * 100.0 / ia.VentaLocal, 2)
             ELSE NULL END AS VariacionInteranualPct,

        -- Flags de datos historicos
        CASE WHEN fi.FechaInicioDatos IS NULL OR fi.FechaInicioDatos > DATEADD(MONTH, -1, DATEFROMPARTS(@Anio, @Mes, 1))
             THEN 1 ELSE 0 END AS SinDatosMesAnterior,
        CASE WHEN fi.FechaInicioDatos IS NULL OR fi.FechaInicioDatos > DATEFROMPARTS(@Anio - 1, @Mes, 1)
             THEN 1 ELSE 0 END AS SinDatosInteranual

    FROM PeriodoActual pa
    CROSS JOIN MesAnterior ma
    CROSS JOIN Interanual ia
    CROSS JOIN FechaInicio fi
)
GO

PRINT 'Funcion fact.fn_ComparativoMensual creada.'
GO

PRINT '============================================'
PRINT 'Script 16 completado: Vistas dashboard v1.2 creadas.'
PRINT '============================================'
GO
