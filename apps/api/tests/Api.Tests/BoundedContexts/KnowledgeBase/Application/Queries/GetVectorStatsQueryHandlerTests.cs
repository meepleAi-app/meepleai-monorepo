using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetVectorStatsQueryHandler.
/// Verifies pgvector stats aggregation by game from VectorDocuments.
/// Task 3: Qdrant → pgvector migration.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetVectorStatsQueryHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static MeepleAiDbContext CreateFreshDbContext()
        => TestDbContextFactory.CreateInMemoryDbContext();

    private static GetVectorStatsQueryHandler CreateHandler(MeepleAiDbContext context)
    {
        var logger = new Mock<ILogger<GetVectorStatsQueryHandler>>().Object;
        return new GetVectorStatsQueryHandler(context, logger);
    }

    // ──────────────────────────────────────────────
    // Test 1: Empty database → zero stats
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroStats()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var query = new GetVectorStatsQuery();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalVectors.Should().Be(0);
        result.GamesIndexed.Should().Be(0);
        result.AvgHealthPercent.Should().Be(100);
        result.GameBreakdown.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // Test 2: Two games with completed documents
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_TwoGamesWithCompletedDocs_ReturnsCorrectBreakdown()
    {
        // Arrange
        using var context = CreateFreshDbContext();

        var gameIdA = Guid.NewGuid();
        var gameIdB = Guid.NewGuid();
        var sharedGameIdA = Guid.NewGuid();
        var sharedGameIdB = Guid.NewGuid();

        // Seed SharedGame records
        context.SharedGames.AddRange(
            new SharedGameEntity { Id = sharedGameIdA, Title = "Catan", Description = "", ImageUrl = "", ThumbnailUrl = "" },
            new SharedGameEntity { Id = sharedGameIdB, Title = "Wingspan", Description = "", ImageUrl = "", ThumbnailUrl = "" }
        );

        // Game A: 10 completed chunks across 2 vector documents
        context.VectorDocuments.AddRange(
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameIdA, SharedGameId = sharedGameIdA, ChunkCount = 6, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid() },
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameIdA, SharedGameId = sharedGameIdA, ChunkCount = 4, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid() }
        );

        // Game B: 5 completed chunks in 1 vector document
        context.VectorDocuments.Add(
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameIdB, SharedGameId = sharedGameIdB, ChunkCount = 5, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid() }
        );

        await context.SaveChangesAsync(TestCancellationToken);

        var handler = CreateHandler(context);
        var query = new GetVectorStatsQuery();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalVectors.Should().Be(15);
        result.GamesIndexed.Should().Be(2);
        result.AvgHealthPercent.Should().Be(100);
        result.GameBreakdown.Should().HaveCount(2);

        var gameA = result.GameBreakdown.SingleOrDefault(g => g.GameId == sharedGameIdA);
        gameA.Should().NotBeNull();
        gameA!.GameName.Should().Be("Catan");
        gameA.VectorCount.Should().Be(10);
        gameA.CompletedCount.Should().Be(10);
        gameA.FailedCount.Should().Be(0);
        gameA.HealthPercent.Should().Be(100);

        var gameB = result.GameBreakdown.SingleOrDefault(g => g.GameId == sharedGameIdB);
        gameB.Should().NotBeNull();
        gameB!.GameName.Should().Be("Wingspan");
        gameB.VectorCount.Should().Be(5);
        gameB.CompletedCount.Should().Be(5);
        gameB.FailedCount.Should().Be(0);
        gameB.HealthPercent.Should().Be(100);
    }

    // ──────────────────────────────────────────────
    // Test 3: Mixed status — health calculation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_MixedStatusDocs_CalculatesHealthPercent()
    {
        // Arrange
        using var context = CreateFreshDbContext();

        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        context.SharedGames.Add(
            new SharedGameEntity { Id = sharedGameId, Title = "Terraforming Mars", Description = "", ImageUrl = "", ThumbnailUrl = "" }
        );

        // 8 completed chunks, 2 failed chunks across separate documents
        context.VectorDocuments.AddRange(
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameId, SharedGameId = sharedGameId, ChunkCount = 8, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid() },
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameId, SharedGameId = sharedGameId, ChunkCount = 2, IndexingStatus = "failed", PdfDocumentId = Guid.NewGuid() }
        );

        await context.SaveChangesAsync(TestCancellationToken);

        var handler = CreateHandler(context);
        var query = new GetVectorStatsQuery();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalVectors.Should().Be(10);
        result.GamesIndexed.Should().Be(1);
        result.AvgHealthPercent.Should().Be(80); // 8/10 = 80%

        result.GameBreakdown.Should().HaveCount(1);
        var game = result.GameBreakdown[0];
        game.GameId.Should().Be(sharedGameId);
        game.GameName.Should().Be("Terraforming Mars");
        game.VectorCount.Should().Be(10);
        game.CompletedCount.Should().Be(8);
        game.FailedCount.Should().Be(2);
        game.HealthPercent.Should().Be(80);
    }
}
