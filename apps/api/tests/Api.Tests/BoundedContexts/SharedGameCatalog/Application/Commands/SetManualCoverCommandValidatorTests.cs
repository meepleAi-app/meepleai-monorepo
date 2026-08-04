using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a — boundary validation for the manual (arbitrary-URL) cover set.
/// The license whitelist rule is the PRIMARY copyright gate for this path (ADR-059 / DEC-3c):
/// it must reject "All Rights Reserved" / BGG / CC-BY-NC BEFORE any download, so the manual
/// path cannot launder around the catalog's redistribution posture. These tests lock that gate.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SetManualCoverCommandValidatorTests
{
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly SetManualCoverCommandValidator _validator = new();

    private static SetManualCoverCommand Valid() =>
        new(GameId, "https://commons.example.org/img.jpg", "CC BY-SA 4.0", "Jane Doe", AdminId);

    [Fact]
    public void Passes_ForAValidCommand()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Passes_WhenAttributionOmitted()
    {
        // Attribution is optional (PD / CC0 need none); only the license gate is mandatory.
        _validator.TestValidate(Valid() with { Attribution = null })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Fails_WhenGameIdEmpty()
    {
        _validator.TestValidate(Valid() with { GameId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Fails_WhenAdminIdEmpty()
    {
        _validator.TestValidate(Valid() with { AdminId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.AdminId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fails_WhenSourceUrlEmpty(string url)
    {
        _validator.TestValidate(Valid() with { SourceUrl = url })
            .ShouldHaveValidationErrorFor(x => x.SourceUrl);
    }

    [Theory]
    [InlineData("http://commons.example.org/img.jpg")]  // http downgrade
    [InlineData("ftp://host/img.jpg")]                   // non-http scheme
    [InlineData("/relative/path.jpg")]                   // not absolute
    [InlineData("not-a-url")]
    public void Fails_WhenSourceUrlNotAbsoluteHttps(string url)
    {
        _validator.TestValidate(Valid() with { SourceUrl = url })
            .ShouldHaveValidationErrorFor(x => x.SourceUrl);
    }

    [Theory]
    [InlineData("https://cf.geekdo-images.com/x/original/cover.jpg")]
    [InlineData("https://images.geekdo.com/x/cover.jpg")]
    [InlineData("https://geekdo-images.com/cover.jpg")]
    [InlineData("https://api.boardgamegeek.com/xmlapi2/thing?id=13")]
    [InlineData("https://www.boardgamegeek.com/image/123")]
    public void Fails_WhenSourceUrlIsBannedBggHost(string url)
    {
        // #3495 C6 — ADR-059 §5 / #2123: the manual (arbitrary-URL) path must reject BGG/geekdo
        // hosts BEFORE download, so it cannot launder around the catalog-wide BGG asset freeze.
        // Each URL is absolute-HTTPS with a whitelisted license, so ONLY the new host rule can reject it.
        _validator.TestValidate(Valid() with { SourceUrl = url })
            .ShouldHaveValidationErrorFor(x => x.SourceUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fails_WhenLicenseEmpty(string license)
    {
        _validator.TestValidate(Valid() with { License = license })
            .ShouldHaveValidationErrorFor(x => x.License);
    }

    [Theory]
    [InlineData("All rights reserved")]
    [InlineData("CC BY-NC 4.0")]   // NonCommercial — redistribution-encumbered
    [InlineData("CC BY-ND 4.0")]   // NoDerivatives
    [InlineData("BGG")]
    [InlineData("Fair use")]
    public void Fails_WhenLicenseNotWhitelisted(string license)
    {
        // The copyright gate: anything outside PD / CC0 / CC-BY / CC-BY-SA is rejected at 400
        // BEFORE the handler downloads or stores anything.
        _validator.TestValidate(Valid() with { License = license })
            .ShouldHaveValidationErrorFor(x => x.License);
    }

    [Theory]
    [InlineData("Public domain")]
    [InlineData("CC0")]
    [InlineData("CC BY 2.0")]
    [InlineData("CC-BY-4.0")]
    [InlineData("CC BY-SA 3.0")]
    public void Passes_ForWhitelistedLicenses(string license)
    {
        _validator.TestValidate(Valid() with { License = license })
            .ShouldNotHaveValidationErrorFor(x => x.License);
    }
}
