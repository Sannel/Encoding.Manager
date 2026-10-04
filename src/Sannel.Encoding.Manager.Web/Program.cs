using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using MudBlazor.Services;
using Sannel.Encoding.Manager.Web.Components;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Data.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Options;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp;
using Sannel.Encoding.Manager.Web.Features.Mcp.Authentication;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Tools;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Hubs;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Runner.Hubs;
using Sannel.Encoding.Manager.Web.Features.Runner.Services;
using Sannel.Encoding.Manager.Web.Features.Runners.Services;
using Sannel.Encoding.Manager.Web.Features.Settings.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Options;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;
using Sannel.Encoding.Manager.Web.Features.Omdb.Options;
using Sannel.Encoding.Manager.Web.Features.Omdb.Services;
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

// Configure Event Log logging on Windows (only logs errors and above)
if (OperatingSystem.IsWindows())
{
	builder.Logging.AddEventLog(eventLogSettings =>
	{
		eventLogSettings.SourceName = "Sannel Encoding Manager";
		eventLogSettings.LogName = "Application";
	});
	// Filter to only Error and Critical level messages for Event Log
	builder.Logging.AddFilter("Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider", LogLevel.Warning);
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

// Per-user API keys for the MCP endpoint
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, _ => { });

// Require authentication for all pages by default; use [AllowAnonymous] to opt out
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("RunnerApi", policy =>
        policy.AddAuthenticationSchemes("RunnerBearer")
              .RequireAuthenticatedUser());

    options.AddPolicy(ApiKeyDefaults.Policy, policy =>
        policy.AddAuthenticationSchemes(ApiKeyDefaults.Scheme)
              .RequireAuthenticatedUser());
});

// Razor Pages are required for the Microsoft Identity login/logout UI
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs") || context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs") || context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

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
builder.Services.AddSingleton<QueueChangeNotifier>();
builder.Services.AddScoped<IEncodeQueueService, EncodeQueueService>();
builder.Services.AddScoped<IPresetService, PresetService>();

// Shared encode-job submission (Scan page + MCP) and background disc scans
builder.Services.AddScoped<IEncodeJobSubmissionService, EncodeJobSubmissionService>();
builder.Services.AddSingleton<IBackgroundScanCoordinator, BackgroundScanCoordinator>();

// Disc menu inspection (runs the DiscMenu.Probe child process)
builder.Services.Configure<DiscMenuOptions>(builder.Configuration.GetSection("DiscMenu"));
builder.Services.PostConfigure<DiscMenuOptions>(o => o.ContentRootPath = builder.Environment.ContentRootPath);
builder.Services.AddSingleton<IDiscMenuProbeRunner, DiscMenuProbeRunner>();
builder.Services.AddSingleton<IDiscMenuService, DiscMenuService>();

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

// OMDb integration for movies. Fall back to legacy "Imdb" config if present.
builder.Services.AddOptions<OmdbOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        configuration.GetSection("Omdb").Bind(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey) && string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            configuration.GetSection("Imdb").Bind(options);
        }
    });
builder.Services.AddHttpClient<IOmdbService, OmdbService>();

// MVC Controllers for API endpoints
builder.Services.AddControllers();
builder.Services.AddSignalR();

// MCP server (AI clients authenticate with per-user API keys)
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection("Mcp"));
var mcpOptions = builder.Configuration.GetSection("Mcp").Get<McpOptions>() ?? new McpOptions();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserApiKeyService, UserApiKeyService>();
builder.Services.AddSingleton<IForcedRescanLimiter, ForcedRescanLimiter>();
builder.Services.AddScoped<McpCaller>();
builder.Services.AddScoped<McpQueueRequestBuilder>();
if (mcpOptions.Enabled)
{
    var mcpJson = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
    mcpJson.Converters.Add(new JsonStringEnumConverter());
    builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = mcpOptions.ServerName,
                Title = "Sannel Encoding Manager",
                Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            };
            options.ServerInstructions = McpServerInstructions.Text;
        })
        .WithHttpTransport(options => options.Stateless = true)
        .WithTools<FilesystemTools>(mcpJson)
        .WithTools<ScanTools>(mcpJson)
        .WithTools<MetadataTools>(mcpJson)
        .WithTools<PresetTools>(mcpJson)
        .WithTools<QueueTools>(mcpJson)
        .WithTools<DiscMenuTools>(mcpJson);
}

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
app.UseWhen(
	context => !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/hubs") && !context.Request.Path.StartsWithSegments("/mcp"),
	branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages(); // Microsoft Identity login/logout endpoints
app.MapControllers(); // API controllers
app.MapHub<QueueHub>("/hubs/queue");
app.MapHub<RunnerStatusHub>("/hubs/runner-status");
if (mcpOptions.Enabled)
{
    app.MapMcp("/mcp").RequireAuthorization(ApiKeyDefaults.Policy);
}
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
