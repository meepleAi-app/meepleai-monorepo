using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class LicenseValidatorTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // IsWhitelisted — happy path (DEC-3c whitelist)
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Public domain")]
    [InlineData("PUBLIC DOMAIN")]
    [InlineData("public domain")]
    [InlineData("PD")]
    [InlineData("pd")]
    [InlineData("CC0")]
    [InlineData("cc0")]
    [InlineData("CC BY")]              // DEC-3c "any version" includes bare unversioned form (rare on Commons but seen on legacy uploads)
    [InlineData("CC-BY")]
    [InlineData("CC BY 2.0")]
    [InlineData("CC BY 3.0")]
    [InlineData("CC BY 4.0")]
    [InlineData("CC-BY-4.0")]
    [InlineData("cc by 2.0")]
    [InlineData("CC BY-SA 3.0")]
    [InlineData("CC BY-SA 4.0")]
    [InlineData("CC-BY-SA-4.0")]
    [InlineData("cc by-sa 3.0")]
    public void IsWhitelisted_WhitelistedLicense_ReturnsTrue(string licenseShortName)
    {
        LicenseValidator.IsWhitelisted(licenseShortName).Should().BeTrue(
            "ADR DEC-3c whitelist includes PD / CC0 / CC-BY (any version) / CC-BY-SA (any version)");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IsWhitelisted — rejected (adversarial fixtures per Crispin C-002 concern)
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CC BY-NC 4.0")]      // non-commercial restriction
    [InlineData("CC-BY-NC-4.0")]
    [InlineData("CC BY-ND 4.0")]      // no-derivatives restriction
    [InlineData("CC-BY-ND-3.0")]
    [InlineData("CC BY-NC-SA 4.0")]   // non-commercial + share-alike
    [InlineData("CC BY-NC-ND 4.0")]   // non-commercial + no-derivatives
    [InlineData("Fair use")]
    [InlineData("All Rights Reserved")]
    [InlineData("Copyright")]
    [InlineData("GFDL")]               // GNU Free Documentation License (not in whitelist)
    [InlineData("Apache 2.0")]
    [InlineData("MIT")]                // software license, not image license
    public void IsWhitelisted_RejectedLicense_ReturnsFalse(string licenseShortName)
    {
        LicenseValidator.IsWhitelisted(licenseShortName).Should().BeFalse(
            "ADR DEC-3c rejects any license outside the explicit whitelist");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IsWhitelisted — degenerate inputs
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsWhitelisted_EmptyOrWhitespace_ReturnsFalse(string licenseShortName)
    {
        LicenseValidator.IsWhitelisted(licenseShortName).Should().BeFalse();
    }

    [Fact]
    public void IsWhitelisted_Null_ReturnsFalse()
    {
        LicenseValidator.IsWhitelisted(null).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Normalize — canonical form for storage
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CC BY 2.0", "CC-BY-2.0")]
    [InlineData("cc by 4.0", "CC-BY-4.0")]
    [InlineData("CC-BY-3.0", "CC-BY-3.0")]
    [InlineData("CC BY-SA 4.0", "CC-BY-SA-4.0")]
    [InlineData("CC-BY-SA-3.0", "CC-BY-SA-3.0")]
    [InlineData("Public domain", "PUBLIC-DOMAIN")]
    [InlineData("PD", "PD")]
    [InlineData("CC0", "CC0")]
    public void Normalize_WhitelistedLicense_ReturnsCanonicalForm(string input, string expected)
    {
        LicenseValidator.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_RejectedLicense_ThrowsArgumentException()
    {
        var act = () => LicenseValidator.Normalize("CC BY-NC 4.0");
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("licenseShortName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrEmpty_ThrowsArgumentException(string? input)
    {
        var act = () => LicenseValidator.Normalize(input!);
        act.Should().Throw<ArgumentException>();
    }
}
