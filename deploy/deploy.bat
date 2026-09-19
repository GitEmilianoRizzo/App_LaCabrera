@echo off
REM ============================================
REM LA CABRERA - Deploy a Produccion
REM ============================================
REM Uso: deploy.bat [mensaje-commit]
REM
REM Ejemplos:
REM   deploy.bat
REM   deploy.bat "Fix: correccion de bug"
REM   deploy.bat --skip-commit
REM ============================================

cd /d "%~dp0"

if "%1"=="--skip-commit" (
    powershell -ExecutionPolicy Bypass -File "deploy-production.ps1" -SkipCommit
) else if "%1"=="--skip-build" (
    powershell -ExecutionPolicy Bypass -File "deploy-production.ps1" -SkipBuildValidation
) else if "%~1"=="" (
    powershell -ExecutionPolicy Bypass -File "deploy-production.ps1"
) else (
    powershell -ExecutionPolicy Bypass -File "deploy-production.ps1" -CommitMessage "%*"
)

pause
