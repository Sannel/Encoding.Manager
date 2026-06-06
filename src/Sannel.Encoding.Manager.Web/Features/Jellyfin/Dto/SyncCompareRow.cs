using Sannel.Encoding.Manager.Jellyfin.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public sealed record SyncCompareRow(
	string DisplayName,
	string ItemType,
	long? RunTimeTicks,
	JellyfinUserData? UserDataA,
	JellyfinUserData? UserDataB);
