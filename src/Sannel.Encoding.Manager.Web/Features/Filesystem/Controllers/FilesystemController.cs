using Microsoft.AspNetCore.Mvc;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Controllers;

/// <summary>
/// API controller for filesystem browsing operations.
/// </summary>
[ApiController]
[Route("api/filesystem")]
public class FilesystemController : ControllerBase
{
	private readonly IFilesystemService _filesystemService;
	private readonly ILogger<FilesystemController> _logger;

	public FilesystemController(IFilesystemService filesystemService, ILogger<FilesystemController> logger)
	{
		_filesystemService = filesystemService;
		_logger = logger;
	}

	/// <summary>
	/// Gets the list of configured root directories (labels only).
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A collection of configured directory information.</returns>
	[HttpGet("configured-directories")]
	[ProducesResponseType(typeof(IEnumerable<ConfiguredDirectoryResponse>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IEnumerable<ConfiguredDirectoryResponse>>> GetConfiguredDirectories(CancellationToken cancellationToken)
	{
		var directories = await _filesystemService.GetConfiguredDirectoriesAsync(cancellationToken);
		return Ok(directories);
	}

	/// <summary>
	/// Browses a directory and returns its immediate children.
	/// </summary>
	/// <param name="label">The label of the configured root directory.</param>
	/// <param name="path">Optional relative path within the root directory.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A browse response containing directories and files.</returns>
	[HttpGet("browse")]
	[ProducesResponseType(typeof(BrowseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<BrowseResponse>> Browse(
		[FromQuery] string label,
		[FromQuery] string? path = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(label))
		{
			return BadRequest(new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Invalid request",
				Detail = "Label parameter is required."
			});
		}

		try
		{
			var result = await _filesystemService.BrowseAsync(label, path, cancellationToken);
			return Ok(result);
		}
		catch (ArgumentException ex)
		{
			_logger.LogWarning(ex, "Invalid browse request: Label={Label}, Path={Path}", label, path);
			return BadRequest(new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Invalid request",
				Detail = ex.Message
			});
		}
		catch (DirectoryNotFoundException ex)
		{
			_logger.LogWarning(ex, "Directory not found: Label={Label}, Path={Path}", label, path);
			return NotFound(new ProblemDetails
			{
				Status = StatusCodes.Status404NotFound,
				Title = "Directory not found",
				Detail = ex.Message
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error browsing directory: Label={Label}, Path={Path}", label, path);
			return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "Internal server error",
				Detail = "An error occurred while browsing the directory."
			});
		}
	}
}
