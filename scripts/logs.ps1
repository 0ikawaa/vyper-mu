<#
    Logs del servidor.
    Uso:  .\scripts\logs.ps1              (OpenMU, en vivo)
          .\scripts\logs.ps1 -Postgres    (PostgreSQL)
          .\scripts\logs.ps1 -Errores     (solo el log de errores de OpenMU)
#>
param([switch]$Postgres, [switch]$Errores)
$ErrorActionPreference = 'Stop'
$logs = Join-Path (Split-Path -Parent $PSScriptRoot) 'server\logs'

$archivo = if ($Postgres) { 'postgres.log' } elseif ($Errores) { 'openmu.err.log' } else { 'openmu.log' }
$ruta = Join-Path $logs $archivo

if (-not (Test-Path $ruta)) { Write-Host "No existe $ruta" -ForegroundColor Red; exit 1 }
Write-Host "Ctrl+C para salir (el servidor sigue corriendo)." -ForegroundColor Cyan
Get-Content $ruta -Wait -Tail 60
