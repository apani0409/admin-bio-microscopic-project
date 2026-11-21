using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;
using UCR.EB.BioMicroscopeAdmin.Backend.DependencyInjection;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Identity;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Persistence;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Security;

// wwwroot está en .gitignore (se genera en el publish), pero UseStaticFiles
// resuelve su WebRootFileProvider al construir el host y necesita que exista.
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCleanArchitectureServices(builder.Configuration);

// Sin claves persistentes, recrear el contenedor cerraría todas las sesiones.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("biomicroscope")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var trustProxy = builder.Configuration.GetValue<bool>("Proxy:TrustForwardedHeaders");
if (trustProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (trustProxy)
{
    app.UseForwardedHeaders();
}

app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Swagger manda el header anti-CSRF para poder probar las escrituras.
    app.UseSwaggerUI(options => options.UseRequestInterceptor(
        $"(req) => {{ req.headers['{CsrfHeaderFilter.HeaderName}'] = '{CsrfHeaderFilter.HeaderValue}'; return req; }}"));
    app.UseWebAssemblyDebugging();
}

// En desarrollo se trabaja por HTTP. En contenedor, el HTTPS lo termina el proxy
// del servidor y DisableHttpsRedirect evita redirigir dentro de la red interna.
if (!app.Environment.IsDevelopment() && !builder.Configuration.GetValue<bool>("DisableHttpsRedirect"))
{
    app.UseHttpsRedirection();
}
// Registra los tipos de contenido y la negociación de compresión que Blazor
// WASM necesita para servir archivos como los .dat de ICU, que no tienen un
// content-type reconocido por UseStaticFiles por sí solo.
app.UseBlazorFrameworkFiles();

// Esta ruta fue pública: si queda algún archivo ahí, nunca se sirve sin autorización.
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    return next();
});
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.StatusCode(503));

// Una ruta inexistente del API responde 404; el resto la resuelve el router de Blazor.
app.MapMethods("/api/{**path}", [HttpMethods.Get, HttpMethods.Head], () => Results.NotFound())
    .ExcludeFromDescription();
app.MapFallbackToFile("index.html");

await MigrateDatabaseWithRetryAsync(app);
await app.Services.SeedIdentityAsync();
ImportLegacyUploads(app);

await app.RunAsync();

static void ImportLegacyUploads(WebApplication app)
{
    var legacyPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    var moved = app.Services.GetRequiredService<IFileStorageService>().ImportLegacyFiles(legacyPath);
    if (moved > 0)
    {
        app.Logger.LogInformation("Moved {Count} image files to private storage", moved);
    }
}

static async Task MigrateDatabaseWithRetryAsync(WebApplication app, int maxAttempts = 30, int delayMs = 2000)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/{MaxAttempts}); retrying in {DelayMs}ms",
                attempt, maxAttempts, delayMs);
            await Task.Delay(delayMs);
        }
    }
}

public partial class Program {}
