using Microsoft.Extensions.FileProviders;

namespace Sannel.Encoding.Manager.Web.Features.Configuration;

public static class ConfigurationBuilderExtensions
{
	/// <summary>
	/// Adds a JSON configuration file where any value prefixed with <c>enc:</c>
	/// is automatically decrypted using Microsoft.Extensions.DataProtection at load time.
	/// </summary>
	public static IConfigurationBuilder AddEncryptedJsonFile(
		this IConfigurationBuilder builder,
		string path,
		bool optional = true,
		bool reloadOnChange = false)
	{
		var fullPath = Path.GetFullPath(path);
		var directory = Path.GetDirectoryName(fullPath)!;
		var fileName = Path.GetFileName(fullPath);

		Directory.CreateDirectory(directory);

		return builder.Add(new EncryptedJsonConfigurationSource
		{
			FileProvider = new PhysicalFileProvider(directory),
			Path = fileName,
			Optional = optional,
			ReloadOnChange = reloadOnChange
		});
	}
}
