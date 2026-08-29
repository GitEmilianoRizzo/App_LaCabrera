using System.Security.Claims;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Login con email y contraseña
    /// </summary>
    [HttpPost("login")]
    [SwaggerOperation(Summary = "Login", Description = "Autenticación con email y contraseña")]
    [SwaggerResponse(200, "Login exitoso", typeof(AuthResponse))]
    [SwaggerResponse(401, "Credenciales inválidas")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();

            var response = await _authService.LoginAsync(request, ipAddress, userAgent);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Login con Google
    /// </summary>
    [HttpPost("google")]
    [SwaggerOperation(Summary = "Login con Google", Description = "Autenticación con token de Google")]
    [SwaggerResponse(200, "Login exitoso", typeof(AuthResponse))]
    [SwaggerResponse(401, "Token inválido o usuario no registrado")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();

            var response = await _authService.GoogleLoginAsync(request, ipAddress, userAgent);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Refrescar token de acceso
    /// </summary>
    [HttpPost("refresh")]
    [SwaggerOperation(Summary = "Refrescar token", Description = "Obtiene un nuevo access token usando el refresh token")]
    [SwaggerResponse(200, "Token refrescado", typeof(AuthResponse))]
    [SwaggerResponse(401, "Refresh token inválido o expirado")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();

            var response = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, userAgent);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cerrar sesión (revocar token)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [SwaggerOperation(Summary = "Logout", Description = "Revoca el refresh token actual")]
    [SwaggerResponse(200, "Sesión cerrada")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken);
        return Ok(new { message = "Sesión cerrada correctamente" });
    }

    /// <summary>
    /// Obtener usuario actual
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(Summary = "Usuario actual", Description = "Obtiene información del usuario autenticado")]
    [SwaggerResponse(200, "Datos del usuario", typeof(UserDto))]
    [SwaggerResponse(401, "No autenticado")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var user = await _authService.GetCurrentUserAsync(userId.Value);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    /// <summary>
    /// Cambiar contraseña
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [SwaggerOperation(Summary = "Cambiar contraseña", Description = "Cambia la contraseña del usuario autenticado")]
    [SwaggerResponse(200, "Contraseña cambiada")]
    [SwaggerResponse(401, "Contraseña actual incorrecta")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            await _authService.ChangePasswordAsync(userId.Value, request);
            return Ok(new { message = "Contraseña actualizada correctamente" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    #region Admin Endpoints

    /// <summary>
    /// Listar todos los usuarios (Admin)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(Summary = "Listar usuarios", Description = "Lista todos los usuarios del sistema (solo Admin)")]
    [SwaggerResponse(200, "Lista de usuarios", typeof(IEnumerable<UserListDto>))]
    public async Task<ActionResult<IEnumerable<UserListDto>>> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Crear usuario (Admin)
    /// </summary>
    [HttpPost("users")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(Summary = "Crear usuario", Description = "Crea un nuevo usuario (solo Admin)")]
    [SwaggerResponse(201, "Usuario creado", typeof(UserDto))]
    [SwaggerResponse(400, "Datos inválidos")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] RegisterRequest request)
    {
        try
        {
            var creatorId = GetCurrentUserId();
            var user = await _authService.RegisterUserAsync(request, creatorId);
            return CreatedAtAction(nameof(GetCurrentUser), new { id = user.UserId }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar usuario (Admin)
    /// </summary>
    [HttpPut("users/{userId}")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(Summary = "Actualizar usuario", Description = "Actualiza datos de un usuario (solo Admin)")]
    [SwaggerResponse(200, "Usuario actualizado")]
    [SwaggerResponse(404, "Usuario no encontrado")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            await _authService.UpdateUserAsync(userId, request);
            return Ok(new { message = "Usuario actualizado" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Desactivar usuario (Admin)
    /// </summary>
    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(Summary = "Desactivar usuario", Description = "Desactiva un usuario (soft delete) (solo Admin)")]
    [SwaggerResponse(200, "Usuario desactivado")]
    [SwaggerResponse(404, "Usuario no encontrado")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        try
        {
            await _authService.DeleteUserAsync(userId);
            return Ok(new { message = "Usuario desactivado" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Listar roles disponibles
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(Summary = "Listar roles", Description = "Lista todos los roles disponibles")]
    [SwaggerResponse(200, "Lista de roles", typeof(IEnumerable<RolDto>))]
    public async Task<ActionResult<IEnumerable<RolDto>>> GetAllRoles()
    {
        var roles = await _authService.GetAllRolesAsync();
        return Ok(roles);
    }

    #endregion

    #region Private Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
            return Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();

        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }

    #endregion
}
