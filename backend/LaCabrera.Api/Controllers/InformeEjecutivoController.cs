using LaCabrera.Api.Services.InformeEjecutivo;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

/// <summary>
/// Informe Ejecutivo: compara un período inicial (base) contra un período final.
/// </summary>
[ApiController]
[Route("api/v1/dashboard/informe-ejecutivo")]
[Produces("application/json")]
public class InformeEjecutivoController : ControllerBase
{
    private const int MaxDiasPorPeriodo = 366;

    private readonly IInformeEjecutivoService _service;
    private readonly ILogger<InformeEjecutivoController> _logger;

    public InformeEjecutivoController(IInformeEjecutivoService service, ILogger<InformeEjecutivoController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Genera el Informe Ejecutivo en PDF
    /// </summary>
    [HttpGet("pdf")]
    [Produces("application/pdf")]
    [SwaggerOperation(Summary = "Informe Ejecutivo (PDF)",
        Description = "Compara el período final contra el período inicial: ventas netas USD, cubiertos, $/cubierto, Palermo vs franquicias y moneda local")]
    public async Task<IActionResult> GetPdf(
        [FromQuery] DateTime baseDesde,
        [FromQuery] DateTime baseHasta,
        [FromQuery] DateTime compDesde,
        [FromQuery] DateTime compHasta,
        CancellationToken ct)
    {
        Validar(baseDesde, baseHasta, compDesde, compHasta);

        _logger.LogInformation("Generando informe ejecutivo {CompDesde:yyyy-MM-dd}..{CompHasta:yyyy-MM-dd} vs {BaseDesde:yyyy-MM-dd}..{BaseHasta:yyyy-MM-dd}",
            compDesde, compHasta, baseDesde, baseHasta);

        var pdf = await _service.GenerarPdfAsync(baseDesde, baseHasta, compDesde, compHasta, ct);
        var nombre = $"Informe_Ejecutivo_{compDesde:yyyyMMdd}-{compHasta:yyyyMMdd}_vs_{baseDesde:yyyyMMdd}-{baseHasta:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", nombre);
    }

    /// <summary>
    /// Datos calculados del Informe Ejecutivo (para control o integraciones)
    /// </summary>
    [HttpGet("datos")]
    [SwaggerOperation(Summary = "Informe Ejecutivo (datos)", Description = "Mismo cálculo que el PDF, en JSON")]
    public async Task<ActionResult<InformeEjecutivoModel>> GetDatos(
        [FromQuery] DateTime baseDesde,
        [FromQuery] DateTime baseHasta,
        [FromQuery] DateTime compDesde,
        [FromQuery] DateTime compHasta)
    {
        Validar(baseDesde, baseHasta, compDesde, compHasta);
        return Ok(await _service.CalcularAsync(baseDesde, baseHasta, compDesde, compHasta));
    }

    private static void Validar(DateTime baseDesde, DateTime baseHasta, DateTime compDesde, DateTime compHasta)
    {
        if (baseDesde == default || baseHasta == default || compDesde == default || compHasta == default)
            throw new ArgumentException("Se requieren las fechas de ambos períodos (baseDesde, baseHasta, compDesde, compHasta)");
        if (baseDesde > baseHasta)
            throw new ArgumentException("En el período inicial, la fecha desde no puede ser posterior a la fecha hasta");
        if (compDesde > compHasta)
            throw new ArgumentException("En el período final, la fecha desde no puede ser posterior a la fecha hasta");
        if ((baseHasta - baseDesde).Days + 1 > MaxDiasPorPeriodo || (compHasta - compDesde).Days + 1 > MaxDiasPorPeriodo)
            throw new ArgumentException($"Cada período puede abarcar como máximo {MaxDiasPorPeriodo} días");
    }
}
