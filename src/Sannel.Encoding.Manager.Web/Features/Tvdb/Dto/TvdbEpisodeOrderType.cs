namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

public enum TvdbEpisodeOrderType
{
	/// <summary>Default broadcast/official order (TVDB "official").</summary>
	Default,

	/// <summary>DVD disc order (TVDB "dvd").</summary>
	Dvd,

	/// <summary>Blu-ray / absolute order (TVDB "absolute").</summary>
	Bluray,

	/// <summary>Streaming release order (TVDB "streaming").</summary>
	Streaming,
}
