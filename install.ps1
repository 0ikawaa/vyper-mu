<#
    vyper-mu - Instalacion completa.

    Deja el servidor MU Online (OpenMU, Season 6 Ep3) y el cliente listos para
    jugar. Lo unico que se instala a nivel sistema es Git y el .NET SDK:
    PostgreSQL queda portable dentro de server\, y se desinstala borrando la
    carpeta.

    Uso:   .\install.ps1
           .\install.ps1 -SkipClient    (solo el servidor)

    Tarda 20-40 minutos la primera vez; casi todo es descarga.
#>
param([switch]$SkipClient)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'
$root = $PSScriptRoot

# git y dotnet escriben su progreso en stderr. En PowerShell 5.1 eso se convierte
# en ErrorRecord y con -EA Stop aborta el script aunque el comando haya ido bien.
# Cada paso verifica $LASTEXITCODE por su cuenta.
function Invoke-Native {
    param([scriptblock]$Cmd)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Cmd } finally { $ErrorActionPreference = $prev }
}
function Write-Step($n, $t) { Write-Host "`n[$n] $t" -ForegroundColor Cyan }
function Sync-Path {
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' +
                [Environment]::GetEnvironmentVariable('Path','User')
}
function Test-Puerto($p) {
    Test-NetConnection -ComputerName 127.0.0.1 -Port $p -InformationLevel Quiet -WarningAction SilentlyContinue
}

Write-Host "`n  vyper-mu  -  MU Online Season 6 Episodio 3" -ForegroundColor Green
Write-Host "  Servidor OpenMU + cliente MuMain, sin addons`n" -ForegroundColor Green

# ------------------------------------------------------------ 1. requisitos
Write-Step 1 "Requisitos"

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "  Falta winget. Actualiza 'Instalador de aplicacion' desde Microsoft Store." -ForegroundColor Red
    exit 1
}

if (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Host "  Git: ya esta" -ForegroundColor DarkGray
} else {
    Write-Host "  Git: instalando..." -ForegroundColor Yellow
    Invoke-Native { winget install --id Git.Git --accept-source-agreements --accept-package-agreements --disable-interactivity }
    Sync-Path
}

$tieneSdk = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $tieneSdk = [bool]((Invoke-Native { dotnet --list-sdks }) -match '^10\.')
}
if ($tieneSdk) {
    Write-Host "  .NET 10 SDK: ya esta" -ForegroundColor DarkGray
} else {
    Write-Host "  .NET 10 SDK: instalando..." -ForegroundColor Yellow
    Invoke-Native { winget install --id Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements --disable-interactivity }
    Sync-Path
    if (-not ((Invoke-Native { dotnet --list-sdks }) -match '^10\.')) {
        Write-Host "  No quedo instalado el SDK 10. Instalalo a mano:" -ForegroundColor Red
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
        exit 1
    }
}

# ---------------------------------------------------------- 2. PostgreSQL
Write-Step 2 "PostgreSQL (portable)"

$pgDir  = Join-Path $root 'server\pgsql'
$pgData = Join-Path $root 'server\pgdata'
$pwFile = Join-Path $root 'server\.pgpassword'
$logs   = Join-Path $root 'server\logs'
New-Item -ItemType Directory -Path (Join-Path $root 'server') -Force | Out-Null
New-Item -ItemType Directory -Path $logs -Force | Out-Null

if (Test-Path (Join-Path $pgDir 'bin\postgres.exe')) {
    Write-Host "  Binarios: ya estan" -ForegroundColor DarkGray
} else {
    # Deliberadamente NO se usa 'winget install PostgreSQL': ese instalador abre
    # un asistente grafico que ignora los flags silenciosos y se queda esperando
    # clicks. Los binarios portables son reproducibles y no tocan el sistema.
    $zip = Join-Path $root 'server\pg.zip'
    if (-not (Test-Path $zip)) {
        Write-Host "  Descargando PostgreSQL 17 (~325 MB)..." -ForegroundColor Yellow
        curl.exe -L --progress-bar -o $zip 'https://get.enterprisedb.com/postgresql/postgresql-17.11-3-windows-x64-binaries.zip'
        if ($LASTEXITCODE -ne 0) { Write-Host "  Fallo la descarga." -ForegroundColor Red; exit 1 }
    }
    Write-Host "  Extrayendo..." -ForegroundColor Yellow
    Expand-Archive -Path $zip -DestinationPath (Join-Path $root 'server') -Force
    Remove-Item $zip -Force
    Write-Host "  OK" -ForegroundColor Green
}

if (Test-Path $pgData) {
    Write-Host "  Base de datos: ya inicializada" -ForegroundColor DarkGray
} else {
    Write-Host "  Inicializando la base..." -ForegroundColor Yellow
    $pw = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ })
    Set-Content $pwFile $pw -NoNewline -Encoding ascii
    $tmp = Join-Path $root 'server\.pginit'
    Set-Content $tmp $pw -NoNewline -Encoding ascii
    Invoke-Native { & (Join-Path $pgDir 'bin\initdb.exe') -D $pgData -U postgres --pwfile=$tmp -E UTF8 --locale=C }
    Remove-Item $tmp -Force
    if (-not (Test-Path (Join-Path $pgData 'PG_VERSION'))) {
        Write-Host "  Fallo initdb." -ForegroundColor Red; exit 1
    }

    # initdb deja autenticacion 'trust' (cualquiera local entra sin password).
    # Lo endurecemos, y movemos el puerto a 5433 para no chocar con otro
    # PostgreSQL que pueda haber instalado en el sistema.
    $hba = Join-Path $pgData 'pg_hba.conf'
    (Get-Content $hba) -replace '\btrust\b', 'scram-sha-256' | Set-Content $hba
    Add-Content (Join-Path $pgData 'postgresql.conf') "`n# --- vyper-mu ---`nlisten_addresses = 'localhost'`nport = 5433"
    Write-Host "  OK" -ForegroundColor Green
}

if (-not (Test-Puerto 5433)) {
    Invoke-Native { & (Join-Path $pgDir 'bin\pg_ctl.exe') -D $pgData -l (Join-Path $logs 'postgres.log') start }
    $n = 0
    while (-not (Test-Puerto 5433)) {
        Start-Sleep 1; $n++
        if ($n -ge 30) { Write-Host "  PostgreSQL no levanto. Revisa server\logs\postgres.log" -ForegroundColor Red; exit 1 }
    }
}
Write-Host "  Corriendo en el puerto 5433" -ForegroundColor Green

# ------------------------------------------------------------- 3. servidor
Write-Step 3 "Servidor OpenMU"

$src    = Join-Path $root 'server\_src'
$srvBin = Join-Path $root 'server\bin'

if (Test-Path $src) {
    Write-Host "  Codigo: ya clonado" -ForegroundColor DarkGray
} else {
    Write-Host "  Clonando OpenMU..." -ForegroundColor Yellow
    Invoke-Native { git clone --quiet --depth 1 https://github.com/MUnique/OpenMU.git $src }
    if ($LASTEXITCODE -ne 0) { Write-Host "  Fallo el clone." -ForegroundColor Red; exit 1 }
}

if (Test-Path (Join-Path $srvBin 'MUnique.OpenMU.Startup.exe')) {
    Write-Host "  Servidor: ya compilado" -ForegroundColor DarkGray
} else {
    # El generador de codigo de Persistence se invoca con --no-build desde otros
    # proyectos, asi que hay que compilarlo antes o el publish falla.
    Write-Host "  Compilando el generador de codigo..." -ForegroundColor Yellow
    Invoke-Native { dotnet build (Join-Path $src 'src\Persistence\SourceGenerator\MUnique.OpenMU.Persistence.SourceGenerator.csproj') -c Release --nologo -v quiet }
    if ($LASTEXITCODE -ne 0) { Write-Host "  Fallo el generador de codigo." -ForegroundColor Red; exit 1 }

    Write-Host "  Compilando el servidor (5-15 min)..." -ForegroundColor Yellow
    Invoke-Native { dotnet publish (Join-Path $src 'src\Startup\MUnique.OpenMU.Startup.csproj') -c Release -o $srvBin --nologo -v minimal }
    if (-not (Test-Path (Join-Path $srvBin 'MUnique.OpenMU.Startup.exe'))) {
        Write-Host "  Fallo la compilacion del servidor." -ForegroundColor Red; exit 1
    }
    Write-Host "  OK" -ForegroundColor Green
}

# Solo las dos primeras cadenas necesitan el superusuario; OpenMU crea el resto
# de los roles por su cuenta al arrancar. Se aplica siempre, porque el publish
# sobrescribe el XML con la copia del codigo fuente.
$pw = (Get-Content $pwFile -Raw).Trim()
foreach ($cs in @((Join-Path $srvBin 'ConnectionSettings.xml'),
                  (Join-Path $src 'src\Persistence\EntityFramework\ConnectionSettings.xml'))) {
    if (Test-Path $cs) {
        $xml = Get-Content $cs -Raw
        $xml = $xml -replace 'Server=localhost;Port=5432;', 'Server=localhost;Port=5433;'
        $xml = $xml -replace 'User Id=postgres;Password=[^;]*;', "User Id=postgres;Password=$pw;"
        Set-Content $cs $xml -Encoding UTF8
    }
}
Write-Host "  Conexion a la base configurada" -ForegroundColor Green

# -------------------------------------------------------------- 4. cliente
if ($SkipClient) {
    Write-Step 4 "Cliente salteado (-SkipClient)"
} else {
    Write-Step 4 "Cliente"
    & (Join-Path $root 'scripts\setup-client.ps1')
}

Write-Host "`n  ---------------------------------------------------------" -ForegroundColor Green
Write-Host "  Listo.`n" -ForegroundColor Green
Write-Host "    .\scripts\start.ps1     levanta el servidor"
Write-Host "    .\scripts\play.ps1      abre el cliente"
Write-Host "    .\scripts\status.ps1    ver como esta todo`n"
Write-Host "  Panel de administracion:  http://localhost:5000"
Write-Host "  ---------------------------------------------------------`n" -ForegroundColor Green
