using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class DriveArchivosPlanTests
{
    private static DriveArchivo A(string nombre) => new(Guid.NewGuid().ToString(), nombre, null, null);
    private static readonly HashSet<string> Nada = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Carga_solo_los_dias_que_no_estan_cargados_ordenados_por_fecha()
    {
        var archivos = new[]
        {
            A("LC_BOG_ZG01_2026-10-19_LC-BOG_ZG01-20261019-001.json"),
            A("LC_BOG_ZG01_2026-10-17_LC-BOG_ZG01-20261017-001.json"),
            A("LC_BOG_ZG01_2026-10-18_LC-BOG_ZG01-20261018-001.json"),
        };
        var cargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LC-BOG_ZG01-20261017-001" };

        var plan = DriveArchivosPlan.Armar(archivos, "BOG_ZG01", null, cargados);

        Assert.Equal(1, plan.YaCargados);
        Assert.Equal(new[] { "LC-BOG_ZG01-20261018-001", "LC-BOG_ZG01-20261019-001" }, plan.Pendientes.Select(p => p.BatchId));
        Assert.Equal(new DateTime(2026, 10, 18), plan.Pendientes[0].Fecha);
    }

    [Fact]
    public void Ignora_copias_manuales_y_nombres_de_otra_tienda()
    {
        var archivos = new[]
        {
            A("LC_CTG_CH01_2026-09-30_LC-CTG_CH01-20260930-001 (1).json"),
            A("LC_BOG_LA8501_2026-10-20_LC-BOG_LA8501-20261020-001.json"),
            A("resumen octubre.xlsx"),
            A("LC_CTG_CH01_2026-10-20_LC-CTG_CH01-20261020-001.json"),
        };

        var plan = DriveArchivosPlan.Armar(archivos, "CTG_CH01", null, Nada);

        Assert.Single(plan.Pendientes);
        Assert.Equal("LC-CTG_CH01-20261020-001", plan.Pendientes[0].BatchId);
        Assert.Equal(3, plan.Ignorados.Count);
    }

    [Fact]
    public void Respeta_la_fecha_de_inicio()
    {
        var archivos = new[]
        {
            A("LC_BOG_LA8501_2026-09-30_LC-BOG_LA8501-20260930-001.json"),
            A("LC_BOG_LA8501_2026-10-01_LC-BOG_LA8501-20261001-001.json"),
        };

        var plan = DriveArchivosPlan.Armar(archivos, "BOG_LA8501", new DateTime(2026, 10, 1), Nada);

        Assert.Single(plan.Pendientes);
        Assert.Equal(new DateTime(2026, 10, 1), plan.Pendientes[0].Fecha);
    }

    [Fact]
    public void Una_correccion_del_mismo_dia_entra_como_lote_nuevo()
    {
        var archivos = new[]
        {
            A("LC_BOG_ZG01_2026-10-20_LC-BOG_ZG01-20261020-001.json"),
            A("LC_BOG_ZG01_2026-10-20_LC-BOG_ZG01-20261020-002.json"),
        };
        var cargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LC-BOG_ZG01-20261020-001" };

        var plan = DriveArchivosPlan.Armar(archivos, "BOG_ZG01", null, cargados);

        Assert.Equal("LC-BOG_ZG01-20261020-002", Assert.Single(plan.Pendientes).BatchId);
    }

    [Fact]
    public void El_mismo_lote_dos_veces_se_carga_una_sola_vez()
    {
        var archivos = new[]
        {
            A("LC_BOG_ZG01_2026-10-21_LC-BOG_ZG01-20261021-001.json"),
            A("LC_BOG_ZG01_2026-10-21_LC-BOG_ZG01-20261021-001.json"),
        };

        var plan = DriveArchivosPlan.Armar(archivos, "BOG_ZG01", null, Nada);

        Assert.Single(plan.Pendientes);
        Assert.Single(plan.Ignorados);
    }
}
