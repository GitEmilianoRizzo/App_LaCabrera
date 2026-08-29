using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Repositories;

public interface IAuthRepository
{
    // Users
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByGoogleIdAsync(string googleId);
    Task<IEnumerable<UserListDto>> GetAllUsersAsync();
    Task<int> CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task UpdateLastAccessAsync(int userId);
    Task IncrementFailedAttemptsAsync(int userId);
    Task ResetFailedAttemptsAsync(int userId);
    Task<bool> EmailExistsAsync(string email);

    // Roles
    Task<IEnumerable<Rol>> GetAllRolesAsync();
    Task<Rol?> GetRolByCodigoAsync(string codigo);

    // Refresh Tokens
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task SaveRefreshTokenAsync(RefreshToken refreshToken);
    Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null);
    Task RevokeAllUserTokensAsync(int userId);

    // Audit
    Task LogAuditAsync(int? userId, string accion, string? detalle, string? ipAddress, string? userAgent);
}

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Users

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.UserId, u.Email, u.EmailConfirmado, u.PasswordHash,
                u.Nombre, u.Apellido, u.RolId,
                u.GoogleId, u.GoogleEmail, u.GooglePictureUrl,
                u.Activo, u.Bloqueado, u.IntentosFallidos,
                u.FechaUltimoAcceso, u.FechaCreacion, u.FechaModificacion,
                r.Codigo AS RolCodigo, r.Nombre AS RolNombre
            FROM auth.Users u
            INNER JOIN auth.Roles r ON u.RolId = r.RolId
            WHERE u.Email = @Email";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.UserId, u.Email, u.EmailConfirmado, u.PasswordHash,
                u.Nombre, u.Apellido, u.RolId,
                u.GoogleId, u.GoogleEmail, u.GooglePictureUrl,
                u.Activo, u.Bloqueado, u.IntentosFallidos,
                u.FechaUltimoAcceso, u.FechaCreacion, u.FechaModificacion,
                r.Codigo AS RolCodigo, r.Nombre AS RolNombre
            FROM auth.Users u
            INNER JOIN auth.Roles r ON u.RolId = r.RolId
            WHERE u.UserId = @UserId";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
    }

    public async Task<User?> GetUserByGoogleIdAsync(string googleId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.UserId, u.Email, u.EmailConfirmado, u.PasswordHash,
                u.Nombre, u.Apellido, u.RolId,
                u.GoogleId, u.GoogleEmail, u.GooglePictureUrl,
                u.Activo, u.Bloqueado, u.IntentosFallidos,
                u.FechaUltimoAcceso, u.FechaCreacion, u.FechaModificacion,
                r.Codigo AS RolCodigo, r.Nombre AS RolNombre
            FROM auth.Users u
            INNER JOIN auth.Roles r ON u.RolId = r.RolId
            WHERE u.GoogleId = @GoogleId";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { GoogleId = googleId });
    }

    public async Task<IEnumerable<UserListDto>> GetAllUsersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                u.UserId, u.Email, u.Nombre, u.Apellido,
                r.Codigo AS RolCodigo, r.Nombre AS RolNombre,
                u.Activo,
                CASE WHEN u.GoogleId IS NOT NULL THEN 1 ELSE 0 END AS TieneGoogle,
                u.FechaUltimoAcceso, u.FechaCreacion
            FROM auth.Users u
            INNER JOIN auth.Roles r ON u.RolId = r.RolId
            ORDER BY u.Nombre, u.Apellido";

        return await connection.QueryAsync<UserListDto>(sql);
    }

    public async Task<int> CreateUserAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO auth.Users (
                Email, EmailConfirmado, PasswordHash, Nombre, Apellido, RolId,
                GoogleId, GoogleEmail, GooglePictureUrl, Activo
            )
            OUTPUT INSERTED.UserId
            VALUES (
                @Email, @EmailConfirmado, @PasswordHash, @Nombre, @Apellido, @RolId,
                @GoogleId, @GoogleEmail, @GooglePictureUrl, @Activo
            )";

        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task UpdateUserAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE auth.Users SET
                Nombre = @Nombre,
                Apellido = @Apellido,
                RolId = @RolId,
                GoogleId = @GoogleId,
                GoogleEmail = @GoogleEmail,
                GooglePictureUrl = @GooglePictureUrl,
                Activo = @Activo,
                Bloqueado = @Bloqueado,
                PasswordHash = @PasswordHash,
                FechaModificacion = GETUTCDATE()
            WHERE UserId = @UserId";

        await connection.ExecuteAsync(sql, user);
    }

    public async Task UpdateLastAccessAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "UPDATE auth.Users SET FechaUltimoAcceso = GETUTCDATE() WHERE UserId = @UserId";
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task IncrementFailedAttemptsAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE auth.Users
            SET IntentosFallidos = IntentosFallidos + 1,
                Bloqueado = CASE WHEN IntentosFallidos >= 4 THEN 1 ELSE 0 END
            WHERE UserId = @UserId";

        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task ResetFailedAttemptsAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "UPDATE auth.Users SET IntentosFallidos = 0, Bloqueado = 0 WHERE UserId = @UserId";
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(1) FROM auth.Users WHERE Email = @Email";
        return await connection.ExecuteScalarAsync<int>(sql, new { Email = email }) > 0;
    }

    #endregion

    #region Roles

    public async Task<IEnumerable<Rol>> GetAllRolesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT RolId, Codigo, Nombre, Descripcion, Activo FROM auth.Roles WHERE Activo = 1 ORDER BY Nombre";
        return await connection.QueryAsync<Rol>(sql);
    }

    public async Task<Rol?> GetRolByCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT RolId, Codigo, Nombre, Descripcion, Activo FROM auth.Roles WHERE Codigo = @Codigo";
        return await connection.QueryFirstOrDefaultAsync<Rol>(sql, new { Codigo = codigo });
    }

    #endregion

    #region Refresh Tokens

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT RefreshTokenId, UserId, Token, FechaExpiracion, FechaCreacion,
                   FechaRevocacion, ReemplazadoPor, IpAddress, UserAgent
            FROM auth.RefreshTokens
            WHERE Token = @Token";

        return await connection.QueryFirstOrDefaultAsync<RefreshToken>(sql, new { Token = token });
    }

    public async Task SaveRefreshTokenAsync(RefreshToken refreshToken)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO auth.RefreshTokens (UserId, Token, FechaExpiracion, IpAddress, UserAgent)
            VALUES (@UserId, @Token, @FechaExpiracion, @IpAddress, @UserAgent)";

        await connection.ExecuteAsync(sql, refreshToken);
    }

    public async Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE auth.RefreshTokens
            SET FechaRevocacion = GETUTCDATE(), ReemplazadoPor = @ReplacedBy
            WHERE Token = @Token";

        await connection.ExecuteAsync(sql, new { Token = token, ReplacedBy = replacedByToken });
    }

    public async Task RevokeAllUserTokensAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE auth.RefreshTokens
            SET FechaRevocacion = GETUTCDATE()
            WHERE UserId = @UserId AND FechaRevocacion IS NULL";

        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    #endregion

    #region Audit

    public async Task LogAuditAsync(int? userId, string accion, string? detalle, string? ipAddress, string? userAgent)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO auth.AuditLog (UserId, Accion, Detalle, IpAddress, UserAgent)
            VALUES (@UserId, @Accion, @Detalle, @IpAddress, @UserAgent)";

        await connection.ExecuteAsync(sql, new { UserId = userId, Accion = accion, Detalle = detalle, IpAddress = ipAddress, UserAgent = userAgent });
    }

    #endregion
}
