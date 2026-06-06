namespace Sannel.Encoding.Manager.Jellyfin.Dto;

public class JellyfinUserData
{
	public bool Played { get; set; }
	public long PlaybackPositionTicks { get; set; }
	public DateTimeOffset? LastPlayedDate { get; set; }
}
