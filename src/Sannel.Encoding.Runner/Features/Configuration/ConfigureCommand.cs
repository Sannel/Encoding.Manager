using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sannel.Encoding.Runner.Features.Configuration;

/// <summary>
/// Interactive configuration wizard that writes to <c>config/appsettings.json</c>
/// alongside the executable. Secrets are encrypted with Microsoft.Extensions.DataProtection (cross-platform).
/// </summary>
/// <remarks>
/// Add <c>config/appsettings.json</c> to .gitignore — it may contain secrets.
/// </remarks>
public static class ConfigureCommand
{
	public static readonly string ConfigDirectory =
		Path.Combine(AppContext.BaseDirectory, "config");

	public static readonly string ConfigFilePath =
		Path.Combine(ConfigDirectory, "appsettings.json");

	private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	/// <summary>Runs the interactive configuration wizard.</summary>
	public static void Run()
	{
		Console.WriteLine("=== Sannel Encoding Runner — Configuration Wizard ===");
		Console.WriteLine($"Config file : {ConfigFilePath}");
		Console.WriteLine("Secrets are encrypted with Microsoft.Extensions.DataProtection (cross-platform).");
		Console.WriteLine("Press Enter to keep an existing value.");
		Console.WriteLine();

		var root = LoadOrCreate();

		// ── Azure AD ──────────────────────────────────────────────────────────
		Console.WriteLine("[Azure AD — OAuth2 Client Credentials]");
		PromptPlaintext(root, "AzureAd:TenantId",     "  Tenant ID    ");
		PromptPlaintext(root, "AzureAd:ClientId",      "  Client ID    ");
		PromptSecret(root,    "AzureAd:ClientSecret",  "  Client Secret");
		Console.WriteLine();

		Save(root);

		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"Configuration saved to {ConfigFilePath}");
		Console.ResetColor();
	}

	// ── Prompt helpers ───────────────────────────────────────────────────────

	private static void PromptPlaintext(JsonObject root, string key, string label)
	{
		var current = GetValue(root, key);
		var display = current ?? "not set";
		Console.Write($"{label} [current: {display}]: ");
		var input = Console.ReadLine();
		if (!string.IsNullOrEmpty(input))
		{
			SetValue(root, key, input);
		}
	}

	private static void PromptSecret(JsonObject root, string key, string label)
	{
		var current = GetValue(root, key);
		var display = current is null
			? "not set"
			: ConfigProtector.IsEncrypted(current) ? "[encrypted]" : "[set — plaintext]";

		Console.Write($"{label} [current: {display}] (leave blank to skip): ");
		var input = ReadHiddenLine();
		Console.WriteLine();

		if (!string.IsNullOrEmpty(input))
		{
			SetValue(root, key, ConfigProtector.Protect(input));
		}
	}

	/// <summary>Reads a line from stdin without echoing characters.</summary>
	private static string ReadHiddenLine()
	{
		var sb = new StringBuilder();
		ConsoleKeyInfo key;
		while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
		{
			if (key.Key == ConsoleKey.Backspace)
			{
				if (sb.Length > 0)
				{
					sb.Length--;
					Console.Write("\b \b");
				}
			}
			else if (key.KeyChar != '\0')
			{
				sb.Append(key.KeyChar);
				Console.Write('*');
			}
		}

		return sb.ToString();
	}

	// ── JSON helpers ─────────────────────────────────────────────────────────

	private static JsonObject LoadOrCreate()
	{
		if (File.Exists(ConfigFilePath))
		{
			try
			{
				var text = File.ReadAllText(ConfigFilePath);
				return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
			}
			catch (JsonException)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"WARNING: Could not parse existing {ConfigFilePath}. Starting fresh.");
				Console.ResetColor();
			}
		}

		return new JsonObject();
	}

	private static void Save(JsonObject root)
	{
		Directory.CreateDirectory(ConfigDirectory);
		File.WriteAllText(ConfigFilePath, root.ToJsonString(WriteOptions));
	}

	/// <summary>Gets a leaf string value by colon-separated path.</summary>
	private static string? GetValue(JsonObject root, string colonKey)
	{
		var parts = colonKey.Split(':');
		JsonNode? node = root;

		foreach (var part in parts)
		{
			if (node is not JsonObject obj || !obj.ContainsKey(part))
			{
				return null;
			}

			node = obj[part];
		}

		return node?.ToString();
	}

	/// <summary>Sets a leaf string value by colon-separated path, creating intermediate objects as needed.</summary>
	private static void SetValue(JsonObject root, string colonKey, string value)
	{
		var parts = colonKey.Split(':');
		var current = root;

		for (var i = 0; i < parts.Length - 1; i++)
		{
			if (!current.ContainsKey(parts[i]) || current[parts[i]] is not JsonObject childObj)
			{
				childObj = new JsonObject();
				current[parts[i]] = childObj;
			}

			current = childObj;
		}

		current[parts[^1]] = value;
	}
}
