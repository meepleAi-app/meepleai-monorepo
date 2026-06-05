using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class KeyFingerprintTests
{
    [Theory]
    [InlineData("sk-deepseek-abcd1234efgh5678", "sk-de..5678")]
    [InlineData("sk-or-v1-foobar1234", "sk-or..1234")]
    [InlineData("1234567890", "12345..7890")]
    public void FromPlaintext_ValidKey_ReturnsMaskedFingerprint(string raw, string expected)
    {
        var fp = KeyFingerprint.FromPlaintext(raw);
        fp.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("123456789")]
    [InlineData("")]
    public void FromPlaintext_TooShort_Throws(string raw)
    {
        var act = () => KeyFingerprint.FromPlaintext(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPlaintext_NullKey_Throws()
    {
        var act = () => KeyFingerprint.FromPlaintext(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPlaintext_DoesNotLeakFullKey_OnlyFingerprintExposed()
    {
        var raw = "sk-this-is-a-very-secret-deepseek-key-abcd1234";
        var fp = KeyFingerprint.FromPlaintext(raw);

        fp.Value.Should().NotContain(raw);
        fp.Value.Length.Should().BeLessThan(raw.Length);
        fp.Value.Should().Contain("..");
    }

    [Fact]
    public void FromStorage_BypassesValidation_ForEfRehydration()
    {
        var fp = KeyFingerprint.FromStorage("sk-de..5678");
        fp.Value.Should().Be("sk-de..5678");
    }
}
