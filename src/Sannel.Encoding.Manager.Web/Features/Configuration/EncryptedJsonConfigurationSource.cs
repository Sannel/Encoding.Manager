using Microsoft.Extensions.Configuration.Json;

namespace Sannel.Encoding.Manager.Web.Features.Configuration;

internal sealed class EncryptedJsonConfigurationSource : JsonConfigurationSource
{
	public override IConfigurationProvider Build(IConfigurationBuilder builder)
	{
		this.EnsureDefaults(builder);
		return new EncryptedJsonConfigurationProvider(this);
	}
}
