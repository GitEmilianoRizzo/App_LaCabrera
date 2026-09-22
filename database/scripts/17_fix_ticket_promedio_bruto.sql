-- ============================================================================
-- MIGRACION: Cambiar TicketPromedio para usar ImporteBruto (con impuestos)
-- Fecha: 2026-09-22
-- Descripcion: El Ticket Promedio debe calcularse sobre el total CON impuestos
--              (ImporteBruto) en lugar del neto sin impuestos (ImporteNeto)
-- ============================================================================

USE LaCabreraDB
GO

PRINT 'Iniciando migracion: TicketPromedio con ImporteBruto...'
GO

-- ============================================================================
-- VISTA 1: vw_VentasResumenDiario
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

    -- Promedios (CAMBIADO: usar ImporteBruto para TicketPromedio)
    AVG(vt.ImporteBruto) AS TicketPromedio,
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
WHERE vt.EstaAnulado = 0
GROUP BY
    vt.FechaNegocio,
    f.FranquiciaId, f.Codigo, f.Nombre,
    ge.GrupoEconomicoId, ge.Codigo, ge.Nombre,
    f.Pais, f.Ciudad, m.CodigoISO
GO

PRINT 'Vista fact.vw_VentasResumenDiario actualizada con TicketPromedio = AVG(ImporteBruto).'
GO

-- ============================================================================
-- VISTA 2: vw_VentasPorMealPeriod - Agregar VentaBruta
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
    SUM(vt.ImporteBruto) AS VentaBruta,
    SUM(vt.ImporteNeto) AS VentaNeta,
    SUM(vt.ImporteNetoSinImpuesto) AS VentaNetaSinImp,
    SUM(vt.CantidadCubiertos) AS TotalCubiertos,

    -- Metricas USD
    SUM(CASE WHEN tc.UnidadesPorUsd > 0 THEN vt.ImporteBruto / tc.UnidadesPorUsd ELSE NULL END) AS VentaBrutaUsd,
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

PRINT 'Vista fact.vw_VentasPorMealPeriod actualizada con VentaBruta.'
GO

-- ============================================================================
-- Verificacion
-- ============================================================================

PRINT ''
PRINT '=== VERIFICACION ==='
PRINT 'TicketPromedio ahora se calcula sobre ImporteBruto (con impuestos)'
PRINT ''

SELECT TOP 5
    FranquiciaNombre,
    FechaNegocio,
    CantidadTickets,
    VentaBruta,
    VentaNeta,
    TicketPromedio,
    VentaBruta / NULLIF(CantidadTickets, 0) AS TicketPromedio_Verificacion
FROM fact.vw_VentasResumenDiario
ORDER BY FechaNegocio DESC
GO

PRINT 'Migracion completada.'
GO
