using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

/// <summary>
/// Issue #2123 — BGG ToS compliance hard ban (data-plane layer).
///
/// Asserts that the seed manifest model no longer exposes BGG image properties
/// at compile-time. The runtime guard (Next.js allowlist + custom image loader)
/// is necessary but not sufficient: leaving these properties on the C# model
/// would let a future YAML edit re-introduce BGG URLs into <c>shared_games</c>
/// without lighting up any lint, because <c>SeedManifestLoader</c> is configured
/// with <c>IgnoreUnmatchedProperties</c> — extra fields are silently dropped.
///
/// Removing the properties forces the compiler to flag any seeder/test that
/// tries to read/write them, preventing accidental regression.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SeedManifestGameSchemaTests
{
    [Theory]
    [InlineData("BggEnhanced")]
    [InlineData("ImageUrl")]
    [InlineData("ThumbnailUrl")]
    [InlineData("FallbackImageUrl")]
    [InlineData("FallbackThumbnailUrl")]
    public void SeedManifestGame_DoesNotExposeBggImageProperty(string propertyName)
    {
        var property = typeof(SeedManifestGame).GetProperty(propertyName);
        property.Should().BeNull(
            $"property '{propertyName}' must be removed for #2123 BGG ToS compliance — see " +
            "docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md");
    }

    [Theory]
    [InlineData("ImageUrl")]
    [InlineData("ThumbnailUrl")]
    public void GameManifestEntry_DoesNotExposeBggImageProperty(string propertyName)
    {
        var property = typeof(GameManifestEntry).GetProperty(propertyName);
        property.Should().BeNull(
            $"property '{propertyName}' must be removed for #2123 BGG ToS compliance — see " +
            "docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md");
    }

    [Fact]
    public void SeedManifestGame_PreservesNonBggProperties()
    {
        // Sanity check: properties that should SURVIVE the refactor.
        var type = typeof(SeedManifestGame);
        type.GetProperty("Title").Should().NotBeNull();
        type.GetProperty("BggId").Should().NotBeNull("the BGG ID is still legitimate metadata — only assets are banned");
        type.GetProperty("Language").Should().NotBeNull();
        type.GetProperty("Description").Should().NotBeNull("description presence/absence replaces the BggEnhanced flag semantically");
        type.GetProperty("YearPublished").Should().NotBeNull();
        type.GetProperty("MinPlayers").Should().NotBeNull();
        type.GetProperty("MaxPlayers").Should().NotBeNull();
        type.GetProperty("Categories").Should().NotBeNull();
        type.GetProperty("Mechanics").Should().NotBeNull();
        type.GetProperty("PdfBlobKey").Should().NotBeNull();
    }
}
