using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class ResumenRangoExtraccionTests
{
    private static readonly DateTime Dia = new(2026, 10, 19);

    [Fact]
    public void Login_fallido_no_se_informa_como_exito_con_cero_tickets()
    {
        var resumen = new ResumenRangoExtraccion();
        // Así devolvía Vinson un día sin login: Success = false y ErrorsCount = 0
        resumen.Agregar(Dia, new EjecucionResultDto { Success = false, Message = "Error de autenticación con Vinson" });

        var r = resumen.Resultado(diasSinProcesar: 24);

        Assert.False(r.Success);
        Assert.Equal(1, r.ErrorsCount);
        Assert.Contains("1 días con error (2026-10-19)", r.Message);
        Assert.Contains("Error de autenticación con Vinson", r.Message);
        Assert.Contains("quedaron 24 días sin procesar", r.Message);
    }

    [Fact]
    public void Rango_sin_errores_es_exito_y_suma_los_dias()
    {
        var resumen = new ResumenRangoExtraccion();
        resumen.Agregar(Dia, new EjecucionResultDto { Success = true, TicketsProcesados = 120, LineasProcesadas = 900 });
        resumen.Agregar(Dia.AddDays(1), new EjecucionResultDto { Success = true, Message = "Sin transacciones para procesar" });

        var r = resumen.Resultado();

        Assert.True(r.Success);
        Assert.Equal(120, r.TicketsProcesados);
        Assert.Equal(0, r.ErrorsCount);
        Assert.Equal("Rango completado: 2 días procesados, 120 tickets, 900 líneas", r.Message);
    }

    [Fact]
    public void Un_dia_con_error_entre_dias_buenos_marca_el_rango_con_error()
    {
        var resumen = new ResumenRangoExtraccion();
        resumen.Agregar(Dia, new EjecucionResultDto { Success = true, TicketsProcesados = 50 });
        resumen.Agregar(Dia.AddDays(1), new EjecucionResultDto { Success = false, Message = "Error API Vinson (cabeceras): HTTP 522", ErrorsCount = 1 });
        resumen.AgregarExcepcion(Dia.AddDays(2), new TimeoutException("timeout"));

        var r = resumen.Resultado();

        Assert.False(r.Success);
        Assert.Equal(50, r.TicketsProcesados);
        Assert.Equal(2, r.ErrorsCount);
        Assert.Contains("1 días procesados", r.Message);
        Assert.Contains("2 días con error (2026-10-20, 2026-10-21)", r.Message);
    }
}
