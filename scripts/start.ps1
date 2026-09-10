<#
    Levanta el servidor: PostgreSQL portable + OpenMU.
    Uso:  .\scripts\start.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Native { param([scriptblock]$Cmd)
    $prev=$ErrorActionPreference; $ErrorActionPreference='Continue'
    try { & $Cmd } finally { $ErrorActionPreference=$prev } }

$pgBin  = Join-Path $root 'server\pgsql\bin'
$pgData = Join-Path $root 'server\pgdata'
$logs   = Join-Path $root 'server\logs'
$srvBin = Join-Path $root 'server\bin'
$pwFile = Join-Path $root 'server\.pgpassword'

foreach ($p in @($pgData, $srvBin, $pwFile)) {
    if (-not (Test-Path $p)) {
        Write-Host "Falta $p" -ForegroundColor Red
        Write-Host "Corre primero:  .\install.ps1" -ForegroundColor Yellow
        exit 1
    }
}
New-Item -ItemType Directory -Path $logs -Force | Out-Null

# ---------- PostgreSQL ----------
$pgUp = Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue
if ($pgUp) {
    Write-Host "PostgreSQL: ya estaba corriendo" -ForegroundColor DarkGray
} else {
    Write-Host "Arrancando PostgreSQL..." -ForegroundColor Cyan
    # -w esperaria a que este listo, pero se queda tomando la consola en algunos
    # terminales de Windows; arrancamos sin esperar y sondeamos el puerto abajo.
    Invoke-Native { & (Join-Path $pgBin 'pg_ctl.exe') -D $pgData -l (Join-Path $logs 'postgres.log') start }

    $espera = 0
    while (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
        Start-Sleep -Seconds 1; $espera++
        if ($espera -ge 30) {
            Write-Host "PostgreSQL no levanto en 30s. Revisa server\logs\postgres.log" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  listo (puerto 5433)" -ForegroundColor Green
}

# ---------- OpenMU ----------
$yaCorre = Get-Process -Name 'MUnique.OpenMU.Startup' -ErrorAction SilentlyContinue
if ($yaCorre) {
    Write-Host "OpenMU: ya estaba corriendo (PID $($yaCorre.Id))" -ForegroundColor DarkGray
} else {
    Write-Host "Arrancando OpenMU..." -ForegroundColor Cyan
    $exe = Join-Path $srvBin 'MUnique.OpenMU.Startup.exe'
    # -autostart levanta los listeners solo; loopback hace que el connect server
    # le informe al cliente 127.127.127.127, que es lo que el cliente acepta.
    Start-Process -FilePath $exe -WorkingDirectory $srvBin `
        -ArgumentList '-autostart', '-resolveIP:loopback' `
        -RedirectStandardOutput (Join-Path $logs 'openmu.log') `
        -RedirectStandardError  (Join-Path $logs 'openmu.err.log')

    Write-Host "  inicializando (la primera vez tarda 1-3 min creando la base)..." -ForegroundColor Yellow
    $espera = 0
    while (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 44406 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
        Start-Sleep -Seconds 2; $espera += 2
        if ($espera % 20 -eq 0) { Write-Host "    ...$espera s" -ForegroundColor DarkGray }
        if ($espera -ge 300) {
            Write-Host "No abrio el puerto 44406 en 5 min. Revisa server\logs\openmu.log" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  listo" -ForegroundColor Green
}

Write-Host ""
Write-Host "Servidor levantado." -ForegroundColor Green
Write-Host "  Panel admin : http://localhost:5000"
Write-Host "  Cliente     : 127.127.127.127 : 44406"
Write-Host ""
Write-Host "Jugar:  .\scripts\play.ps1" -ForegroundColor Cyan
