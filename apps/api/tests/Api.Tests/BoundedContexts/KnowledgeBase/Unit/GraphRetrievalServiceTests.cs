using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
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
/// Tests for GraphRetrievalService — queries entity-relation knowledge graph
/// and formats results as contextual information for RAG prompts.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GraphRetrievalServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GraphRetrievalService _sut;
    private readonly Guid _gameId = Guid.NewGuid();

    public GraphRetrievalServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"GraphRetrievalServiceTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _sut = new GraphRetrievalService(
            _db,
            NullLogger<GraphRetrievalService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetEntityContextAsync_WithRelations_ReturnsFormattedContext()
    {
        // Arrange
        _db.GameEntityRelations.AddRange(
            CreateRelation("Catan", "Game", "HasMechanic", "Trading", "Mechanic", 0.9f),
            CreateRelation("Catan", "Game", "HasComponent", "Hex Tiles", "Component", 0.85f));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntityContextAsync(_gameId, maxRelations: 10, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("[Knowledge Graph]");
        result.Should().Contain("Catan (Game) --HasMechanic--> Trading (Mechanic)");
        result.Should().Contain("Catan (Game) --HasComponent--> Hex Tiles (Component)");
    }

    [Fact]
    public async Task GetEntityContextAsync_NoRelations_ReturnsEmptyString()
    {
        // Act
        var result = await _sut.GetEntityContextAsync(_gameId, maxRelations: 10, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntityContextAsync_RespectsMaxRelationsLimit()
    {
        // Arrange — add 5 relations but request only 2
        for (int i = 0; i < 5; i++)
        {
            _db.GameEntityRelations.Add(
                CreateRelation($"Entity{i}", "Type", "Rel", $"Target{i}", "Type", 0.5f + i * 0.1f));
        }
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntityContextAsync(_gameId, maxRelations: 2, CancellationToken.None);

        // Assert
        result.Should().Contain("[Knowledge Graph]");
        // Count lines starting with "- " to verify only 2 relations
        var relationLines = result.Split('\n').Count(l => l.TrimStart().StartsWith("- "));
        relationLines.Should().Be(2);
    }

    [Fact]
    public async Task GetEntityContextAsync_OrdersByConfidenceDescending()
    {
        // Arrange
        _db.GameEntityRelations.AddRange(
            CreateRelation("LowConf", "Type", "Rel", "Target1", "Type", 0.5f),
            CreateRelation("HighConf", "Type", "Rel", "Target2", "Type", 0.95f),
            CreateRelation("MedConf", "Type", "Rel", "Target3", "Type", 0.7f));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntityContextAsync(_gameId, maxRelations: 10, CancellationToken.None);

        // Assert — HighConf should appear before MedConf, which should appear before LowConf
        var highIdx = result.IndexOf("HighConf", StringComparison.Ordinal);
        var medIdx = result.IndexOf("MedConf", StringComparison.Ordinal);
        var lowIdx = result.IndexOf("LowConf", StringComparison.Ordinal);

        highIdx.Should().BeLessThan(medIdx, "highest confidence should come first");
        medIdx.Should().BeLessThan(lowIdx, "medium confidence should come before low");
    }

    [Fact]
    public async Task GetEntityContextAsync_DoesNotReturnRelationsFromOtherGames()
    {
        // Arrange
        var otherGameId = Guid.NewGuid();
        _db.GameEntityRelations.Add(new GameEntityRelationEntity
        {
            Id = Guid.NewGuid(),
            GameId = otherGameId,
            SourceEntity = "OtherGame",
            SourceType = "Game",
            Relation = "HasMechanic",
            TargetEntity = "Bluffing",
            TargetType = "Mechanic",
            Confidence = 0.9f,
            ExtractedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntityContextAsync(_gameId, maxRelations: 10, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private GameEntityRelationEntity CreateRelation(
        string source, string sourceType, string relation,
        string target, string targetType, float confidence)
    {
        return new GameEntityRelationEntity
        {
            Id = Guid.NewGuid(),
            GameId = _gameId,
            SourceEntity = source,
            SourceType = sourceType,
            Relation = relation,
            TargetEntity = target,
            TargetType = targetType,
            Confidence = confidence,
            ExtractedAt = DateTime.UtcNow
        };
    }
}
