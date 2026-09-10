# Cliente

```
client/
├── runtime/          El cliente armado y listo (764 MB, ignorado por git)
│   ├── Main.exe                        build x64 de MuMain
│   ├── Data/                           assets del juego, 13.160 archivos
│   ├── fonts/
│   ├── config.ini                      apunta a 127.127.127.127:44406
│   └── MUnique.Client.Library.dll      <- FALTA, hay que compilarla
├── _build/           Clon temporal de MuMain (lo crea build-network-library.ps1)
└── MuMain-data.tar.gz  Archivo descargado, se puede borrar tras extraer
```

## Comandos

| Comando | Que hace |
|---|---|
| `..\scripts\setup-client.ps1` | Arma el cliente (Main.exe + Data + config) |
| `..\scripts\build-network-library.ps1` | Compila la DLL de red que falta |
| `..\scripts\play.ps1` | Lanza el cliente |

Documentacion completa: [../docs/02-cliente.md](../docs/02-cliente.md)

## Espacio en disco

`runtime/` ocupa ~764 MB. El `.tar.gz` de datos son 426 MB mas: una vez extraido
lo podes borrar, `setup-client.ps1` lo vuelve a bajar si hace falta.
