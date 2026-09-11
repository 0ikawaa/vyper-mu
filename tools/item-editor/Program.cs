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

var pwFile = Path.Combine(root, "server", ".pgpassword");
var password = File.Exists(pwFile) ? File.ReadAllText(pwFile).Trim() : "admin";
var port = Environment.GetEnvironmentVariable("VYPER_PG_PORT") ?? "5433";
var db = new Db($"Server=localhost;Port={port};User Id=postgres;Password={password};Database=openmu;Command Timeout=60;");

var clientFilePath = Path.Combine(root, "client", "runtime", "Data", "Local", "Eng", "Item_eng.bmd");
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
