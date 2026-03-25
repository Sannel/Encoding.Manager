using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
using Sannel.Encoding.Manager.Web.Features.Settings.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Options;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;
using Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

var builder = WebApplication.CreateBuilder(args);

// Microsoft Entra (Azure AD) authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Require authentication for all pages by default; use [AllowAnonymous] to opt out
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
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
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
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

app.Run();
