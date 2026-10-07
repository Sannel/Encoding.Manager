using System.Buffers.Binary;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>Reads the clip (play item) names from a Blu-ray playlist (BDMV/PLAYLIST/NNNNN.mpls).</summary>
public static class MplsReader
{
	/// <summary>
	/// Clip names ("00012") of the playlist's play items, in order; empty when the file is not a readable MPLS.
	/// Layout: "MPLS" + version, PlayList start address (uint32 BE at 8); PlayList: length (4), reserved (2),
	/// number_of_PlayItems (uint16 at +6), number_of_SubPaths (2), then PlayItems: length (uint16) followed by
	/// Clip_Information_file_name (5 ASCII characters) and the rest of the item.
	/// </summary>
	public static IReadOnlyList<string> ReadClipNames(byte[] mpls)
	{
		var clips = new List<string>();
		if (mpls.Length < 12 || System.Text.Encoding.ASCII.GetString(mpls, 0, 4) != "MPLS")
		{
			return clips;
		}

		var playList = (int)BinaryPrimitives.ReadUInt32BigEndian(mpls.AsSpan(8));
		if (playList + 10 > mpls.Length)
		{
			return clips;
		}

		int count = BinaryPrimitives.ReadUInt16BigEndian(mpls.AsSpan(playList + 6));
		var position = playList + 10;
		for (var i = 0; i < count && position + 7 <= mpls.Length; i++)
		{
			int length = BinaryPrimitives.ReadUInt16BigEndian(mpls.AsSpan(position));
			var name = System.Text.Encoding.ASCII.GetString(mpls, position + 2, 5);
			if (name.All(char.IsAsciiDigit) && !clips.Contains(name))
			{
				clips.Add(name);
			}

			position += 2 + length;
		}

		return clips;
	}

	/// <summary>
	/// The largest clip file (BDMV/STREAM/NNNNN.m2ts) of a playlist on a Blu-ray folder, or null when the playlist or its
	/// clips are not found. The largest clip is the most representative sample of what the title plays.
	/// </summary>
	public static string? LargestClip(string discPath, int playlist)
	{
		var bdmv = Path.Combine(discPath, "BDMV");
		var mpls = Path.Combine(bdmv, "PLAYLIST", playlist.ToString("00000", System.Globalization.CultureInfo.InvariantCulture) + ".mpls");
		if (!File.Exists(mpls))
		{
			return null;
		}

		return ReadClipNames(File.ReadAllBytes(mpls))
			.Select(name => new FileInfo(Path.Combine(bdmv, "STREAM", name + ".m2ts")))
			.Where(f => f.Exists)
			.OrderByDescending(f => f.Length)
			.FirstOrDefault()?.FullName;
	}
}
