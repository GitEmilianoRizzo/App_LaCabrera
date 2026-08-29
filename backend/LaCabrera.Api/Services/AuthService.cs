using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LaCabrera.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, string? ipAddress, string? userAgent);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent);
    Task<UserDto> RegisterUserAsync(RegisterRequest request, int? createdByUserId);
    Task RevokeTokenAsync(string refreshToken);
    Task<UserDto?> GetCurrentUserAsync(int userId);
    Task<IEnumerable<UserListDto>> GetAllUsersAsync();
    Task<IEnumerable<RolDto>> GetAllRolesAsync();
    Task UpdateUserAsync(int userId, UpdateUserRequest request);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task DeleteUserAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly JwtSettings _jwtSettings;
    private readonly GoogleSettings _googleSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAuthRepository authRepository,
        IOptions<JwtSettings> jwtSettings,
        IOptions<GoogleSettings> googleSettings,
        ILogger<AuthService> logger)
    {
        _authRepository = authRepository;
        _jwtSettings = jwtSettings.Value;
        _googleSettings = googleSettings.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
    {
        var user = await _authRepository.GetUserByEmailAsync(request.Email);

        if (user == null)
        {
            await _authRepository.LogAuditAsync(null, "LOGIN_FAILED", $"Email no encontrado: {request.Email}", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }

        if (!user.Activo)
        {
            await _authRepository.LogAuditAsync(user.UserId, "LOGIN_FAILED", "Usuario inactivo", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Usuario inactivo");
        }

        if (user.Bloqueado)
        {
            await _authRepository.LogAuditAsync(user.UserId, "LOGIN_FAILED", "Usuario bloqueado", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Usuario bloqueado por múltiples intentos fallidos. Contacte al administrador.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            await _authRepository.LogAuditAsync(user.UserId, "LOGIN_FAILED", "Usuario solo tiene Google auth", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Este usuario solo puede acceder con Google");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _authRepository.IncrementFailedAttemptsAsync(user.UserId);
            await _authRepository.LogAuditAsync(user.UserId, "LOGIN_FAILED", "Contraseña incorrecta", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }

        // Login exitoso
        await _authRepository.ResetFailedAttemptsAsync(user.UserId);
        await _authRepository.UpdateLastAccessAsync(user.UserId);
        await _authRepository.LogAuditAsync(user.UserId, "LOGIN", "Login con email/password", ipAddress, userAgent);

        return await GenerateAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, string? ipAddress, string? userAgent)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleSettings.ClientId }
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Token de Google inválido");
            throw new UnauthorizedAccessException("Token de Google inválido");
        }

        // Buscar usuario por GoogleId o Email
        var user = await _authRepository.GetUserByGoogleIdAsync(payload.Subject);

        if (user == null)
        {
            // Buscar por email
            user = await _authRepository.GetUserByEmailAsync(payload.Email);

            if (user != null)
            {
                // Vincular cuenta de Google a usuario existente
                user.GoogleId = payload.Subject;
                user.GoogleEmail = payload.Email;
                user.GooglePictureUrl = payload.Picture;
                await _authRepository.UpdateUserAsync(user);

                await _authRepository.LogAuditAsync(user.UserId, "GOOGLE_LINKED", "Cuenta de Google vinculada", ipAddress, userAgent);
            }
        }

        if (user == null)
        {
            // Usuario no existe - no permitir auto-registro por Google
            await _authRepository.LogAuditAsync(null, "GOOGLE_LOGIN_FAILED", $"Email no registrado: {payload.Email}", ipAddress, userAgent);
            throw new UnauthorizedAccessException("No existe una cuenta asociada a este email. Contacte al administrador para crear su cuenta.");
        }

        if (!user.Activo)
        {
            await _authRepository.LogAuditAsync(user.UserId, "GOOGLE_LOGIN_FAILED", "Usuario inactivo", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Usuario inactivo");
        }

        // Login exitoso
        await _authRepository.UpdateLastAccessAsync(user.UserId);
        await _authRepository.LogAuditAsync(user.UserId, "LOGIN", "Login con Google", ipAddress, userAgent);

        return await GenerateAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        var storedToken = await _authRepository.GetRefreshTokenAsync(refreshToken);

        if (storedToken == null)
        {
            throw new UnauthorizedAccessException("Token inválido");
        }

        if (storedToken.IsRevoked)
        {
            // Posible reuso de token - revocar todos los tokens del usuario
            await _authRepository.RevokeAllUserTokensAsync(storedToken.UserId);
            await _authRepository.LogAuditAsync(storedToken.UserId, "TOKEN_REUSE_DETECTED", "Posible robo de token detectado", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Token inválido");
        }

        if (storedToken.IsExpired)
        {
            throw new UnauthorizedAccessException("Token expirado");
        }

        var user = await _authRepository.GetUserByIdAsync(storedToken.UserId);

        if (user == null || !user.Activo)
        {
            throw new UnauthorizedAccessException("Usuario no válido");
        }

        // Revocar token actual y generar nuevo
        var newRefreshToken = GenerateRefreshToken();
        await _authRepository.RevokeRefreshTokenAsync(refreshToken, newRefreshToken);

        // Guardar nuevo refresh token
        var newToken = new RefreshToken
        {
            UserId = user.UserId,
            Token = newRefreshToken,
            FechaExpiracion = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        await _authRepository.SaveRefreshTokenAsync(newToken);

        var accessToken = GenerateJwtToken(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
            User = MapToUserDto(user)
        };
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var storedToken = await _authRepository.GetRefreshTokenAsync(refreshToken);

        if (storedToken != null && storedToken.IsActive)
        {
            await _authRepository.RevokeRefreshTokenAsync(refreshToken);
        }
    }

    public async Task<UserDto> RegisterUserAsync(RegisterRequest request, int? createdByUserId)
    {
        // Verificar email único
        if (await _authRepository.EmailExistsAsync(request.Email))
        {
            throw new InvalidOperationException("Ya existe un usuario con este email");
        }

        // Obtener rol
        var rol = await _authRepository.GetRolByCodigoAsync(request.RolCodigo);
        if (rol == null)
        {
            throw new InvalidOperationException($"Rol '{request.RolCodigo}' no existe");
        }

        // Crear usuario
        var user = new User
        {
            Email = request.Email,
            EmailConfirmado = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            RolId = rol.RolId,
            Activo = true
        };

        user.UserId = await _authRepository.CreateUserAsync(user);
        user.RolCodigo = rol.Codigo;
        user.RolNombre = rol.Nombre;

        await _authRepository.LogAuditAsync(createdByUserId, "USER_CREATED", $"Usuario creado: {user.Email}", null, null);

        return MapToUserDto(user);
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var user = await _authRepository.GetUserByIdAsync(userId);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<IEnumerable<UserListDto>> GetAllUsersAsync()
    {
        return await _authRepository.GetAllUsersAsync();
    }

    public async Task<IEnumerable<RolDto>> GetAllRolesAsync()
    {
        var roles = await _authRepository.GetAllRolesAsync();
        return roles.Select(r => new RolDto
        {
            RolId = r.RolId,
            Codigo = r.Codigo,
            Nombre = r.Nombre,
            Descripcion = r.Descripcion
        });
    }

    public async Task UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        var user = await _authRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario no encontrado");
        }

        if (!string.IsNullOrEmpty(request.Nombre))
            user.Nombre = request.Nombre;

        if (request.Apellido != null)
            user.Apellido = request.Apellido;

        if (!string.IsNullOrEmpty(request.RolCodigo))
        {
            var rol = await _authRepository.GetRolByCodigoAsync(request.RolCodigo);
            if (rol == null)
                throw new InvalidOperationException($"Rol '{request.RolCodigo}' no existe");
            user.RolId = rol.RolId;
        }

        if (request.Activo.HasValue)
            user.Activo = request.Activo.Value;

        await _authRepository.UpdateUserAsync(user);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _authRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario no encontrado");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash) && !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Contraseña actual incorrecta");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _authRepository.UpdateUserAsync(user);

        // Revocar todos los refresh tokens
        await _authRepository.RevokeAllUserTokensAsync(userId);

        await _authRepository.LogAuditAsync(userId, "PASSWORD_CHANGED", null, null, null);
    }

    public async Task DeleteUserAsync(int userId)
    {
        var user = await _authRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario no encontrado");
        }

        // Soft delete
        user.Activo = false;
        await _authRepository.UpdateUserAsync(user);
        await _authRepository.RevokeAllUserTokensAsync(userId);

        await _authRepository.LogAuditAsync(userId, "USER_DELETED", $"Usuario desactivado: {user.Email}", null, null);
    }

    #region Private Methods

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, string? ipAddress, string? userAgent)
    {
        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Guardar refresh token
        var token = new RefreshToken
        {
            UserId = user.UserId,
            Token = refreshToken,
            FechaExpiracion = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        await _authRepository.SaveRefreshTokenAsync(token);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
            User = MapToUserDto(user)
        };
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Nombre),
            new Claim(ClaimTypes.Role, user.RolCodigo ?? "COMERCIAL"),
            new Claim("rol_id", user.RolId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            Nombre = user.Nombre,
            Apellido = user.Apellido,
            GooglePictureUrl = user.GooglePictureUrl,
            Activo = user.Activo,
            FechaUltimoAcceso = user.FechaUltimoAcceso,
            FechaCreacion = user.FechaCreacion,
            Rol = new RolDto
            {
                RolId = user.RolId,
                Codigo = user.RolCodigo ?? "",
                Nombre = user.RolNombre ?? ""
            }
        };
    }

    #endregion
}

#region Settings Classes

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public class GoogleSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

#endregion
