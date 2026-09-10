<#
    Restaura un backup. SOBRESCRIBE la base actual.
    Uso:  .\scripts\restore.ps1 -File .\backups\openmu-20260910-143000.dump
#>
param([Parameter(Mandatory=$true)][string]$File)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Native { param([scriptblock]$Cmd)
    $prev=$ErrorActionPreference; $ErrorActionPreference='Continue'
    try { & $Cmd } finally { $ErrorActionPreference=$prev } }

if (-not (Test-Path $File)) { Write-Host "No existe: $File" -ForegroundColor Red; exit 1 }

Write-Host "`n  Esto BORRA la base actual y la reemplaza por el backup." -ForegroundColor Red
Write-Host "  Archivo: $File" -ForegroundColor Yellow
if ((Read-Host "  Escribi SI para continuar") -ne 'SI') { Write-Host "Cancelado." -ForegroundColor Yellow; exit 0 }

# OpenMU tiene que estar parado o mantiene conexiones abiertas contra la base.
$mu = Get-Process -Name 'MUnique.OpenMU.Startup' -ErrorAction SilentlyContinue
if ($mu) { Write-Host "Parando OpenMU..." -ForegroundColor Cyan; $mu | Stop-Process -Force; Start-Sleep 3 }

$env:PGPASSWORD = (Get-Content (Join-Path $root 'server\.pgpassword') -Raw).Trim()
$restore = Join-Path $root 'server\pgsql\bin\pg_restore.exe'

Write-Host "Restaurando..." -ForegroundColor Cyan
Invoke-Native { & $restore -U postgres -h 127.0.0.1 -p 5433 -d openmu --clean --if-exists $File }

Write-Host "Restore terminado. Levanta el servidor con .\scripts\start.ps1" -ForegroundColor Green
