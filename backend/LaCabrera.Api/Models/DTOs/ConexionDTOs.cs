using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

/// <summary>
/// DTO para listar nodos de conexion
/// </summary>
public class NodoConexionDto
{
    [JsonPropertyName("nodo_conexion_id")]
    public int NodoConexionId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("tipo_conector")]
    public string TipoConector { get; set; } = string.Empty;

    [JsonPropertyName("modo")]
    public string Modo { get; set; } = string.Empty;

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("franquicia_codigo")]
    public string FranquiciaCodigo { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_nombre")]
    public string FranquiciaNombre { get; set; } = string.Empty;

    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("ciudad")]
    public string? Ciudad { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";

    [JsonPropertyName("moneda")]
    public string Moneda { get; set; } = "USD";

    [JsonPropertyName("cron_expression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("convencion_importes")]
    public string ConvencionImportes { get; set; } = "VAT_INCLUDED";

    [JsonPropertyName("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [JsonPropertyName("ultimo_estado")]
    public string? UltimoEstado { get; set; }

    [JsonPropertyName("ultimo_batch_id")]
    public string? UltimoBatchId { get; set; }

    [JsonPropertyName("mapeos_categoria")]
    public int MapeosCategoria { get; set; }

    [JsonPropertyName("mapeos_medio_pago")]
    public int MapeosMedioPago { get; set; }

    [JsonPropertyName("valores_pendientes")]
    public int ValoresPendientes { get; set; }

    [JsonPropertyName("tickets_ultima_ejecucion")]
    public int? TicketsUltimaEjecucion { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }
}

/// <summary>
/// DTO para crear/actualizar un nodo de conexion
/// </summary>
public class NodoConexionCreateDto
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("franquicia_id")]
    public int FranquiciaId { get; set; }

    [JsonPropertyName("tipo_conector")]
    public string TipoConector { get; set; } = string.Empty;

    [JsonPropertyName("modo")]
    public string Modo { get; set; } = "PUSH";

    [JsonPropertyName("configuracion")]
    public NodoConfiguracionDto? Configuracion { get; set; }

    [JsonPropertyName("cron_expression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";

    [JsonPropertyName("moneda")]
    public string Moneda { get; set; } = "USD";

    [JsonPropertyName("convencion_importes")]
    public string ConvencionImportes { get; set; } = "VAT_INCLUDED";

    [JsonPropertyName("politica_devoluciones")]
    public string PoliticaDevoluciones { get; set; } = "NEGATIVE_LINES";

    [JsonPropertyName("tolerancia_reconciliacion")]
    public decimal ToleranciaReconciliacion { get; set; } = 0.05m;
}

/// <summary>
/// DTO para configuracion de conexion (serializado como JSON en la BD)
/// </summary>
public class NodoConfiguracionDto
{
    [JsonPropertyName("connection")]
    public ConnectionConfigDto? Connection { get; set; }

    [JsonPropertyName("parser_code")]
    public string? ParserCode { get; set; }

    [JsonPropertyName("export_filter")]
    public string? ExportFilter { get; set; }

    [JsonPropertyName("include_processed")]
    public bool IncludeProcessed { get; set; }

    [JsonPropertyName("mark_processed_after_accept")]
    public bool MarkProcessedAfterAccept { get; set; } = true;

    [JsonPropertyName("workplace_ids")]
    public List<int>? WorkplaceIds { get; set; }

    [JsonPropertyName("field_mappings")]
    public List<FieldMappingDto>? FieldMappings { get; set; }
}

/// <summary>
/// DTO para mapeo de campos origen -> destino
/// </summary>
public class FieldMappingDto
{
    [JsonPropertyName("mapping_id")]
    public string? MappingId { get; set; }

    [JsonPropertyName("target_entity")]
    public string TargetEntity { get; set; } = string.Empty;

    [JsonPropertyName("target_field")]
    public string TargetField { get; set; } = string.Empty;

    [JsonPropertyName("source_path")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("transformation")]
    public FieldTransformationDto? Transformation { get; set; }

    [JsonPropertyName("default_value")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class FieldTransformationDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "NONE";

    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}

public class ConnectionConfigDto
{
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("api_token")]
    public string? ApiToken { get; set; }

    // Campos adicionales para autenticación JWT (usado por Vinson)
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    // ID de tienda para sistemas que lo requieren (Vinson)
    [JsonPropertyName("store_id")]
    public string? StoreId { get; set; }

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 60;

    [JsonPropertyName("retry")]
    public RetryConfigDto? Retry { get; set; }
}

public class RetryConfigDto
{
    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; } = 3;

    [JsonPropertyName("backoff_seconds")]
    public List<int>? BackoffSeconds { get; set; }
}

/// <summary>
/// DTO para mapeo de categoria de producto
/// </summary>
public class MapeoCategoriaDto
{
    [JsonPropertyName("mapeo_categoria_id")]
    public int MapeoCategoriaId { get; set; }

    [JsonPropertyName("nodo_conexion_id")]
    public int NodoConexionId { get; set; }

    [JsonPropertyName("codigo_origen")]
    public string CodigoOrigen { get; set; } = string.Empty;

    [JsonPropertyName("nombre_origen")]
    public string? NombreOrigen { get; set; }

    [JsonPropertyName("categoria_destino")]
    public string CategoriaDestino { get; set; } = "OTHER";

    [JsonPropertyName("familia_destino")]
    public string? FamiliaDestino { get; set; }

    [JsonPropertyName("subfamilia_destino")]
    public string? SubfamiliaDestino { get; set; }

    [JsonPropertyName("verificado")]
    public bool Verificado { get; set; }
}

/// <summary>
/// DTO para mapeo de medio de pago
/// </summary>
public class MapeoMedioPagoDto
{
    [JsonPropertyName("mapeo_medio_pago_id")]
    public int MapeoMedioPagoId { get; set; }

    [JsonPropertyName("nodo_conexion_id")]
    public int NodoConexionId { get; set; }

    [JsonPropertyName("codigo_origen")]
    public string CodigoOrigen { get; set; } = string.Empty;

    [JsonPropertyName("nombre_origen")]
    public string? NombreOrigen { get; set; }

    [JsonPropertyName("medio_pago_destino")]
    public string MedioPagoDestino { get; set; } = "OTHER";

    [JsonPropertyName("marca_tarjeta")]
    public string? MarcaTarjeta { get; set; }

    [JsonPropertyName("verificado")]
    public bool Verificado { get; set; }
}

/// <summary>
/// DTO para valores no mapeados (pendientes)
/// </summary>
public class ValorNoMapeadoDto
{
    [JsonPropertyName("valor_no_mapeado_id")]
    public int ValorNoMapeadoId { get; set; }

    [JsonPropertyName("nodo_conexion_id")]
    public int NodoConexionId { get; set; }

    [JsonPropertyName("tipo_mapeo")]
    public string TipoMapeo { get; set; } = string.Empty;

    [JsonPropertyName("codigo_origen")]
    public string CodigoOrigen { get; set; } = string.Empty;

    [JsonPropertyName("nombre_origen")]
    public string? NombreOrigen { get; set; }

    [JsonPropertyName("ocurrencias")]
    public int Ocurrencias { get; set; }

    [JsonPropertyName("primera_vez")]
    public DateTime PrimeraVez { get; set; }

    [JsonPropertyName("ultima_vez")]
    public DateTime UltimaVez { get; set; }
}

/// <summary>
/// DTO para log de ejecucion
/// </summary>
public class EjecucionNodoDto
{
    [JsonPropertyName("ejecucion_nodo_id")]
    public long EjecucionNodoId { get; set; }

    [JsonPropertyName("nodo_conexion_id")]
    public int NodoConexionId { get; set; }

    [JsonPropertyName("ejecucion_id")]
    public Guid EjecucionId { get; set; }

    [JsonPropertyName("inicio_ejecucion")]
    public DateTime InicioEjecucion { get; set; }

    [JsonPropertyName("fin_ejecucion")]
    public DateTime? FinEjecucion { get; set; }

    [JsonPropertyName("fecha_negocio")]
    public DateTime FechaNegocio { get; set; }

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("modo_ejecucion")]
    public string? ModoEjecucion { get; set; }

    [JsonPropertyName("tickets_procesados")]
    public int TicketsProcesados { get; set; }

    [JsonPropertyName("lineas_procesadas")]
    public int LineasProcesadas { get; set; }

    [JsonPropertyName("warnings_count")]
    public int WarningsCount { get; set; }

    [JsonPropertyName("errors_count")]
    public int ErrorsCount { get; set; }

    [JsonPropertyName("batch_id")]
    public string? BatchId { get; set; }

    [JsonPropertyName("batch_estado")]
    public string? BatchEstado { get; set; }

    [JsonPropertyName("api_version_origen")]
    public string? ApiVersionOrigen { get; set; }
}

/// <summary>
/// DTO con detalle completo del nodo (incluye mapeos y ejecuciones)
/// </summary>
public class NodoDetalleDto
{
    [JsonPropertyName("nodo")]
    public NodoConexionDto Nodo { get; set; } = null!;

    [JsonPropertyName("configuracion")]
    public NodoConfiguracionDto? Configuracion { get; set; }

    [JsonPropertyName("mapeos_categoria")]
    public List<MapeoCategoriaDto> MapeosCategoria { get; set; } = new();

    [JsonPropertyName("mapeos_medio_pago")]
    public List<MapeoMedioPagoDto> MapeosMedioPago { get; set; } = new();

    [JsonPropertyName("valores_no_mapeados")]
    public List<ValorNoMapeadoDto> ValoresNoMapeados { get; set; } = new();

    [JsonPropertyName("ultimas_ejecuciones")]
    public List<EjecucionNodoDto> UltimasEjecuciones { get; set; } = new();
}

/// <summary>
/// DTO para actualizar estado de un nodo
/// </summary>
public class UpdateNodoEstadoDto
{
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;
}

/// <summary>
/// Categorias de producto estandar
/// </summary>
public static class CategoriasProducto
{
    public static readonly string[] Valores = {
        "STARTER", "MAIN_COURSE", "SIDE_DISH", "DESSERT",
        "COFFEE", "BEVERAGE", "WINE", "COCKTAIL", "OTHER"
    };
}

/// <summary>
/// Medios de pago estandar
/// </summary>
public static class MediosPago
{
    public static readonly string[] Valores = {
        "CASH", "DEBIT_CARD", "CREDIT_CARD", "BANK_TRANSFER",
        "QR", "MERCADO_PAGO", "DELIVERY_APP", "OTHER"
    };
}
