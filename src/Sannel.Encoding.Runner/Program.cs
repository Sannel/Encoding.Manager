using Microsoft.Extensions.Logging.EventLog;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Runner;
using Sannel.Encoding.Runner.Features.Configuration;
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
builder.Services.AddHttpClient<IRunnerApiClient, RunnerApiClient>();

// Worker
builder.Services.AddHostedService<EncodingWorkerService>();

var host = builder.Build();
host.Run();
