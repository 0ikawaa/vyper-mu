<#
    Abre el Item Editor: editor web de items del servidor (OpenMU) y del
    cliente (Item_eng.bmd), en http://localhost:5050.

    Uso:  .\scripts\item-editor.ps1
          .\scripts\item-editor.ps1 -Port 5060      # otro puerto
          .\scripts\item-editor.ps1 -NoBrowser      # no abre el navegador

    Necesita PostgreSQL corriendo (.\scripts\start.ps1). OpenMU puede estar
    apagado o prendido; los cambios en la base se aplican al reiniciarlo.
    Ver docs\06-item-editor.md
#>
param(
    [int]$Port = 5050,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'tools\item-editor\ItemEditor.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Falta el .NET SDK. Corre primero: .\install.ps1" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path (Join-Path $root 'server\.pgpassword'))) {
    Write-Host "No esta instalado el servidor. Corre primero: .\install.ps1" -ForegroundColor Red
    exit 1
}
$pgUp = Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue
if (-not $pgUp) {
    Write-Host "PostgreSQL no esta corriendo. Levantalo con: .\scripts\start.ps1" -ForegroundColor Yellow
    exit 1
}

$env:VYPER_ROOT = $root
$env:VYPER_EDITOR_URL = "http://localhost:$Port"
if ($NoBrowser) { $env:VYPER_EDITOR_NOBROWSER = '1' } else { Remove-Item Env:VYPER_EDITOR_NOBROWSER -ErrorAction SilentlyContinue }

Write-Host "Compilando y abriendo el Item Editor (la primera vez tarda ~30 s)..." -ForegroundColor Cyan
dotnet run --project $proj -c Release --nologo -v quiet
