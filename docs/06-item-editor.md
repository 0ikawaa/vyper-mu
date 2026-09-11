# Item Editor

Editor web de items, al estilo Mu Maker pero para este stack: edita **las dos
mitades** de un item, la del servidor (OpenMU, en PostgreSQL) y la del cliente
(MuMain, en `Item_eng.bmd`), y te avisa cuando no coinciden.

```powershell
.\scripts\start.ps1          # hace falta PostgreSQL corriendo
.\scripts\item-editor.ps1    # abre http://localhost:5050
```

La primera vez compila (~30 s). Es una app local: escucha solo en `localhost`
y no tiene login, no la expongas a internet.

### Dejarlo siempre disponible

```powershell
.\scripts\item-editor.ps1 -Instalar
```

Lo registra como tarea de Windows que arranca al iniciar sesion, corriendo
oculto, y deja un acceso directo **Item Editor** en el escritorio. Desde ese
momento http://localhost:5050 esta siempre, este o no corriendo el juego: el
editor no necesita OpenMU, solo la base, y si PostgreSQL esta apagado lo levanta
solo. `.\scripts\item-editor.ps1 -Desinstalar` lo saca (el `uninstall.ps1`
tambien). `status.ps1` muestra si esta corriendo.

## Cuentas, personajes, inventario y baul

Arriba, el boton **Cuentas** cambia de modo. A la izquierda la lista de cuentas
(buscador por cuenta o nombre de personaje; marca GM y baneadas). Al elegir una:

- **Personajes** como en la pantalla de seleccion del juego: nombre, clase,
  nivel, mapa, zen, cantidad de items. Se elige uno.
- **Equipo** (los 12 slots: manos, casco, armadura, pantalon, guantes, botas,
  alas, mascota, pendiente, anillos) y **stats** del personaje.
- **Inventario** (8x8 mas las extensiones que tenga) y **baul** de la cuenta
  (8x15, o 8x30 si es extendido), dibujados como en el juego, con la miniatura
  3D de cada item, el +nivel, borde verde para excelentes y azul para ancient.
- **Zen** del inventario y del baul, editable.

Con los items:

- **Clic** en uno: se edita a la derecha (modelo 3D, definicion, nivel,
  durabilidad/cantidad, skill, sockets, slot y todas las **opciones** que esa
  definicion admite: luck, option +N, excelentes, alas, harmony, sockets, y el
  **set ancient** con su bonus +5/+10).
- **Clic en una celda vacia** (o "+ Agregar item"): agrega uno nuevo ahi.
- **Arrastrar** entre inventario, equipo y baul; o los botones "Al baul" /
  "Al inventario" (primer lugar libre). **Duplicar** y **Borrar**.

El editor no deja pisar items (calcula el tamaño de cada uno en la grilla) y
guarda directo en la base, sin backup previo: son datos de jugadores, no
configuracion. Si necesitas volver atras, esta `backup.ps1`.

> **Importante**: editá personajes que **no esten conectados**. OpenMU guarda
> el personaje al salir del juego y pisaria tus cambios. Si OpenMU esta
> corriendo, el editor te lo recuerda con un aviso.

## Que se puede hacer

| Pestaña | Que edita | Donde vive |
|---|---|---|
| **General** | Nombre, grupo/numero, slot de equipo, tamaño en la grilla, nivel del item, nivel maximo (+15), durabilidad, valor, sockets, munición / ligado / quest, skill del item, efecto al consumir | Servidor |
| **Stats** | Stats base (`ItemBasePowerUpDefinition`): daño min/max, defensa, velocidad, rise, vida, mana, cualquier atributo de los 321 que tiene OpenMU; modo (suma / multiplica / suma final / maximo) y tabla de bonus por nivel | Servidor |
| **Requisitos** | Nivel, fuerza, agilidad, vitalidad, energia, comando o cualquier otro atributo | Servidor |
| **Clases** | Que clases lo equipan, por evolucion (DW / SM / GM, etc.) | Servidor |
| **Opciones y sets** | Opciones posibles (luck, skill, excelentes, harmony, sockets, ancient) y sets a los que pertenece | Servidor |
| **Drops** | Si dropea de monstruos, entre que niveles, y en que grupos de drop especiales entra (jewels, cajas, eventos) | Servidor |
| **Cliente** | El registro completo de `Item_eng.bmd`: nombre, nivel, tamaño, slot, daño, defensa, requisitos, clases, resistencias… | Cliente |

Y arriba de todo, para cualquier item, la **vista 3D**: el modelo `.bmd` que usa el
cliente, con su textura, girando. Se arrastra para rotar, rueda para acercar,
doble clic para centrar; botones para girar solo, wireframe y agrandar.

Y ademas:

- **Buscar** por nombre (servidor o cliente), por `grupo/numero` (`7 5`, `7/5`) o por indice.
- **Filtros** por grupo, clase, "dropea", "sin registro en cliente" y **"cliente desincronizado"**.
- **Clonar**: crea un item nuevo copiando stats, requisitos, clases y opciones a otro grupo/numero, y opcionalmente su registro en el cliente.
- **Borrar**: si algun personaje tiene uno en el inventario, el servidor lo rechaza y no borra nada.
- **Deshacer** (`Ctrl+Z`) el ultimo guardado, servidor o cliente.
- `Ctrl+S` guarda, `/` va al buscador, `↑` `↓` recorren la lista.

## Servidor vs. cliente: por que hay dos lados

En MU el cliente no le pregunta al servidor como es un item: tiene su propia
tabla (`Data\Local\Eng\Item_eng.bmd`) con nombre, tamaño, requisitos, daño,
defensa, clases… y es lo que el jugador **ve** en el tooltip y en el inventario.
El servidor (OpenMU, esquema `config` en PostgreSQL) tiene su propia
definicion, que es la que **aplica**: daño real, si te deja equiparlo, que
dropea.

Si editas solo un lado, el juego miente: el tooltip dice una cosa y pasa otra.
Por eso la pestaña **Cliente** compara los dos y ofrece:

- **← Aplicar valores del servidor al cliente**: rellena el registro del cliente
  con lo que el servidor implica (nombre, nivel, tamaño, requisitos, daño,
  defensa, bloqueo, velocidad, poder magico, clases).
- **Importar del cliente al servidor →**: al reves, util si editaste el
  `Item_eng.bmd` con otra herramienta.

Los dos lados se relacionan por `indice = grupo * 512 + numero`. La
correspondencia de campos es la que usa OpenMU al inicializar la data de
Season 6:

| Cliente (`Item_eng.bmd`) | Servidor (atributo de OpenMU) |
|---|---|
| Level | `DropLevel` |
| RequireStrength / Dexterity / Vitality / Energy / Charisma / Level | Requisito `Total … Requirement Value` / `Level` |
| DamageMin / DamageMax | `Minimum/Maximum Physical Base Damage By Weapon` |
| Defense | `Base Defense` (escudos: `Shield Defense (item)`) |
| SuccessfulBlocking | `Defense Rate (PvM)` |
| WeaponSpeed | `Attack Speed by Weapons` |
| MagicPower | `Staff Rise Percentage` × 2 (cetros: `Scepter Rise Percentage` × 2) |
| RequireClass[DW, DK, ELF, MG, DL, SUM, RF] | Clases habilitadas; el valor es el paso minimo (1 base, 2 segunda, 3 tercera) |

Al abrir la lista vas a ver algunos items marcados `≠`: son diferencias que
**ya vienen** entre la data de OpenMU y la del cliente MuMain (por ejemplo
`Katache` en el servidor y `Katana` en el cliente, o alas que OpenMU deja
equipar a la clase base). No es que el editor las haya roto; es informacion.

## Cuando se aplican los cambios

- **Cliente**: al proximo `play.ps1`. El archivo se reescribe entero con el
  mismo cifrado y checksum que espera MuMain.
- **Servidor**: OpenMU carga la configuracion **al arrancar**. Despues de guardar,
  la barra de arriba te avisa "Reinicia el server para aplicar":

  ```powershell
  .\scripts\stop.ps1
  .\scripts\start.ps1
  ```

  Los jugadores conectados se desconectan. Para un server con gente, editá y
  reiniciá en un horario tranquilo.

## Backups

Antes de escribir cualquier cosa el editor guarda en `tools\item-editor\backups\`:

| Archivo | Cuando |
|---|---|
| `Item_eng.<fecha>.bmd` | Copia del archivo del cliente, la primera vez que lo toca en cada sesion |
| `items-db.<fecha>.json` | Volcado de los 677 items del servidor, la primera vez que escribe en la base en cada sesion |
| `items\<grupo>-<numero>.<fecha>.<accion>.json` | El item tal como estaba antes de cada guardado o borrado |

Para volver atras el cliente: copiá el `.bmd` de backup sobre
`client\runtime\Data\Local\Eng\Item_eng.bmd`. Para el servidor, el
`backup.ps1` de siempre sigue siendo la red de seguridad grande; los JSON son
para recuperar un item puntual a mano (o pegarle el contenido al editor por la
API, `PUT /api/items/{id}`).

## Vista 3D: de donde sale

El visor lee los mismos archivos que el juego:

- **Modelo**: `Data\Item\*.bmd` o `Data\Player\*.bmd` (formato BMD de Webzen;
  version 0xC cifrada con la clave de mapas, 0xA en claro). Se calcula la pose
  del frame 0 con los huesos del propio archivo, igual que `BMD::Animation()`
  de MuMain.
- **Textura**: el nombre guardado dentro del BMD (`sword02.jpg`) se busca en la
  misma carpeta como `.OZJ` (JPEG con 24 bytes de cabecera), `.OZT` (TGA de 32
  bits con 4 bytes, se convierte a PNG) u `.OZB`.
- **Que modelo le toca a cada item**: `tools\item-editor\item-models.json`,
  845 indices. MuMain no tiene esa tabla en datos: la asocia en codigo C++
  (`OpenItems()`, `OpenPlayers()`, `CMonkSystem::LoadModelItem()`,
  `CChangeRingManager::LoadItemModel()`). El JSON lo genera
  `extract-item-models.py` interpretando esas funciones:

  ```powershell
  python tools\item-editor\extract-item-models.py <carpeta del repo de MuMain>
  ```

  Si actualizas el cliente y aparecen items nuevos, regeneralo. Quedan sin
  modelo 3 de los 677 items (Weapon of Archangel, Wizard's Ring, Christmas
  Star), que el cliente carga por rutas especiales.

Lo que se ve es una aproximacion fiel pero no identica al juego: no se aplican
los efectos de brillo/animacion de textura ("texture scripts") ni el color de
nivel (+7, +11…).

## Items nuevos y modelos 3D

Clonar un item a un numero libre funciona del lado del servidor y del cliente
(nombre, stats, tooltip). Lo que **no** se puede definir desde aca es el modelo
3D: MuMain asocia cada indice a su archivo `.bmd` en codigo, no en una tabla
(ver arriba). Un indice que el cliente no conoce se ve sin modelo — el visor te
lo dice ("Sin modelo"). Para darle uno hay que recompilar el cliente (ver
`docs\build\windows\console.md` en el repo de MuMain).

Lo que si funciona sin recompilar: reusar un indice que ya tenga modelo (por
ejemplo, redefinir por completo un item que no uses) o cambiar todo lo demas de
cualquiera de los 677 items originales.

## Detalles tecnicos

- `tools\item-editor\`: .NET 10 minimal API + Npgsql, frontend en HTML/JS plano
  (`wwwroot\`). No agrega dependencias: usa el mismo SDK que compila OpenMU.
- Lee la password de PostgreSQL de `server\.pgpassword` y se conecta a
  `localhost:5433/openmu`.
- Escribe con SQL directo en el esquema `config` (`ItemDefinition`,
  `AttributeRequirement`, `ItemBasePowerUpDefinition` y las tablas de union),
  siempre dentro de una transaccion. Los ids son GUID nuevos; el
  `GameConfigurationId` se toma de la fila unica de `GameConfiguration`.
- `Item_eng.bmd`: 8192 registros de 84 bytes (formato "legacy" de 30 bytes de
  nombre; tambien soporta el de 50), XOR con `FC CF AB`, checksum
  `GenerateCheckSum2` con clave `0xE2F1`. Igual que `ItemDataLoader.cpp` de MuMain.
- Vista 3D: `BmdModel.cs` (parser BMD + pose), `Textures.cs` (OZJ/OZT/OZB →
  JPEG/PNG/BMP), `wwwrootiewer.js` sobre three.js r128 (`wwwrootendor\`,
  MIT, sin CDN: funciona sin internet).
- Cuentas: `AccountsDb.cs` (esquema `data`: Account, Character, ItemStorage, Item,
  ItemOptionLink, ItemItemOfItemSet, StatAttribute) y `wwwrootccounts.js`.
- API cuentas: `GET /api/accounts`, `GET /api/accounts/{id}`,
  `GET /api/definitions/{id}/options`, `POST/PUT/DELETE /api/inventory/items[/{id}]`,
  `POST /api/inventory/items/{id}/move`, `PUT /api/storages/{id}/money`.
- API items: `GET/PUT /api/items/{id}`, `POST /api/items/{id}/clone`,
  `DELETE /api/items/{id}`, `GET/PUT/DELETE /api/client/items/{indice}`,
  `GET /api/models/{indice}` (malla ya posada, en JSON), `GET /api/textures?path=`,
  `GET /api/meta`, `GET /api/status`, `GET /api/backups`.
