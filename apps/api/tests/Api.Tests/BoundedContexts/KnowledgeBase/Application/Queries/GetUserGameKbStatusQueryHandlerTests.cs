using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NSubstitute;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetUserGameKbStatusQueryHandler.
/// KB-03: User-facing per-game KB status query.
/// Issue #1529: Extended coverage for chunk/embedding counts, last-reindex/RAPTOR
/// timestamps, and lifetime/last-7-days cost aggregates with explicit null-default
/// scenarios for each new field.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetUserGameKbStatusQueryHandlerTests
{
    private readonly IVectorDocumentRepository _vectorRepoMock;
    private readonly IConfigurationRepository _configRepoMock;
    private readonly MeepleAiDbContext _db;
    private readonly GetUserGameKbStatusQueryHandler _sut;

    public GetUserGameKbStatusQueryHandlerTests()
    {
        _vectorRepoMock = Substitute.For<IVectorDocumentRepository>();
        _configRepoMock = Substitute.For<IConfigurationRepository>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new GetUserGameKbStatusQueryHandler(
            _vectorRepoMock,
            _configRepoMock,
            _db);
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsNotIndexed()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument>());

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
        result.IsIndexed.Should().BeFalse();
        result.DocumentCount.Should().Be(0);
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
        result.SuggestedQuestions.Should().BeEmpty();

        // Issue #1529 null-default policy
        result.ChunksCount.Should().Be(0);
        result.EmbeddingsCount.Should().Be(0);
        result.LastReindexAt.Should().BeNull();
        result.RaptorLastRebuildAt.Should().BeNull();
        result.LifetimeCostUsd.Should().Be(0.00m);
        result.CostHistoryLast7Days.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HasDocuments_NoCoverageConfig_ReturnsIndexedWithDefaults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        _configRepoMock
            .GetByKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.DocumentCount.Should().Be(1);
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
        result.SuggestedQuestions.Should().BeEmpty();

        // #1529: single 10-chunk doc → ChunksCount=10, EmbeddingsCount=10 (1:1 mapping)
        result.ChunksCount.Should().Be(10);
        result.EmbeddingsCount.Should().Be(10);
        result.LastReindexAt.Should().NotBeNull("VectorDocument ctor stamps IndexedAt = DateTime.UtcNow");
    }

    [Fact]
    public async Task Handle_HasDocuments_WithCoverageConfig_ReturnsCoverageData()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "it", 25);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var coverageConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "{\"score\":75,\"level\":\"Complete\"}",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(coverageConfig);

        _configRepoMock
            .GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.DocumentCount.Should().Be(1);
        result.CoverageScore.Should().Be(75);
        result.CoverageLevel.Should().Be("Complete");
        result.SuggestedQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HasDocuments_WithSuggestedQuestions_ReturnsQuestions()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 15);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        var questionsKey = $"KB:SuggestedQuestions:{gameId}";
        var questionsConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(questionsKey),
            "[\"How do you win?\",\"What happens on your turn?\"]",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                questionsKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(questionsConfig);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.IsIndexed.Should().BeTrue();
        result.SuggestedQuestions.Should().HaveCount(2);
        result.SuggestedQuestions.Should().Contain("How do you win?");
        result.SuggestedQuestions.Should().Contain("What happens on your turn?");
    }

    [Fact]
    public async Task Handle_MalformedCoverageJson_UsesDefaults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 5);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

        var coverageKey = $"KB:Coverage:{gameId}";
        var badConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey(coverageKey),
            "not-valid-json",
            "json",
            Guid.NewGuid(),
            category: "KnowledgeBase");

        _configRepoMock
            .GetByKeyAsync(
                coverageKey,
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(badConfig);

        _configRepoMock
            .GetByKeyAsync(
                $"KB:SuggestedQuestions:{gameId}",
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert — malformed JSON should not throw; defaults used
        result.IsIndexed.Should().BeTrue();
        result.CoverageScore.Should().Be(0);
        result.CoverageLevel.Should().Be("None");
    }

    // ---- Issue #1529: aggregate-field unit coverage ----

    [Fact]
    public async Task Handle_MultipleVectorDocs_SumsChunksAcrossAll()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var doc1 = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 30);
        var doc2 = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "it", 45);
        var doc3 = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "de", 25);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { doc1, doc2, doc3 });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.DocumentCount.Should().Be(3);
        result.ChunksCount.Should().Be(100, "sum of 30 + 45 + 25");
        result.EmbeddingsCount.Should().Be(100, "1:1 mapping with chunks");
    }

    [Fact]
    public async Task Handle_MultipleVectorDocs_PicksMaxIndexedAtForLastReindex()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // Use Rehydrate to control IndexedAt explicitly (the public ctor stamps DateTime.UtcNow).
        var older = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfId,
            language: "en",
            totalChunks: 10,
            indexedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            sharedGameId: null);
        var newer = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            sharedGameId: null);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { older, newer });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.LastReindexAt.Should().Be(new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_NoRaptorSummaries_ReturnsNullRaptorLastRebuild()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var doc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { doc });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // No raptor_summaries seeded
        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.RaptorLastRebuildAt.Should().BeNull("RAPTOR has never been rebuilt for this game");
    }

    [Fact]
    public async Task Handle_HasRaptorSummaries_ReturnsMaxCreatedAt()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var doc = new VectorDocument(Guid.NewGuid(), gameId, pdfId, "en", 10);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { doc });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        var olderTs = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newerTs = new DateTime(2026, 6, 20, 14, 0, 0, DateTimeKind.Utc);

        _db.RaptorSummaries.AddRange(
            new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PdfDocumentId = pdfId,
                TreeLevel = 0,
                ClusterIndex = 0,
                SummaryText = "older leaf",
                SourceChunkCount = 1,
                CreatedAt = olderTs,
            },
            new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PdfDocumentId = pdfId,
                TreeLevel = 1,
                ClusterIndex = 0,
                SummaryText = "newer section",
                SourceChunkCount = 3,
                CreatedAt = newerTs,
            });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), TestContext.Current.CancellationToken);

        // Assert
        result.RaptorLastRebuildAt.Should().Be(newerTs);
    }

    [Fact]
    public async Task Handle_RaptorSummariesForDifferentGame_AreIgnored()
    {
        // Regression guard: RAPTOR query MUST be game-scoped, not cross-game.
        // Arrange
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var doc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { doc });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        _db.RaptorSummaries.Add(new RaptorSummaryEntity
        {
            Id = Guid.NewGuid(),
            GameId = otherGameId, // different game
            PdfDocumentId = Guid.NewGuid(),
            TreeLevel = 0,
            ClusterIndex = 0,
            SummaryText = "other game's leaf",
            SourceChunkCount = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), TestContext.Current.CancellationToken);

        // Assert
        result.RaptorLastRebuildAt.Should().BeNull("RAPTOR summaries belong to a different game");
    }

    [Fact]
    public async Task Handle_CostFields_AreAlwaysZeroAndEmpty_NullDefaultPolicy()
    {
        // Issue #1529: per-game cost attribution does not exist yet.
        // LifetimeCostUsd → constant 0.00m, CostHistoryLast7Days → empty [].
        // Arrange
        var gameId = Guid.NewGuid();
        var doc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 99);

        _vectorRepoMock
            .GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { doc });
        _configRepoMock
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((SystemConfig?)null);

        // Act
        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        // Assert
        result.LifetimeCostUsd.Should().Be(0.00m);
        result.CostHistoryLast7Days.Should().BeEmpty(
            "no per-game cost attribution exists yet — [] communicates 'no data ever', distinct from a 7-element zero array which would mean 'KB used but free in last week'");
    }
}
