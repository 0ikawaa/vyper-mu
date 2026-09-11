# vyper-mu

Servidor de **MU Online Season 6 Episodio 3** listo para jugar, sin addons.

- **Servidor**: [OpenMU](https://github.com/MUnique/OpenMU) (MIT), compilado y corriendo nativo
- **Cliente**: [MuMain](https://github.com/sven-n/MuMain), el cliente open source
- **Base de datos**: PostgreSQL 17 portable, dentro del propio proyecto

Sin alas custom, sin sets custom, sin armas custom, sin mapas custom. Solo la
data original de Season 6.

---

## Instalacion

En una PC limpia con Windows 10 u 11, lo unico que hace falta tener de antemano
es **Git**. Todo lo demas lo resuelve el instalador.

```powershell
git clone https://github.com/0ikawaa/vyper-mu.git
cd vyper-mu
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\install.ps1
```

La linea de `Set-ExecutionPolicy` solo afecta a esa ventana de PowerShell y es
necesaria porque Windows bloquea por defecto los scripts descargados.

Tarda entre 20 y 40 minutos: casi todo es descarga (PostgreSQL 325 MB, assets
del juego 426 MB) y compilar el servidor.

### Que instala, y que no

| | Donde queda |
|---|---|
| Git | En el sistema (lo instala con winget si falta) |
| .NET 10 SDK | En el sistema (idem) |
| PostgreSQL 17 | **Portable**, dentro de `server\pgsql\` |
| OpenMU | Compilado en `server\bin\` |
| Assets del juego | `client\runtime\Data\` |

Para sacarlo todo hay un desinstalador — ver [abajo](#desinstalar).

### Si Windows bloquea el cliente

`Main.exe` es un ejecutable descargado sin firma digital, asi que Defender o
SmartScreen pueden marcarlo. Es un build del cliente open source
[MuMain](https://github.com/sven-n/MuMain). Si te lo bloquea, agrega la carpeta
del repo como exclusion en Seguridad de Windows.

> No necesitas instalar el Visual C++ Redistributable: `Main.exe` esta enlazado
> estaticamente y la libreria de red usa la Universal CRT, que ya viene en
> Windows 10 y 11.

## Jugar

```powershell
.\scripts\start.ps1     # levanta el servidor (1-3 min la primera vez)
.\scripts\play.ps1      # abre el cliente
```

### Cuentas de prueba

**La password es igual al nombre de usuario.**

| Usuario | Que tiene |
|---|---|
| `test400` | Nivel 400, personajes master — el mejor para probar |
| `test300` | Nivel 300 |
| `test0` … `test9` | Nivel 1 a 90, de 10 en 10 |
| `testgm` | Game Master |
| `ancient` | Sets ancient, nivel 330 |
| `socket` | Sets con socket, nivel 380 |
| `quest1` / `quest2` / `quest3` | Quests de nivel 150 / 220 / 400 |

> Estas cuentas tienen credenciales publicas y algunas son GM. Si algun dia
> abris el servidor a otra gente, borralas desde el panel en **Accounts**.

Para crear tu propia cuenta, escribi usuario y password nuevos en la pantalla de
login: se registra sola.

---

## Comandos

**Servidor**

| Comando | Que hace |
|---|---|
| `.\scripts\start.ps1` | Levanta PostgreSQL + OpenMU |
| `.\scripts\stop.ps1` | Apaga todo (los datos se conservan) |
| `.\scripts\status.ps1` | Estado de procesos, puertos y cliente |
| `.\scripts\logs.ps1` | Logs en vivo (`-Postgres`, `-Errores`) |
| `.\scripts\backup.ps1` | Backup de la base |
| `.\scripts\restore.ps1 -File <archivo>` | Restaura un backup |
| `.\scripts\reset.ps1` | Borra la base y empieza de cero |
| `.\uninstall.ps1` | Desinstala todo (ver [Desinstalar](#desinstalar)) |

**Cliente**

| Comando | Que hace |
|---|---|
| `.\scripts\play.ps1` | Lanza el cliente |
| `.\scripts\play.ps1 -ServerIP <ip>` | Lanza apuntando a otro servidor |
| `.\scripts\setup-client.ps1` | Descarga los assets del juego |
| `.\scripts\build-network-library.ps1` | Recompila la DLL de red |

**Herramientas**

| Comando | Que hace |
|---|---|
| `.\scripts\item-editor.ps1` | Editor web de items (servidor + cliente) con vista 3D, en http://localhost:5050 — ver [docs/06-item-editor.md](docs/06-item-editor.md) |

**Panel de administracion**: http://localhost:5000 — rates, drops, servidores,
cuentas, spawns.

---

## Puertos

| Puerto | Que es |
|---|---|
| 5000 | Panel de administracion |
| 5433 | PostgreSQL (solo localhost) |
| 44405 | Connect server — cliente original de Webzen |
| 44406 | Connect server — MuMain **(el que usa este cliente)** |
| 55901-55903 | Game servers 1 a 3 |
| 55980 | Chat server |

Para jugar con otra gente ver [docs/04-red-y-puertos.md](docs/04-red-y-puertos.md).

---

## Documentacion

| Archivo | Contenido |
|---|---|
| [01-instalacion.md](docs/01-instalacion.md) | Instalacion paso a paso, cuentas de prueba |
| [02-cliente.md](docs/02-cliente.md) | El cliente MuMain: que trae, como funciona |
| [03-configuracion.md](docs/03-configuracion.md) | Rates, drops, servidores, que NO trae |
| [04-red-y-puertos.md](docs/04-red-y-puertos.md) | Firewall, LAN, internet |
| [05-mantenimiento.md](docs/05-mantenimiento.md) | Backups, updates, problemas comunes |
| [06-item-editor.md](docs/06-item-editor.md) | Item Editor: editar items del servidor y del cliente, con vista 3D |
| [docker/README.md](docker/README.md) | Alternativa con Docker |

---

## Que trae el servidor

Vanilla Season 6 Episodio 3, tal cual lo implementa OpenMU. La base se
inicializa con **73 mapas, 677 items y 472 monstruos** — todos originales.

- **Clases**: Dark Wizard, Dark Knight, Fairy Elf, Magic Gladiator, Dark Lord,
  Summoner, Rage Fighter, con sus evoluciones y Master Level
- **Mapas**: Lorencia, Noria, Devias, Dungeon, Atlans, Tarkan, Icarus,
  Kalima 1-7, Aida, Crywolf, Kanturu, Raklion, Swamp of Calmness, Vulcanus,
  Barracks, Refuge, Elvenland
- **Items**: sets Bronze -> Great Dragon, ancients, sockets, alas 1/2/3, Fenrir
- **Sistemas**: party, guild y alianzas, comercio, warehouse, Chaos Machine,
  PvP, quests, chat server, tiendas personales

Anda bien: login, personajes, combate, drops, NPCs, party, guild, comercio,
Chaos Machine, Master Level.

Parcial o pendiente: algunos eventos (Blood Castle, Devil Square y Chaos Castle
funcionan; Illusion Temple y Raklion estan a medias) y algunas skills. OpenMU es
un proyecto activo, no un file comercial cerrado.

## Sobre el cliente

`Main.exe` y `MUnique.Client.Library.dll` vienen en este repo: son builds de
codigo open source (MuMain y su libreria de red).

Los **assets del juego** (`Data\` y `fonts\`, 764 MB) **no** estan aca: son
propiedad de Webzen. `setup-client.ps1` los descarga del release publico de
MuMain y verifica el checksum SHA256.

MuMain trae algunas cosas por encima del cliente original — inventario y baul
extendidos, MU Helper, auto-reconnect, framerate desbloqueado — que vienen
compiladas y no se desactivan. La data del juego, en cambio, es la original de
Season 6. Detalle en [docs/02-cliente.md](docs/02-cliente.md).

## Desinstalar

```powershell
.\uninstall.ps1 -DryRun    # primero mira que encontraria, sin borrar nada
.\uninstall.ps1            # y despues, si queres, borra
```

Lista todo lo que dejo la instalacion con su tamano y va preguntando que sacar:

- Los datos del juego (assets, PostgreSQL portable, OpenMU compilado, la base)
- Los backups, si tenes
- La regla de firewall y la tarea programada de backup, si las creaste
- La cache de NuGet, que compilar OpenMU infla bastante
- Git y el .NET 10 SDK

**Siempre pregunta antes de tocar tus backups, la cache de NuGet, Git y el .NET
SDK**, incluso con `-Todo`: o son irreemplazables, o los puede estar usando otro
programa tuyo. Y si hay una base con personajes, te ofrece hacer un backup antes
de borrarla.

Al final queda la carpeta del repo vacia de datos; borrala a mano para terminar
(un script no puede borrar la carpeta desde la que se esta ejecutando).

## Requisitos

- Windows 10 u 11, 64 bits
- ~6 GB de disco
- 4 GB de RAM libres
- Git y .NET 10 SDK (los instala `install.ps1` si faltan)

## Licencias

- **OpenMU**: MIT — https://github.com/MUnique/OpenMU
- **MuMain**: sin licencia declarada en su repo — https://github.com/sven-n/MuMain
- **MU Online**, su arte y sus assets: © Webzen Inc. Este repo no los
  redistribuye; los scripts los descargan de sus fuentes originales.
