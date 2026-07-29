using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Covers;

/// <summary>
/// Issue #3384 (ADR-087 D5-A) — the Docker-free contract test the issue asks for.
/// Asserts that the SINGLE key-builder makes write-key and read-key coincide
/// by construction for every <see cref="CoverKind"/>, so the recurring
/// <c>.webp.webp</c> double-suffix / writer↔resolver drift bug is structurally
/// impossible. Replaces the value of <c>CoverR2ConventionIntegrationTests</c>
/// (which needs MinIO and skips locally) with a pure, always-runnable check.
///
/// The golden keys also pin the EXACT production convention — this refactor must
/// remain byte-compatible with keys already stored in R2/DB (no data migration).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedKernel")]
public sealed class CoverKeyContractTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PdfId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const int BggId = 13;

    [Fact]
    public void ForUser_MatchesProductionConvention()
    {
        var key = CoverKeyBuilder.ForUser(UserId, GameId);

        key.Kind.Should().Be(CoverKind.User);
        key.DbKey.Should().Be($"user-covers/{UserId:D}/{GameId:D}/cover");
        key.PhysicalKey.Should().Be($"user-covers/{UserId:D}/{GameId:D}/cover.webp");
    }

    [Fact]
    public void ForPdf_MatchesProductionConvention()
    {
        var key = CoverKeyBuilder.ForPdf(PdfId);

        key.Kind.Should().Be(CoverKind.Pdf);
        key.DbKey.Should().Be($"covers/pdf/{PdfId:D}/cover");
        key.PhysicalKey.Should().Be($"covers/pdf/{PdfId:D}/cover-preview.webp");
    }

    [Fact]
    public void ForWikidata_MatchesProductionConvention()
    {
        var key = CoverKeyBuilder.ForWikidata(GameId);

        key.Kind.Should().Be(CoverKind.Wikidata);
        key.DbKey.Should().Be($"covers/{GameId:D}/cover");
        key.PhysicalKey.Should().Be($"covers/{GameId:D}/cover.webp");
    }

    [Theory]
    [InlineData(".jpg", "bgg-covers/13/cover.jpg")]
    [InlineData("jpg", "bgg-covers/13/cover.jpg")]    // normalizes a missing leading dot
    [InlineData(".JPG", "bgg-covers/13/cover.jpg")]   // normalizes case
    [InlineData(".jpeg", "bgg-covers/13/cover.jpeg")]
    [InlineData(".png", "bgg-covers/13/cover.png")]
    [InlineData(".webp", "bgg-covers/13/cover.webp")]
    [InlineData("", "bgg-covers/13/cover.jpg")]        // empty → default extension
    [InlineData(".bmp", "bgg-covers/13/cover.jpg")]    // unsupported → default extension
    public void ForBgg_MatchesProductionConventionAndNormalizesExtension(string ext, string expected)
    {
        var key = CoverKeyBuilder.ForBgg(BggId, ext);

        key.Kind.Should().Be(CoverKind.Bgg);
        // BGG stores its source extension IN the key → physical == db (no suffix appended).
        key.DbKey.Should().Be(expected);
        key.PhysicalKey.Should().Be(expected);
    }

    /// <summary>
    /// THE core contract: a key stored in the DB (the suffix-free <c>DbKey</c>),
    /// re-read and passed to <see cref="CoverKeyBuilder.PhysicalKeyFor"/>, must
    /// reconstruct EXACTLY the physical key the writer PUT — for every kind,
    /// including the BGG-with-.webp-source edge case where the ext equals the
    /// suffix (must NOT produce <c>cover.webp.webp</c>).
    /// </summary>
    [Fact]
    public void PhysicalKeyFor_FromStoredDbKey_ReconstructsWriterPhysicalKey_ForEveryKind()
    {
        var keys = new[]
        {
            CoverKeyBuilder.ForUser(UserId, GameId),
            CoverKeyBuilder.ForPdf(PdfId),
            CoverKeyBuilder.ForWikidata(GameId),
            CoverKeyBuilder.ForBgg(BggId, ".jpg"),
            CoverKeyBuilder.ForBgg(BggId, ".webp"),
        };

        foreach (var k in keys)
        {
            CoverKeyBuilder.PhysicalKeyFor(k.Kind, k.DbKey).Should().Be(
                k.PhysicalKey,
                $"the resolver must reconstruct the writer's physical key for {k.Kind}");
            k.PhysicalKey.Should().NotContain(".webp.webp");
            k.PhysicalKey.Should().NotContain("-preview.webp-preview.webp");
        }
    }

    [Fact]
    public void PhysicalKeyFor_RejectsBlankDbKey()
    {
        var act = () => CoverKeyBuilder.PhysicalKeyFor(CoverKind.Pdf, "  ");

        act.Should().Throw<ArgumentException>();
    }
}
