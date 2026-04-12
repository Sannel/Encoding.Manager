using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace Sannel.Encoding.Runner.Features.Configuration;

/// <summary>
/// Encrypts and decrypts sensitive configuration values using
/// <see cref="Microsoft.AspNetCore.DataProtection"/> — cross-platform, key-ring
/// based. Keys are stored in <c>config/keys/</c> next to the executable.
/// On Windows the key ring is additionally protected by DPAPI automatically.
/// </summary>
public static class ConfigProtector
{
	public const string Prefix = "enc:";

	private static readonly string KeyDirectory =
		Path.Combine(AppContext.BaseDirectory, "config", "keys");

	private static readonly Lazy<IDataProtector> ProtectorLazy = new(() =>
		DataProtectionProvider.Create(
			new DirectoryInfo(KeyDirectory),
			opt => opt.SetApplicationName("Sannel.Encoding.Runner"))
		.CreateProtector("ConfigSecrets.v1"));

	private static IDataProtector Protector => ProtectorLazy.Value;

	/// <summary>Encrypts <paramref name="plaintext"/> and prefixes it with <c>enc:</c>.</summary>
	public static string Protect(string plaintext) =>
		Prefix + Protector.Protect(plaintext);

	/// <summary>
	/// Decrypts an encrypted value. If the value does not start with the
	/// <c>enc:</c> prefix it is returned as-is.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when decryption fails (e.g. key ring missing or keys rotated).
	/// Re-run 'runner configure' to re-enter the secret.
	/// </exception>
	public static string Unprotect(string value)
	{
		if (!IsEncrypted(value))
		{
			return value;
		}

		try
		{
			return Protector.Unprotect(value[Prefix.Length..]);
		}
		catch (CryptographicException ex)
		{
			throw new InvalidOperationException(
				"Failed to decrypt a protected configuration value. " +
				"The key ring may be missing or the keys may have been rotated. " +
				"Re-run 'runner configure' to re-enter the secret.", ex);
		}
	}

	/// <summary>Returns <see langword="true"/> if <paramref name="value"/> has the <c>enc:</c> prefix.</summary>
	public static bool IsEncrypted(string? value) =>
		value?.StartsWith(Prefix, StringComparison.Ordinal) ?? false;
}
