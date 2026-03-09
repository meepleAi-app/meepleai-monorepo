using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameNight;

/// <summary>
/// Integration tests for GameSessionContext — verifies cross-context orchestration
/// with multiple expansions, mixed PDF coverage, and no-game-id scenarios.
/// Issue #5589: Game Night GameSessionContext integration tests.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameNight")]
[Trait("Issue", "5589")]
public sealed class GameSessionContextIntegrationTests
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

    // =========================================================================
    // 1. Two expansions, one missing PDF — correct degradation and warnings
    // =========================================================================

    [Fact]
    public async Task BuildContext_TwoExpansions_OneMissingPdf_ShowsCorrectWarnings()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();
        var expansion1 = Guid.NewGuid();
        var expansion2 = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(sessionId, primaryGameId));

        _entityLinkRepoMock.Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, primaryGameId,
                null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityLink>
            {
                EntityLink.Create(MeepleEntityType.Game, expansion1,
                    MeepleEntityType.Game, primaryGameId,
                    EntityLinkType.ExpansionOf, EntityLinkScope.Shared, Guid.NewGuid()),
                EntityLink.Create(MeepleEntityType.Game, expansion2,
                    MeepleEntityType.Game, primaryGameId,
                    EntityLinkType.ExpansionOf, EntityLinkScope.Shared, Guid.NewGuid()),
            });

        // Primary + expansion1 have KB; expansion2 does not
        var mockDoc = new VectorDocument(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "en", 10);
        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(primaryGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { mockDoc });
        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(expansion1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { mockDoc });
        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(expansion2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        _rulebookRepoMock.Setup(r => r.GetBySharedGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        _sharedGameRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame>());

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.AllGameIds.Should().HaveCount(3);
        context.DegradationLevel.Should().Be(SessionDegradationLevel.Partial);
        context.GamesWithoutPdf.Should().ContainSingle().Which.Should().Be(expansion2);
        context.KbCardIds.Should().HaveCount(2);
        context.KbCardIds.Should().Contain(primaryGameId);
        context.KbCardIds.Should().Contain(expansion1);
    }

    // =========================================================================
    // 2. Session without a game ID (no catalog link)
    // =========================================================================

    [Fact]
    public async Task BuildContext_NoGameId_ReturnsEmptyCollections()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(sessionId, gameId: null, gameName: "Custom Game"));

        var svc = CreateService();

        var context = await svc.BuildContextAsync(sessionId);

        context.PrimaryGameId.Should().BeNull();
        context.AllGameIds.Should().BeEmpty();
        context.ExpansionGameIds.Should().BeEmpty();
        context.KbCardIds.Should().BeEmpty();
        context.DegradationLevel.Should().Be(SessionDegradationLevel.NoAI);
    }

    // =========================================================================
    // 3. CalculateDegradationLevel static method — exhaustive edge cases
    // =========================================================================

    [Fact]
    public void CalculateDegradation_NoPrimaryNoKb_ReturnsNoAI()
    {
        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: null,
            kbCardGameIds: new List<Guid>(),
            expansionGameIds: new List<Guid>(),
            gamesWithoutPdf: new List<Guid>());

        result.Should().Be(SessionDegradationLevel.NoAI);
    }

    [Fact]
    public void CalculateDegradation_PrimaryWithKb_NoExpansions_ReturnsFull()
    {
        var primary = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primary,
            kbCardGameIds: new List<Guid> { primary },
            expansionGameIds: new List<Guid>(),
            gamesWithoutPdf: new List<Guid>());

        result.Should().Be(SessionDegradationLevel.Full);
    }

    [Fact]
    public void CalculateDegradation_PrimaryWithKb_AllExpansionsHaveKb_ReturnsFull()
    {
        var primary = Guid.NewGuid();
        var exp1 = Guid.NewGuid();
        var exp2 = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primary,
            kbCardGameIds: new List<Guid> { primary, exp1, exp2 },
            expansionGameIds: new List<Guid> { exp1, exp2 },
            gamesWithoutPdf: new List<Guid>());

        result.Should().Be(SessionDegradationLevel.Full);
    }

    [Fact]
    public void CalculateDegradation_PrimaryMissingKb_ReturnsBasicOnly()
    {
        var primary = Guid.NewGuid();
        var exp = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primary,
            kbCardGameIds: new List<Guid> { exp },
            expansionGameIds: new List<Guid> { exp },
            gamesWithoutPdf: new List<Guid> { primary });

        result.Should().Be(SessionDegradationLevel.BasicOnly);
    }

    [Fact]
    public void CalculateDegradation_ExpansionMissingKb_ReturnsPartial()
    {
        var primary = Guid.NewGuid();
        var exp = Guid.NewGuid();

        var result = GameSessionOrchestratorService.CalculateDegradationLevel(
            primaryGameId: primary,
            kbCardGameIds: new List<Guid> { primary },
            expansionGameIds: new List<Guid> { exp },
            gamesWithoutPdf: new List<Guid> { exp });

        result.Should().Be(SessionDegradationLevel.Partial);
    }

    // =========================================================================
    // 4. RefreshContext returns same result as BuildContext (v1 — no caching)
    // =========================================================================

    [Fact]
    public async Task RefreshContext_CallsBuildContextInternally()
    {
        var sessionId = Guid.NewGuid();
        var primaryGameId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(sessionId, primaryGameId));

        _entityLinkRepoMock.Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, primaryGameId,
                null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityLink>());

        _vectorDocRepoMock.Setup(r => r.GetByGameIdAsync(primaryGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        var svc = CreateService();

        var buildResult = await svc.BuildContextAsync(sessionId);
        var refreshResult = await svc.RefreshContextAsync(sessionId);

        refreshResult.SessionId.Should().Be(buildResult.SessionId);
        refreshResult.PrimaryGameId.Should().Be(buildResult.PrimaryGameId);
        refreshResult.DegradationLevel.Should().Be(buildResult.DegradationLevel);
    }
}
