<#
    Estado del servidor y del cliente.
    Uso:  .\scripts\status.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Test-Puerto($p) {
    Test-NetConnection -ComputerName 127.0.0.1 -Port $p -InformationLevel Quiet -WarningAction SilentlyContinue
}

Write-Host "`n--- Procesos ---" -ForegroundColor Cyan
$pg = Get-Process -Name 'postgres' -ErrorAction SilentlyContinue
$mu = Get-Process -Name 'MUnique.OpenMU.Startup' -ErrorAction SilentlyContinue
Write-Host ("  PostgreSQL : {0}" -f $(if($pg){"corriendo ($($pg.Count) procesos)"}else{"parado"})) -ForegroundColor $(if($pg){'Green'}else{'DarkGray'})
Write-Host ("  OpenMU     : {0}" -f $(if($mu){"corriendo (PID $($mu.Id))"}else{"parado"}))          -ForegroundColor $(if($mu){'Green'}else{'DarkGray'})

Write-Host "`n--- Puertos ---" -ForegroundColor Cyan
$puertos = [ordered]@{
    5433  = 'PostgreSQL'
    5000  = 'Panel de administracion'
    44405 = 'Connect server (cliente original)'
    44406 = 'Connect server (MuMain) <- el que usa tu cliente'
    55901 = 'Game server 1'
    55902 = 'Game server 2'
    55903 = 'Game server 3'
    55980 = 'Chat server'
}
foreach ($p in $puertos.Keys) {
    $ok = Test-Puerto $p
    Write-Host ("  {0,-6} {1,-8} {2}" -f $p, $(if($ok){'ABIERTO'}else{'cerrado'}), $puertos[$p]) -ForegroundColor $(if($ok){'Green'}else{'DarkGray'})
}

Write-Host "`n--- Cliente ---" -ForegroundColor Cyan
$rt = Join-Path $root 'client\runtime'
$piezas = [ordered]@{
    'Main.exe'                     = Join-Path $rt 'Main.exe'
    'MUnique.Client.Library.dll'   = Join-Path $rt 'MUnique.Client.Library.dll'
    'config.ini'                   = Join-Path $rt 'config.ini'
    'Data\'                        = Join-Path $rt 'Data'
    'fonts\'                       = Join-Path $rt 'fonts'
}
foreach ($k in $piezas.Keys) {
    $ok = Test-Path $piezas[$k]
    Write-Host ("  {0,-28} {1}" -f $k, $(if($ok){'OK'}else{'FALTA'})) -ForegroundColor $(if($ok){'Green'}else{'Red'})
}
Write-Host ""
