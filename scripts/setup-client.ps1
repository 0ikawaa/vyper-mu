<#
    Completa el cliente descargando los assets del juego.

    Main.exe y MUnique.Client.Library.dll ya vienen en el repo (son builds de
    codigo open source). Lo unico que falta bajar es Data\ y fonts\, que son
    los assets de MU Online y no se redistribuyen aca: se descargan del release
    publico de MuMain.

    Uso:  .\scripts\setup-client.ps1
          .\scripts\setup-client.ps1 -Force    (rebaja los assets)
#>
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path -Parent $PSScriptRoot
$rt   = Join-Path $root 'client\runtime'

# --- Verificar lo que viene del repo ---
foreach ($f in @('Main.exe','MUnique.Client.Library.dll')) {
    if (-not (Test-Path (Join-Path $rt $f))) {
        Write-Host "Falta $f en client\runtime\" -ForegroundColor Red
        Write-Host "Deberia venir en el repo. Volve a clonar, o compilalo:" -ForegroundColor Yellow
        Write-Host "  .\scripts\build-network-library.ps1   (para la DLL)" -ForegroundColor Yellow
        exit 1
    }
}

# --- Assets del juego ---
if ((Test-Path (Join-Path $rt 'Data')) -and -not $Force) {
    Write-Host "Los assets ya estan (Data\ y fonts\)." -ForegroundColor DarkGray
} else {
    if ($Force) {
        Remove-Item (Join-Path $rt 'Data'),(Join-Path $rt 'fonts') -Recurse -Force -ErrorAction SilentlyContinue
    }

    # El release de datos se identifica por un hash del arbol de assets, asi que
    # el tag cambia solo cuando cambian los datos. Buscamos el vigente.
    Write-Host "Buscando el release de assets..." -ForegroundColor Cyan
    $api = 'https://api.github.com/repos/sven-n/MuMain/releases/latest'
    try {
        $rel = Invoke-RestMethod -Uri $api -Headers @{ 'User-Agent' = 'vyper-mu' }
    } catch {
        Write-Host "No pude consultar GitHub: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    $tag = $rel.tag_name
    if ($tag -notlike 'data-*') {
        Write-Host "El ultimo release de MuMain no es de datos ($tag)." -ForegroundColor Red
        Write-Host "Buscalo a mano en https://github.com/sven-n/MuMain/releases" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  $tag" -ForegroundColor DarkGray

    $tgz = Join-Path $root "client\MuMain-data.tar.gz"
    $sum = "$tgz.sha256"
    $base = "https://github.com/sven-n/MuMain/releases/download/$tag"

    if (-not (Test-Path $tgz)) {
        Write-Host "Descargando assets (~426 MB, tarda varios minutos)..." -ForegroundColor Cyan
        curl.exe -L --progress-bar -o $tgz "$base/MuMain-$tag.tar.gz"
        if ($LASTEXITCODE -ne 0) { Write-Host "Fallo la descarga." -ForegroundColor Red; exit 1 }
        curl.exe -L -s -o $sum "$base/MuMain-$tag.tar.gz.sha256"
    } else {
        Write-Host "Ya estaba descargado el .tar.gz, lo reuso." -ForegroundColor DarkGray
    }

    Write-Host "Verificando checksum..." -ForegroundColor Cyan
    $esperado = (Get-Content $sum).Split()[0].ToUpperInvariant()
    $real     = (Get-FileHash $tgz -Algorithm SHA256).Hash
    if ($real -ne $esperado) {
        Remove-Item $tgz -Force
        Write-Host "Checksum no coincide: descarga corrupta. Volve a correr el script." -ForegroundColor Red
        exit 1
    }
    Write-Host "  OK" -ForegroundColor Green

    Write-Host "Extrayendo (~764 MB)..." -ForegroundColor Cyan
    tar -xzf $tgz -C $rt
    if ($LASTEXITCODE -ne 0) { Write-Host "Fallo la extraccion." -ForegroundColor Red; exit 1 }
    Write-Host "  OK" -ForegroundColor Green
}

# --- config.ini ---
$cfg = Join-Path $rt 'config.ini'
if (-not (Test-Path $cfg)) {
    # Puerto 44406: MuMain usa el protocolo extendido, no el del cliente original.
    # IP 127.127.127.127: el cliente de MU bloquea 127.0.0.1 explicitamente.
    @'
[LOGIN]
RememberMe=0
Language=Eng
EncryptedUsername=
EncryptedPassword=
[Window]
Width=1024
Height=768
Windowed=1
[Audio]
SoundVolume=5
MusicVolume=5
[CONNECTION SETTINGS]
ServerIP=127.127.127.127
ServerPort=44406
[UI]
Locale=en
Font=
[Render]
VSync=1
'@ | Set-Content $cfg -Encoding utf8
}

$n = (Get-ChildItem (Join-Path $rt 'Data') -Recurse -File -ErrorAction SilentlyContinue).Count
Write-Host ""
Write-Host "Cliente completo ($n archivos de assets)." -ForegroundColor Green
Write-Host "Jugar con:  .\scripts\play.ps1" -ForegroundColor Cyan
