-- =====================================================
-- Alta de API keys de TESTING para Manila (T-380) en api.ApiKeyFranquicia.
--
-- - La key la genera SQL Server al azar (CRYPT_GEN_RANDOM): no queda en el repo ni en la tarea.
-- - Re-ejecutable: si la franquicia ya tiene una key de este alta activa y vigente, no crea otra
--   ni la vuelve a mostrar.
-- - La salida (franquicia=key) se manda a un archivo con permisos 600 y se le pasa a Emiliano por canal
--   seguro. Ejemplo en pill02:
--     umask 077; sqlcmd ... -h -1 -W -i database/scripts/alta_api_keys_testing_manila.sql > .dev/salida/T-380_api_keys_testing.txt
-- - Los códigos de franquicia definitivos los confirma el proveedor: ajustar @Franquicias si cambian.
-- - Solo ambientes de desarrollo o testing. En producción, únicamente con autorización explícita de Emiliano.
-- =====================================================
SET NOCOUNT ON;

DECLARE @Usuario NVARCHAR(200) = N'ALTA_TESTING_T380';
DECLARE @DiasVigencia INT = 90;
DECLARE @Franquicias TABLE (Codigo NVARCHAR(50) PRIMARY KEY);
INSERT INTO @Franquicias (Codigo) VALUES (N'MANILA_SHANGRILA'), (N'MANILA_AYALA');

DECLARE @Creadas TABLE (Codigo NVARCHAR(50), ApiKey NVARCHAR(200));

DECLARE @Codigo NVARCHAR(50), @FranquiciaId INT, @Key NVARCHAR(200);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT f.Codigo, df.FranquiciaId
    FROM @Franquicias f
    JOIN dim.Franquicia df ON df.Codigo = f.Codigo;
OPEN c;
FETCH NEXT FROM c INTO @Codigo, @FranquiciaId;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM api.ApiKeyFranquicia
        WHERE FranquiciaId = @FranquiciaId AND UsuarioCreacion = @Usuario AND Activo = 1
          AND (FechaExpiracion IS NULL OR FechaExpiracion > SYSUTCDATETIME()))
    BEGIN
        SET @Key = N'LC-TEST-' + @Codigo + N'-' + LOWER(CONVERT(NVARCHAR(64), CRYPT_GEN_RANDOM(24), 2));
        INSERT INTO api.ApiKeyFranquicia
            (FranquiciaId, ApiKey, Nombre, FechaExpiracion, ContadorUsos, FechaCreacion, FechaModificacion,
             UsuarioCreacion, UsuarioModificacion, Activo)
        VALUES
            (@FranquiciaId, @Key, N'Testing Aquarius POS (T-380)', DATEADD(DAY, @DiasVigencia, SYSUTCDATETIME()), 0,
             SYSUTCDATETIME(), SYSUTCDATETIME(), @Usuario, @Usuario, 1);
        INSERT INTO @Creadas VALUES (@Codigo, @Key);
    END
    FETCH NEXT FROM c INTO @Codigo, @FranquiciaId;
END
CLOSE c; DEALLOCATE c;

IF EXISTS (SELECT 1 FROM @Creadas)
BEGIN
    PRINT 'API keys de TESTING para POST /api/v1/sales/daily-batch (header X-API-KEY). Vencen a los ' + CAST(@DiasVigencia AS VARCHAR) + ' días.';
    SELECT Codigo + N'=' + ApiKey FROM @Creadas ORDER BY Codigo;
END
ELSE
    PRINT 'Sin altas nuevas: las franquicias ya tienen una key de testing vigente (o el código no existe en dim.Franquicia).';

SELECT f.Codigo + N': ' + CASE WHEN df.FranquiciaId IS NULL THEN N'NO EXISTE en dim.Franquicia' ELSE N'ok' END
FROM @Franquicias f LEFT JOIN dim.Franquicia df ON df.Codigo = f.Codigo
WHERE df.FranquiciaId IS NULL;
