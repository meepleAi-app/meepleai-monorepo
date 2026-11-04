using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// #4282 — il controllo che state, definition, config ed end applicano prima di toccare una sessione
/// agente. La chat ripete la stessa regola nel proprio stream; il percorso HTTP di tutte le rotte è in
/// <c>AgentSessionOwnershipIntegrationTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentSessionAccessTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid GameSession = Guid.NewGuid();

    private static AgentSession OwnersSession() => new(
        id: Guid.NewGuid(),
        agentDefinitionId: Guid.NewGuid(),
        gameSessionId: GameSession,
        userId: Owner,
        gameId: Guid.NewGuid(),
        initialState: GameState.Initial(Owner));

    [Fact]
    public void TheOwner_OnTheGameSessionOfTheSession_GetsItBack()
    {
        var session = OwnersSession();

        AgentSessionAccess.EnsureOwnedBy(session, session.Id, GameSession, Owner).Should().BeSameAs(session);
    }

    [Fact]
    public void AMissingSession_IsNotFound()
    {
        var act = () => AgentSessionAccess.EnsureOwnedBy(null, Guid.NewGuid(), GameSession, Owner);

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void AnotherUser_IsForbidden()
    {
        var session = OwnersSession();

        var act = () => AgentSessionAccess.EnsureOwnedBy(session, session.Id, GameSession, Guid.NewGuid());

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void AnotherUser_IsForbidden_EvenOnTheWrongGameSession()
    {
        // The owner check comes first: a non-owner is refused the same way on every route.
        var session = OwnersSession();

        var act = () => AgentSessionAccess.EnsureOwnedBy(session, session.Id, Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void TheOwner_OnAnotherGameSession_IsNotFound()
    {
        var session = OwnersSession();

        var act = () => AgentSessionAccess.EnsureOwnedBy(session, session.Id, Guid.NewGuid(), Owner);

        act.Should().Throw<NotFoundException>();
    }
}
