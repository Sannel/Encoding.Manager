using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using Sannel.Encoding.Manager.Web.Components;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Data.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Runner.Services;
using Sannel.Encoding.Manager.Web.Features.Runners.Services;
using Sannel.Encoding.Manager.Web.Features.Settings.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Options;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Configuration;

// Handle the 'configure' subcommand before building the web host.
if (args.Length > 0 && args[0].Equals("configure", StringComparison.OrdinalIgnoreCase))
{
	ConfigureCommand.Run();
	return;
}

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
	// Configure for Windows Service hosting
	builder.Host.UseWindowsService();
}

if (OperatingSystem.IsLinux())
{
	// Configure for systemd hosting (Linux)
	builder.Host.UseSystemd();
}

// Encrypted config overlay — loaded after appsettings.json so it takes precedence.
// Values prefixed with "enc:" are transparently decrypted at startup.
// Add config/appsettings.json to .gitignore — it may contain secrets.
builder.Configuration.AddEncryptedJsonFile(ConfigureCommand.ConfigFilePath, optional: true);

// Microsoft Entra (Azure AD) authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// Bearer token auth for runner API
builder.Services.AddAuthentication()
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), jwtBearerScheme: "RunnerBearer");

// Require authentication for all pages by default; use [AllowAnonymous] to opt out
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("RunnerApi", policy =>
        policy.AddAuthenticationSchemes("RunnerBearer")
              .RequireAuthenticatedUser());
});

// Razor Pages are required for the Microsoft Identity login/logout UI
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Expose authentication state to Blazor components
builder.Services.AddCascadingAuthenticationState();

// HandBrake CLI integration
builder.Services.Configure<HandBrakeOptions>(builder.Configuration.GetSection("HandBrake"));
builder.Services.PostConfigure<HandBrakeOptions>(o => o.ContentRootPath = builder.Environment.ContentRootPath);
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IScanCacheProvider, EfCoreScanCacheProvider>();
builder.Services.AddSingleton<IHandBrakeService, HandBrakeService>();

// Filesystem browsing
builder.Services.Configure<FilesystemOptions>(builder.Configuration.GetSection("Filesystem"));
builder.Services.AddSingleton<IFilesystemService, FilesystemService>();

// Database (Entity Framework Core — SQLite or PostgreSQL)
// AddDbContextFactory registers both IDbContextFactory<AppDbContext> (singleton) and AppDbContext (scoped),
// which allows transient services such as TvdbService to safely create short-lived contexts via the factory.
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
var dbOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    switch (dbOptions.Provider.ToLowerInvariant())
    {
        case "postgres":
        case "postgresql":
            options.UseNpgsql(dbOptions.ConnectionString,
                b => b.MigrationsAssembly("Sannel.Encoding.Manager.Migrations.Postgres"));
            break;
        default:
            // Ensure the directory exists for the SQLite database file
            var dataSource = dbOptions.ConnectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Data Source=".Length)
                ?? "data/encoding.db";
            var dir = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            options.UseSqlite(dbOptions.ConnectionString,
                b => b.MigrationsAssembly("Sannel.Encoding.Manager.Migrations.Sqlite"));
            break;
    }
});

// Application settings
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Encoding queue
builder.Services.AddScoped<IEncodeQueueService, EncodeQueueService>();
builder.Services.AddScoped<IPresetService, PresetService>();

// Runner job service
builder.Services.AddScoped<IRunnerJobService, RunnerJobService>();

// Runner management (web UI)
builder.Services.AddScoped<IRunnerManagementService, RunnerManagementService>();

// TheTVDB integration
builder.Services.Configure<TvdbOptions>(builder.Configuration.GetSection("Tvdb"));
builder.Services.AddHttpClient<ITvdbService, TvdbService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<TvdbOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});

// MVC Controllers for API endpoints
builder.Services.AddControllers();

var app = builder.Build();

// Apply any pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages(); // Microsoft Identity login/logout endpoints
app.MapControllers(); // API controllers
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
