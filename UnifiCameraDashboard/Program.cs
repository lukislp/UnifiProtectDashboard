using Microsoft.EntityFrameworkCore;
using UnifiCameraDashboard.Components;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Services;
using UnifiCameraDashboard.Services.Classification;
using UnifiCameraDashboard.Services.Protect;
using UnifiCameraDashboard.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SQLite database
// DATA_DIR can be set via environment variable (e.g. in a Docker container)
var dataDir = Environment.GetEnvironmentVariable("DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnifiCameraDashboard");
var dbPath = Path.Combine(dataDir, "dashboard.db");

// Ensure the directory exists
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton(new DataDirectoryOptions(dataDir));
// Coordinates the write-active instance across a rolling update - see InstanceLock.cs. A
// singleton so EventIngestionService/EventClassificationService share the same lock attempt
// (one FileStream, one OS-level lock) instead of each independently contending for it.
builder.Services.AddSingleton<IInstanceLock, FileInstanceLock>();

// Register HttpClientFactory for better cookie handling
builder.Services.AddHttpClient();

// i18n: TranslationStore is a singleton that loads all i18n/*.json once at startup.
// I18nService is scoped per circuit so every user gets their own language state.
builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddScoped<I18nService>();

// Register Services
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ICameraRepository, CameraRepository>();
builder.Services.AddScoped<IUnifiProtectService, UnifiProtectService>();
builder.Services.AddScoped<IUnifiCameraService, UnifiCameraService>();
builder.Services.AddScoped<IProtectWebSocketClient, ProtectWebSocketClient>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddSingleton<IFfmpegService, FfmpegService>();
// The ONNX model is loaded once here - expensive, and the model is stateless/reusable, so a
// singleton rather than resolving it per classification.
builder.Services.AddSingleton<IYoloClassifier>(_ => new YoloClassifier(YoloClassifier.ResolveModelPath()));

// Register Background Services
builder.Services.AddHostedService<CameraAutoDiscoveryService>();
builder.Services.AddHostedService<EventIngestionService>();
// EventClassificationService is both a hosted service and injectable via IClassificationQueue -
// registered once as a singleton so both resolve the same instance/channel.
builder.Services.AddSingleton<EventClassificationService>();
builder.Services.AddSingleton<IClassificationQueue>(sp => sp.GetRequiredService<EventClassificationService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EventClassificationService>());

// Add Controllers for API endpoints
builder.Services.AddControllers();

// Port configuration: ASPNETCORE_URLS takes precedence (e.g. set by Dockerfile)
// Fallback: appsettings -> default 5003
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var httpPort = builder.Configuration.GetValue<int>("ServerSettings:HttpPort", 5003);
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(httpPort);
    });
}

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await dbContext.MigrateSafelyAsync();
        logger.LogInformation("Database initialized: {DbPath}", dbPath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing database");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Static Files
app.UseStaticFiles();

// Custom Static File Options for HLS
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";

        if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Content-Type"] = "application/vnd.apple.mpegurl";
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        }
        else if (path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Content-Type"] = "video/MP2T";
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000";
        }

        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";

        var logger = ctx.Context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogDebug("Static File Request: {Path}", path);
    }
};

app.UseStaticFiles(staticFileOptions);

app.UseRouting();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Setup Check
using (var scope = app.Services.CreateScope())
{
    var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    var isSetupComplete = await settingsService.IsInitialSetupCompleteAsync();

    if (!isSetupComplete)
    {
        Console.WriteLine("\nINITIAL SETUP REQUIRED");
        Console.WriteLine($"   Open: http://localhost:{builder.Configuration.GetValue<int>("ServerSettings:HttpPort", 5003)}/setup");
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine("\nDashboard configured");
    }
}

var displayPort = builder.Configuration.GetValue<int>("ServerSettings:HttpPort", 5003);
Console.WriteLine($"Dashboard started:");
Console.WriteLine($"   HTTP:  http://localhost:{displayPort}");
Console.WriteLine($"   Database: {dbPath}");
Console.WriteLine();

app.Run();
