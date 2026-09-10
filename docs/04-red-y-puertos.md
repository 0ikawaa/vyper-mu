# Red y puertos

## Puertos que usa el servidor

| Puerto | Que es | Abrir al exterior? |
|---|---|---|
| 5000 | Panel de administracion | **NO** — dejalo solo local |
| 5433 | PostgreSQL | **NO** — ya esta atado a localhost |
| 44405 | Connect server — cliente **original** de Webzen | Si |
| 44406 | Connect server — **MuMain** (el de este repo) | Si |
| 55901 | Game server 1 | Si |
| 55902 | Game server 2 | Si |
| 55903 | Game server 3 | Si |
| 55980 | Chat server (mensajeria in-game) | Si |

PostgreSQL corre en el **5433** (no en el 5432, para no chocar con otro
PostgreSQL que tengas instalado) y escucha solo en `localhost`. Si queres
conectarte con pgAdmin o DBeaver: `127.0.0.1:5433`, usuario `postgres`, y la
password esta en `server\.pgpassword`.

## Como conecta el cliente

No conecta directo al game server. El flujo es:

```
Cliente  --(44406)-->  Connect Server     "dame la lista de servidores"
Cliente  <---------->  Connect Server     lista de servers
Cliente  --(44406)-->  Connect Server     "quiero el Server 1"
Cliente  <---------->  Connect Server     "anda a <IP del resolver>:55901"
Cliente  --(55901)-->  Game Server        ya juega
```

La `<IP del resolver>` es la clave: si esa direccion no es alcanzable desde
donde esta el cliente, se desconecta justo al elegir servidor.

Se controla con el parametro `-resolveIP:` en `scripts\start.ps1`:

```powershell
-ArgumentList '-autostart', '-resolveIP:loopback'
```

| Valor | Reporta | Usar cuando |
|---|---|---|
| `loopback` | `127.127.127.127` | Cliente y server en la misma PC (por defecto) |
| `local` | Tu IP de LAN | Jugas con gente de tu red |
| `public` | Tu IP publica (via ipify) | Server abierto a internet |
| una IP o dominio | Lo que pongas | IP fija o dominio propio |

Tambien se puede cambiar en caliente desde el panel, en
**Configuration → System**.

## Escenarios

### Todo en tu PC (viene asi)

`-resolveIP:loopback`, y el cliente apunta a `127.127.127.127:44406`.
No hay que abrir nada en el firewall.

> El cliente de MU **bloquea `127.0.0.1`**. Por eso se usa `127.127.127.127`,
> que es lo que reporta el resolver `loopback`.

### Jugar con gente de tu red local

Edita `scripts\start.ps1` y cambia `-resolveIP:loopback` por `-resolveIP:local`.
Reinicia con `.\scripts\stop.ps1` y `.\scripts\start.ps1`, y averigua tu IP LAN:

```powershell
(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.PrefixOrigin -eq 'Dhcp' }).IPAddress
```

Los demas apuntan su cliente a esa IP:

```powershell
.\scripts\play.ps1 -ServerIP 192.168.1.50
```

Abri los puertos en el firewall de Windows (una sola vez, **como
administrador**):

```powershell
New-NetFirewallRule -DisplayName "MU Online Server" -Direction Inbound `
  -Protocol TCP -LocalPort 44405,44406,55901,55902,55903,55980 -Action Allow
```

Fijate que el 5000 y el 5433 **no** estan en esa lista, a proposito.

### Servidor abierto a internet

Cambia a `-resolveIP:public` (o pone tu IP fija / dominio). Ademas de la regla
de firewall de arriba, necesitas:

1. **Port forwarding** en el router: 44405, 44406, 55901-55903 y 55980 hacia la
   IP local de la maquina del servidor.
2. **IP publica real.** Si tu ISP te da CGNAT (IP compartida), el port
   forwarding no funciona y necesitas un VPS o un tunel.
3. **No expongas el panel admin** (5000) ni PostgreSQL (5433). Para administrar
   de forma remota, usa un tunel SSH:
   ```powershell
   ssh -L 5000:localhost:5000 usuario@tu-servidor
   ```

> Antes de abrirlo: borra las cuentas de prueba (`testgm`, `test0`…`test9`,
> `test400`, etc.) desde el panel, en **Accounts**. Tienen credenciales publicas
> y algunas son game master.
