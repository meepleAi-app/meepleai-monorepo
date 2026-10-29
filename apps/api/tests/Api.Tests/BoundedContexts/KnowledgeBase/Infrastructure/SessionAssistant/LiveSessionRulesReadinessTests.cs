using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// #4729: whether the game of a live session has a rulebook the assistant can consult. It follows the definition
/// the session-creation gate already uses (<c>GetKbReadinessQuery</c>) so that a panel notice and a 422 never
/// contradict each other, and says nothing (<c>null</c>) where it cannot tell: no game, or a private game.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LiveSessionRulesReadinessTests : IDisposable
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediator> _mediator = new();
    private readonly MeepleAiDbContext _db;
    private readonly LiveSessionRulesReadiness _sut;

    public LiveSessionRulesReadinessTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _sut = new LiveSessionRulesReadiness(_mediator.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static LiveSessionDto Session(Guid? gameId) =>
        new(
            Id: SessionId,
            SessionCode: "ABC123",
            GameId: gameId,
            GameName: "Azul",
            GameSlug: "azul",
            CreatedByUserId: Guid.NewGuid(),
            Status: LiveSessionStatus.InProgress,
            Visibility: PlayRecordVisibility.Private,
            GroupId: null,
            CreatedAt: Now,
            StartedAt: Now,
            PausedAt: null,
            CompletedAt: null,
            UpdatedAt: Now,
            LastSavedAt: null,
            CurrentTurnIndex: 0,
            CurrentTurnPlayerId: null,
            AgentMode: AgentSessionMode.None,
            Notes: null,
            GameState: null,
            Players: [],
            Teams: [],
            RoundScores: [],
            ScoringConfig: new LiveSessionScoringConfigDto(["punti"], new Dictionary<string, string>(StringComparer.Ordinal)));

    private void TheSessionPlays(Guid? gameId) =>
        _mediator.Setup(m => m.Send(It.Is<GetLiveSessionQuery>(q => q.SessionId == SessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Session(gameId));

    private async Task<Guid> CatalogGameAsync()
    {
        var id = Guid.NewGuid();
        _db.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = "Azul",
            YearPublished = 2017,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = Now,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }

    private void KbReadinessIs(Guid gameId, bool isReady, string state) =>
        _mediator.Setup(m => m.Send(It.Is<GetKbReadinessQuery>(q => q.GameId == gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KbReadinessDto(isReady, state, isReady ? 1 : 0, 0, []));

    [Fact]
    public async Task SessionWithoutAGame_SaysNothing()
    {
        TheSessionPlays(gameId: null);

        var result = await _sut.HasIndexedRulesAsync(SessionId, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        _mediator.Verify(m => m.Send(It.IsAny<GetKbReadinessQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrivateGame_SaysNothing_RatherThanClaimingThereIsNoRulebook()
    {
        // The id is in no catalog: the readiness rule is not defined for it, so nothing is claimed.
        TheSessionPlays(Guid.NewGuid());

        var result = await _sut.HasIndexedRulesAsync(SessionId, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        _mediator.Verify(m => m.Send(It.IsAny<GetKbReadinessQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CatalogGameWithAReadyRulebook_IsTrue()
    {
        var gameId = await CatalogGameAsync();
        TheSessionPlays(gameId);
        KbReadinessIs(gameId, isReady: true, "Ready");

        var result = await _sut.HasIndexedRulesAsync(SessionId, TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("None")]
    [InlineData("VectorPending")]
    [InlineData("Embedding")]
    [InlineData("Failed")]
    public async Task CatalogGameWithoutAReadyRulebook_IsFalse_WhateverTheReason(string state)
    {
        var gameId = await CatalogGameAsync();
        TheSessionPlays(gameId);
        KbReadinessIs(gameId, isReady: false, state);

        var result = await _sut.HasIndexedRulesAsync(SessionId, TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AskedAboutTheGameOfThatSession_NotAnotherOne()
    {
        var gameId = await CatalogGameAsync();
        var otherGame = await CatalogGameAsync();
        TheSessionPlays(gameId);
        KbReadinessIs(gameId, isReady: false, "None");
        KbReadinessIs(otherGame, isReady: true, "Ready");

        var result = await _sut.HasIndexedRulesAsync(SessionId, TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }
}
