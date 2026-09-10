<#
    Lanza el cliente. Verifica que este completo y que el servidor responda.
    Uso:  .\scripts\play.ps1
          .\scripts\play.ps1 -ServerIP 192.168.1.50    (cambia el server y lanza)
#>
param([string]$ServerIP, [int]$ServerPort)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dir  = Join-Path $root 'client\runtime'
$exe  = Join-Path $dir 'Main.exe'
$cfg  = Join-Path $dir 'config.ini'

if (-not (Test-Path $exe)) {
    Write-Host "No esta el cliente. Corre primero .\scripts\setup-client.ps1" -ForegroundColor Red
    exit 1
}

# La DLL de red se carga en runtime; sin ella el cliente abre pero no conecta.
if (-not (Test-Path (Join-Path $dir 'MUnique.Client.Library.dll'))) {
    Write-Host ""
    Write-Host "  Falta MUnique.Client.Library.dll" -ForegroundColor Red
    Write-Host "  El cliente va a abrir pero no va a poder conectarse." -ForegroundColor Red
    Write-Host "  Compilala con: .\scripts\build-network-library.ps1" -ForegroundColor Yellow
    Write-Host ""
    $r = Read-Host "  Abrir igual? (s/N)"
    if ($r -ne 's') { exit 0 }
}

# Actualizar config.ini si pasaron parametros
if ($ServerIP -or $ServerPort) {
    $c = Get-Content $cfg -Raw
    if ($ServerIP)   { $c = $c -replace '(?m)^ServerIP=.*',   "ServerIP=$ServerIP" }
    if ($ServerPort) { $c = $c -replace '(?m)^ServerPort=.*', "ServerPort=$ServerPort" }
    Set-Content $cfg $c -NoNewline -Encoding utf8
    Write-Host "config.ini actualizado." -ForegroundColor Cyan
}

# Mostrar y probar el destino
$conf = Get-Content $cfg -Raw
$ip   = if ($conf -match '(?m)^ServerIP=(.+)$')   { $Matches[1].Trim() } else { '?' }
$port = if ($conf -match '(?m)^ServerPort=(\d+)') { [int]$Matches[1] }   else { 0 }
Write-Host "Conectando a $ip`:$port" -ForegroundColor Cyan

if ($port -gt 0) {
    $probe = if ($ip -like '127.*') { '127.0.0.1' } else { $ip }
    $ok = Test-NetConnection -ComputerName $probe -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue
    if (-not $ok) {
        Write-Host "  El puerto $port no responde. Esta levantado el servidor?" -ForegroundColor Yellow
        Write-Host "  Levantalo con: .\scripts\start.ps1" -ForegroundColor Yellow
    }
}

# Main.exe busca sus assets en el directorio de trabajo, hay que lanzarlo desde ahi
Push-Location $dir
try { Start-Process -FilePath $exe -WorkingDirectory $dir }
finally { Pop-Location }

Write-Host "Cliente lanzado." -ForegroundColor Green
