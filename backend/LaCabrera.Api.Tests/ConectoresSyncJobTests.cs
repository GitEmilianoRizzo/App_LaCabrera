using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class ConectoresSyncJobTests
{
    private static readonly TimeZoneInfo Arg = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    [Fact]
    public void Las_10_de_Argentina_son_las_13_UTC_del_mismo_dia()
    {
        // 26/10 08:00 en Argentina
        var proxima = ConectoresSyncJob.ProximaEjecucionUtc(new DateTime(2026, 10, 26, 11, 0, 0, DateTimeKind.Utc), 10, 0, Arg);

        Assert.Equal(new DateTime(2026, 10, 26, 13, 0, 0), proxima);
    }

    [Fact]
    public void Si_ya_pasaron_las_10_corre_al_dia_siguiente()
    {
        // 26/10 14:30 en Argentina
        var proxima = ConectoresSyncJob.ProximaEjecucionUtc(new DateTime(2026, 10, 26, 17, 30, 0, DateTimeKind.Utc), 10, 0, Arg);

        Assert.Equal(new DateTime(2026, 10, 27, 13, 0, 0), proxima);
    }

    [Fact]
    public void Justo_a_las_10_no_repite_la_corrida()
    {
        var proxima = ConectoresSyncJob.ProximaEjecucionUtc(new DateTime(2026, 10, 26, 13, 0, 0, DateTimeKind.Utc), 10, 0, Arg);

        Assert.Equal(new DateTime(2026, 10, 27, 13, 0, 0), proxima);
    }
}
