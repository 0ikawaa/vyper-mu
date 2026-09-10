# Alternativa: correr el servidor con Docker

El setup por defecto de este repo corre **nativo** (PostgreSQL portable + OpenMU
compilado), porque Docker Desktop en Windows necesita WSL2 y eso obliga a
reiniciar la maquina.

Si preferis Docker, aca esta el compose listo.

## Requisitos

- Docker Desktop con el plugin compose: https://docs.docker.com/get-started/get-docker/
- WSL2 habilitado (Docker Desktop lo instala, **pide reiniciar**)

## Uso

```powershell
cd docker
copy .env.example .env
notepad .env            # cambiar DB_PASSWORD y ADMIN_PASSWORD
docker compose up -d
```

Panel de administracion: http://localhost:8080

## Diferencias con el modo nativo

| | Nativo (por defecto) | Docker |
|---|---|---|
| Panel admin | http://localhost | http://localhost:8080 |
| PostgreSQL | portable, puerto 5433 | contenedor, sin puerto publicado |
| Requiere reiniciar | no | si (WSL2) |
| Arranque | `..\scripts\start.ps1` | `docker compose up -d` |

Los puertos del juego son los mismos en ambos: 44405, 44406, 55901-55903, 55980.

> Los dos modos usan bases de datos distintas. Si venias jugando en nativo y
> pasas a Docker, hace un backup primero (`..\scripts\backup.ps1`) — los
> personajes no se migran solos.
