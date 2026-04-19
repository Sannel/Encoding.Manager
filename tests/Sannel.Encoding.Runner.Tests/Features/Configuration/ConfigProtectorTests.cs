using Sannel.Encoding.Runner.Features.Configuration;

namespace Sannel.Encoding.Runner.Tests.Features.Configuration;

public sealed class ConfigProtectorTests
{
	[Fact]
	public void IsEncrypted_Null_ReturnsFalse()
		=> Assert.False(ConfigProtector.IsEncrypted(null));

	[Fact]
	public void IsEncrypted_EmptyString_ReturnsFalse()
		=> Assert.False(ConfigProtector.IsEncrypted(string.Empty));

	[Fact]
	public void IsEncrypted_PlainValue_ReturnsFalse()
		=> Assert.False(ConfigProtector.IsEncrypted("plain-value"));

	[Fact]
	public void IsEncrypted_EncPrefix_ReturnsTrue()
		=> Assert.True(ConfigProtector.IsEncrypted("enc:someencrypteddata"));

	[Fact]
	public void IsEncrypted_WrongCase_ReturnsFalse()
		=> Assert.False(ConfigProtector.IsEncrypted("ENC:someencrypteddata"));

	[Fact]
	public void Protect_ReturnsValueWithEncPrefix()
	{
		var result = ConfigProtector.Protect("my-secret");

		Assert.StartsWith(ConfigProtector.Prefix, result, StringComparison.Ordinal);
	}

	[Fact]
	public void Protect_Unprotect_RoundTrip()
	{
		var original = "super-secret-value";

		var encrypted = ConfigProtector.Protect(original);
		var decrypted = ConfigProtector.Unprotect(encrypted);

		Assert.Equal(original, decrypted);
	}

	[Theory]
	[InlineData("plain-value")]
	[InlineData("")]
	[InlineData("http://example.com/api")]
	public void Unprotect_PlainValue_ReturnsAsIs(string plain)
		=> Assert.Equal(plain, ConfigProtector.Unprotect(plain));

	[Fact]
	public void Unprotect_InvalidPayload_ThrowsInvalidOperationException()
	{
		var bad = ConfigProtector.Prefix + "this-is-not-valid-encrypted-data";

		Assert.Throws<InvalidOperationException>(() => ConfigProtector.Unprotect(bad));
	}
}
