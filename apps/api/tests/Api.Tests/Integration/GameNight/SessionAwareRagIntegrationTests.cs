using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameNight;

/// <summary>
/// Integration tests verifying that the GameSessionOrchestrator builds correct context
/// and that session-aware RAG uses AllGameIds to scope vector searches.
/// Tests cross-context data aggregation: LiveSession + EntityLinks + VectorDocuments + RulebookAnalysis.
/// Issue #5589: Game Night integration tests.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameNight")]
[Trait("Issue", "5589")]
public sealed class SessionAwareRagIntegrationTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock = new();
    private readonly Mock<IEntityLinkRepository> _entityLinkRepoMock = new();
    private readonly Mock<IRulebookAnalysisRepository> _rulebookRepoMock = new();
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepoMock = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock = new();
    private readonly Mock<ILogger<GameSessionOrchestratorService>> _loggerMock = new();

    private GameSessionOrchestratorService CreateService() => new(
        _sessionRepoMock.Object,
        _entityLinkRepoMock.Object,
        _rulebookRepoMock.Object,
        _vectorDocRepoMock.Object,
        _sharedGameRepoMock.Object,
        _loggerMock.Object);

    // ----- helpers -----------------------------------------------------------

    private static LiveGameSession CreateSession(Guid sessionId, Guid? gameId, string gameName = "Catan")
    {
        var session = LiveGameSession.Create(
            sessionId,
            createdByUserId: Guid.NewGuid(),
            gameName: gameName,
            gameId: gameId,
            agentMode: AgentSessionMode.Assistant);

        session.AddPlayer(null, "Host", PlayerColor.Red);
        session.Start();

        return session;
    }

    private static EntityLink CreateExpansionLink(Guid expansionId, Guid primaryId) =>
        EntityLink.Create(
            sourceEntityType: MeepleEntityType.Game,
            sourceEntityId: expansionId,
            targetEntityType: MeepleEntityType.Game,
            targetEntityId: primaryId,
            linkType: EntityLinkType.ExpansionOf,
            scope: EntityLinkScope.Shared,
            ownerUserId: Guid.NewGuid());

    private void SetupSessionMock(Guid sessionId, LiveGameSession session)
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupNoExpansions(Guid primaryGameId)
    {
        _entityLinkRepoMock.Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, primaryGameId,
                null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityLink>());
    }

    private void SetupExpansions(Guid primaryGameId, params EntityLink[] links)
    {
        _entityLinkRepoMock.Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, primaryGameId,
                null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links.ToList());
    }

    private void SetupVectorDocs(Guid gameId, bool hasDocs)
    {
        var docs = hasDocs
            ? new List<VectorDocument> { new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 10) }
            : new List<VectorDocument>();

        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);
    }

    // =========================================================================
    // 1. Session context with primary game only
    // =========================================================================

    [Fact]
    public async Task BuildContext_PrimaryGameOnly_ReturnsCorrectAllGameIds()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupNoExpansions(primaryGameId);
        SetupVectorDocs(primaryGameId, false);

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.PrimaryGameId.Should().Be(primaryGameId);
        context.AllGameIds.Should().ContainSingle().Which.Should().Be(primaryGameId);
        context.ExpansionGameIds.Should().BeEmpty();
    }

    // =========================================================================
    // 2. Session context with expansions
    // =========================================================================

    [Fact]
    public async Task BuildContext_WithExpansions_AllGameIdsContainsPrimaryAndExpansions()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansionId1 = Guid.NewGuid();
        var expansionId2 = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupExpansions(primaryGameId,
            CreateExpansionLink(expansionId1, primaryGameId),
            CreateExpansionLink(expansionId2, primaryGameId));
        SetupVectorDocs(primaryGameId, false);
        SetupVectorDocs(expansionId1, false);
        SetupVectorDocs(expansionId2, false);

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.AllGameIds.Should().HaveCount(3);
        context.AllGameIds.Should().Contain(primaryGameId);
        context.AllGameIds.Should().Contain(expansionId1);
        context.AllGameIds.Should().Contain(expansionId2);
        context.ExpansionGameIds.Should().HaveCount(2);
    }

    // =========================================================================
    // 3. Degradation levels
    // =========================================================================

    [Fact]
    public async Task BuildContext_AllGamesHaveKbCards_ReturnsFull()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupNoExpansions(primaryGameId);
        SetupVectorDocs(primaryGameId, true);

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.DegradationLevel.Should().Be(SessionDegradationLevel.Full);
        context.GamesWithoutPdf.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildContext_NoKbCards_ReturnsNoAI()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupNoExpansions(primaryGameId);
        SetupVectorDocs(primaryGameId, false);

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.DegradationLevel.Should().Be(SessionDegradationLevel.NoAI);
    }

    [Fact]
    public async Task BuildContext_ExpansionMissingPdf_ReturnsPartial()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupExpansions(primaryGameId, CreateExpansionLink(expansionId, primaryGameId));
        SetupVectorDocs(primaryGameId, true);
        SetupVectorDocs(expansionId, false);

        _sharedGameRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame>());

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.DegradationLevel.Should().Be(SessionDegradationLevel.Partial);
        context.GamesWithoutPdf.Should().ContainSingle().Which.Should().Be(expansionId);
    }

    [Fact]
    public async Task BuildContext_PrimaryMissingPdf_ReturnsBasicOnly()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansionId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupExpansions(primaryGameId, CreateExpansionLink(expansionId, primaryGameId));
        SetupVectorDocs(primaryGameId, false);
        SetupVectorDocs(expansionId, true);

        _sharedGameRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame>());

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.DegradationLevel.Should().Be(SessionDegradationLevel.BasicOnly);
    }

    // =========================================================================
    // 4. Fail-open resilience
    // =========================================================================

    [Fact]
    public async Task BuildContext_EntityLinkRepoThrows_ContinuesWithEmptyExpansions()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));

        _entityLinkRepoMock.Setup(r => r.GetForEntityAsync(
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<EntityLinkScope?>(), It.IsAny<EntityLinkType?>(),
                It.IsAny<MeepleEntityType?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        SetupVectorDocs(primaryGameId, false);

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.ExpansionGameIds.Should().BeEmpty();
        context.AllGameIds.Should().ContainSingle().Which.Should().Be(primaryGameId);
    }

    [Fact]
    public async Task BuildContext_VectorDocRepoThrows_ContinuesWithEmptyKbCards()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        SetupSessionMock(sessionId, CreateSession(sessionId, primaryGameId));
        SetupNoExpansions(primaryGameId);

        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Qdrant down"));

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.KbCardIds.Should().BeEmpty();
        context.DegradationLevel.Should().Be(SessionDegradationLevel.NoAI);
    }

    // =========================================================================
    // 5. SessionContextPromptBuilder integration
    // =========================================================================

    [Fact]
    public void SessionPreamble_WithFullContext_ContainsAllSections()
    {
        var context = new GameSessionContextDto(
            SessionId: Guid.NewGuid(),
            PrimaryGameId: Guid.NewGuid(),
            ExpansionGameIds: new List<Guid> { Guid.NewGuid() },
            AllGameIds: new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            KbCardIds: new List<Guid> { Guid.NewGuid() },
            CurrentPhase: "Setup",
            PrimaryRules: new RulebookAnalysisSummaryDto(
                GameId: Guid.NewGuid(), GameTitle: "Catan",
                Summary: "A resource management game",
                KeyMechanics: new List<string> { "Trading", "Building" },
                CurrentPhaseName: "Setup",
                PhaseNames: new List<string> { "Setup", "Main" }),
            ExpansionRules: new List<RulebookAnalysisSummaryDto>
            {
                new(GameId: Guid.NewGuid(), GameTitle: "Catan: Seafarers",
                    Summary: "Ships expansion",
                    KeyMechanics: new List<string> { "Ships" },
                    CurrentPhaseName: null,
                    PhaseNames: new List<string> { "Setup" })
            },
            MissingAnalysisGameNames: new List<string> { "Catan: Cities & Knights" },
            GamesWithoutPdf: new List<Guid> { Guid.NewGuid() },
            DegradationLevel: SessionDegradationLevel.Partial);

        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        preamble.Should().Contain("Catan");
        preamble.Should().Contain("Seafarers");
        preamble.Should().Contain("Setup");
        preamble.Should().Contain("Trading");
        preamble.Should().Contain("Cities & Knights");
        preamble.Should().Contain("ATTENZIONE");
        preamble.Should().Contain("CONTESTO SESSIONE");
    }

    [Fact]
    public void SessionPreamble_MinimalContext_DoesNotThrow()
    {
        var context = new GameSessionContextDto(
            SessionId: Guid.NewGuid(),
            PrimaryGameId: null,
            ExpansionGameIds: new List<Guid>(),
            AllGameIds: new List<Guid>(),
            KbCardIds: new List<Guid>(),
            CurrentPhase: null,
            PrimaryRules: null,
            ExpansionRules: new List<RulebookAnalysisSummaryDto>(),
            MissingAnalysisGameNames: new List<string>(),
            GamesWithoutPdf: new List<Guid>(),
            DegradationLevel: SessionDegradationLevel.NoAI);

        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        preamble.Should().Contain("CONTESTO SESSIONE");
        preamble.Should().NotContain("ATTENZIONE");
    }

    [Fact]
    public void GetNoAiDegradationMessage_ReturnsNonEmptyString()
    {
        var message = SessionContextPromptBuilder.GetNoAiDegradationMessage();

        message.Should().NotBeNullOrWhiteSpace();
        message.Should().Contain("Knowledge Base");
    }
}
