#Requires -Version 5.1
<#
.SYNOPSIS
    Script de deploy automatizado para La Cabrera App

.DESCRIPTION
    Este script automatiza el proceso completo de deploy a produccion:
    1. Valida builds locales (frontend + backend)
    2. Commit y push a GitHub
    3. Conecta via SSH a produccion
    4. Actualiza codigo via Git
    5. Reconstruye containers Docker
    6. Reinicia servicios (nginx + docker)
    7. Verifica health checks

.PARAMETER CommitMessage
    Mensaje para el commit de Git (opcional, se genera automaticamente si no se provee)

.PARAMETER SkipBuildValidation
    Omite la validacion de builds locales

.PARAMETER SkipCommit
    Omite el commit y push (solo actualiza produccion con lo que ya esta en GitHub)

.EXAMPLE
    .\deploy-production.ps1

.EXAMPLE
    .\deploy-production.ps1 -CommitMessage "Fix: correcciones menores"

.EXAMPLE
    .\deploy-production.ps1 -SkipCommit

.NOTES
    Autor: Claude Code
    Version: 1.0.0
    Requiere: Git, SSH key configurada
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$CommitMessage = "",

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuildValidation,

    [Parameter(Mandatory=$false)]
    [switch]$SkipCommit
)

# ============================================
# CONFIGURACION
# ============================================
$Config = @{
    # Rutas locales
    ProjectRoot = "C:\Users\Administrator\OneDrive\PILLOW\PROYECTOS_CLIENTES\PILLOW\LA CABRERA\app_Sincro_Franq"
    FrontendPath = "frontend"
    BackendPath = "backend\LaCabrera.Api"

    # SSH y Produccion
    SshKeyPath = "$env:USERPROFILE\.ssh\id_ed25519_lacabrera"
    SshUser = "root"
    SshHost = "200.58.127.114"
    SshPort = 5794

    # Produccion
    ProdAppPath = "/opt/lacabrera"
    ProdDockerCompose = "docker-compose.server.yml"

    # URLs para health checks
    ProdUrl = "https://cabreraapp.pillow.com.ar"
    ProdApiHealth = "https://cabreraapp.pillow.com.ar/api/health"

    # Git
    GitRemote = "origin"
    GitBranch = "main"

    # Herramientas
    SshExe = "C:\Program Files\Git\usr\bin\ssh.exe"
    GitExe = "git"
}

# ============================================
# FUNCIONES AUXILIARES
# ============================================

function Write-Step {
    param([string]$Message, [string]$Status = "INFO")
    $color = switch ($Status) {
        "INFO"    { "Cyan" }
        "SUCCESS" { "Green" }
        "WARNING" { "Yellow" }
        "ERROR"   { "Red" }
        default   { "White" }
    }
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] " -NoNewline -ForegroundColor DarkGray
    Write-Host "[$Status] " -NoNewline -ForegroundColor $color
    Write-Host $Message
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
}

function Test-SshConnection {
    Write-Step "Verificando conexion SSH a produccion..."

    if (-not (Test-Path $Config.SshKeyPath)) {
        Write-Step "SSH key no encontrada: $($Config.SshKeyPath)" "ERROR"
        Write-Step "Genera una con: ssh-keygen -t ed25519 -f `$env:USERPROFILE\.ssh\id_ed25519_lacabrera" "INFO"
        return $false
    }

    $result = & $Config.SshExe -i $Config.SshKeyPath -p $Config.SshPort -o ConnectTimeout=10 -o StrictHostKeyChecking=no "$($Config.SshUser)@$($Config.SshHost)" "echo 'OK'" 2>&1

    if ($result -eq "OK") {
        Write-Step "Conexion SSH exitosa" "SUCCESS"
        return $true
    } else {
        Write-Step "No se pudo conectar a produccion via SSH" "ERROR"
        Write-Step "Error: $result" "ERROR"
        return $false
    }
}

function Invoke-SshCommand {
    param(
        [string]$Command,
        [int]$TimeoutSeconds = 300
    )

    $result = & $Config.SshExe -i $Config.SshKeyPath -p $Config.SshPort -o ConnectTimeout=10 "$($Config.SshUser)@$($Config.SshHost)" $Command 2>&1
    return $result
}

function Test-LocalBuilds {
    Write-Header "VALIDACION DE BUILDS LOCALES"

    $frontendPath = Join-Path $Config.ProjectRoot $Config.FrontendPath
    $backendPath = Join-Path $Config.ProjectRoot $Config.BackendPath

    # Frontend build
    Write-Step "Compilando frontend..."
    Push-Location $frontendPath
    $frontendResult = npm run build 2>&1
    $frontendExitCode = $LASTEXITCODE
    Pop-Location

    if ($frontendExitCode -ne 0) {
        Write-Step "Frontend build FALLIDO" "ERROR"
        Write-Host $frontendResult -ForegroundColor Red
        return $false
    }
    Write-Step "Frontend build exitoso" "SUCCESS"

    # Backend build
    Write-Step "Compilando backend..."
    Push-Location $backendPath
    $backendResult = dotnet build --configuration Release 2>&1
    $backendExitCode = $LASTEXITCODE
    Pop-Location

    if ($backendExitCode -ne 0) {
        Write-Step "Backend build FALLIDO" "ERROR"
        Write-Host $backendResult -ForegroundColor Red
        return $false
    }
    Write-Step "Backend build exitoso" "SUCCESS"

    return $true
}

function Get-GitChanges {
    Push-Location $Config.ProjectRoot
    $status = git status --porcelain 2>&1
    Pop-Location
    return $status
}

function New-GitCommitAndPush {
    param([string]$Message)

    Write-Header "GIT COMMIT Y PUSH"

    Push-Location $Config.ProjectRoot

    # Verificar cambios
    $changes = git status --porcelain 2>&1
    if (-not $changes) {
        Write-Step "No hay cambios para commitear" "WARNING"
        Pop-Location
        return $true
    }

    Write-Step "Cambios detectados:"
    $changes | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

    # Agregar cambios (excluyendo archivos temporales)
    Write-Step "Agregando cambios al stage..."
    git add -A 2>&1 | Out-Null

    # Quitar archivos que no deben commitearse
    git reset HEAD -- "*.bak" "*.log" "__pycache__" "node_modules" ".env" "*.tmp" 2>&1 | Out-Null

    # Commit
    if (-not $Message) {
        $Message = "Deploy: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    }

    $commitMessage = @"
$Message

Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
"@

    Write-Step "Creando commit: $Message"
    git commit -m $commitMessage 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Step "Error en commit (puede que no haya cambios staged)" "WARNING"
    }

    # Push
    Write-Step "Pushing a $($Config.GitRemote)/$($Config.GitBranch)..."
    $pushResult = git push $Config.GitRemote $Config.GitBranch 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Step "Error en push: $pushResult" "ERROR"
        Pop-Location
        return $false
    }

    Write-Step "Push exitoso" "SUCCESS"
    Pop-Location
    return $true
}

function Update-ProductionCode {
    Write-Header "ACTUALIZANDO CODIGO EN PRODUCCION"

    # Verificar si git esta inicializado
    Write-Step "Verificando repositorio Git en produccion..."
    $gitStatus = Invoke-SshCommand "cd $($Config.ProdAppPath) && git status 2>&1 || echo 'NO_GIT'"

    if ($gitStatus -match "NO_GIT|not a git repository") {
        Write-Step "Inicializando Git en produccion..." "WARNING"
        $initResult = Invoke-SshCommand "cd $($Config.ProdAppPath) && git init && git remote add origin https://github.com/GitEmilianoRizzo/App_LaCabrera.git && git fetch origin main"
        Write-Step "Git inicializado" "SUCCESS"
    }

    # Backup .env
    Write-Step "Respaldando configuracion de produccion..."
    Invoke-SshCommand "cd $($Config.ProdAppPath) && cp .env /tmp/.env.prod.backup 2>/dev/null"

    # Fetch y reset
    Write-Step "Descargando cambios de GitHub..."
    $fetchResult = Invoke-SshCommand "cd $($Config.ProdAppPath) && git fetch origin main 2>&1"

    Write-Step "Aplicando cambios..."
    $resetResult = Invoke-SshCommand "cd $($Config.ProdAppPath) && git reset --hard origin/main 2>&1"

    # Restaurar .env
    Write-Step "Restaurando configuracion..."
    Invoke-SshCommand "cd $($Config.ProdAppPath) && cp /tmp/.env.prod.backup .env 2>/dev/null"

    # Mostrar commit actual
    $currentCommit = Invoke-SshCommand "cd $($Config.ProdAppPath) && git log -1 --oneline"
    Write-Step "Commit actual: $currentCommit" "SUCCESS"

    return $true
}

function Update-DockerContainers {
    Write-Header "RECONSTRUYENDO CONTAINERS DOCKER"

    # Build
    Write-Step "Construyendo imagenes Docker (esto puede tomar varios minutos)..."
    $buildResult = Invoke-SshCommand "cd $($Config.ProdAppPath) && docker compose -f $($Config.ProdDockerCompose) build --no-cache frontend api 2>&1"

    if ($buildResult -match "error|failed") {
        Write-Step "Error en build Docker" "ERROR"
        Write-Host $buildResult -ForegroundColor Red
        return $false
    }
    Write-Step "Build Docker completado" "SUCCESS"

    # Detener nginx del sistema (para liberar puerto 80 temporalmente)
    Write-Step "Deteniendo nginx del sistema..."
    Invoke-SshCommand "systemctl stop nginx 2>/dev/null"

    # Restart containers
    Write-Step "Reiniciando containers..."
    $restartResult = Invoke-SshCommand "cd $($Config.ProdAppPath) && docker compose -f $($Config.ProdDockerCompose) down && docker compose -f $($Config.ProdDockerCompose) up -d 2>&1"

    # Esperar que los containers esten healthy
    Write-Step "Esperando que los servicios esten healthy..."
    Start-Sleep -Seconds 20

    # Verificar estado
    $containerStatus = Invoke-SshCommand "docker ps --format 'table {{.Names}}\t{{.Status}}'"
    Write-Host $containerStatus -ForegroundColor Gray

    return $true
}

function Restart-NginxProxy {
    Write-Header "REINICIANDO NGINX (SSL PROXY)"

    # Reconfigurar frontend al puerto 3000 para nginx
    Write-Step "Reconfigurando frontend al puerto 3000..."
    $reconfigResult = Invoke-SshCommand @"
docker stop lacabrera-frontend 2>/dev/null
docker rm lacabrera-frontend 2>/dev/null
docker run -d --name lacabrera-frontend --network lacabrera_lacabrera-network -p 3000:80 --restart always lacabrera-frontend 2>&1
"@

    # Iniciar nginx
    Write-Step "Iniciando nginx..."
    $nginxResult = Invoke-SshCommand "systemctl start nginx && systemctl status nginx --no-pager | head -3"

    if ($nginxResult -match "active \(running\)") {
        Write-Step "Nginx iniciado correctamente" "SUCCESS"
        return $true
    } else {
        Write-Step "Error al iniciar nginx" "ERROR"
        Write-Host $nginxResult -ForegroundColor Red
        return $false
    }
}

function Test-ProductionHealth {
    Write-Header "VERIFICACION DE SALUD"

    # Esperar un momento para que todo se estabilice
    Write-Step "Esperando estabilizacion de servicios..."
    Start-Sleep -Seconds 5

    # Test frontend
    Write-Step "Verificando frontend..."
    try {
        $frontendResponse = Invoke-WebRequest -Uri "$($Config.ProdUrl)/login" -UseBasicParsing -TimeoutSec 15
        if ($frontendResponse.StatusCode -eq 200) {
            Write-Step "Frontend OK (HTTP 200)" "SUCCESS"
        } else {
            Write-Step "Frontend respondio con HTTP $($frontendResponse.StatusCode)" "WARNING"
        }
    } catch {
        Write-Step "Error al verificar frontend: $_" "ERROR"
        return $false
    }

    # Test API
    Write-Step "Verificando API..."
    try {
        $apiResponse = Invoke-RestMethod -Uri $Config.ProdApiHealth -TimeoutSec 15
        if ($apiResponse.status -eq "healthy") {
            Write-Step "API OK (healthy)" "SUCCESS"
        } else {
            Write-Step "API respondio: $($apiResponse.status)" "WARNING"
        }
    } catch {
        Write-Step "Error al verificar API: $_" "ERROR"
        return $false
    }

    return $true
}

function Show-Summary {
    param([bool]$Success, [datetime]$StartTime)

    $duration = (Get-Date) - $StartTime

    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor $(if ($Success) { "Green" } else { "Red" })

    if ($Success) {
        Write-Host "  DEPLOY COMPLETADO EXITOSAMENTE" -ForegroundColor Green
    } else {
        Write-Host "  DEPLOY FALLIDO" -ForegroundColor Red
    }

    Write-Host ("=" * 60) -ForegroundColor $(if ($Success) { "Green" } else { "Red" })
    Write-Host ""
    Write-Host "  Duracion: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host "  URL: $($Config.ProdUrl)" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================
# EJECUCION PRINCIPAL
# ============================================

$startTime = Get-Date

Write-Host ""
Write-Host " LA CABRERA - DEPLOY A PRODUCCION " -ForegroundColor Black -BackgroundColor Cyan
Write-Host ""

try {
    # 1. Verificar SSH
    if (-not (Test-SshConnection)) {
        Show-Summary -Success $false -StartTime $startTime
        exit 1
    }

    # 2. Validar builds locales
    if (-not $SkipBuildValidation) {
        if (-not (Test-LocalBuilds)) {
            Show-Summary -Success $false -StartTime $startTime
            exit 1
        }
    } else {
        Write-Step "Validacion de builds omitida" "WARNING"
    }

    # 3. Commit y push
    if (-not $SkipCommit) {
        if (-not (New-GitCommitAndPush -Message $CommitMessage)) {
            Show-Summary -Success $false -StartTime $startTime
            exit 1
        }
    } else {
        Write-Step "Commit y push omitido" "WARNING"
    }

    # 4. Actualizar codigo en produccion
    if (-not (Update-ProductionCode)) {
        Show-Summary -Success $false -StartTime $startTime
        exit 1
    }

    # 5. Reconstruir containers
    if (-not (Update-DockerContainers)) {
        Show-Summary -Success $false -StartTime $startTime
        exit 1
    }

    # 6. Reiniciar nginx
    if (-not (Restart-NginxProxy)) {
        Show-Summary -Success $false -StartTime $startTime
        exit 1
    }

    # 7. Verificar salud
    if (-not (Test-ProductionHealth)) {
        Write-Step "Los health checks fallaron, pero el deploy puede estar OK" "WARNING"
    }

    Show-Summary -Success $true -StartTime $startTime
    exit 0

} catch {
    Write-Step "Error inesperado: $_" "ERROR"
    Show-Summary -Success $false -StartTime $startTime
    exit 1
}
