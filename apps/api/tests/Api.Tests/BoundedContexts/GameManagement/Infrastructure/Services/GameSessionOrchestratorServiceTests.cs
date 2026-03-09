using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Unit tests for GameSessionOrchestratorService.
/// Tests cross-context data collection, fail-open resilience, and degradation level calculation.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameSessionOrchestratorServiceTests
{
    private readonly Mock<ILiveSessionRepository> _liveSessionRepoMock;
    private readonly Mock<IEntityLinkRepository> _entityLinkRepoMock;
    private readonly Mock<IRulebookAnalysisRepository> _rulebookRepoMock;
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepoMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock;
    private readonly Mock<ILogger<GameSessionOrchestratorService>> _loggerMock;
    private readonly GameSessionOrchestratorService _service;

    public GameSessionOrchestratorServiceTests()
    {
        _liveSessionRepoMock = new Mock<ILiveSessionRepository>();
        _entityLinkRepoMock = new Mock<IEntityLinkRepository>();
        _rulebookRepoMock = new Mock<IRulebookAnalysisRepository>();
        _vectorDocRepoMock = new Mock<IVectorDocumentRepository>();
        _sharedGameRepoMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<GameSessionOrchestratorService>>();

        _service = new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            _entityLinkRepoMock.Object,
            _rulebookRepoMock.Object,
            _vectorDocRepoMock.Object,
            _sharedGameRepoMock.Object,
            _loggerMock.Object);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static LiveGameSession CreateTestSession(
        Guid? id = null,
        Guid? gameId = null,
        string gameName = "Test Game")
    {
        return LiveGameSession.Create(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            gameName,
            TimeProvider.System,
            gameId: gameId);
    }

    private static RulebookAnalysis CreateTestAnalysis(
        Guid sharedGameId,
        string gameTitle = "Test Game",
        bool isActive = true)
    {
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId,
            Guid.NewGuid(),
            gameTitle,
            "A test game summary with mechanics and phases.",
            new List<string> { "Worker Placement", "Resource Management" },
            null,
            new List<Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.Resource>(),
            new List<Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.GamePhase>(),
            new List<string>(),
            0.85m,
            Guid.NewGuid());

        if (isActive)
            analysis.SetAsActive();

        return analysis;
    }

    private void SetupSessionRepository(Guid sessionId, LiveGameSession? session)
    {
        _liveSessionRepoMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupEntityLinks(Guid gameId, IReadOnlyList<EntityLink> links)
    {
        _entityLinkRepoMock
            .Setup(x => x.GetForEntityAsync(
                MeepleEntityType.Game,
                gameId,
                null,
                EntityLinkType.ExpansionOf,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(links);
    }

    private readonly Dictionary<Guid, List<VectorDocument>> _vectorDocSetups = new();

    private void SetupVectorDocuments(Guid gameId, List<VectorDocument> docs)
    {
        _vectorDocSetups[gameId] = docs;

        _vectorDocRepoMock
            .Setup(x => x.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);

        // Re-setup batch query to reflect all registered game IDs
        _vectorDocRepoMock
            .Setup(x => x.GetGameIdsWithDocumentsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) =>
            {
                var result = ids
                    .Where(id => _vectorDocSetups.TryGetValue(id, out var d) && d.Count > 0)
                    .ToList();
                return Task.FromResult(result);
            });
    }

    private void SetupRulebookAnalyses(Guid sharedGameId, List<RulebookAnalysis> analyses)
    {
        _rulebookRepoMock
            .Setup(x => x.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
    }

    // ========================================================================
    // BuildContextAsync Tests
    // ========================================================================

    [Fact]
    public async Task BuildContext_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupSessionRepository(sessionId, null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.BuildContextAsync(sessionId));
    }

    [Fact]
    public async Task BuildContext_SessionWithNoGame_ReturnsNoAIDegradation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: null);
        SetupSessionRepository(sessionId, session);

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.Null(result.PrimaryGameId);
        Assert.Empty(result.ExpansionGameIds);
        Assert.Empty(result.AllGameIds);
        Assert.Empty(result.KbCardIds);
        Assert.Equal(SessionDegradationLevel.NoAI, result.DegradationLevel);
    }

    [Fact]
    public async Task BuildContext_SessionWithGameAndFullPdf_ReturnsFullDegradation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        // No expansions
        SetupEntityLinks(gameId, new List<EntityLink>());

        // Vector documents exist for primary game
        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);
        SetupVectorDocuments(gameId, new List<VectorDocument> { vectorDoc });

        // Rulebook analysis exists
        var analysis = CreateTestAnalysis(gameId, "Test Game");
        SetupRulebookAnalyses(gameId, new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(gameId, result.PrimaryGameId);
        Assert.Empty(result.ExpansionGameIds);
        Assert.Single(result.AllGameIds);
        Assert.Single(result.KbCardIds);
        Assert.NotNull(result.PrimaryRules);
        Assert.Equal("Test Game", result.PrimaryRules.GameTitle);
        Assert.Empty(result.GamesWithoutPdf);
        Assert.Equal(SessionDegradationLevel.Full, result.DegradationLevel);
    }

    [Fact]
    public async Task BuildContext_PrimaryWithoutPdf_NoExpansions_ReturnsNoAIDegradation()
    {
        // Arrange: primary game has no vector docs and no expansions
        // This means no KB cards at all → NoAI
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        SetupEntityLinks(gameId, new List<EntityLink>());
        SetupVectorDocuments(gameId, new List<VectorDocument>());

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Equal(SessionDegradationLevel.NoAI, result.DegradationLevel);
        Assert.Contains(gameId, result.GamesWithoutPdf);
    }

    [Fact]
    public async Task BuildContext_PrimaryWithoutPdf_ExpansionWithPdf_ReturnsBasicOnlyDegradation()
    {
        // Arrange: primary game has no PDF but an expansion does → BasicOnly
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: primaryGameId);
        SetupSessionRepository(sessionId, session);

        // Set up expansion link
        var expansionLink = EntityLink.Create(
            MeepleEntityType.Game,
            expansionId,
            MeepleEntityType.Game,
            primaryGameId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.Shared,
            Guid.NewGuid());
        SetupEntityLinks(primaryGameId, new List<EntityLink> { expansionLink });

        // Primary game has NO vector doc
        SetupVectorDocuments(primaryGameId, new List<VectorDocument>());
        // Expansion HAS vector doc
        var expansionVectorDoc = new VectorDocument(Guid.NewGuid(), expansionId, Guid.NewGuid(), "en", 5);
        SetupVectorDocuments(expansionId, new List<VectorDocument> { expansionVectorDoc });

        // No rulebook analyses
        SetupRulebookAnalyses(primaryGameId, new List<RulebookAnalysis>());
        SetupRulebookAnalyses(expansionId, new List<RulebookAnalysis>());

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Equal(SessionDegradationLevel.BasicOnly, result.DegradationLevel);
        Assert.Contains(primaryGameId, result.GamesWithoutPdf);
        Assert.DoesNotContain(expansionId, result.GamesWithoutPdf);
    }

    [Fact]
    public async Task BuildContext_WithExpansionsMissingPdf_ReturnsPartialDegradation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: primaryGameId);
        SetupSessionRepository(sessionId, session);

        // Set up expansion link
        var expansionLink = EntityLink.Create(
            MeepleEntityType.Game,
            expansionId,
            MeepleEntityType.Game,
            primaryGameId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.Shared,
            Guid.NewGuid());

        SetupEntityLinks(primaryGameId, new List<EntityLink> { expansionLink });

        // Primary game has vector doc
        var primaryVectorDoc = new VectorDocument(Guid.NewGuid(), primaryGameId, Guid.NewGuid(), "en", 10);
        SetupVectorDocuments(primaryGameId, new List<VectorDocument> { primaryVectorDoc });

        // Expansion does NOT have vector doc
        SetupVectorDocuments(expansionId, new List<VectorDocument>());

        // No rulebook analyses needed for this test
        SetupRulebookAnalyses(primaryGameId, new List<RulebookAnalysis>());
        SetupRulebookAnalyses(expansionId, new List<RulebookAnalysis>());

        // SharedGame lookup for missing names
        _sharedGameRepoMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame>());

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Equal(SessionDegradationLevel.Partial, result.DegradationLevel);
        Assert.Single(result.ExpansionGameIds);
        Assert.Equal(expansionId, result.ExpansionGameIds[0]);
        Assert.Contains(expansionId, result.GamesWithoutPdf);
        Assert.DoesNotContain(primaryGameId, result.GamesWithoutPdf);
    }

    // ========================================================================
    // Fail-Open Resilience Tests
    // ========================================================================

    [Fact]
    public async Task BuildContext_EntityLinkRepoThrows_ContinuesWithEmptyExpansions()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        _entityLinkRepoMock
            .Setup(x => x.GetForEntityAsync(
                It.IsAny<MeepleEntityType>(),
                It.IsAny<Guid>(),
                It.IsAny<EntityLinkScope?>(),
                It.IsAny<EntityLinkType?>(),
                It.IsAny<MeepleEntityType?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        SetupVectorDocuments(gameId, new List<VectorDocument>());

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert — should succeed with empty expansions
        Assert.Equal(sessionId, result.SessionId);
        Assert.Empty(result.ExpansionGameIds);
    }

    [Fact]
    public async Task BuildContext_VectorDocRepoThrows_ContinuesWithEmptyKbCards()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        SetupEntityLinks(gameId, new List<EntityLink>());

        _vectorDocRepoMock
            .Setup(x => x.GetGameIdsWithDocumentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Qdrant unavailable"));

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert — should succeed with no KB cards and NoAI degradation
        Assert.Equal(sessionId, result.SessionId);
        Assert.Empty(result.KbCardIds);
        Assert.Equal(SessionDegradationLevel.NoAI, result.DegradationLevel);
    }

    [Fact]
    public async Task BuildContext_RulebookRepoThrows_ContinuesWithNullRules()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        SetupEntityLinks(gameId, new List<EntityLink>());

        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);
        SetupVectorDocuments(gameId, new List<VectorDocument> { vectorDoc });

        _rulebookRepoMock
            .Setup(x => x.GetBySharedGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Rulebook service unavailable"));

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert — should succeed with null primary rules
        Assert.Equal(sessionId, result.SessionId);
        Assert.Null(result.PrimaryRules);
        Assert.Equal(SessionDegradationLevel.Full, result.DegradationLevel);
    }

    // ========================================================================
    // CalculateDegradationLevel Static Tests
    // ========================================================================

    [Fact]
    public void CalculateDegradationLevel_NoKbCards_ReturnsNoAI()
    {
        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: Guid.NewGuid(),
            kbCardGameIds: new List<Guid>(),
            expansionGameIds: new List<Guid>(),
            gamesWithoutPdf: new List<Guid> { Guid.NewGuid() });

        Assert.Equal(SessionDegradationLevel.NoAI, result);
    }

    [Fact]
    public void CalculateDegradationLevel_PrimaryMissing_ReturnsBasicOnly()
    {
        var primaryId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primaryId,
            kbCardGameIds: new List<Guid> { expansionId }, // Expansion has KB, primary does not
            expansionGameIds: new List<Guid> { expansionId },
            gamesWithoutPdf: new List<Guid> { primaryId });

        Assert.Equal(SessionDegradationLevel.BasicOnly, result);
    }

    [Fact]
    public void CalculateDegradationLevel_ExpansionMissing_ReturnsPartial()
    {
        var primaryId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primaryId,
            kbCardGameIds: new List<Guid> { primaryId }, // Primary has KB, expansion does not
            expansionGameIds: new List<Guid> { expansionId },
            gamesWithoutPdf: new List<Guid> { expansionId });

        Assert.Equal(SessionDegradationLevel.Partial, result);
    }

    [Fact]
    public void CalculateDegradationLevel_AllPresent_ReturnsFull()
    {
        var primaryId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primaryId,
            kbCardGameIds: new List<Guid> { primaryId, expansionId },
            expansionGameIds: new List<Guid> { expansionId },
            gamesWithoutPdf: new List<Guid>());

        Assert.Equal(SessionDegradationLevel.Full, result);
    }

    [Fact]
    public void CalculateDegradationLevel_NoPrimaryGameId_WithKbCards_ReturnsFull()
    {
        // Edge case: session has no game linked but somehow has KB cards
        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: null,
            kbCardGameIds: new List<Guid> { Guid.NewGuid() },
            expansionGameIds: new List<Guid>(),
            gamesWithoutPdf: new List<Guid>());

        Assert.Equal(SessionDegradationLevel.Full, result);
    }

    // ========================================================================
    // RefreshContextAsync Tests
    // ========================================================================

    [Fact]
    public async Task RefreshContext_DelegatesToBuildContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: null);
        SetupSessionRepository(sessionId, session);

        // Act
        var result = await _service.RefreshContextAsync(sessionId);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        _liveSessionRepoMock.Verify(
            x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================================================
    // RulebookAnalysisSummary Mapping Tests
    // ========================================================================

    [Fact]
    public async Task BuildContext_WithActiveAnalysis_MapsSummaryCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        SetupEntityLinks(gameId, new List<EntityLink>());

        var vectorDoc = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10);
        SetupVectorDocuments(gameId, new List<VectorDocument> { vectorDoc });

        var analysis = CreateTestAnalysis(gameId, "Catan");
        SetupRulebookAnalyses(gameId, new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.NotNull(result.PrimaryRules);
        Assert.Equal(gameId, result.PrimaryRules.GameId);
        Assert.Equal("Catan", result.PrimaryRules.GameTitle);
        Assert.Contains("Worker Placement", result.PrimaryRules.KeyMechanics);
        Assert.Contains("Resource Management", result.PrimaryRules.KeyMechanics);
    }

    [Fact]
    public async Task BuildContext_WithNoAnalysis_PrimaryRulesIsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateTestSession(id: sessionId, gameId: gameId);
        SetupSessionRepository(sessionId, session);

        SetupEntityLinks(gameId, new List<EntityLink>());
        SetupVectorDocuments(gameId, new List<VectorDocument>());
        SetupRulebookAnalyses(gameId, new List<RulebookAnalysis>());

        // Act
        var result = await _service.BuildContextAsync(sessionId);

        // Assert
        Assert.Null(result.PrimaryRules);
    }

    // ========================================================================
    // Constructor Null Guard Tests
    // ========================================================================

    [Fact]
    public void Constructor_NullLiveSessionRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            null!,
            _entityLinkRepoMock.Object,
            _rulebookRepoMock.Object,
            _vectorDocRepoMock.Object,
            _sharedGameRepoMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullEntityLinkRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            null!,
            _rulebookRepoMock.Object,
            _vectorDocRepoMock.Object,
            _sharedGameRepoMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullRulebookRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            _entityLinkRepoMock.Object,
            null!,
            _vectorDocRepoMock.Object,
            _sharedGameRepoMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullVectorDocRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            _entityLinkRepoMock.Object,
            _rulebookRepoMock.Object,
            null!,
            _sharedGameRepoMock.Object,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullSharedGameRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            _entityLinkRepoMock.Object,
            _rulebookRepoMock.Object,
            _vectorDocRepoMock.Object,
            null!,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameSessionOrchestratorService(
            _liveSessionRepoMock.Object,
            _entityLinkRepoMock.Object,
            _rulebookRepoMock.Object,
            _vectorDocRepoMock.Object,
            _sharedGameRepoMock.Object,
            null!));
    }
}
