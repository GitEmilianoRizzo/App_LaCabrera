using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace LaCabrera.Api.Tests;

/// <summary>
/// La API exige usuario logueado en todo, salvo una lista cerrada de endpoints públicos.
/// No necesita base de datos: solo arranca la API en memoria.
/// </summary>
public class AutorizacionApiTests : IClassFixture<AutorizacionApiTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Sin jobs en segundo plano (el de tipos de cambio usa la base)
                services.RemoveAll<IHostedService>();
            });
        }
    }

    // Lo único que se puede llamar sin login
    private static readonly HashSet<string> EndpointsPublicos = new()
    {
        "GET /",
        "GET /health",
        "GET /api/health",
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/google",
        "POST /api/v1/auth/refresh",
        // POS de las franquicias: se autentican con X-API-KEY dentro de la acción
        "POST /api/v1/sales/daily-batch",
        "GET /api/v1/sales/daily-batch/{batchId}/status",
    };

    private readonly ApiFactory _factory;

    public AutorizacionApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Solo_los_endpoints_de_la_lista_son_publicos()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(endpoints);

        var publicos = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() != null)
            .SelectMany(e =>
            {
                var ruta = "/" + (e.RoutePattern.RawText ?? "").TrimStart('/');
                var metodos = e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" };
                return metodos.Select(m => $"{m} {ruta}");
            })
            .ToHashSet();

        Assert.Equal(EndpointsPublicos.OrderBy(x => x), publicos.OrderBy(x => x));
    }

    [Theory]
    [InlineData("/api/v1/conexiones")]
    [InlineData("/api/v1/franquicias")]
    [InlineData("/api/v1/fx/currencies")]
    [InlineData("/api/v1/preferencias")]
    [InlineData("/api/v1/integration/schema/daily-sales-batch")]
    [InlineData("/api/v1/dashboard/informe-ejecutivo/datos?baseDesde=2026-08-01&baseHasta=2026-08-31&compDesde=2026-09-01&compHasta=2026-09-30")]
    public async Task Sin_login_responde_401(string url)
    {
        var respuesta = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Health_check_sigue_publico()
    {
        var respuesta = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_token_valido_responde()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerarToken(_factory.Services, minutos: 5));

        var respuesta = await cliente.GetAsync("/api/v1/integration/schema/daily-sales-batch");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_token_vencido_responde_401()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerarToken(_factory.Services, minutos: -5));

        var respuesta = await cliente.GetAsync("/api/v1/integration/schema/daily-sales-batch");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static string GenerarToken(IServiceProvider services, int minutos)
    {
        var jwt = services.GetRequiredService<IConfiguration>().GetSection("Jwt").Get<JwtSettings>()!;
        var credenciales = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)), SecurityAlgorithms.HmacSha256);
        var ahora = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "1"), new Claim(ClaimTypes.Role, "COMERCIAL") },
            notBefore: ahora.AddMinutes(Math.Min(minutos, 0) - 10),
            expires: ahora.AddMinutes(minutos),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
