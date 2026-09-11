// vyper-mu Item Editor: editor web local de items para OpenMU + MuMain.
//
//   dotnet run --project tools\item-editor          (o scripts\item-editor.ps1)
//
// Lee la password de PostgreSQL de server\.pgpassword, escribe en la base
// "openmu" (esquema config) y en client\runtime\Data\Local\Eng\Item_eng.bmd.
// Antes de tocar cualquier cosa deja una copia en tools\item-editor\backups\.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using VyperMu.ItemEditor;

var root = FindRoot();
var backupDir = Path.Combine(root, "tools", "item-editor", "backups");
Directory.CreateDirectory(backupDir);

EnsurePostgres(root);

var pwFile = Path.Combine(root, "server", ".pgpassword");
var password = File.Exists(pwFile) ? File.ReadAllText(pwFile).Trim() : "admin";
var port = Environment.GetEnvironmentVariable("VYPER_PG_PORT") ?? "5433";
var db = new Db($"Server=localhost;Port={port};User Id=postgres;Password={password};Database=openmu;Command Timeout=60;");

var accounts = new AccountsDb(db);
var clientFilePath = Path.Combine(root, "client", "runtime", "Data", "Local", "Eng", "Item_eng.bmd");
var clientRuntime = Path.Combine(root, "client", "runtime");
var modelTable = LoadModelTable(Path.Combine(root, "tools", "item-editor", "item-models.json"));
float[][]? playerSkeleton = null;
var clientLock = new SemaphoreSlim(1, 1);
var clientBackedUp = false;
var serverBackedUp = false;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Path.Combine(root, "tools", "item-editor"),
    WebRootPath = Path.Combine(root, "tools", "item-editor", "wwwroot"),
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("VYPER_EDITOR_URL") ?? "http://localhost:5050");
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/status", async () =>
{
    string? dbError = null;
    try { await db.PingAsync(); }
    catch (Exception ex) { dbError = ex.Message; }

    int nameLength = 0;
    bool clientOk = false;
    if (ClientItemFile.Exists(clientFilePath))
    {
        try { nameLength = ClientItemFile.Load(clientFilePath).NameLength; clientOk = true; }
        catch { clientOk = false; }
    }

    return Results.Ok(new Status(dbError is null, dbError, clientOk, clientFilePath, nameLength, IsServerRunning(), root, backupDir));
});

api.MapGet("/meta", async () => Results.Ok(await db.GetMetaAsync()));

api.MapGet("/items", async () => Results.Ok(await db.GetItemsAsync()));

api.MapGet("/items/{id:guid}", async (Guid id) =>
{
    var item = await db.GetItemAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

api.MapPut("/items/{id:guid}", async (Guid id, ItemDetail item) =>
{
    if (item.Id != id) return Problem("El id del cuerpo no coincide con la URL.");
    if (item.Number is < 0 or > 511) return Problem("El numero tiene que estar entre 0 y 511.");
    if (item.Group > 15) return Problem("El grupo tiene que estar entre 0 y 15.");
    if (string.IsNullOrWhiteSpace(item.Name)) return Problem("El nombre no puede estar vacio.");
    if (await db.ExistsGroupNumberAsync(item.Group, item.Number, id)) return Problem($"Ya existe otro item con grupo {item.Group} / numero {item.Number}.");

    var previous = await db.GetItemAsync(id);
    if (previous is null) return Results.NotFound();
    await BackupServerAsync(previous, "update");

    var saved = await db.UpdateItemAsync(item);
    return Results.Ok(saved);
});

api.MapPost("/items/{id:guid}/clone", async (Guid id, CloneRequest request) =>
{
    if (request.Number is < 0 or > 511) return Problem("El numero tiene que estar entre 0 y 511.");
    if (request.Group > 15) return Problem("El grupo tiene que estar entre 0 y 15.");
    if (await db.ExistsGroupNumberAsync(request.Group, request.Number, null)) return Problem($"Ya existe un item con grupo {request.Group} / numero {request.Number}.");

    await EnsureServerSnapshotAsync();
    var meta = await db.GetMetaAsync();
    var created = await db.CloneItemAsync(id, request, meta.GameConfigurationId);
    return Results.Ok(created);
});

api.MapDelete("/items/{id:guid}", async (Guid id) =>
{
    var previous = await db.GetItemAsync(id);
    if (previous is null) return Results.NotFound();
    await BackupServerAsync(previous, "delete");
    try
    {
        await db.DeleteItemAsync(id);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Problem(ex.Message);
    }
});

// ------------------------------------------------------------ cliente (Item_eng.bmd)

api.MapGet("/client/items", async () =>
{
    if (!ClientItemFile.Exists(clientFilePath)) return Results.Ok(Array.Empty<ClientItem>());
    await clientLock.WaitAsync();
    try
    {
        var file = ClientItemFile.Load(clientFilePath);
        return Results.Ok(file.ReadAll().Where(i => !i.IsEmpty).ToList());
    }
    finally { clientLock.Release(); }
});

api.MapGet("/client/items/{index:int}", async (int index) =>
{
    if (!ClientItemFile.Exists(clientFilePath)) return Results.NotFound();
    await clientLock.WaitAsync();
    try
    {
        return Results.Ok(ClientItemFile.Load(clientFilePath).Read(index));
    }
    finally { clientLock.Release(); }
});

api.MapPut("/client/items/{index:int}", async (int index, ClientItem item) =>
{
    if (!ClientItemFile.Exists(clientFilePath)) return Problem("No esta el archivo Item_eng.bmd del cliente (corre scripts\\setup-client.ps1).");
    if (index != item.Index) return Problem("El indice del cuerpo no coincide con la URL.");
    if (string.IsNullOrWhiteSpace(item.Name)) return Problem("El nombre no puede estar vacio.");

    await clientLock.WaitAsync();
    try
    {
        BackupClientFile();
        var file = ClientItemFile.Load(clientFilePath);
        file.Write(item);
        file.Save();
        return Results.Ok(file.Read(index));
    }
    finally { clientLock.Release(); }
});

api.MapDelete("/client/items/{index:int}", async (int index) =>
{
    if (!ClientItemFile.Exists(clientFilePath)) return Results.NotFound();
    await clientLock.WaitAsync();
    try
    {
        BackupClientFile();
        var file = ClientItemFile.Load(clientFilePath);
        file.Clear(index);
        file.Save();
        return Results.NoContent();
    }
    finally { clientLock.Release(); }
});

// ------------------------------------------------------------ modelos 3D (Fase 2)

api.MapGet("/models", () => Results.Ok(modelTable));

api.MapGet("/models/{index:int}", (int index) =>
{
    if (!modelTable.TryGetValue(index, out var relative)) return Results.Ok(new { index, file = (string?)null, exists = false });
    var path = Path.Combine(clientRuntime, relative);
    if (!File.Exists(path)) return Results.Ok(new { index, file = relative, exists = false });

    try
    {
        var model = BmdModel.Load(path);
        var warnings = new List<string>();
        float[][]? skeleton = null;
        if (model.Bones.Count == 0 && model.Meshes.Any(m => m.Vertices.Any(v => v.Node > 0)))
        {
            // partes de armadura: los vertices refieren a los huesos de Player.bmd
            playerSkeleton ??= LoadPlayerSkeleton();
            skeleton = playerSkeleton;
            if (skeleton is null) warnings.Add(@"No pude cargar Data\Player\Player.bmd para posar la armadura.");
        }

        var dir = Path.GetDirectoryName(path)!;
        var meshes = model.Bake(skeleton).Select(m =>
        {
            var texPath = Textures.Resolve(dir, m.Texture);
            if (texPath is null && !string.IsNullOrWhiteSpace(m.Texture)) warnings.Add($"Textura no encontrada: {m.Texture}");
            return new
            {
                texture = m.Texture,
                textureUrl = texPath is null ? null : "/api/textures?path=" + Uri.EscapeDataString(Path.GetRelativePath(clientRuntime, texPath)),
                positions = m.Positions, normals = m.Normals, uvs = m.Uvs, indices = m.Indices,
            };
        }).ToList();

        return Results.Ok(new
        {
            index, file = relative, exists = true, name = model.Name, version = model.Version,
            bones = model.Bones.Count, actions = model.ActionFrames.Count, skeleton = skeleton is not null ? "Player.bmd" : null,
            meshes, warnings,
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { index, file = relative, exists = true, error = ex.Message });
    }
});

api.MapGet("/textures", (string path) =>
{
    var full = Path.GetFullPath(Path.Combine(clientRuntime, path));
    if (!full.StartsWith(Path.GetFullPath(clientRuntime), StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return Results.NotFound();
    var decoded = Textures.Decode(full);
    if (decoded is null) return Results.NotFound();
    return Results.Bytes(decoded.Value.Data, decoded.Value.ContentType);
});

// ------------------------------------------------------------ cuentas, personajes, inventario y baul (Fase 3)

api.MapGet("/accounts", async () => Results.Ok(await accounts.ListAsync()));

api.MapGet("/accounts/{id:guid}", async (Guid id) =>
{
    var detail = await accounts.GetAsync(id);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
});

api.MapGet("/definitions/{id:guid}/options", async (Guid id) =>
{
    var options = await accounts.GetDefinitionOptionsAsync(id);
    return options is null ? Results.NotFound() : Results.Ok(options);
});

api.MapPost("/inventory/items", async (ItemWrite item) =>
{
    try
    {
        var id = await accounts.CreateItemAsync(item);
        return Results.Ok(await accounts.GetItemAsync(id));
    }
    catch (InvalidOperationException ex) { return Problem(ex.Message); }
});

api.MapPut("/inventory/items/{id:guid}", async (Guid id, ItemWrite item) =>
{
    try
    {
        await accounts.UpdateItemAsync(id, item);
        return Results.Ok(await accounts.GetItemAsync(id));
    }
    catch (InvalidOperationException ex) { return Problem(ex.Message); }
});

api.MapPost("/inventory/items/{id:guid}/move", async (Guid id, MoveRequest move) =>
{
    try
    {
        await accounts.MoveItemAsync(id, move);
        return Results.Ok(await accounts.GetItemAsync(id));
    }
    catch (InvalidOperationException ex) { return Problem(ex.Message); }
});

api.MapDelete("/inventory/items/{id:guid}", async (Guid id) =>
{
    await accounts.DeleteItemAsync(id);
    return Results.NoContent();
});

api.MapPut("/storages/{id:guid}/money", async (Guid id, MoneyRequest body) =>
{
    if (body.Money < 0 || body.Money > 2_000_000_000) return Problem("El zen tiene que estar entre 0 y 2.000.000.000.");
    await accounts.SetMoneyAsync(id, body.Money);
    return Results.NoContent();
});

api.MapGet("/backups", () =>
{
    var files = Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories)
        .Select(f => new { path = Path.GetRelativePath(backupDir, f), size = new FileInfo(f).Length, modified = File.GetLastWriteTime(f) })
        .OrderByDescending(f => f.modified)
        .Take(200);
    return Results.Ok(files);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Urls.FirstOrDefault() ?? "http://localhost:5050";
    Console.WriteLine();
    Console.WriteLine($"  vyper-mu Item Editor  ->  {url}");
    Console.WriteLine($"  Base:    localhost:{port}/openmu");
    Console.WriteLine($"  Cliente: {clientFilePath}");
    Console.WriteLine($"  Backups: {backupDir}");
    Console.WriteLine("  Ctrl+C para cerrar.");
    Console.WriteLine();
    if (Environment.GetEnvironmentVariable("VYPER_EDITOR_NOBROWSER") is null)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* sin navegador, no pasa nada */ }
    }
});

app.Run();

// ------------------------------------------------------------ funciones

static IResult Problem(string message) => Results.Problem(detail: message, statusCode: 400);

static bool IsServerRunning() => Process.GetProcessesByName("MUnique.OpenMU.Startup").Length > 0;

string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss");

void BackupClientFile()
{
    if (clientBackedUp) return;
    var target = Path.Combine(backupDir, $"Item_eng.{Stamp()}.bmd");
    File.Copy(clientFilePath, target, overwrite: true);
    clientBackedUp = true;
}

async Task EnsureServerSnapshotAsync()
{
    if (serverBackedUp) return;
    var all = await db.GetItemsAsync();
    var target = Path.Combine(backupDir, $"items-db.{Stamp()}.json");
    await File.WriteAllTextAsync(target, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    serverBackedUp = true;
}

async Task BackupServerAsync(ItemDetail previous, string action)
{
    await EnsureServerSnapshotAsync();
    var dir = Path.Combine(backupDir, "items");
    Directory.CreateDirectory(dir);
    var target = Path.Combine(dir, $"{previous.Group}-{previous.Number}.{Stamp()}.{action}.json");
    await File.WriteAllTextAsync(target, JsonSerializer.Serialize(previous, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}

Dictionary<int, string> LoadModelTable(string file)
{
    if (!File.Exists(file)) return new Dictionary<int, string>();
    var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new();
    return raw.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
}

float[][]? LoadPlayerSkeleton()
{
    var path = Path.Combine(clientRuntime, "Data", "Player", "Player.bmd");
    if (!File.Exists(path)) return null;
    try { return BmdModel.Load(path).BoneMatrices(0, 0); } catch { return null; }
}

/// <summary>Si PostgreSQL (portable, server\pgsql) no esta escuchando, lo levanta. El editor no necesita OpenMU, pero si la base.</summary>
static void EnsurePostgres(string root)
{
    var port = int.Parse(Environment.GetEnvironmentVariable("VYPER_PG_PORT") ?? "5433");
    if (PortOpen(port)) return;
    var pgCtl = Path.Combine(root, "server", "pgsql", "bin", "pg_ctl.exe");
    var pgData = Path.Combine(root, "server", "pgdata");
    if (!File.Exists(pgCtl) || !Directory.Exists(pgData)) return;
    var logs = Path.Combine(root, "server", "logs");
    Directory.CreateDirectory(logs);
    Console.WriteLine("  PostgreSQL no esta corriendo: lo levanto...");
    try
    {
        using var p = Process.Start(new ProcessStartInfo(pgCtl, $"-D \"{pgData}\" -l \"{Path.Combine(logs, "postgres.log")}\" start")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        });
        p?.WaitForExit(30000);
        for (int i = 0; i < 30 && !PortOpen(port); i++) Thread.Sleep(1000);
        Console.WriteLine(PortOpen(port) ? "  PostgreSQL listo." : @"  PostgreSQL no levanto; revisa server\logs\postgres.log");
    }
    catch (Exception ex)
    {
        Console.WriteLine("  No pude levantar PostgreSQL: " + ex.Message);
    }
}

static bool PortOpen(int port)
{
    try
    {
        using var client = new System.Net.Sockets.TcpClient();
        return client.ConnectAsync("127.0.0.1", port).Wait(500) && client.Connected;
    }
    catch { return false; }
}

static string FindRoot()
{
    var env = Environment.GetEnvironmentVariable("VYPER_ROOT");
    if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return Path.GetFullPath(env);

    foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "start.ps1")) && Directory.Exists(Path.Combine(dir.FullName, "tools", "item-editor")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
    }

    throw new InvalidOperationException("No encuentro la raiz de vyper-mu. Corre el editor con scripts\\item-editor.ps1 o define VYPER_ROOT.");
}

record MoneyRequest(int Money);
