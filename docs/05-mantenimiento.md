# Mantenimiento y problemas comunes

## Backups

```powershell
.\scripts\backup.ps1
```

Guarda en `.\backups\openmu-AAAAMMDD-HHmmss.dump` y conserva los ultimos 10.
Incluye todo: cuentas, personajes, items, guilds y la configuracion del server.

Para automatizarlo todos los dias a las 5 AM (ajusta la ruta):

```powershell
$accion  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -File "C:\ruta\a\vyper-mu\scripts\backup.ps1"'
$trigger = New-ScheduledTaskTrigger -Daily -At 5am
Register-ScheduledTask -TaskName 'MU Backup' -Action $accion -Trigger $trigger
```

Restaurar:

```powershell
.\scripts\restore.ps1 -File .\backups\openmu-20260910-143000.dump
```

## Actualizar OpenMU

```powershell
.\scripts\backup.ps1              # primero el backup, siempre
.\scripts\stop.ps1
git -C server\_src pull
Remove-Item server\bin -Recurse -Force
.\install.ps1 -SkipClient          # recompila
.\scripts\start.ps1
```

Si la version nueva trae cambios de esquema, las migraciones corren al arrancar.

## Actualizar el cliente

```powershell
.\scripts\build-network-library.ps1   # rebaja MuMain y recompila la DLL de red
.\scripts\setup-client.ps1 -Force     # rebaja los assets
```

`Main.exe` viene en el repo. Para una version mas nueva hay que compilarla desde
el codigo de MuMain: ver `docs/build/windows/console.md` en ese repo.

---

## Problemas comunes

### El cliente se desconecta al elegir servidor

El caso mas frecuente. Es el IP resolver: el connect server le esta informando
al cliente una IP a la que no llega. Cambia `-resolveIP:` en `scripts\start.ps1`
segun donde este el cliente (ver [04-red-y-puertos.md](04-red-y-puertos.md)) y
reinicia el servidor.

### El cliente no conecta a 127.0.0.1

El cliente de MU **bloquea `127.0.0.1`** explicitamente. Usa `127.127.127.127`,
que es lo que viene en `client\runtime\config.ini`.

### "MUnique.Client.Library.dll missing"

Falta la libreria de red al lado de `Main.exe`. Compilala:

```powershell
.\scripts\build-network-library.ps1
```

Necesita el .NET 10 SDK y las Build Tools de C++ (workload
"Desktop development with C++").

### Warnings en el log sobre el protocolo

Estas conectando por el puerto equivocado. MuMain va por **44406**; el cliente
original de Webzen por **44405**. Hoy puede llegar a funcionar cruzado con
warnings, pero deja de andar en cuanto cambien las claves de encriptacion.

### El panel admin no abre

```powershell
.\scripts\status.ps1
.\scripts\logs.ps1 -Errores
```

El panel esta en http://localhost:5000. Si ese puerto esta ocupado, OpenMU elige
otro; buscalo en el log:

```powershell
Select-String -Path server\logs\openmu.log -Pattern "bound to urls"
```

### Tarda muchisimo el primer arranque

Normal: 1 a 3 minutos inicializando la base con la data de Season 6. Segui el
progreso con `.\scripts\logs.ps1`. Si pasan mas de 5 minutos sin moverse, revisa
la base:

```powershell
.\scripts\logs.ps1 -Postgres
```

### "Falta server\.pgpassword"

Se borro el archivo con la password de la base; sin el no hay forma de
conectarse. Si tenes backup: borra `server\pgdata`, corre `.\install.ps1` para
reinicializar, y despues `.\scripts\restore.ps1`. Si no, se pierde la base.

### PostgreSQL no levanta

```powershell
.\scripts\logs.ps1 -Postgres
```

Causa habitual: quedo un `postmaster.pid` de un apagado sucio. Si el log lo
menciona y estas seguro de que no hay otro PostgreSQL corriendo:

```powershell
Remove-Item server\pgdata\postmaster.pid
.\scripts\start.ps1
```

### Se llena el disco

Lo que mas ocupa:

| Carpeta | Tamano | Se puede borrar? |
|---|---|---|
| `client\runtime\Data` | 764 MB | Si, `setup-client.ps1` la rebaja |
| `client\MuMain-data.tar.gz` | 426 MB | Si, es solo el archivo descargado |
| `server\pgsql` | 325 MB | No, son los binarios de PostgreSQL |
| `server\_src` | ~200 MB | Si, pero no vas a poder recompilar sin volver a clonar |
| `server\bin` | ~150 MB | Si, se regenera con `install.ps1` |
| `server\pgdata` | crece con el uso | **No**, es tu base de datos |

---

## Empezar de cero

```powershell
.\scripts\backup.ps1   # por las dudas
.\scripts\reset.ps1
.\scripts\start.ps1
```

`reset.ps1` borra la base; al arrancar, OpenMU la recrea e inicializa de nuevo
con la data de Season 6 y las cuentas de prueba.
