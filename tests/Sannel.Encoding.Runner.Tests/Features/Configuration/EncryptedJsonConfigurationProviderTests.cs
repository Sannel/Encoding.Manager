using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Sannel.Encoding.Runner.Features.Configuration;

namespace Sannel.Encoding.Runner.Tests.Features.Configuration;

public sealed class EncryptedJsonConfigurationProviderTests : IDisposable
{
	private readonly string _tempDir;

	public EncryptedJsonConfigurationProviderTests()
	{
		this._tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(this._tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(this._tempDir))
		{
			Directory.Delete(this._tempDir, recursive: true);
		}
	}

	[Fact]
	public void Load_PlainValues_ReturnedAsIs()
	{
		var configPath = this.WriteJson(new Dictionary<string, string?>
		{
			["Section:Key"] = "plain-value"
		});

		var config = new ConfigurationBuilder()
			.AddEncryptedJsonFile(configPath, optional: false)
			.Build();

		Assert.Equal("plain-value", config["Section:Key"]);
	}

	[Fact]
	public void Load_EncryptedValues_AreDecrypted()
	{
		var secret = "database-password";
		var encrypted = ConfigProtector.Protect(secret);

		var configPath = this.WriteJson(new Dictionary<string, string?>
		{
			["Database:Password"] = encrypted
		});

		var config = new ConfigurationBuilder()
			.AddEncryptedJsonFile(configPath, optional: false)
			.Build();

		Assert.Equal(secret, config["Database:Password"]);
	}

	[Fact]
	public void Load_MixedValues_OnlyEncryptedAreDecrypted()
	{
		var secret = "api-key";
		var encrypted = ConfigProtector.Protect(secret);

		var configPath = this.WriteJson(new Dictionary<string, string?>
		{
			["App:Name"] = "my-app",
			["App:Secret"] = encrypted
		});

		var config = new ConfigurationBuilder()
			.AddEncryptedJsonFile(configPath, optional: false)
			.Build();

		Assert.Equal("my-app", config["App:Name"]);
		Assert.Equal(secret, config["App:Secret"]);
	}

	[Fact]
	public void Load_OptionalMissingFile_DoesNotThrow()
	{
		var missingPath = Path.Combine(this._tempDir, "nonexistent.json");

		var config = new ConfigurationBuilder()
			.AddEncryptedJsonFile(missingPath, optional: true)
			.Build();

		Assert.Null(config["Anything"]);
	}

	private string WriteJson(Dictionary<string, string?> flatValues)
	{
		var path = Path.Combine(this._tempDir, $"{Path.GetRandomFileName()}.json");
		var root = new JsonObject();

		foreach (var (colonKey, value) in flatValues)
		{
			var parts = colonKey.Split(':');
			var current = root;

			for (var i = 0; i < parts.Length - 1; i++)
			{
				if (current[parts[i]] is not JsonObject child)
				{
					child = new JsonObject();
					current[parts[i]] = child;
				}

				current = child;
			}

			current[parts[^1]] = value;
		}

		File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		return path;
	}
}
