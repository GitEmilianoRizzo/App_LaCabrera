using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

#region Request DTOs

public class LoginRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    [JsonPropertyName("credential")]
    public string Credential { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("apellido")]
    public string? Apellido { get; set; }

    [JsonPropertyName("rol_codigo")]
    public string RolCodigo { get; set; } = "COMERCIAL";
}

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [JsonPropertyName("current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [JsonPropertyName("apellido")]
    public string? Apellido { get; set; }

    [JsonPropertyName("rol_codigo")]
    public string? RolCodigo { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}

#endregion

#region Response DTOs

public class AuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("user")]
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("apellido")]
    public string? Apellido { get; set; }

    [JsonPropertyName("nombre_completo")]
    public string NombreCompleto => string.IsNullOrEmpty(Apellido) ? Nombre : $"{Nombre} {Apellido}";

    [JsonPropertyName("rol")]
    public RolDto Rol { get; set; } = null!;

    [JsonPropertyName("google_picture_url")]
    public string? GooglePictureUrl { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("fecha_ultimo_acceso")]
    public DateTime? FechaUltimoAcceso { get; set; }

    [JsonPropertyName("fecha_creacion")]
    public DateTime FechaCreacion { get; set; }
}

public class RolDto
{
    [JsonPropertyName("rol_id")]
    public int RolId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}

public class UserListDto
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("apellido")]
    public string? Apellido { get; set; }

    [JsonPropertyName("rol_codigo")]
    public string RolCodigo { get; set; } = string.Empty;

    [JsonPropertyName("rol_nombre")]
    public string RolNombre { get; set; } = string.Empty;

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("tiene_google")]
    public bool TieneGoogle { get; set; }

    [JsonPropertyName("fecha_ultimo_acceso")]
    public DateTime? FechaUltimoAcceso { get; set; }

    [JsonPropertyName("fecha_creacion")]
    public DateTime FechaCreacion { get; set; }
}

#endregion

#region Internal Models

public class User
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmado { get; set; }
    public string? PasswordHash { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Apellido { get; set; }
    public int RolId { get; set; }
    public string? GoogleId { get; set; }
    public string? GoogleEmail { get; set; }
    public string? GooglePictureUrl { get; set; }
    public bool Activo { get; set; }
    public bool Bloqueado { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? FechaUltimoAcceso { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Navigation
    public string? RolCodigo { get; set; }
    public string? RolNombre { get; set; }
}

public class Rol
{
    public int RolId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class RefreshToken
{
    public int RefreshTokenId { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime FechaExpiracion { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaRevocacion { get; set; }
    public string? ReemplazadoPor { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool IsExpired => DateTime.UtcNow >= FechaExpiracion;
    public bool IsRevoked => FechaRevocacion != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}

#endregion
