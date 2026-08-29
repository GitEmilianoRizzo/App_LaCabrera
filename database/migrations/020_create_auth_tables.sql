/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 020_create_auth_tables.sql
  Descripcion: Crea tablas de autenticación y autorización
  Autor: Claude Code
  Fecha: 2026-08-27

  TABLAS:
  - auth.Roles: Roles del sistema (Administrador, Gerencia, Comercial)
  - auth.Users: Usuarios del sistema
  - auth.RefreshTokens: Tokens de refresco para JWT
  - auth.UserSessions: Sesiones activas (opcional, para auditoría)

================================================================================
*/

USE [LaCabreraDB]
GO

-- ============================================================================
-- ESQUEMA: auth (Autenticación)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'auth')
BEGIN
    EXEC('CREATE SCHEMA [auth] AUTHORIZATION [dbo]')
    PRINT 'Esquema [auth] creado.'
END
GO

-- ============================================================================
-- TABLA: auth.Roles
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[auth].[Roles]') AND type = 'U')
BEGIN
    CREATE TABLE [auth].[Roles] (
        RolId           INT IDENTITY(1,1) PRIMARY KEY,
        Codigo          NVARCHAR(50) NOT NULL UNIQUE,
        Nombre          NVARCHAR(100) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Activo          BIT NOT NULL DEFAULT 1,
        FechaCreacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion DATETIME2 NULL
    )
    PRINT 'Tabla [auth].[Roles] creada.'
END
GO

-- ============================================================================
-- TABLA: auth.Users
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[auth].[Users]') AND type = 'U')
BEGIN
    CREATE TABLE [auth].[Users] (
        UserId              INT IDENTITY(1,1) PRIMARY KEY,
        Email               NVARCHAR(256) NOT NULL UNIQUE,
        EmailConfirmado     BIT NOT NULL DEFAULT 0,
        PasswordHash        NVARCHAR(500) NULL,  -- NULL si usa solo Google
        Nombre              NVARCHAR(200) NOT NULL,
        Apellido            NVARCHAR(200) NULL,
        RolId               INT NOT NULL,

        -- Google OAuth (Sin UNIQUE aquí - se usa índice filtrado para permitir múltiples NULLs)
        GoogleId            NVARCHAR(256) NULL,
        GoogleEmail         NVARCHAR(256) NULL,
        GooglePictureUrl    NVARCHAR(1000) NULL,

        -- Estado
        Activo              BIT NOT NULL DEFAULT 1,
        Bloqueado           BIT NOT NULL DEFAULT 0,
        IntentosFallidos    INT NOT NULL DEFAULT 0,
        FechaUltimoAcceso   DATETIME2 NULL,

        -- Auditoría
        FechaCreacion       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2 NULL,
        CreadoPor           INT NULL,

        -- FK
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RolId) REFERENCES [auth].[Roles](RolId)
    )

    -- Índices
    CREATE INDEX IX_Users_Email ON [auth].[Users](Email)
    -- Índice UNIQUE filtrado para GoogleId - permite múltiples NULLs pero IDs únicos cuando no son NULL
    CREATE UNIQUE INDEX IX_Users_GoogleId ON [auth].[Users](GoogleId) WHERE GoogleId IS NOT NULL
    CREATE INDEX IX_Users_RolId ON [auth].[Users](RolId)

    PRINT 'Tabla [auth].[Users] creada.'
END
GO

-- ============================================================================
-- TABLA: auth.RefreshTokens
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[auth].[RefreshTokens]') AND type = 'U')
BEGIN
    CREATE TABLE [auth].[RefreshTokens] (
        RefreshTokenId  INT IDENTITY(1,1) PRIMARY KEY,
        UserId          INT NOT NULL,
        Token           NVARCHAR(500) NOT NULL UNIQUE,
        FechaExpiracion DATETIME2 NOT NULL,
        FechaCreacion   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FechaRevocacion DATETIME2 NULL,
        ReemplazadoPor  NVARCHAR(500) NULL,
        IpAddress       NVARCHAR(50) NULL,
        UserAgent       NVARCHAR(500) NULL,

        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES [auth].[Users](UserId) ON DELETE CASCADE
    )

    CREATE INDEX IX_RefreshTokens_UserId ON [auth].[RefreshTokens](UserId)
    CREATE INDEX IX_RefreshTokens_Token ON [auth].[RefreshTokens](Token)

    PRINT 'Tabla [auth].[RefreshTokens] creada.'
END
GO

-- ============================================================================
-- TABLA: auth.AuditLog (Auditoría de acciones)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[auth].[AuditLog]') AND type = 'U')
BEGIN
    CREATE TABLE [auth].[AuditLog] (
        AuditLogId      BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId          INT NULL,
        Accion          NVARCHAR(100) NOT NULL,  -- LOGIN, LOGOUT, LOGIN_FAILED, PASSWORD_CHANGE, etc.
        Detalle         NVARCHAR(MAX) NULL,
        IpAddress       NVARCHAR(50) NULL,
        UserAgent       NVARCHAR(500) NULL,
        Fecha           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserId) REFERENCES [auth].[Users](UserId)
    )

    CREATE INDEX IX_AuditLog_UserId ON [auth].[AuditLog](UserId)
    CREATE INDEX IX_AuditLog_Fecha ON [auth].[AuditLog](Fecha DESC)

    PRINT 'Tabla [auth].[AuditLog] creada.'
END
GO

-- ============================================================================
-- DATOS INICIALES: Roles
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [auth].[Roles] WHERE Codigo = 'ADMIN')
BEGIN
    INSERT INTO [auth].[Roles] (Codigo, Nombre, Descripcion)
    VALUES
        ('ADMIN', 'Administrador', 'Acceso total al sistema. Gestiona usuarios y configuración.'),
        ('GERENCIA', 'Gerencia', 'Acceso a reportes y dashboards gerenciales.'),
        ('COMERCIAL', 'Comercial', 'Acceso a información comercial y ventas.')

    PRINT 'Roles iniciales insertados.'
END
GO

-- ============================================================================
-- USUARIO ADMIN INICIAL (password: Admin123!)
-- Hash generado con BCrypt - cambiar en producción
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM [auth].[Users] WHERE Email = 'admin@lacabrera.com')
BEGIN
    INSERT INTO [auth].[Users] (Email, EmailConfirmado, PasswordHash, Nombre, Apellido, RolId, Activo)
    VALUES (
        'admin@lacabrera.com',
        1,
        '$2a$11$kso9L8mjFkKsK/YL7ETBqeHFakN56pONNu.2SokCNbfbEErzjQJoK',  -- Admin123!
        'Administrador',
        'Sistema',
        (SELECT RolId FROM [auth].[Roles] WHERE Codigo = 'ADMIN'),
        1
    )

    PRINT 'Usuario admin inicial creado (password: Admin123! - CAMBIAR EN PRODUCCION).'
END
GO

PRINT '========================================'
PRINT 'Migración 020_create_auth_tables completada.'
PRINT '========================================'
GO
