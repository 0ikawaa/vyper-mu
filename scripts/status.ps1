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
$ie = Get-Process -Name 'VyperMu.ItemEditor' -ErrorAction SilentlyContinue
$ieTask = Get-ScheduledTask -TaskName 'vyper-mu Item Editor' -ErrorAction SilentlyContinue
Write-Host ("  Item Editor: {0}{1}" -f $(if($ie){"corriendo (http://localhost:5050)"}else{"parado"}), $(if($ieTask){" - instalado al iniciar sesion"}else{""})) -ForegroundColor $(if($ie){'Green'}else{'DarkGray'})

Write-Host "`n--- Puertos ---" -ForegroundColor Cyan
# Lista de pares en vez de hashtable: en un [ordered], indexar con un entero
# busca por POSICION y no por clave, y las descripciones salen vacias.
$puertos = @(
    @{ N = 5433;  D = 'PostgreSQL' },
    @{ N = 5000;  D = 'Panel de administracion' },
    @{ N = 44405; D = 'Connect server (cliente original)' },
    @{ N = 44406; D = 'Connect server (MuMain) <- el que usa tu cliente' },
    @{ N = 55901; D = 'Game server 1' },
    @{ N = 55902; D = 'Game server 2' },
    @{ N = 55903; D = 'Game server 3' },
    @{ N = 55980; D = 'Chat server' }
)
foreach ($item in $puertos) {
    $ok = Test-Puerto $item.N
    Write-Host ("  {0,-6} {1,-8} {2}" -f $item.N, $(if($ok){'ABIERTO'}else{'cerrado'}), $item.D) -ForegroundColor $(if($ok){'Green'}else{'DarkGray'})
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
