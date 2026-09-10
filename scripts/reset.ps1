<#
    BORRA la base entera: cuentas, personajes, items, guilds.
    El servidor la vuelve a crear e inicializar al arrancar.
    Uso:  .\scripts\reset.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Native { param([scriptblock]$Cmd)
    $prev=$ErrorActionPreference; $ErrorActionPreference='Continue'
    try { & $Cmd } finally { $ErrorActionPreference=$prev } }

Write-Host "`n  BORRA cuentas, personajes, items y guilds. No se puede deshacer." -ForegroundColor Red
if ((Read-Host "  Escribi BORRAR TODO para continuar") -ne 'BORRAR TODO') { Write-Host "Cancelado." -ForegroundColor Yellow; exit 0 }

$mu = Get-Process -Name 'MUnique.OpenMU.Startup' -ErrorAction SilentlyContinue
if ($mu) { Write-Host "Parando OpenMU..." -ForegroundColor Cyan; $mu | Stop-Process -Force; Start-Sleep 3 }

$env:PGPASSWORD = (Get-Content (Join-Path $root 'server\.pgpassword') -Raw).Trim()
$psql = Join-Path $root 'server\pgsql\bin\psql.exe'

Write-Host "Borrando la base..." -ForegroundColor Cyan
Invoke-Native { & $psql -U postgres -h 127.0.0.1 -p 5433 -d postgres -c "DROP DATABASE IF EXISTS openmu WITH (FORCE);" }

Write-Host "Listo. Al levantar con .\scripts\start.ps1 se recrea desde cero." -ForegroundColor Green
