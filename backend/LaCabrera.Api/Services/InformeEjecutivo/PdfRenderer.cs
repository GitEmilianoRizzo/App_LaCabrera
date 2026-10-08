using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace LaCabrera.Api.Services.InformeEjecutivo;

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string html, CancellationToken ct = default);
}

/// <summary>
/// HTML → PDF con Chromium headless (PuppeteerSharp). Se lanza un navegador por informe:
/// es poco frecuente y así no queda un proceso residente consumiendo memoria.
/// </summary>
public class ChromiumPdfRenderer : IPdfRenderer
{
    private static readonly SemaphoreSlim Concurrencia = new(2);

    private static readonly string[] RutasConocidas =
    {
        "/usr/bin/chromium",
        "/usr/bin/chromium-browser",
        "/usr/bin/google-chrome",
        "/usr/bin/google-chrome-stable",
    };

    private readonly InformeEjecutivoSettings _settings;
    private readonly ILogger<ChromiumPdfRenderer> _logger;

    public ChromiumPdfRenderer(IOptions<InformeEjecutivoSettings> settings, ILogger<ChromiumPdfRenderer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct = default)
    {
        var ejecutable = ResolverChromium();

        await Concurrencia.WaitAsync(ct);
        try
        {
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = ejecutable,
                Args = new[] { "--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu", "--font-render-hinting=none" },
            });
            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });
            await page.EvaluateExpressionAsync("document.fonts.ready");

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                Landscape = true,
                PrintBackground = true,
                PreferCSSPageSize = true,
                MarginOptions = new MarginOptions { Top = "0", Right = "0", Bottom = "0", Left = "0" },
            });
        }
        finally
        {
            Concurrencia.Release();
        }
    }

    private string ResolverChromium()
    {
        var candidatos = new[] { _settings.ChromiumPath, Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH") }
            .Concat(RutasConocidas);

        var ruta = candidatos.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
        if (ruta is null)
        {
            _logger.LogError("No se encontró Chromium para generar PDFs. Configurar InformeEjecutivo:ChromiumPath o PUPPETEER_EXECUTABLE_PATH");
            throw new InvalidOperationException("El servidor no tiene Chromium instalado para generar el PDF del informe.");
        }
        return ruta;
    }
}
