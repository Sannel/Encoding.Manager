using Microsoft.Extensions.Options;
using NSubstitute;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public sealed class McpQueueRequestBuilderTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("mcp-queue-tests").FullName;
	private readonly IFilesystemService _filesystem = Substitute.For<IFilesystemService>();
	private readonly IBackgroundScanCoordinator _scans = Substitute.For<IBackgroundScanCoordinator>();
	private readonly IPresetService _presets = Substitute.For<IPresetService>();
	private readonly ITvdbService _tvdb = Substitute.For<ITvdbService>();
	private readonly McpQueueRequestBuilder _builder;

	public McpQueueRequestBuilderTests()
	{
		Directory.CreateDirectory(Path.Combine(this._root, "Show", "Season1"));
		File.WriteAllText(Path.Combine(this._root, "Show", "Season1", "ep1.mkv"), string.Empty);
		Directory.CreateDirectory(Path.Combine(this._root, "Disc", "VIDEO_TS"));

		this._filesystem.ResolvePhysicalPath("T", Arg.Any<string>())
			.Returns(c => Path.Combine(this._root, c.ArgAt<string>(1)));
		this._filesystem.GetMediaFilesRecursiveAsync("T", "Show", Arg.Any<CancellationToken>())
			.Returns([new FileEntryResponse { Name = "ep1.mkv", RelativePath = "Season1/ep1.mkv", SizeBytes = 1 }]);
		this._presets.GetPresetsAsync(Arg.Any<CancellationToken>())
			.Returns([new EncodingPreset { Label = "1080p", PresetName = "Fast 1080p30" }]);
		this._scans.GetStatus("T", "Disc").Returns(new ScanJobStatus
		{
			State = BackgroundJobState.Completed,
			PhysicalPath = "x",
			StartedAt = DateTimeOffset.UtcNow,
			Result = new HandBrakeScanResult
			{
				IsSuccess = true,
				InputPath = "x",
				Titles = [new TitleInfo { TitleNumber = 1, Chapters = [.. Enumerable.Range(1, 6).Select(n => new ChapterInfo { ChapterNumber = n })] }],
			},
		});

		this._builder = new McpQueueRequestBuilder(this._filesystem, this._scans, this._presets, this._tvdb, Options.Create(new McpOptions()));
	}

	[Fact]
	public async Task Folder_BuildsFilesJobWithSourcePaths()
	{
		var request = new McpQueueEncodeRequest
		{
			Root = "T",
			Path = "Show",
			Selection = "folder",
			PresetLabel = "1080p",
			Tracks = [new McpQueueTrack { SourceRelativePath = "Season1/ep1.mkv", OutputName = "Pilot", SeasonNumber = 1, EpisodeNumber = 1 }],
		};

		var (submission, errors) = await this._builder.BuildAsync(request, "Tester", "oid", CancellationToken.None);

		Assert.Empty(errors);
		Assert.NotNull(submission);
		Assert.Equal("Files", submission!.Mode);
		Assert.Equal("Show", submission.DiscPath);
		Assert.Equal("MCP", submission.CreatedVia);
		Assert.Equal("Season1/ep1.mkv", submission.Tracks[0].SourceRelativePath);
		Assert.Equal(1, submission.Tracks[0].TitleNumber);
	}

	[Fact]
	public async Task File_UsesParentFolderAndFileName()
	{
		var request = new McpQueueEncodeRequest
		{
			Root = "T",
			Path = "Show/Season1/ep1.mkv",
			Selection = "file",
			MovieYear = "2017",
			Tracks = [new McpQueueTrack { OutputName = "Movie", Resolution = "1080p" }],
		};

		var (submission, errors) = await this._builder.BuildAsync(request, null, null, CancellationToken.None);

		Assert.Empty(errors);
		Assert.Equal("Show/Season1", submission!.DiscPath);
		Assert.Equal("ep1.mkv", submission.Tracks[0].SourceRelativePath);
		Assert.Equal("2017", submission.Tracks[0].MovieYear);
	}

	[Fact]
	public async Task Disc_Chapters_ValidRangeIsAccepted()
	{
		var request = new McpQueueEncodeRequest
		{
			Root = "T",
			Path = "Disc",
			Selection = "disc",
			Mode = "chapters",
			Tracks = [new McpQueueTrack { TitleNumber = 1, StartChapter = 3, EndChapter = 4, OutputName = "Episode 2" }],
		};

		var (submission, errors) = await this._builder.BuildAsync(request, null, null, CancellationToken.None);

		Assert.Empty(errors);
		Assert.Equal("Chapters", submission!.Mode);
		Assert.Equal(3, submission.Tracks[0].StartChapter);
		Assert.Equal(4, submission.Tracks[0].EndChapter);
	}

	[Fact]
	public async Task InvalidRequest_ReportsEveryProblem()
	{
		var request = new McpQueueEncodeRequest
		{
			Root = "T",
			Path = "Disc",
			Selection = "disc",
			Mode = "Chapters",
			PresetLabel = "missing",
			Tracks =
			[
				new McpQueueTrack { TitleNumber = 1, StartChapter = 5, EndChapter = 9, OutputName = "a/b" },
				new McpQueueTrack { TitleNumber = 7, StartChapter = 1, EndChapter = 1, OutputName = "x" },
			],
		};

		var (submission, errors) = await this._builder.BuildAsync(request, null, null, CancellationToken.None);

		Assert.Null(submission);
		Assert.Contains(errors, e => e.Contains("out of range"));
		Assert.Contains(errors, e => e.Contains("title 7 does not exist"));
		Assert.Contains(errors, e => e.Contains("presetLabel"));
		Assert.Contains(errors, e => e.Contains("not allowed in file names"));
	}

	public void Dispose() => Directory.Delete(this._root, recursive: true);
}
