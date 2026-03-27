using Sannel.Encoding.Manager.Web.Features.Settings.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Settings.Services;

/// <summary>Common languages available for audio/subtitle selection.</summary>
public static class LanguageList
{
	public static IReadOnlyList<LanguageDefinition> Languages { get; } =
	[
		new() { Name = "English", Code = "eng" },
		new() { Name = "Japanese", Code = "jpn" },
		new() { Name = "Spanish", Code = "spa" },
		new() { Name = "French", Code = "fra" },
		new() { Name = "German", Code = "deu" },
		new() { Name = "Korean", Code = "kor" },
		new() { Name = "Chinese", Code = "zho" },
		new() { Name = "Italian", Code = "ita" },
		new() { Name = "Portuguese", Code = "por" },
		new() { Name = "Russian", Code = "rus" },
		new() { Name = "Arabic", Code = "ara" },
		new() { Name = "Hindi", Code = "hin" },
		new() { Name = "Dutch", Code = "nld" },
		new() { Name = "Swedish", Code = "swe" },
		new() { Name = "Norwegian", Code = "nor" },
		new() { Name = "Finnish", Code = "fin" },
		new() { Name = "Danish", Code = "dan" },
		new() { Name = "Polish", Code = "pol" },
		new() { Name = "Czech", Code = "ces" },
		new() { Name = "Hungarian", Code = "hun" },
	];
}
