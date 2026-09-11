<#
    Item Editor: editor web de items (servidor + cliente), cuentas, personajes,
    inventario y baul, en http://localhost:5050.

    Uso:
      .\scripts\item-editor.ps1               abre el editor (compila la primera vez)
      .\scripts\item-editor.ps1 -Port 5060    otro puerto
      .\scripts\item-editor.ps1 -NoBrowser    no abre el navegador

      .\scripts\item-editor.ps1 -Instalar     lo deja SIEMPRE disponible: se registra
                                              como tarea al iniciar sesion de Windows,
                                              corre oculto y deja un acceso directo
                                              "Item Editor" en el escritorio. Levanta
                                              PostgreSQL solo si hace falta; OpenMU no
                                              tiene que estar corriendo.
      .\scripts\item-editor.ps1 -Desinstalar  saca la tarea y el acceso directo
      .\scripts\item-editor.ps1 -Servicio     (uso interno de la tarea) corre oculto

    Ver docs\06-item-editor.md
#>
param(
    [int]$Port = 5050,
    [switch]$NoBrowser,
    [switch]$Instalar,
    [switch]$Desinstalar,
    [switch]$Servicio
)

$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent $PSScriptRoot
$proj    = Join-Path $root 'tools\item-editor\ItemEditor.csproj'
$publish = Join-Path $root 'tools\item-editor\publish'
$exe     = Join-Path $publish 'VyperMu.ItemEditor.exe'
$tarea   = 'vyper-mu Item Editor'
$acceso  = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Item Editor.url'

function Publicar {
    # Compila una sola vez a tools\item-editor\publish; se vuelve a compilar si cambio el codigo.
    $fuentes = Get-ChildItem (Join-Path $root 'tools\item-editor') -Recurse -Include *.cs,*.csproj,*.js,*.css,*.html,*.json |
               Where-Object { $_.FullName -notmatch '\\(bin|obj|publish|backups)\\' }
    $ultima = ($fuentes | Measure-Object LastWriteTime -Maximum).Maximum
    if ((Test-Path $exe) -and (Get-Item $exe).LastWriteTime -gt $ultima) { return }
    Write-Host "Compilando el Item Editor (~30 s)..." -ForegroundColor Cyan
    dotnet publish $proj -c Release -o $publish --nologo -v quiet
    if (-not (Test-Path $exe)) { throw "No se pudo compilar el Item Editor." }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Falta el .NET SDK. Corre primero: .\install.ps1" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path (Join-Path $root 'server\.pgpassword'))) {
    Write-Host "No esta instalado el servidor. Corre primero: .\install.ps1" -ForegroundColor Red
    exit 1
}

$env:VYPER_ROOT = $root
$env:VYPER_EDITOR_URL = "http://localhost:$Port"

# ------------------------------------------------------------ desinstalar
if ($Desinstalar) {
    Stop-Process -Name 'VyperMu.ItemEditor' -Force -ErrorAction SilentlyContinue
    if (Get-ScheduledTask -TaskName $tarea -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $tarea -Confirm:$false
        Write-Host "Tarea '$tarea' borrada." -ForegroundColor Green
    } else {
        Write-Host "La tarea '$tarea' no existia." -ForegroundColor DarkGray
    }
    Remove-Item $acceso -ErrorAction SilentlyContinue
    Write-Host "Listo. Para abrirlo a mano: .\scripts\item-editor.ps1"
    exit 0
}

# ------------------------------------------------------------ instalar (siempre disponible)
if ($Instalar) {
    Publicar
    $ps = (Get-Command powershell.exe).Source
    $arg = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Servicio -Port $Port"
    $action  = New-ScheduledTaskAction -Execute $ps -Argument $arg -WorkingDirectory $root
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    if (Get-ScheduledTask -TaskName $tarea -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $tarea -Confirm:$false }
    Register-ScheduledTask -TaskName $tarea -Action $action -Trigger $trigger -Settings $settings -Description 'vyper-mu Item Editor en http://localhost:5050' | Out-Null

    "[InternetShortcut]`r`nURL=http://localhost:$Port`r`n" | Set-Content $acceso -Encoding ascii

    Stop-Process -Name 'VyperMu.ItemEditor' -Force -ErrorAction SilentlyContinue
    Start-ScheduledTask -TaskName $tarea

    Write-Host ""
    Write-Host "  Item Editor instalado." -ForegroundColor Green
    Write-Host "    - Arranca solo al iniciar sesion (tarea '$tarea')"
    Write-Host "    - Corre oculto; abrilo desde http://localhost:$Port o el acceso 'Item Editor' del escritorio"
    Write-Host "    - No necesita que OpenMU este corriendo; PostgreSQL lo levanta solo si hace falta"
    Write-Host "    - Para sacarlo: .\scripts\item-editor.ps1 -Desinstalar"
    Write-Host ""
    $n = 0
    while (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue)) {
        Start-Sleep 1; $n++; if ($n -ge 60) { break }
    }
    if ($n -lt 60) { Start-Process "http://localhost:$Port" }
    exit 0
}

# ------------------------------------------------------------ servicio (lo lanza la tarea)
if ($Servicio) {
    Publicar
    $env:VYPER_EDITOR_NOBROWSER = '1'
    $log = Join-Path $root 'server\logs\item-editor.log'
    New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
    & $exe *>> $log
    exit $LASTEXITCODE
}

# ------------------------------------------------------------ uso normal: ventana abierta
if ($NoBrowser) { $env:VYPER_EDITOR_NOBROWSER = '1' } else { Remove-Item Env:VYPER_EDITOR_NOBROWSER -ErrorAction SilentlyContinue }
if (Get-Process -Name 'VyperMu.ItemEditor' -ErrorAction SilentlyContinue) {
    Write-Host "El Item Editor ya esta corriendo: http://localhost:$Port" -ForegroundColor Green
    if (-not $NoBrowser) { Start-Process "http://localhost:$Port" }
    exit 0
}
Publicar
& $exe
