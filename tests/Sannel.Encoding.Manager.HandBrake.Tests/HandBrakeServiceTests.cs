using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

namespace Sannel.Encoding.Manager.HandBrake.Tests;

public class HandBrakeServiceTests
{
	private static IProcessRunner CreateMockRunner(
		string versionOutput = "HandBrake 10.1.0",
		int versionExitCode = 0)
	{
		var runner = Substitute.For<IProcessRunner>();

		// Default: --version returns success
		runner.RunAsync(
				Arg.Any<string>(),
				Arg.Is<IEnumerable<string>>(a => a.Any(x => x == "--version")),
				Arg.Any<CancellationToken>())
			.Returns(new ProcessResult
			{
				ExitCode = versionExitCode,
				StandardOutput = versionOutput,
				StandardError = ""
			});

		// Default: flatpak list (for locator) returns not found
		runner.RunAsync(
				Arg.Is("flatpak"),
				Arg.Any<IEnumerable<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new ProcessResult
			{
				ExitCode = 1,
				StandardOutput = "",
				StandardError = ""
			});

		return runner;
	}

	private static HandBrakeService CreateService(
		IProcessRunner? runner = null,
		HandBrakeOptions? options = null,
		string? executablePath = null)
	{
		runner ??= CreateMockRunner();
		options ??= new HandBrakeOptions();

		// We need a real executable path for the locator to work or set it in config.
		// For tests, set the executable path to a known HandBrakeCLI path.
		if (executablePath is not null)
		{
			options.ExecutablePath = executablePath;
		}

		var optionsMock = Substitute.For<IOptions<HandBrakeOptions>>();
		optionsMock.Value.Returns(options);

		var logger = Substitute.For<ILogger<HandBrakeService>>();

		var env = Substitute.For<IWebHostEnvironment>();
		env.ContentRootPath.Returns(Path.GetTempPath());

		var dbFactory = Substitute.For<IDbContextFactory<AppDbContext>>();

		return new HandBrakeService(runner, optionsMock, logger, env, dbFactory);
	}

	[Fact]
	public void Constructor_ValidVersionMeetsMinimum_Succeeds()
	{
		// Create a temp file to act as the HandBrakeCLI executable
		var tempExe = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");
			var service = CreateService(runner, executablePath: tempExe);

			Assert.Equal("10.1.0", service.CliVersion);
		}
		finally
		{
			File.Delete(tempExe);
		}
	}

	[Fact]
	public void Constructor_VersionBelowMinimum_Throws()
	{
		var tempExe = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 9.0.0");

			var ex = Assert.Throws<InvalidOperationException>(() =>
				CreateService(runner, executablePath: tempExe));

			Assert.Contains("10.1", ex.Message);
			Assert.Contains("9.0.0", ex.Message);
		}
		finally
		{
			File.Delete(tempExe);
		}
	}

	[Fact]
	public void Constructor_VersionCheckFails_Throws()
	{
		var tempExe = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner(versionExitCode: 1);

			Assert.Throws<InvalidOperationException>(() =>
				CreateService(runner, executablePath: tempExe));
		}
		finally
		{
			File.Delete(tempExe);
		}
	}

	[Fact]
	public void Constructor_UnparseableVersion_Throws()
	{
		var tempExe = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("some garbage output");

			Assert.Throws<InvalidOperationException>(() =>
				CreateService(runner, executablePath: tempExe));
		}
		finally
		{
			File.Delete(tempExe);
		}
	}

	[Fact]
	public async Task ScanAsync_Success_ReturnsTitles()
	{
		var tempExe = Path.GetTempFileName();
		var tempInput = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");

			runner.RunAsync(
					Arg.Any<string>(),
					Arg.Is<IEnumerable<string>>(a => a.Any(x => x == "--scan")),
					Arg.Any<CancellationToken>())
				.Returns(new ProcessResult
				{
					ExitCode = 0,
					StandardOutput = """
					Version: {
					    "VersionString": "1.10.2"
					}
					JSON Title Set: {
					    "MainFeature": 1,
					    "TitleList": [
					        {
					            "Index": 1,
					            "Duration": { "Hours": 0, "Minutes": 5, "Seconds": 30 },
					            "Geometry": { "Width": 1280, "Height": 720 }
					        }
					    ]
					}
					""",
					StandardError = ""
				});

			var service = CreateService(runner, executablePath: tempExe);
			var result = await service.ScanAsync(tempInput);

			Assert.True(result.IsSuccess);
			Assert.Single(result.Titles);
			Assert.Equal(1, result.Titles[0].TitleNumber);
			Assert.Equal(1280, result.Titles[0].Width);
		}
		finally
		{
			File.Delete(tempExe);
			File.Delete(tempInput);
		}
	}

	[Fact]
	public async Task ScanAsync_CliFailure_ReturnsError()
	{
		var tempExe = Path.GetTempFileName();
		var tempInput = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");

			runner.RunAsync(
					Arg.Any<string>(),
					Arg.Is<IEnumerable<string>>(a => a.Any(x => x == "--scan")),
					Arg.Any<CancellationToken>())
				.Returns(new ProcessResult
				{
					ExitCode = 1,
					StandardOutput = "",
					StandardError = "Error opening file"
				});

			var service = CreateService(runner, executablePath: tempExe);
			var result = await service.ScanAsync(tempInput);

			Assert.False(result.IsSuccess);
			Assert.NotNull(result.Error);
			Assert.Equal(1, result.Error!.ExitCode);
		}
		finally
		{
			File.Delete(tempExe);
			File.Delete(tempInput);
		}
	}

	[Fact]
	public async Task EncodeAsync_NoPreset_ThrowsArgumentException()
	{
		var tempExe = Path.GetTempFileName();
		var tempInput = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");

			var service = CreateService(runner, executablePath: tempExe);

			await Assert.ThrowsAsync<ArgumentException>(() =>
				service.EncodeAsync(new HandBrakeJob
				{
					InputPath = tempInput,
					OutputPath = Path.Combine(Path.GetTempPath(), "out.mkv")
				}));
		}
		finally
		{
			File.Delete(tempExe);
			File.Delete(tempInput);
		}
	}

	[Fact]
	public async Task EncodeAsync_Success_ReturnsResult()
	{
		var tempExe = Path.GetTempFileName();
		var tempInput = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");

			runner.RunWithLineCallbackAsync(
					Arg.Any<string>(),
					Arg.Any<IEnumerable<string>>(),
					Arg.Any<Action<string>>(),
					Arg.Any<CancellationToken>())
				.Returns(callInfo =>
				{
					// Simulate progress callbacks
					var callback = callInfo.ArgAt<Action<string>>(2);
					callback("Encoding: task 1 of 1, 50.00 % (100.00 fps, avg 95.00 fps, ETA 00h05m00s)");
					callback("Encoding: task 1 of 1, 100.00 % (120.00 fps, avg 100.00 fps, ETA 00h00m00s)");

					return new ProcessResult
					{
						ExitCode = 0,
						StandardOutput = "",
						StandardError = ""
					};
				});

			var service = CreateService(runner, executablePath: tempExe);
			var progressValues = new List<ProgressInfo>();

			var result = await service.EncodeAsync(
				new HandBrakeJob
				{
					InputPath = tempInput,
					OutputPath = Path.Combine(Path.GetTempPath(), "out.mkv"),
					PresetName = "Fast 1080p30"
				},
				new Progress<ProgressInfo>(p => progressValues.Add(p)));

			Assert.True(result.IsSuccess);
			Assert.Null(result.Error);
		}
		finally
		{
			File.Delete(tempExe);
			File.Delete(tempInput);
		}
	}

	[Fact]
	public async Task EncodeAsync_CliFailure_ReturnsError()
	{
		var tempExe = Path.GetTempFileName();
		var tempInput = Path.GetTempFileName();
		try
		{
			var runner = CreateMockRunner("HandBrake 10.1.0");

			runner.RunWithLineCallbackAsync(
					Arg.Any<string>(),
					Arg.Any<IEnumerable<string>>(),
					Arg.Any<Action<string>>(),
					Arg.Any<CancellationToken>())
				.Returns(new ProcessResult
				{
					ExitCode = 1,
					StandardOutput = "",
					StandardError = "Encode failed"
				});

			var service = CreateService(runner, executablePath: tempExe);

			var result = await service.EncodeAsync(new HandBrakeJob
			{
				InputPath = tempInput,
				OutputPath = Path.Combine(Path.GetTempPath(), "out.mkv"),
				PresetName = "Fast 1080p30"
			});

			Assert.False(result.IsSuccess);
			Assert.NotNull(result.Error);
			Assert.Equal(1, result.Error!.ExitCode);
			Assert.Contains("Encode failed", result.Error.RawOutput);
		}
		finally
		{
			File.Delete(tempExe);
			File.Delete(tempInput);
		}
	}
}
