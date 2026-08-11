using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="LiveGameSession.SetCorrelatedGameSessionId"/>.
/// TDD: Tests written first (RED → GREEN).
/// Issue #2587 Slice 1 Task 1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSession_SetCorrelatedGameSessionIdTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private static LiveGameSession CreateSession(Guid? correlatedGameSessionId = null)
        => LiveGameSession.Create(
            Guid.NewGuid(),
            UserId,
            "Test Game",
            TimeProvider.System,
            gameId: GameId,
            correlatedGameSessionId: correlatedGameSessionId);

    // ── Set when null ──────────────────────────────────────────────────────────

    [Fact]
    public void SetCorrelatedGameSessionId_WhenNull_SetsValue()
    {
        var session = CreateSession(correlatedGameSessionId: null);
        var correlatedId = Guid.NewGuid();

        session.SetCorrelatedGameSessionId(correlatedId);

        session.CorrelatedGameSessionId.Should().Be(correlatedId);
    }

    // ── No-op when same value ─────────────────────────────────────────────────

    [Fact]
    public void SetCorrelatedGameSessionId_WhenSameValue_IsNoOp()
    {
        var correlatedId = Guid.NewGuid();
        var session = CreateSession(correlatedGameSessionId: correlatedId);

        // Must not throw and property must remain unchanged
        var act = () => session.SetCorrelatedGameSessionId(correlatedId);
        act.Should().NotThrow();
        session.CorrelatedGameSessionId.Should().Be(correlatedId);
    }

    // ── Throw when different value ────────────────────────────────────────────

    [Fact]
    public void SetCorrelatedGameSessionId_WhenAlreadySetToDifferentValue_Throws()
    {
        var existing = Guid.NewGuid();
        var session = CreateSession(correlatedGameSessionId: existing);
        var different = Guid.NewGuid();

        var act = () => session.SetCorrelatedGameSessionId(different);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CorrelatedGameSessionId*");
    }

    // ── Guard: empty Guid ────────────────────────────────────────────────────

    [Fact]
    public void SetCorrelatedGameSessionId_EmptyGuid_Throws()
    {
        var session = CreateSession(correlatedGameSessionId: null);

        var act = () => session.SetCorrelatedGameSessionId(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
