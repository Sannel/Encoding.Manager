using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Runner;
using Sannel.Encoding.Runner.Features.Runner.Options;
using Sannel.Encoding.Runner.Features.Runner.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();
builder.Services.AddSystemd();

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
