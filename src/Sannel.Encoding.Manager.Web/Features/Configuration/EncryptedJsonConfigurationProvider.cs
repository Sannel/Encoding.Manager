using Microsoft.Extensions.Configuration.Json;

namespace Sannel.Encoding.Manager.Web.Features.Configuration;

internal sealed class EncryptedJsonConfigurationProvider : JsonConfigurationProvider
{
	public EncryptedJsonConfigurationProvider(JsonConfigurationSource source) : base(source) { }

	public override void Load()
	{
		base.Load();

		foreach (var key in this.Data.Keys.ToList())
		{
			var value = this.Data[key];
			if (ConfigProtector.IsEncrypted(value))
			{
				this.Data[key] = ConfigProtector.Unprotect(value!);
			}
		}
	}
}
