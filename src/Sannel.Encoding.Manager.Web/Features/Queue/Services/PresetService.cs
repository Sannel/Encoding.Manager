using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public class PresetService : IPresetService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public PresetService(IDbContextFactory<AppDbContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EncodingPreset>> GetPresetsAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.EncodingPresets
			.AsNoTracking()
			.OrderBy(p => p.Label)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task AddPresetAsync(EncodingPreset preset, CancellationToken ct = default)
	{
		preset.RelativePath = PathHelper.ToForwardSlash(preset.RelativePath);
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		ctx.EncodingPresets.Add(preset);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DeletePresetAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		await ctx.EncodingPresets
			.Where(p => p.Id == id)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Reads a HandBrake preset JSON file and extracts the first preset's name.
	/// </summary>
	/// <param name="filePath">Absolute path to the .json preset file.</param>
	/// <returns>The preset name from the JSON, or null if not found or file doesn't exist.</returns>
	public static string? ExtractPresetNameFromFile(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		try
		{
			var json = File.ReadAllText(filePath);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// HandBrake preset files have a PresetList array with preset objects
			if (root.TryGetProperty("PresetList", out var presetList) && presetList.ValueKind == JsonValueKind.Array)
			{
				foreach (var preset in presetList.EnumerateArray())
				{
					if (preset.TryGetProperty("PresetName", out var presetName) && presetName.ValueKind == JsonValueKind.String)
					{
						return presetName.GetString();
					}
				}
			}

			return null;
		}
		catch
		{
			return null;
		}
	}
}
