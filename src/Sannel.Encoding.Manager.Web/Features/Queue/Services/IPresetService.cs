using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public interface IPresetService
{
	Task<IReadOnlyList<EncodingPreset>> GetPresetsAsync(CancellationToken ct = default);
	Task AddPresetAsync(EncodingPreset preset, CancellationToken ct = default);
	Task DeletePresetAsync(Guid id, CancellationToken ct = default);
}
