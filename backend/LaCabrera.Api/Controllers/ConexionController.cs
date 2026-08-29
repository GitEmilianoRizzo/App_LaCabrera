using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/conexiones")]
[Produces("application/json")]
public class ConexionController : ControllerBase
{
    private readonly IConexionService _conexionService;
    private readonly IAgoraExtractorService _agoraExtractor;
    private readonly IVinsonExtractorService _vinsonExtractor;

    public ConexionController(
        IConexionService conexionService,
        IAgoraExtractorService agoraExtractor,
        IVinsonExtractorService vinsonExtractor)
    {
        _conexionService = conexionService;
        _agoraExtractor = agoraExtractor;
        _vinsonExtractor = vinsonExtractor;
    }

    /// <summary>
    /// Obtiene todos los nodos de conexion
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Listar conexiones", Description = "Obtiene todos los nodos de conexion configurados")]
    [SwaggerResponse(200, "Lista de nodos", typeof(IEnumerable<NodoConexionDto>))]
    public async Task<ActionResult<IEnumerable<NodoConexionDto>>> GetAllNodos()
    {
        var nodos = await _conexionService.GetAllNodosAsync();
        return Ok(nodos);
    }

    /// <summary>
    /// Obtiene un nodo por su ID
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Obtener conexion por ID", Description = "Obtiene un nodo de conexion por su ID")]
    [SwaggerResponse(200, "Nodo encontrado", typeof(NodoConexionDto))]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult<NodoConexionDto>> GetNodoById(int id)
    {
        var nodo = await _conexionService.GetNodoByIdAsync(id);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }
        return Ok(nodo);
    }

    /// <summary>
    /// Obtiene un nodo por su codigo
    /// </summary>
    [HttpGet("codigo/{codigo}")]
    [SwaggerOperation(Summary = "Obtener conexion por codigo", Description = "Obtiene un nodo de conexion por su codigo unico")]
    [SwaggerResponse(200, "Nodo encontrado", typeof(NodoConexionDto))]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult<NodoConexionDto>> GetNodoByCodigo(string codigo)
    {
        var nodo = await _conexionService.GetNodoByCodigoAsync(codigo);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con codigo '{codigo}' no encontrado" });
        }
        return Ok(nodo);
    }

    /// <summary>
    /// Obtiene el detalle completo de un nodo incluyendo mapeos y ejecuciones
    /// </summary>
    [HttpGet("{id:int}/detalle")]
    [SwaggerOperation(Summary = "Detalle completo de conexion", Description = "Obtiene el nodo con sus mapeos, valores pendientes y ultimas ejecuciones")]
    [SwaggerResponse(200, "Detalle del nodo", typeof(NodoDetalleDto))]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult<NodoDetalleDto>> GetNodoDetalle(int id)
    {
        try
        {
            var detalle = await _conexionService.GetNodoDetalleAsync(id);
            return Ok(detalle);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }
    }

    /// <summary>
    /// Crea un nuevo nodo de conexion
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Crear conexion", Description = "Crea un nuevo nodo de conexion")]
    [SwaggerResponse(201, "Nodo creado", typeof(object))]
    [SwaggerResponse(400, "Datos invalidos")]
    public async Task<ActionResult> CreateNodo([FromBody] NodoConexionCreateDto nodo)
    {
        var id = await _conexionService.CreateNodoAsync(nodo);
        return CreatedAtAction(nameof(GetNodoById), new { id }, new { nodo_conexion_id = id });
    }

    /// <summary>
    /// Actualiza el estado de un nodo
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    [SwaggerOperation(Summary = "Actualizar estado", Description = "Cambia el estado de un nodo (ACTIVE, PAUSED, ERROR, PENDING_CONFIG)")]
    [SwaggerResponse(200, "Estado actualizado")]
    [SwaggerResponse(400, "Estado invalido")]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult> UpdateNodoEstado(int id, [FromBody] UpdateNodoEstadoDto request)
    {
        var nodo = await _conexionService.GetNodoByIdAsync(id);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }

        try
        {
            await _conexionService.UpdateNodoEstadoAsync(id, request.Estado);
            return Ok(new { message = "Estado actualizado", nodo_conexion_id = id, estado = request.Estado });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea o actualiza un mapeo de categoria
    /// </summary>
    [HttpPut("{id:int}/mapeos/categoria")]
    [SwaggerOperation(Summary = "Upsert mapeo categoria", Description = "Crea o actualiza un mapeo de categoria de producto")]
    [SwaggerResponse(200, "Mapeo guardado", typeof(object))]
    [SwaggerResponse(400, "Datos invalidos")]
    public async Task<ActionResult> UpsertMapeoCategoria(int id, [FromBody] MapeoCategoriaDto mapeo)
    {
        mapeo.NodoConexionId = id;
        try
        {
            var mapeoId = await _conexionService.UpsertMapeoCategoriaAsync(mapeo);
            return Ok(new { mapeo_categoria_id = mapeoId, message = "Mapeo guardado" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea o actualiza un mapeo de medio de pago
    /// </summary>
    [HttpPut("{id:int}/mapeos/medio-pago")]
    [SwaggerOperation(Summary = "Upsert mapeo medio de pago", Description = "Crea o actualiza un mapeo de medio de pago")]
    [SwaggerResponse(200, "Mapeo guardado", typeof(object))]
    [SwaggerResponse(400, "Datos invalidos")]
    public async Task<ActionResult> UpsertMapeoMedioPago(int id, [FromBody] MapeoMedioPagoDto mapeo)
    {
        mapeo.NodoConexionId = id;
        try
        {
            var mapeoId = await _conexionService.UpsertMapeoMedioPagoAsync(mapeo);
            return Ok(new { mapeo_medio_pago_id = mapeoId, message = "Mapeo guardado" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Resuelve valores pendientes que ya tienen mapeo
    /// </summary>
    [HttpPost("{id:int}/resolver-pendientes")]
    [SwaggerOperation(Summary = "Resolver valores pendientes", Description = "Marca como resueltos los valores que ya tienen mapeo de categoria o medio de pago")]
    [SwaggerResponse(200, "Valores resueltos")]
    public async Task<ActionResult> ResolverValoresPendientes(int id)
    {
        var resueltos = await _conexionService.ResolverValoresYaMapeadosAsync(id);
        return Ok(new { message = $"{resueltos} valores marcados como resueltos", nodo_conexion_id = id, valores_resueltos = resueltos });
    }

    /// <summary>
    /// Actualiza los mapeos de campos de un nodo
    /// </summary>
    [HttpPut("{id:int}/field-mappings")]
    [SwaggerOperation(Summary = "Actualizar mapeos de campos", Description = "Actualiza la configuracion de mapeo de campos origen->destino")]
    [SwaggerResponse(200, "Mapeos actualizados")]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult> UpdateFieldMappings(int id, [FromBody] List<FieldMappingDto> mappings)
    {
        var nodo = await _conexionService.GetNodoByIdAsync(id);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }

        await _conexionService.UpdateFieldMappingsAsync(id, mappings);
        return Ok(new { message = "Mapeos actualizados", nodo_conexion_id = id, field_mappings_count = mappings.Count });
    }

    /// <summary>
    /// Ejecuta extraccion manual de datos del POS
    /// </summary>
    [HttpPost("{id:int}/ejecutar")]
    [SwaggerOperation(Summary = "Ejecutar extraccion", Description = "Ejecuta manualmente la extraccion de datos del POS para una fecha de negocio")]
    [SwaggerResponse(200, "Extraccion completada", typeof(EjecucionResultDto))]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult<EjecucionResultDto>> EjecutarExtraccion(int id, [FromQuery] DateTime? fechaNegocio = null)
    {
        var nodo = await _conexionService.GetNodoByIdAsync(id);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }

        if (nodo.Estado != "ACTIVE")
        {
            return BadRequest(new { message = $"Nodo no esta activo. Estado actual: {nodo.Estado}" });
        }

        EjecucionResultDto resultado;
        var tipoConector = nodo.TipoConector.ToUpperInvariant();

        if (tipoConector.StartsWith("AGORA"))
        {
            resultado = await _agoraExtractor.EjecutarExtraccionAsync(id, fechaNegocio);
        }
        else if (tipoConector.StartsWith("VINSON"))
        {
            resultado = await _vinsonExtractor.EjecutarExtraccionAsync(id, fechaNegocio);
        }
        else
        {
            return BadRequest(new { message = $"Tipo de conector '{nodo.TipoConector}' no soportado. Tipos validos: AGORA*, VINSON*" });
        }

        if (resultado.Success)
        {
            return Ok(resultado);
        }
        else
        {
            return StatusCode(500, resultado);
        }
    }

    /// <summary>
    /// Ejecuta extraccion de un rango de fechas
    /// </summary>
    [HttpPost("{id:int}/ejecutar-rango")]
    [SwaggerOperation(Summary = "Ejecutar extraccion de rango", Description = "Ejecuta extraccion de datos para un rango de fechas (catch-up)")]
    [SwaggerResponse(200, "Extraccion completada", typeof(EjecucionResultDto))]
    [SwaggerResponse(404, "Nodo no encontrado")]
    public async Task<ActionResult<EjecucionResultDto>> EjecutarExtraccionRango(
        int id,
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        var nodo = await _conexionService.GetNodoByIdAsync(id);
        if (nodo == null)
        {
            return NotFound(new { message = $"Nodo con id {id} no encontrado" });
        }

        if (nodo.Estado != "ACTIVE")
        {
            return BadRequest(new { message = $"Nodo no esta activo. Estado actual: {nodo.Estado}" });
        }

        if (fechaDesde > fechaHasta)
        {
            return BadRequest(new { message = "fechaDesde debe ser menor o igual a fechaHasta" });
        }

        EjecucionResultDto resultado;
        var tipoConector = nodo.TipoConector.ToUpperInvariant();

        if (tipoConector.StartsWith("AGORA"))
        {
            resultado = await _agoraExtractor.EjecutarExtraccionRangoAsync(id, fechaDesde, fechaHasta);
        }
        else if (tipoConector.StartsWith("VINSON"))
        {
            resultado = await _vinsonExtractor.EjecutarExtraccionRangoAsync(id, fechaDesde, fechaHasta);
        }
        else
        {
            return BadRequest(new { message = $"Tipo de conector '{nodo.TipoConector}' no soportado. Tipos validos: AGORA*, VINSON*" });
        }

        if (resultado.Success)
        {
            return Ok(resultado);
        }
        else
        {
            return StatusCode(500, resultado);
        }
    }

    /// <summary>
    /// Obtiene las categorias disponibles para mapeo
    /// </summary>
    [HttpGet("catalogos/categorias")]
    [SwaggerOperation(Summary = "Catalogo de categorias", Description = "Obtiene las categorias de producto estandar disponibles")]
    [SwaggerResponse(200, "Lista de categorias")]
    public ActionResult<string[]> GetCategorias()
    {
        return Ok(CategoriasProducto.Valores);
    }

    /// <summary>
    /// Obtiene los medios de pago disponibles para mapeo
    /// </summary>
    [HttpGet("catalogos/medios-pago")]
    [SwaggerOperation(Summary = "Catalogo de medios de pago", Description = "Obtiene los medios de pago estandar disponibles")]
    [SwaggerResponse(200, "Lista de medios de pago")]
    public ActionResult<string[]> GetMediosPago()
    {
        return Ok(MediosPago.Valores);
    }
}
