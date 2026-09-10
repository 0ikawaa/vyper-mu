# Instalacion

## Requisitos

- Windows 10 u 11, 64 bits
- ~6 GB de disco libre
- 4 GB de RAM libres
- Conexion decente: se bajan ~750 MB entre PostgreSQL y los assets del juego

`install.ps1` instala Git y el .NET 10 SDK si faltan. PostgreSQL queda portable
adentro de `server\`, no se instala en el sistema.

## Pasos

```powershell
git clone https://github.com/0ikawaa/vyper-mu.git
cd vyper-mu
.\install.ps1
```

Si PowerShell bloquea el script:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Que hace, en orden:

1. Instala Git y .NET 10 SDK si no estan
2. Descarga PostgreSQL 17 portable (325 MB) y crea la base
3. Clona OpenMU y lo compila (5-15 min)
4. Descarga los assets del juego (426 MB) y verifica el checksum

## Arrancar

```powershell
.\scripts\start.ps1
```

La primera vez OpenMU crea e inicializa la base: **1 a 3 minutos**. Queda con
73 mapas, 677 items y 472 monstruos de Season 6.

Verificar que quedo todo arriba:

```powershell
.\scripts\status.ps1
```

Panel de administracion: http://localhost:5000

## Jugar

```powershell
.\scripts\play.ps1
```

### Cuentas de prueba

**La password es igual al nombre de usuario.**

| Usuario | Que tiene |
|---|---|
| `test400` | Nivel 400, personajes master |
| `test300` | Nivel 300 |
| `test0` ... `test9` | Nivel 1 a 90, de 10 en 10 |
| `testgm` | Game Master |
| `testgm2` | Game Master con summoner y rage fighter |
| `testunlock` | Sin personajes, clases desbloqueadas |
| `ancient` | Sets ancient, nivel 330 |
| `socket` | Sets con socket, nivel 380 |
| `quest1` / `quest2` / `quest3` | Quests de nivel 150 / 220 / 400 |

Para crear una cuenta propia, escribi usuario y password nuevos en la pantalla
de login: se registra sola.

> **No dejes estas cuentas en un servidor publico.** Tienen credenciales
> conocidas y algunas son GM. Borralas desde el panel, en **Accounts**.

## Comandos del dia a dia

| Comando | Que hace |
|---|---|
| `.\scripts\start.ps1` | Levanta el servidor |
| `.\scripts\stop.ps1` | Lo apaga |
| `.\scripts\status.ps1` | Estado de todo |
| `.\scripts\logs.ps1` | Logs en vivo |
| `.\scripts\play.ps1` | Abre el cliente |
| `.\scriptsackup.ps1` | Backup de la base |
