using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/franquicias")]
[Produces("application/json")]
public class FranquiciaController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FranquiciaController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Obtiene todas las franquicias
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Listar franquicias", Description = "Obtiene todas las franquicias activas")]
    [SwaggerResponse(200, "Lista de franquicias", typeof(IEnumerable<FranquiciaDto>))]
    public async Task<ActionResult<IEnumerable<FranquiciaDto>>> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();

        var franquicias = await connection.QueryAsync<FranquiciaDto>(@"
            SELECT
                f.FranquiciaId,
                f.Codigo,
                f.Nombre,
                f.GrupoEconomicoId,
                ge.Nombre AS GrupoEconomicoNombre,
                f.Pais,
                f.Ciudad,
                f.ZonaHoraria,
                f.SistemaOrigen,
                f.ContactoNombre,
                f.ContactoEmail,
                f.ContactoTelefono,
                f.EstadoIntegracion,
                f.UltimaSincronizacion
            FROM dim.Franquicia f
            LEFT JOIN dim.GrupoEconomico ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
            WHERE f.Activo = 1
            ORDER BY f.Nombre");

        return Ok(franquicias);
    }

    /// <summary>
    /// Obtiene una franquicia por ID
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Obtener franquicia", Description = "Obtiene una franquicia por su ID")]
    [SwaggerResponse(200, "Franquicia encontrada", typeof(FranquiciaDto))]
    [SwaggerResponse(404, "Franquicia no encontrada")]
    public async Task<ActionResult<FranquiciaDto>> GetById(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var franquicia = await connection.QueryFirstOrDefaultAsync<FranquiciaDto>(@"
            SELECT
                f.FranquiciaId,
                f.Codigo,
                f.Nombre,
                f.GrupoEconomicoId,
                ge.Nombre AS GrupoEconomicoNombre,
                f.Pais,
                f.Ciudad,
                f.ZonaHoraria,
                f.SistemaOrigen,
                f.ContactoNombre,
                f.ContactoEmail,
                f.ContactoTelefono,
                f.EstadoIntegracion,
                f.UltimaSincronizacion
            FROM dim.Franquicia f
            LEFT JOIN dim.GrupoEconomico ge ON f.GrupoEconomicoId = ge.GrupoEconomicoId
            WHERE f.FranquiciaId = @Id AND f.Activo = 1",
            new { Id = id });

        if (franquicia == null)
        {
            return NotFound(new { message = $"Franquicia con ID {id} no encontrada" });
        }

        return Ok(franquicia);
    }

    /// <summary>
    /// Actualiza los datos de una franquicia
    /// </summary>
    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Actualizar franquicia", Description = "Actualiza los datos de una franquicia")]
    [SwaggerResponse(200, "Franquicia actualizada")]
    [SwaggerResponse(404, "Franquicia no encontrada")]
    public async Task<ActionResult> Update(int id, [FromBody] FranquiciaUpdateDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Verificar que existe
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dim.Franquicia WHERE FranquiciaId = @Id AND Activo = 1",
            new { Id = id });

        if (exists == 0)
        {
            return NotFound(new { message = $"Franquicia con ID {id} no encontrada" });
        }

        // Construir UPDATE dinámico solo con campos que vienen
        var updates = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        if (dto.Nombre != null)
        {
            updates.Add("Nombre = @Nombre");
            parameters.Add("Nombre", dto.Nombre);
        }
        if (dto.GrupoEconomicoId.HasValue)
        {
            updates.Add("GrupoEconomicoId = @GrupoEconomicoId");
            parameters.Add("GrupoEconomicoId", dto.GrupoEconomicoId);
        }
        if (dto.Pais != null)
        {
            updates.Add("Pais = @Pais");
            parameters.Add("Pais", dto.Pais);
        }
        if (dto.Ciudad != null)
        {
            updates.Add("Ciudad = @Ciudad");
            parameters.Add("Ciudad", dto.Ciudad);
        }
        if (dto.SistemaOrigen != null)
        {
            updates.Add("SistemaOrigen = @SistemaOrigen");
            parameters.Add("SistemaOrigen", dto.SistemaOrigen);
        }
        if (dto.ContactoNombre != null)
        {
            updates.Add("ContactoNombre = @ContactoNombre");
            parameters.Add("ContactoNombre", dto.ContactoNombre);
        }
        if (dto.ContactoEmail != null)
        {
            updates.Add("ContactoEmail = @ContactoEmail");
            parameters.Add("ContactoEmail", dto.ContactoEmail);
        }
        if (dto.ContactoTelefono != null)
        {
            updates.Add("ContactoTelefono = @ContactoTelefono");
            parameters.Add("ContactoTelefono", dto.ContactoTelefono);
        }
        if (dto.ZonaHoraria != null)
        {
            updates.Add("ZonaHoraria = @ZonaHoraria");
            parameters.Add("ZonaHoraria", dto.ZonaHoraria);
        }

        if (updates.Count == 0)
        {
            return BadRequest(new { message = "No se proporcionaron campos para actualizar" });
        }

        updates.Add("FechaModificacion = GETUTCDATE()");

        var sql = $"UPDATE dim.Franquicia SET {string.Join(", ", updates)} WHERE FranquiciaId = @Id";
        await connection.ExecuteAsync(sql, parameters);

        return Ok(new { message = "Franquicia actualizada", franquicia_id = id });
    }

    /// <summary>
    /// Obtiene todos los grupos económicos
    /// </summary>
    [HttpGet("/api/v1/grupos-economicos")]
    [SwaggerOperation(Summary = "Listar grupos económicos", Description = "Obtiene todos los grupos económicos activos")]
    [SwaggerResponse(200, "Lista de grupos", typeof(IEnumerable<GrupoEconomicoDto>))]
    public async Task<ActionResult<IEnumerable<GrupoEconomicoDto>>> GetGrupos()
    {
        using var connection = _connectionFactory.CreateConnection();

        var grupos = await connection.QueryAsync<GrupoEconomicoDto>(@"
            SELECT
                GrupoEconomicoId,
                Codigo,
                Nombre,
                Pais
            FROM dim.GrupoEconomico
            WHERE Activo = 1
            ORDER BY Nombre");

        return Ok(grupos);
    }
}
