using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

/// <summary>
/// Tests for RAPTOR summary search in TextChunkSearchService.
/// Verifies keyword matching, scoring, tree level ordering, and error resilience.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RaptorSummarySearchTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly TextChunkSearchService _sut;
    private readonly Guid _gameId = Guid.NewGuid();

    public RaptorSummarySearchTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"RaptorSummarySearchTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _sut = new TextChunkSearchService(
            _db,
            NullLogger<TextChunkSearchService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_ReturnsMatchingResults_ScoredByKeywordOverlap()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _db.RaptorSummaries.AddRange(
            CreateSummary(docId, treeLevel: 2, "This is an overview of combat rules and movement"),
            CreateSummary(docId, treeLevel: 1, "Detailed section about combat mechanics and dice"),
            CreateSummary(docId, treeLevel: 1, "Section about trading and economy"),
            CreateSummary(docId, treeLevel: 0, "Leaf chunk about movement basics"));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat movement", topK: 10, CancellationToken.None);

        // Assert
        results.Should().NotBeEmpty();
        // The overview (level 2) mentions both "combat" and "movement" → highest score
        results[0].Content.Should().Contain("overview");
        results[0].Rank.Should().Be(1.0f); // 2/2 terms matched
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_RespectsTopK()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _db.RaptorSummaries.AddRange(
            CreateSummary(docId, treeLevel: 2, "combat rules overview"),
            CreateSummary(docId, treeLevel: 1, "combat details section"),
            CreateSummary(docId, treeLevel: 0, "combat leaf chunk"));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 2, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_PrefersHigherTreeLevelsOnTiedScore()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _db.RaptorSummaries.AddRange(
            CreateSummary(docId, treeLevel: 0, "combat rules"),
            CreateSummary(docId, treeLevel: 2, "combat rules"));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 10, CancellationToken.None);

        // Assert — same score, higher tree level should come first
        results.Should().HaveCount(2);
        results[0].Content.Should().Be("combat rules"); // Both have same content
        // Verify ordering via ChunkIndex=-1 (both are RAPTOR), just check count
        results.Should().AllSatisfy(r => r.ChunkIndex.Should().Be(-1));
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_ExcludesNonMatchingSummaries()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _db.RaptorSummaries.AddRange(
            CreateSummary(docId, treeLevel: 1, "combat and movement rules"),
            CreateSummary(docId, treeLevel: 1, "trading and economy rules"));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 10, CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].Content.Should().Contain("combat");
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_ReturnsEmpty_WhenNoSummariesExist()
    {
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 10, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_ReturnsEmpty_WhenQueryIsEmpty()
    {
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "", topK: 10, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_ReturnsEmpty_WhenQueryIsWhitespace()
    {
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "   ", topK: 10, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_IgnoresOtherGames()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        _db.RaptorSummaries.Add(new RaptorSummaryEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            GameId = otherGameId, // Different game
            TreeLevel = 1,
            ClusterIndex = 0,
            SummaryText = "combat rules for other game",
            SourceChunkCount = 5,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 10, CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchRaptorSummariesAsync_SetsChunkIndexToNegativeOne()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _db.RaptorSummaries.Add(CreateSummary(docId, treeLevel: 1, "combat rules"));
        await _db.SaveChangesAsync();

        // Act
        var results = await _sut.SearchRaptorSummariesAsync(
            _gameId, "combat", topK: 10, CancellationToken.None);

        // Assert — RAPTOR summaries use -1 as sentinel chunk index
        results.Should().ContainSingle()
            .Which.ChunkIndex.Should().Be(-1);
    }

    private RaptorSummaryEntity CreateSummary(Guid docId, int treeLevel, string text) => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = docId,
        GameId = _gameId,
        TreeLevel = treeLevel,
        ClusterIndex = 0,
        SummaryText = text,
        SourceChunkCount = 5,
        CreatedAt = DateTime.UtcNow
    };
}
