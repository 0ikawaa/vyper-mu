# Configuracion del servidor

Casi todo se toca en el **panel admin** (http://localhost:5000), no en archivos.
Eso es a proposito: los cambios se aplican sobre la base de datos, en caliente.

## Rates de experiencia y drop

**Configuration → Game Configuration**

| Ajuste | Que controla |
|---|---|
| `ExperienceRate` | Multiplicador de experiencia. `1.0` = como el original |
| `ItemDropDuration` | Segundos que un item queda en el piso |
| `MaximumLevel` | Nivel maximo (`400` en S6E3) |
| `MaximumMasterLevel` | Nivel master maximo |

Los drops por monstruo se editan en **Monsters**, en su *drop item group*.
Ahi tambien defines la chance de cada grupo.

## Servidores de juego

**Servers** — lista de todos los sub-servidores (connect, game 1-3, chat).

Desde ahi podes:
- Arrancar y parar cada uno por separado
- Cambiar puertos
- Cambiar el limite de jugadores por game server
- Marcar un server como PvP o no-PvP

## Sistema e IP

**Configuration → System** — aca esta el **IP resolver**, que es lo que decide
que direccion se le informa al cliente al elegir servidor. Si te desconecta en
la pantalla de seleccion, el problema esta aca. Ver [02-cliente.md](02-cliente.md).

## Cuentas y bans

**Accounts** — buscar cuentas, cambiar estado, banear, dar permisos de GM.

## Reinicializar la base con otra version

**Setup** te deja recrear la base eligiendo:
- Version del juego (Season 6 / 0.75 / 0.95d)
- Cuantos game servers crear
- Si crear o no las cuentas de prueba

> **Recrear la base borra todos los personajes.** Haceté un backup primero:
> `.\scripts\backup.ps1`

---

## Que NO trae este setup

Como pediste, esto es vanilla Season 6 Episodio 3. Concretamente **no hay**:

- Alas custom (solo alas 1, 2 y 3 originales)
- Sets custom (solo del Bronze al Great Dragon originales)
- Armas custom
- Mapas custom (solo los originales: Lorencia, Noria, Devias, Dungeon, Atlans,
  Tarkan, Icarus, Kalima, Aida, Crywolf, Kanturu, Raklion, Swamp of Calmness,
  Vulcanus, Barracks, Refuge, Elvenland)
- Sistemas de shop de creditos, VIP, rankings web, ni nada del estilo

Todo eso simplemente no existe en el codigo de OpenMU. No hay nada que
desinstalar ni desactivar.

## Que esta incompleto

OpenMU es un proyecto activo, no un file comercial pulido. Anda bien:
login, creacion de personajes, movimiento, monstruos, drops, NPCs y tiendas,
party, guild, comercio, warehouse, Chaos Machine, PvP, Master Level, quests.

Puede estar parcial o faltar: algunos eventos (Blood Castle, Devil Square y
Chaos Castle funcionan; otros como Illusion Temple o Raklion estan a medias) y
algunas skills. Antes de prometerle algo a jugadores, probalo vos primero.
