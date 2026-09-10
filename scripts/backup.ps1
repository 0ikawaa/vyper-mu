<#
    Backup de la base: cuentas, personajes, items, guilds y config del servidor.
    Uso:  .\scripts\backup.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Native { param([scriptblock]$Cmd)
    $prev=$ErrorActionPreference; $ErrorActionPreference='Continue'
    try { & $Cmd } finally { $ErrorActionPreference=$prev } }

$dump   = Join-Path $root 'server\pgsql\bin\pg_dump.exe'
$pwFile = Join-Path $root 'server\.pgpassword'
$dir    = Join-Path $root 'backups'

if (-not (Test-Path $dump))   { Write-Host "Falta PostgreSQL. Corre .\install.ps1" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $pwFile)) { Write-Host "Falta server\.pgpassword" -ForegroundColor Red; exit 1 }
if (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    Write-Host "PostgreSQL no esta corriendo. Levantalo con .\scripts\start.ps1" -ForegroundColor Red; exit 1
}

New-Item -ItemType Directory -Path $dir -Force | Out-Null
$file = Join-Path $dir ("openmu-{0}.dump" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

$env:PGPASSWORD = (Get-Content $pwFile -Raw).Trim()
Write-Host "Generando backup..." -ForegroundColor Cyan
Invoke-Native { & $dump -U postgres -h 127.0.0.1 -p 5433 -Fc -f $file openmu }

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $file)) {
    Write-Host "Fallo el backup." -ForegroundColor Red
    if (Test-Path $file) { Remove-Item $file }
    exit 1
}

Write-Host ("Backup listo: {0}  ({1} MB)" -f $file, [math]::Round((Get-Item $file).Length/1MB,2)) -ForegroundColor Green

# Conservar los ultimos 10
Get-ChildItem $dir -Filter 'openmu-*.dump' | Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 10 | ForEach-Object {
        Remove-Item $_.FullName; Write-Host "  borrado backup viejo: $($_.Name)" -ForegroundColor DarkGray }
