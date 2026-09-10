<#
    vyper-mu - Desinstalador.

    Muestra todo lo que dejo la instalacion, con su tamano, y te deja elegir
    que borrar. No borra nada sin confirmacion explicita.

    Uso:   .\uninstall.ps1              interactivo, pregunta por cada cosa
           .\uninstall.ps1 -DryRun      solo muestra, no borra nada
           .\uninstall.ps1 -Todo        borra sin preguntar lo que se puede
                                        volver a bajar o recompilar
           .\uninstall.ps1 -SinBackup   no ofrece hacer backup de la base

    Aunque uses -Todo, siempre pregunta antes de tocar: tus backups, la cache
    de NuGet, Git y el .NET SDK. Esas cosas o son irreemplazables o las puede
    estar usando otro programa tuyo.
#>
param(
    [switch]$DryRun,
    [switch]$Todo,
    [switch]$SinBackup
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Invoke-Native {
    param([scriptblock]$Cmd)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Cmd } finally { $ErrorActionPreference = $prev }
}

function Get-Tamano($ruta) {
    if (-not (Test-Path $ruta)) { return $null }
    $item = Get-Item $ruta -ErrorAction SilentlyContinue
    if ($item -is [System.IO.FileInfo]) { return $item.Length }
    $s = (Get-ChildItem $ruta -Recurse -File -ErrorAction SilentlyContinue |
          Measure-Object -Property Length -Sum).Sum
    if ($null -eq $s) { return 0 }
    return $s
}

function Format-Tamano($bytes) {
    if ($null -eq $bytes) { return '' }
    if ($bytes -ge 1GB) { return ('{0:N1} GB' -f ($bytes / 1GB)) }
    if ($bytes -ge 1MB) { return ('{0:N0} MB' -f ($bytes / 1MB)) }
    return ('{0:N0} KB' -f ($bytes / 1KB))
}

# Pregunta siempre, aunque se haya pasado -Todo. Para lo irreversible o lo que
# puede estar en uso por otra cosa: backups, cache de NuGet, Git, .NET SDK.
function Confirmar($pregunta) {
    $r = Read-Host "  $pregunta (s/N)"
    return ($r -eq 's' -or $r -eq 'S')
}

# Solo para lo que es estrictamente del juego y se puede volver a descargar o
# recompilar. Esto si lo saltea -Todo.
function ConfirmarJuego($pregunta) {
    if ($Todo) { Write-Host "  $pregunta -> si (-Todo)" -ForegroundColor DarkGray; return $true }
    return (Confirmar $pregunta)
}

function Quitar($ruta, $etiqueta) {
    if (-not (Test-Path $ruta)) { return }
    if ($DryRun) { Write-Host "    [dry-run] borraria $etiqueta" -ForegroundColor DarkGray; return }
    try {
        Remove-Item $ruta -Recurse -Force -ErrorAction Stop
        Write-Host "    borrado: $etiqueta" -ForegroundColor Green
    } catch {
        Write-Host "    no se pudo borrar $etiqueta : $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n  vyper-mu - Desinstalador" -ForegroundColor Yellow
if ($DryRun) { Write-Host "  MODO DRY-RUN: no se borra nada`n" -ForegroundColor Cyan } else { Write-Host "" }

# ------------------------------------------------------- 1. procesos
Write-Host "[1] Procesos" -ForegroundColor Cyan

$procesos = @(
    @{ Nombre = 'MUnique.OpenMU.Startup'; Que = 'servidor OpenMU' },
    @{ Nombre = 'Main';                   Que = 'cliente MU' },
    @{ Nombre = 'postgres';               Que = 'PostgreSQL' }
)
$huboProcesos = $false
foreach ($p in $procesos) {
    $proc = Get-Process -Name $p.Nombre -ErrorAction SilentlyContinue
    if ($proc) {
        $huboProcesos = $true
        Write-Host "  corriendo: $($p.Que)" -ForegroundColor Yellow
    }
}

if ($huboProcesos) {
    # PostgreSQL se apaga con pg_ctl para no corromper la base si despues se
    # restaura un backup; el resto se puede matar directo.
    if (-not $DryRun) {
        Write-Host "  Parando todo..." -ForegroundColor Yellow
        Get-Process -Name 'MUnique.OpenMU.Startup','Main' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 2
        $pgCtl = Join-Path $root 'server\pgsql\bin\pg_ctl.exe'
        if (Test-Path $pgCtl) {
            Invoke-Native { & $pgCtl -D (Join-Path $root 'server\pgdata') -m fast stop }
        }
        Start-Sleep -Seconds 2
        Get-Process -Name 'postgres' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Host "  OK" -ForegroundColor Green
    }
} else {
    Write-Host "  nada corriendo" -ForegroundColor DarkGray
}

# --------------------------------------------- 2. backup antes de borrar
$dbExiste = Test-Path (Join-Path $root 'server\pgdata\PG_VERSION')
if ($dbExiste -and -not $SinBackup -and -not $DryRun) {
    Write-Host "`n[2] Backup previo" -ForegroundColor Cyan
    Write-Host "  Hay una base de datos con tus personajes." -ForegroundColor Yellow
    if (ConfirmarJuego "Hacer un backup antes de borrarla?") {
        # La base tiene que estar arriba para poder volcarla.
        $pgCtl = Join-Path $root 'server\pgsql\bin\pg_ctl.exe'
        Invoke-Native { & $pgCtl -D (Join-Path $root 'server\pgdata') -l (Join-Path $root 'server\logs\postgres.log') start }
        $n = 0
        while (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
            Start-Sleep 1; $n++; if ($n -ge 30) { break }
        }
        & (Join-Path $root 'scripts\backup.ps1')
        Invoke-Native { & $pgCtl -D (Join-Path $root 'server\pgdata') -m fast stop }
        Write-Host "  El backup quedo en backups\ - copialo a otro lado antes de seguir." -ForegroundColor Yellow
        if (-not $Todo) { Read-Host "  Enter para continuar" | Out-Null }
    }
} else {
    Write-Host "`n[2] Backup previo: salteado" -ForegroundColor DarkGray
}

# ------------------------------------------------- 3. datos del proyecto
Write-Host "`n[3] Archivos dentro de la carpeta del repo" -ForegroundColor Cyan

$items = @(
    @{ Ruta = 'client\runtime\Data';      Que = 'assets del juego';            Grupo = 'juego' },
    @{ Ruta = 'client\runtime\fonts';     Que = 'fuentes del cliente';         Grupo = 'juego' },
    @{ Ruta = 'client\MuMain-data.tar.gz';Que = 'archivo de assets descargado';Grupo = 'juego' },
    @{ Ruta = 'client\_build';            Que = 'clon de MuMain';              Grupo = 'juego' },
    @{ Ruta = 'server\pgsql';             Que = 'PostgreSQL portable';         Grupo = 'servidor' },
    @{ Ruta = 'server\bin';               Que = 'OpenMU compilado';            Grupo = 'servidor' },
    @{ Ruta = 'server\_src';              Que = 'codigo fuente de OpenMU';     Grupo = 'servidor' },
    @{ Ruta = 'server\logs';              Que = 'logs';                        Grupo = 'servidor' },
    @{ Ruta = 'server\pg.zip';            Que = 'zip de PostgreSQL';           Grupo = 'servidor' },
    @{ Ruta = 'server\pgdata';            Que = 'BASE DE DATOS (personajes)';  Grupo = 'datos'    },
    @{ Ruta = 'server\.pgpassword';       Que = 'password de la base';         Grupo = 'datos'    }
)

$total = 0
$presentes = @()
foreach ($i in $items) {
    $full = Join-Path $root $i.Ruta
    $sz = Get-Tamano $full
    if ($null -ne $sz) {
        $i.Bytes = $sz
        $i.Full  = $full
        $presentes += $i
        $total += $sz
        Write-Host ("  {0,-10} {1,-34} {2}" -f $i.Grupo, $i.Que, (Format-Tamano $sz))
    }
}

if ($presentes.Count -eq 0) {
    Write-Host "  no queda nada instalado" -ForegroundColor DarkGray
} else {
    Write-Host ("`n  Total: {0}" -f (Format-Tamano $total)) -ForegroundColor Yellow
    Write-Host ""
    if (ConfirmarJuego "Borrar todo esto?") {
        foreach ($i in $presentes) { Quitar $i.Full $i.Que }
    } else {
        Write-Host "  salteado" -ForegroundColor DarkGray
    }
}

# ------------------------------------------------------ 4. backups
$dirBackups = Join-Path $root 'backups'
$szBackups = Get-Tamano $dirBackups
if ($null -ne $szBackups -and $szBackups -gt 0) {
    Write-Host "`n[4] Backups" -ForegroundColor Cyan
    Write-Host ("  backups\  {0}" -f (Format-Tamano $szBackups))
    Write-Host "  Son tus personajes guardados. Si los borras no se recuperan." -ForegroundColor Yellow
    if (Confirmar "Borrar los backups tambien?") { Quitar $dirBackups 'backups' }
    else { Write-Host "  conservados" -ForegroundColor Green }
} else {
    Write-Host "`n[4] Backups: no hay" -ForegroundColor DarkGray
}

# ------------------------------------- 5. rastros fuera de la carpeta
Write-Host "`n[5] Cosas fuera de la carpeta del repo" -ForegroundColor Cyan

# Regla de firewall, si la creaste siguiendo docs/04-red-y-puertos.md
$regla = Get-NetFirewallRule -DisplayName 'MU Online Server' -ErrorAction SilentlyContinue
if ($regla) {
    Write-Host "  Regla de firewall 'MU Online Server'"
    if (Confirmar "Borrarla? (necesita PowerShell como administrador)") {
        if ($DryRun) { Write-Host "    [dry-run]" -ForegroundColor DarkGray }
        else {
            try { $regla | Remove-NetFirewallRule -ErrorAction Stop; Write-Host "    borrada" -ForegroundColor Green }
            catch { Write-Host "    no se pudo (abri PowerShell como administrador)" -ForegroundColor Red }
        }
    }
} else {
    Write-Host "  Regla de firewall: no existe" -ForegroundColor DarkGray
}

# Tarea programada de backup, si la creaste siguiendo docs/05-mantenimiento.md
$tarea = Get-ScheduledTask -TaskName 'MU Backup' -ErrorAction SilentlyContinue
if ($tarea) {
    Write-Host "  Tarea programada 'MU Backup'"
    if (Confirmar "Borrarla?") {
        if ($DryRun) { Write-Host "    [dry-run]" -ForegroundColor DarkGray }
        else { Unregister-ScheduledTask -TaskName 'MU Backup' -Confirm:$false; Write-Host "    borrada" -ForegroundColor Green }
    }
} else {
    Write-Host "  Tarea programada: no existe" -ForegroundColor DarkGray
}

# Cache de NuGet: compilar OpenMU la infla bastante. Es compartida con
# cualquier otro proyecto .NET tuyo, por eso se pregunta aparte.
$nuget = Join-Path $env:USERPROFILE '.nuget\packages'
$szNuget = Get-Tamano $nuget
if ($null -ne $szNuget -and $szNuget -gt 0) {
    Write-Host ("  Cache de NuGet ({0})  <- compartida con otros proyectos .NET tuyos" -f (Format-Tamano $szNuget))
    if (Confirmar "Vaciarla?") { Quitar $nuget 'cache de NuGet' }
    else { Write-Host "    conservada" -ForegroundColor Green }
}

# --------------------------------------------------- 6. programas
Write-Host "`n[6] Programas instalados en el sistema" -ForegroundColor Cyan
Write-Host "  Estos los instalo el setup, pero puede que los uses para otra cosa." -ForegroundColor Yellow

$programas = @(
    @{ Id = 'Microsoft.DotNet.SDK.10'; Nombre = '.NET 10 SDK' },
    @{ Id = 'Git.Git';                 Nombre = 'Git' }
)
foreach ($prog in $programas) {
    $instalado = Invoke-Native { winget list --id $prog.Id --exact 2>$null }
    if ($LASTEXITCODE -eq 0 -and ($instalado -join '') -match [regex]::Escape($prog.Id)) {
        Write-Host "  $($prog.Nombre): instalado"
        if (Confirmar "Desinstalar $($prog.Nombre)?") {
            if ($DryRun) { Write-Host "    [dry-run] winget uninstall $($prog.Id)" -ForegroundColor DarkGray }
            else {
                Invoke-Native { winget uninstall --id $prog.Id --exact --disable-interactivity }
                Write-Host "    desinstalado" -ForegroundColor Green
            }
        } else {
            Write-Host "    conservado" -ForegroundColor Green
        }
    } else {
        Write-Host "  $($prog.Nombre): no esta instalado" -ForegroundColor DarkGray
    }
}

# Las Build Tools de C++ solo hacen falta para recompilar la DLL de red del
# cliente. install.ps1 no las instala, pero build-network-library.ps1 las pide.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    $vc = Invoke-Native { & $vswhere -products * -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath }
    if ($vc) {
        Write-Host "  Visual Studio Build Tools (C++): instalado - varios GB"
        Write-Host "    Solo hacen falta si vas a recompilar la DLL de red del cliente."
        Write-Host "    Para sacarlas:  winget uninstall Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor DarkGray
    }
}

# ------------------------------------------------------------ resumen
Write-Host "`n  ---------------------------------------------------------" -ForegroundColor Green
if ($DryRun) {
    Write-Host "  Dry-run terminado. No se borro nada." -ForegroundColor Cyan
} else {
    Write-Host "  Desinstalacion terminada." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Para terminar de sacar todo, borra esta carpeta:"
    Write-Host "    $root" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  (no se puede borrar sola mientras corre este script)"
}
Write-Host "  ---------------------------------------------------------`n" -ForegroundColor Green
