<#
    Apaga OpenMU y PostgreSQL. Los datos se conservan.
    Uso:  .\scripts\stop.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Native { param([scriptblock]$Cmd)
    $prev=$ErrorActionPreference; $ErrorActionPreference='Continue'
    try { & $Cmd } finally { $ErrorActionPreference=$prev } }

$p = Get-Process -Name 'MUnique.OpenMU.Startup' -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "Parando OpenMU (PID $($p.Id))..." -ForegroundColor Cyan
    $p | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "  ok" -ForegroundColor Green
} else { Write-Host "OpenMU no estaba corriendo." -ForegroundColor DarkGray }

$pgData = Join-Path $root 'server\pgdata'
$pgCtl  = Join-Path $root 'server\pgsql\bin\pg_ctl.exe'
if ((Test-Path $pgCtl) -and (Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    Write-Host "Parando PostgreSQL..." -ForegroundColor Cyan
    Invoke-Native { & $pgCtl -D $pgData -m fast stop }
    Write-Host "  ok" -ForegroundColor Green
} else { Write-Host "PostgreSQL no estaba corriendo." -ForegroundColor DarkGray }

Write-Host "`nServidor apagado. Los personajes y cuentas siguen guardados." -ForegroundColor Green
