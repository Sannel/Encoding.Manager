using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Controllers;

[ApiController]
[Route("api/jellyfin")]
[Authorize]
public class JellyfinController : ControllerBase
{
	private readonly IJellyfinServerService _serverService;
	private readonly IJellyfinClientFactory _clientFactory;
	private readonly IJellyfinEncodeService _encodeService;
	private readonly ILogger<JellyfinController> _logger;

	public JellyfinController(
		IJellyfinServerService serverService,
		IJellyfinClientFactory clientFactory,
		IJellyfinEncodeService encodeService,
		ILogger<JellyfinController> logger)
	{
		this._serverService = serverService;
		this._clientFactory = clientFactory;
		this._encodeService = encodeService;
		this._logger = logger;
	}

	[HttpGet("servers/{serverId:guid}/ping")]
	public async Task<IActionResult> PingServer(Guid serverId, CancellationToken ct)
	{
		var server = await this._serverService.GetServerAsync(serverId, ct).ConfigureAwait(false);
		if (server is null)
		{
			return this.NotFound();
		}

		try
		{
			var serverServiceImpl = (JellyfinServerService)this._serverService;
			var client = this._clientFactory.CreateClient(server.BaseUrl, serverServiceImpl.DecryptApiKey(server.ApiKey));
			var info = await client.GetSystemInfoAsync(ct).ConfigureAwait(false);
			return this.Ok(new { Status = "Online", info?.ServerName, info?.Version });
		}
		catch (Exception ex)
		{
			this._logger.LogWarning(ex, "Ping failed for server {ServerId}.", serverId);
			return this.Ok(new { Status = "Offline", Error = ex.Message });
		}
	}

	[HttpGet("servers/{serverId:guid}/items")]
	public async Task<IActionResult> GetItems(
		Guid serverId,
		[FromQuery] string? type,
		[FromQuery] string? search,
		[FromQuery] string? parentId,
		[FromQuery] int page = 0,
		[FromQuery] int pageSize = 50,
		CancellationToken ct = default)
	{
		var server = await this._serverService.GetServerAsync(serverId, ct).ConfigureAwait(false);
		if (server is null)
		{
			return this.NotFound();
		}

		var serverServiceImpl = (JellyfinServerService)this._serverService;
		var client = this._clientFactory.CreateClient(server.BaseUrl, serverServiceImpl.DecryptApiKey(server.ApiKey));
		var response = await client.GetItemsAsync(new GetItemsRequest
		{
			IncludeItemTypes = type,
			SearchTerm = search,
			ParentId = parentId,
			StartIndex = page * pageSize,
			Limit = pageSize,
			Recursive = true,
			Fields = "ProviderIds",
		}, ct).ConfigureAwait(false);

		return this.Ok(response);
	}

	[HttpPost("servers/{serverId:guid}/encode")]
	public async Task<IActionResult> QueueEncode(Guid serverId, [FromBody] JellyfinEncodeRequest request, CancellationToken ct)
	{
		request.ServerId = serverId;
		var item = await this._encodeService.QueueItemAsync(request, ct).ConfigureAwait(false);
		return this.Accepted(new { item.Id });
	}

	[HttpGet("servers/{serverId:guid}/destination-roots")]
	public async Task<IActionResult> GetDestinationRoots(Guid serverId, CancellationToken ct) =>
		this.Ok(await this._serverService.GetDestinationRootsAsync(serverId, ct).ConfigureAwait(false));

	[HttpPost("servers/{serverId:guid}/destination-roots")]
	public async Task<IActionResult> CreateDestinationRoot(Guid serverId, [FromBody] JellyfinDestinationRootDto dto, CancellationToken ct)
	{
		dto.ServerId = serverId;
		var root = await this._serverService.CreateDestinationRootAsync(dto, ct).ConfigureAwait(false);
		return this.Created($"/api/jellyfin/destination-roots/{root.Id}", root);
	}

	[HttpPut("destination-roots/{rootId:guid}")]
	public async Task<IActionResult> UpdateDestinationRoot(Guid rootId, [FromBody] JellyfinDestinationRootDto dto, CancellationToken ct)
	{
		var result = await this._serverService.UpdateDestinationRootAsync(rootId, dto, ct).ConfigureAwait(false);
		return result is not null ? this.Ok(result) : this.NotFound();
	}

	[HttpDelete("destination-roots/{rootId:guid}")]
	public async Task<IActionResult> DeleteDestinationRoot(Guid rootId, CancellationToken ct) =>
		await this._serverService.DeleteDestinationRootAsync(rootId, ct).ConfigureAwait(false) ? this.NoContent() : this.NotFound();
}
