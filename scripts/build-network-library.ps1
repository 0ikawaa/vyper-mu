<#
    Compila MUnique.Client.Library.dll, la unica pieza del cliente que no se
    publica precompilada. Sin ella el cliente abre pero no conecta:
    muestra "MUnique.Client.Library.dll missing".

    Requisitos (instalar ANTES de correr esto):
      1. .NET 10 SDK          https://dotnet.microsoft.com/download/dotnet/10.0
      2. Visual Studio Build Tools con el workload "Desktop development with C++"
         https://visualstudio.microsoft.com/downloads/  (seccion Build Tools)
         Native AOT necesita el linker de MSVC y el Windows SDK.

    Uso:  .\scripts\build-network-library.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# git y dotnet escriben progreso en stderr. En PowerShell 5.1 eso se convierte en
# ErrorRecord y con -EA Stop aborta el script aunque el comando haya salido bien.
# Abajo se verifica $LASTEXITCODE en cada paso, asi que stderr no debe matar nada.
function Invoke-Native {
    param([scriptblock]$Cmd)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Cmd } finally { $ErrorActionPreference = $prev }
}
$dest = Join-Path $root 'client\runtime'
$work = Join-Path $root 'client\_build'

# --- Chequeo de requisitos ---
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "Falta el .NET SDK." -ForegroundColor Red
    Write-Host "  Instalalo desde https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host "  o con winget:  winget install Microsoft.DotNet.SDK.10"
    exit 1
}

$sdks = & dotnet --list-sdks
if (-not ($sdks | Where-Object { $_ -match '^10\.' })) {
    Write-Host "Hay dotnet, pero no el SDK 10.x. SDKs encontrados:" -ForegroundColor Red
    $sdks | ForEach-Object { Write-Host "    $_" }
    Write-Host "  Instala el 10:  winget install Microsoft.DotNet.SDK.10"
    exit 1
}

if (-not (Test-Path $dest)) {
    Write-Host "No existe $dest. Corre primero .\scripts\setup-client.ps1" -ForegroundColor Red
    exit 1
}

# El compilador Native AOT (ilcompiler) invoca vswhere.exe sin ruta absoluta para
# ubicar el linker de MSVC. vswhere solo esta en el PATH dentro de un Developer
# Command Prompt, asi que lo agregamos a mano.
$vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path (Join-Path $vsInstaller 'vswhere.exe')) {
    if ($env:Path -notlike "*$vsInstaller*") { $env:Path = "$vsInstaller;$env:Path" }
} else {
    Write-Host "No encontre vswhere.exe en $vsInstaller" -ForegroundColor Red
    Write-Host "Instala las Build Tools de C++:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor Yellow
    exit 1
}

# Verificar que estan las herramientas de C++ que necesita el linker.
$vcCheck = & (Join-Path $vsInstaller 'vswhere.exe') -products * -latest `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vcCheck) {
    Write-Host "Faltan las herramientas de C++ (workload 'Desktop development with C++')." -ForegroundColor Red
    Write-Host "  winget install Microsoft.VisualStudio.2022.BuildTools --override `"--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended`"" -ForegroundColor Yellow
    exit 1
}

# --- Clonar solo lo necesario ---
if (-not (Test-Path $work)) {
    Write-Host "Clonando MuMain (shallow, sin submodulos)..." -ForegroundColor Cyan
    Invoke-Native { git clone --quiet --depth 1 --no-recurse-submodules https://github.com/sven-n/MuMain.git $work }
    if ($LASTEXITCODE -ne 0) { Write-Host "Fallo el clone." -ForegroundColor Red; exit 1 }
} else {
    Write-Host "Actualizando el clon existente..." -ForegroundColor Cyan
    Invoke-Native { git -C $work fetch --quiet --depth 1 origin main }
    Invoke-Native { git -C $work reset --quiet --hard origin/main }
}

# --- Compilar con Native AOT ---
# ci=true saltea el paso de XSLT: los .cs generados ya vienen en el repo.
Write-Host "Compilando MUnique.Client.Library.dll (Native AOT, tarda varios minutos)..." -ForegroundColor Cyan
$proj = Join-Path $work 'ClientLibrary\MUnique.Client.Library.csproj'
$out  = Join-Path $work 'ClientLibrary\publish'

Invoke-Native { dotnet publish $proj -c Release -r win-x64 /p:ci=true /p:PublishAot=true -o $out }

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Fallo la compilacion." -ForegroundColor Red
    Write-Host "La causa mas comun es que falten las Build Tools de C++:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor Yellow
    Write-Host "  ...y en el instalador, tildar 'Desktop development with C++'." -ForegroundColor Yellow
    exit 1
}

# --- Copiar al lado de Main.exe ---
$dll = Join-Path $out 'MUnique.Client.Library.dll'
if (-not (Test-Path $dll)) {
    Write-Host "Compilo pero no aparecio la DLL en $out" -ForegroundColor Red
    Get-ChildItem $out -Filter *.dll | ForEach-Object { Write-Host "   encontrado: $($_.Name)" }
    exit 1
}

Copy-Item $dll $dest -Force
$kb = [math]::Round((Get-Item (Join-Path $dest 'MUnique.Client.Library.dll')).Length / 1KB)
Write-Host ""
Write-Host "Listo: MUnique.Client.Library.dll ($kb KB) copiada a client\runtime\" -ForegroundColor Green
Write-Host "Ya podes lanzar el cliente con .\scripts\play.ps1" -ForegroundColor Green
