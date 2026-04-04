using Api.BoundedContexts.Administration.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Unit tests for <see cref="RagBackupPathHelper"/> (pure static functions — no I/O, no mocks needed).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class RagBackupStorageServiceTests
{
    // -------------------------------------------------------------------------
    // BuildSnapshotBasePath
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSnapshotBasePath_ReturnsCorrectPath()
    {
        // Arrange
        const string snapshotId = "2026-03-28T12-00-00Z";

        // Act
        var path = RagBackupPathHelper.BuildSnapshotBasePath(snapshotId);

        // Assert
        path.Should().Be("rag-exports/2026-03-28T12-00-00Z");
    }

    [Fact]
    public void BuildSnapshotBasePath_StartsWithRagExports()
    {
        var path = RagBackupPathHelper.BuildSnapshotBasePath("any-id");
        path.Should().StartWith("rag-exports/");
    }

    [Theory]
    [InlineData("snapshot-1", "rag-exports/snapshot-1")]
    [InlineData("2026-01-01", "rag-exports/2026-01-01")]
    [InlineData("weekly-42", "rag-exports/weekly-42")]
    public void BuildSnapshotBasePath_VariousIds(string snapshotId, string expected)
    {
        RagBackupPathHelper.BuildSnapshotBasePath(snapshotId).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // BuildDocumentPath
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildDocumentPath_ReturnsCorrectStructure()
    {
        // Arrange
        const string snapshotId = "2026-03-28T12-00-00Z";
        const string gameSlug = "ark-nova";
        var docId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        // Act
        var path = RagBackupPathHelper.BuildDocumentPath(snapshotId, gameSlug, docId);

        // Assert
        path.Should().Be("rag-exports/2026-03-28T12-00-00Z/games/ark-nova/3fa85f64-5717-4562-b3fc-2c963f66afa6");
    }

    [Fact]
    public void BuildDocumentPath_ContainsGamesSeparator()
    {
        var path = RagBackupPathHelper.BuildDocumentPath("snap", "my-game", Guid.NewGuid());
        path.Should().Contain("/games/my-game/");
    }

    [Fact]
    public void BuildDocumentPath_StartsWithSnapshotBasePath()
    {
        const string snapshotId = "snap-001";
        const string gameSlug = "gloomhaven";
        var docId = Guid.NewGuid();

        var basePath = RagBackupPathHelper.BuildSnapshotBasePath(snapshotId);
        var docPath = RagBackupPathHelper.BuildDocumentPath(snapshotId, gameSlug, docId);

        docPath.Should().StartWith(basePath);
    }

    // -------------------------------------------------------------------------
    // Slugify
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Ark Nova", "ark-nova")]
    [InlineData("7 Wonders Duel", "7-wonders-duel")]
    [InlineData("King's Dilemma", "kings-dilemma")]
    [InlineData("Ticket to Ride: Europe", "ticket-to-ride-europe")]
    public void Slugify_CommonBoardGameNames(string name, string expected)
    {
        RagBackupPathHelper.Slugify(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("GLOOMHAVEN", "gloomhaven")]
    [InlineData("ALL CAPS GAME", "all-caps-game")]
    public void Slugify_AllCaps_LowerCasesResult(string name, string expected)
    {
        RagBackupPathHelper.Slugify(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Café Citéa", "cafe-citea")]           // accented characters removed after decomposition
    [InlineData("Römer", "romer")]
    public void Slugify_AccentedCharacters_RemovesAccents(string name, string expected)
    {
        RagBackupPathHelper.Slugify(name).Should().Be(expected);
    }

    [Fact]
    public void Slugify_MultipleSpaces_ProducesSingleHyphen()
    {
        RagBackupPathHelper.Slugify("Game  With   Spaces").Should().Be("game-with-spaces");
    }

    [Fact]
    public void Slugify_LeadingTrailingSpaces_AreTrimmed()
    {
        RagBackupPathHelper.Slugify("  padded game  ").Should().Be("padded-game");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Slugify_EmptyOrWhitespace_ReturnsUnknown(string name)
    {
        RagBackupPathHelper.Slugify(name).Should().Be("unknown");
    }

    [Fact]
    public void Slugify_OnlySpecialChars_ReturnsUnknown()
    {
        // After removing all special chars, nothing remains
        RagBackupPathHelper.Slugify("!@#$%^&*()").Should().Be("unknown");
    }

    [Fact]
    public void Slugify_NumbersArePreserved()
    {
        RagBackupPathHelper.Slugify("Race for the Galaxy 2").Should().Be("race-for-the-galaxy-2");
    }

    [Fact]
    public void Slugify_ColonSeparatedTitle_UsesHyphen()
    {
        // "Pandemic: Legacy Season 2" → "pandemic-legacy-season-2"
        RagBackupPathHelper.Slugify("Pandemic: Legacy Season 2").Should().Be("pandemic-legacy-season-2");
    }
}
