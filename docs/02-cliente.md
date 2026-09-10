# El cliente (MuMain — open source)

Este servidor esta armado para **MuMain**, el cliente open source:
https://github.com/sven-n/MuMain

Es un fork del codigo fuente del cliente Season 5.2 (publicado por Louis en
`LouisEmulator/Main5.2`), limpiado y llevado a Season 6 Episodio 3. Es la unica
via publica y trazable para tener un cliente; el propio OpenMU lo referencia en
su documentacion y le reserva el puerto **44406**.

## Estado actual: 3 de 4 piezas listas

El cliente ya esta armado en `client\runtime\`:

| Pieza | Estado | De donde salio |
|---|---|---|
| `Main.exe` (22 MB, x64) | **Listo** | Artifact de CI de MuMain, branch `main`, 2026-08-28 |
| `Data\` (13.160 archivos) | **Listo** | Release publico `data-7fce146eb3fe34a0`, checksum verificado |
| `fonts\` | **Listo** | Mismo release |
| `config.ini` | **Listo** | Generado, apuntando a `127.127.127.127:44406` |
| `MUnique.Client.Library.dll` | **FALTA** | Hay que compilarla — ver abajo |

## Lo que falta: la libreria de red

`Main.exe` la carga en runtime con `LoadLibrary`. Sin ella el cliente abre, pero
no conecta: tira **"MUnique.Client.Library.dll missing"**.

No se publica precompilada en ningun lado — lo verifique:

- Los artifacts de CI de MuMain contienen **solo** `Main.exe`
- El proyecto no tiene releases de runtime publicados (solo el de datos)
- No esta en NuGet (`MUnique.Client.Library` no existe; solo hay
  `MUnique.OpenMU.Network`, `.Packets` y `.PlugIns`)
- No esta en los releases de OpenMU (solo ClientLauncher y Network.Analyzer)

Es un proyecto C# (`ClientLibrary/`) compilado con **.NET Native AOT**.

### Compilarla

Instalar primero los dos requisitos:

```powershell
winget install Microsoft.DotNet.SDK.10
winget install Microsoft.VisualStudio.2022.BuildTools
```

En el instalador de Build Tools hay que **tildar "Desktop development with C++"**:
Native AOT necesita el linker de MSVC y el Windows SDK. Sin eso la compilacion
falla.

Despues:

```powershell
.\scripts\build-network-library.ps1
```

El script clona MuMain (shallow), compila solo `ClientLibrary` con
`dotnet publish -r win-x64 /p:ci=true` y copia la DLL al lado de `Main.exe`.
Tarda varios minutos: Native AOT es lento.

> El flag `ci=true` saltea un paso de transformacion XSLT que solo corre en
> entornos de desarrollo. Los `.cs` que genera ya vienen commiteados en el repo.

## Jugar

```powershell
.\scripts\play.ps1
```

Verifica que el cliente este completo, avisa si el servidor no responde, y lo
lanza desde su directorio (`Main.exe` busca los assets en el working directory,
por eso no sirve el doble click desde otro lado).

Para apuntar a otro servidor sin editar el `config.ini` a mano:

```powershell
.\scripts\play.ps1 -ServerIP 192.168.1.50 -ServerPort 44406
```

## Por que el puerto 44406 y no el 44405

OpenMU levanta **dos** connect servers:

| Puerto | Cliente |
|---|---|
| 44405 | Cliente original de Webzen (protocolo estandar S6E3) |
| 44406 | MuMain (protocolo extendido) |

MuMain extiende el protocolo: damage y experiencia de mas de 16 bits,
serializacion de items mejorada, barra de vida del monstruo. Por eso tiene su
propio puerto. Cruzarlos puede llegar a funcionar hoy con warnings en el log,
pero deja de andar en cuanto cambien las claves o el metodo de encriptacion.

## Por que 127.127.127.127 y no 127.0.0.1

El cliente de MU **bloquea `127.0.0.1`**. Cualquier otra IP del rango
`127.x.x.x` anda, y `127.127.127.127` es la que reporta el resolver `loopback`
de OpenMU, que es como viene configurado `scripts\start.ps1`.

## Si te desconecta al elegir servidor

El cliente le pide al connect server la direccion del game server, y el server
le contesta la IP que determino su **IP resolver**. Si esa IP no es alcanzable
desde el cliente, corta ahi. Se arregla con el parametro `-resolveIP:` en
`scripts\start.ps1`:

| Valor | Reporta | Usar cuando |
|---|---|---|
| `loopback` | `127.127.127.127` | Cliente y server en la misma PC |
| `local` | Tu IP de LAN | Jugas con gente de tu red |
| `public` | Tu IP publica | Server abierto a internet |

---

## Que trae MuMain de mas

Vos pediste sin addons. La **data del juego es la original de Season 6**: no hay
alas, sets, armas ni mapas custom. Pero el cliente si trae features por encima
del original:

- Inventario y baul (vault) extendidos
- MU Helper integrado (auto-play)
- Auto-reconnect
- Resoluciones de pantalla extra
- Framerate desbloqueado (V-Sync sin limite de fps por defecto)
- Comandos de chat de diagnostico: `$fps <n>`, `$vsync on|off`, `$fpscounter on`,
  `$details on`, `$glstats on`

Nada de eso se puede desactivar desde config, viene compilado. Si queres el
cliente 100% vanilla, la unica opcion es el binario original 1.04d de Webzen,
que no se distribuye oficialmente — la respuesta del proyecto OpenMU a esa
pregunta es su Discord: https://discord.gg/2u5Agkd

## Rehacer el cliente desde cero

```powershell
.\scripts\setup-client.ps1 -Force
```

Vuelve a bajar `Main.exe` (busca el artifact vigente mas reciente por API), los
datos, verifica el checksum y regenera el `config.ini`.

> Los artifacts de CI **expiran a los ~90 dias**. El que se uso vence alrededor
> del 2026-11-26. Si ya expiro, el script te avisa y hay que compilar el cliente
> completo desde el codigo: ver `docs/build/windows/console.md` en el repo de
> MuMain (necesita Visual Studio, vcpkg y CMake).
