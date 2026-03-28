using Microsoft.Extensions.Configuration.Json;

namespace Sannel.Encoding.Runner.Features.Configuration;

/// <summary>Configuration source that reads a JSON file and decrypts <c>enc:</c> values.</summary>
internal sealed class EncryptedJsonConfigurationSource : JsonConfigurationSource
{
	public override IConfigurationProvider Build(IConfigurationBuilder builder)
	{
		this.EnsureDefaults(builder);
		return new EncryptedJsonConfigurationProvider(this);
	}
}
