using Microsoft.Extensions.Configuration.Json;

namespace Sannel.Encoding.Runner.Features.Configuration;

/// <summary>
/// Extends <see cref="JsonConfigurationProvider"/> to transparently decrypt
/// any configuration values prefixed with <c>enc:</c> at load time.
/// </summary>
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
