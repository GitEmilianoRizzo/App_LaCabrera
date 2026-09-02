using System.Text;
using FluentValidation;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Middleware;
using LaCabrera.Api.Repositories;
using LaCabrera.Api.Services;
using LaCabrera.Api.Services.ExchangeRate;
using LaCabrera.Api.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Configuration
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleSettings>(builder.Configuration.GetSection("Google"));
builder.Services.AddSingleton(builder.Configuration);

// Database connection
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("LaCabreraDb")!));

// Repositories
builder.Services.AddScoped<IFranquiciaRepository, FranquiciaRepository>();
builder.Services.AddScoped<IIngestionRepository, IngestionRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IConexionRepository, ConexionRepository>();
builder.Services.AddScoped<IPreferenciasRepository, PreferenciasRepository>();
builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();

// Services
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IConexionService, ConexionService>();
builder.Services.AddScoped<IAgoraExtractorService, AgoraExtractorService>();
builder.Services.AddScoped<IVinsonExtractorService, VinsonExtractorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<ITxtParserService, TxtParserService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for large file parsing
});

// Exchange Rate Services (Tipo de Cambio)
builder.Services.AddHttpClient<BcraExchangeRateProvider>();
builder.Services.AddHttpClient<ExchangeRateHostProvider>();
builder.Services.AddHttpClient<DolarApiProvider>();
builder.Services.AddScoped<IExchangeRateProvider, BcraExchangeRateProvider>();
builder.Services.AddScoped<IExchangeRateProvider, ExchangeRateHostProvider>();
builder.Services.AddScoped<IExchangeRateProvider, DolarApiProvider>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// Background Job para refresh diario de tasas
builder.Services.AddHostedService<ExchangeRateRefreshJob>();

// HttpClient para llamadas a APIs externas (Ágora, Vinson, etc)
builder.Services.AddHttpClient();

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<DailySalesBatchRequestValidator>();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LA CABRERA - API de Integracion",
        Version = "v1",
        Description = "API para integracion de ventas de franquicias de LA CABRERA",
        Contact = new OpenApiContact
        {
            Name = "LA CABRERA IT",
            Email = "integraciones@lacabrera.com"
        }
    });

    // JWT Bearer security definition
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Ejemplo: 'Bearer {token}'"
    });

    // API Key security definition (para ingesta de franquicias)
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-KEY",
        Description = "API Key de la franquicia"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.EnableAnnotations();
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSerilogRequestLogging();

// Global exception handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger (enabled in all environments for demo)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LA CABRERA API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Redirect root to swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

Log.Information("LA CABRERA API iniciada en {Environment}", app.Environment.EnvironmentName);

app.Run();
