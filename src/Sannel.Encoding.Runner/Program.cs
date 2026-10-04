using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Runner;
using Sannel.Encoding.Runner.Features.Configuration;
using Sannel.Encoding.Runner.Features.Logging.Services;
using Sannel.Encoding.Runner.Features.Runner.Options;
using Sannel.Encoding.Runner.Features.Runner.Services;

var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1";
Console.WriteLine($"Sannel Encoding Runner v{version}");

// Handle the 'configure' subcommand before building the host.
if (args.Length > 0 && args[0].Equals("configure", StringComparison.OrdinalIgnoreCase))
{
	ConfigureCommand.Run();
	return;
}

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
	builder.Services.AddWindowsService();
}

if (OperatingSystem.IsLinux())
{
	builder.Services.AddSystemd();
}

// Configure Event Log logging on Windows (only logs errors and above)
if (OperatingSystem.IsWindows())
{
	builder.Logging.AddEventLog(eventLogSettings =>
	{
		eventLogSettings?.SourceName = "Sannel Encoding Runner";
		eventLogSettings?.LogName = "Application";
	});
	// Filter to only Error and Critical level messages for Event Log
	builder.Logging.AddFilter("Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider", LogLevel.Warning);
}

// Encrypted config overlay — loaded after appsettings.json so it takes precedence.
// Values prefixed with "enc:" are transparently decrypted at startup.
// Add config/appsettings.json to .gitignore — it may contain secrets.
builder.Configuration.AddEncryptedJsonFile(ConfigureCommand.ConfigFilePath, optional: true);

// Options
builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection("Runner"));
builder.Services.Configure<HandBrakeOptions>(builder.Configuration.GetSection("HandBrake"));
builder.Services.PostConfigure<HandBrakeOptions>(o => o.ContentRootPath = AppContext.BaseDirectory);
builder.Services.Configure<FilesystemOptions>(builder.Configuration.GetSection("Filesystem"));

// HandBrake services
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IScanCacheProvider, NoOpScanCacheProvider>();
builder.Services.AddSingleton<IHandBrakeService, HandBrakeService>();

// Runner services
builder.Services.AddSingleton<PathNormalizer>();
builder.Services.AddSingleton<IRunnerAccessTokenProvider, RunnerAccessTokenProvider>();
builder.Services.AddTransient<RunnerBearerTokenHandler>();
builder.Services.AddHttpClient<IRunnerApiClient, RunnerApiClient>()
	.AddHttpMessageHandler<RunnerBearerTokenHandler>();
builder.Services.AddHttpClient(nameof(IRunnerApiClient), (sp, client) =>
{
	var runnerOpts = sp.GetRequiredService<IOptions<RunnerOptions>>().Value;
	var baseUrl = runnerOpts.ServiceBaseUrl?.Trim().TrimEnd('/') + "/";
	client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<RunnerBearerTokenHandler>();

// Remote log shipping to server
var runnerName = builder.Configuration.GetSection("Runner")["Name"] ?? "runner-01";
builder.Services.AddSingleton<ILoggerProvider>(sp =>
	new RemoteLoggerProvider(sp.GetRequiredService<IHttpClientFactory>(), runnerName));

// Worker
builder.Services.AddHostedService<EncodingWorkerService>();

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILoggerFactory>()
	.CreateLogger("Sannel.Encoding.Runner.Startup");
var configuration = host.Services.GetRequiredService<IConfiguration>();

var tenantId = configuration["AzureAd:TenantId"]?.Trim();
var clientId = configuration["AzureAd:ClientId"]?.Trim();
var clientSecret = configuration["AzureAd:ClientSecret"]?.Trim();
var configuredScope = configuration["AzureAd:Scope"]?.Trim();

if (!IsConfigured(tenantId))
{
	startupLogger.LogWarning("AzureAd:TenantId is not configured. Token acquisition will fail.");
}

if (!IsConfigured(clientId))
{
	startupLogger.LogWarning("AzureAd:ClientId is not configured. Token acquisition will fail.");
}

if (!IsConfigured(clientSecret))
{
	startupLogger.LogWarning("AzureAd:ClientSecret is not configured. Token acquisition will fail.");
}

if (string.IsNullOrWhiteSpace(configuredScope))
{
	if (IsConfigured(clientId))
	{
		startupLogger.LogWarning(
			"AzureAd:Scope is not configured. Runner will fall back to scope 'api://{ClientId}/.default'. Configure AzureAd:Scope explicitly to avoid audience mismatch.",
			clientId);
	}
	else
	{
		startupLogger.LogWarning(
			"AzureAd:Scope is not configured and AzureAd:ClientId is missing. Token acquisition will fail until Azure AD settings are configured.");
	}
}

static bool IsConfigured(string? value) =>
	!string.IsNullOrWhiteSpace(value)
	&& !value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

host.Run();
