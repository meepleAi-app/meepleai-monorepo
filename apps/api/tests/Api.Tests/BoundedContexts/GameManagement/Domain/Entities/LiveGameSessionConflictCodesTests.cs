using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4844 — the 409 of an action the session's status does not admit carries a stable code, one per
/// CAUSE: the client translates by code and never shows the English sentence. The cells come from the
/// action × status matrix on the issue. The one that confuses players most is a rule dispute (or a
/// turn) on a PAUSED game — refused, while ending the game is allowed — which must read «paused», not
/// «not started».
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveGameSessionConflictCodesTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    private (LiveGameSession Session, LiveSessionPlayer Player) SessionIn(LiveSessionStatus status)
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", _time, null);
        var player = session.AddPlayer(null, "Giulia", PlayerColor.Red, _time, null);
        switch (status)
        {
            case LiveSessionStatus.Created:
                break;
            case LiveSessionStatus.Setup:
                session.MoveToSetup(_time);
                break;
            case LiveSessionStatus.InProgress:
                session.Start(_time);
                break;
            case LiveSessionStatus.Paused:
                session.Start(_time);
                session.Pause(_time);
                break;
            case LiveSessionStatus.Completed:
                session.Start(_time);
                session.Complete(_time);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        session.Status.Should().Be(status, "the arrangement must land on the status under test");
        return (session, player);
    }

    private static ConflictException Refusal(Action act) =>
        act.Should().Throw<ConflictException>().Which;

    [Theory]
    [InlineData(LiveSessionStatus.Created, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Setup, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void RecordScore_OutsidePlay_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, player) = SessionIn(status);

        var refusal = Refusal(() => session.RecordScore(player.Id, 1, "points", 3, _time));

        refusal.ErrorCode.Should().Be(expectedCode);
        refusal.Message.Should().StartWith("Cannot record scores", "the English sentence stays for the logs");
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Setup, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void Complete_OutsidePlay_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, _) = SessionIn(status);

        Refusal(() => session.Complete(_time)).ErrorCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(LiveSessionStatus.InProgress, LiveSessionConflictCodes.SessionAlreadyStarted)]
    [InlineData(LiveSessionStatus.Paused, LiveSessionConflictCodes.SessionPaused)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void Start_OnAStartedGame_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, _) = SessionIn(status);

        Refusal(() => session.Start(_time)).ErrorCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.InProgress, LiveSessionConflictCodes.SessionNotPaused)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void Resume_OnAGameNotPaused_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, _) = SessionIn(status);

        Refusal(() => session.Resume(_time)).ErrorCode.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Paused, LiveSessionConflictCodes.SessionPaused)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void Pause_OnAGameNotRunning_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, _) = SessionIn(status);

        Refusal(() => session.Pause(_time)).ErrorCode.Should().Be(expectedCode);
    }

    /// <summary>The paused cell: an action that wants the game RUNNING says «paused», not «not started».</summary>
    [Fact]
    public void AdvanceTurn_OnAPausedGame_SaysPaused()
    {
        var (session, _) = SessionIn(LiveSessionStatus.Paused);

        Refusal(() => session.AdvanceTurn(_time)).ErrorCode.Should().Be(LiveSessionConflictCodes.SessionPaused);
    }

    [Fact]
    public void AddPlayer_OnACompletedGame_SaysCompleted()
    {
        var (session, _) = SessionIn(LiveSessionStatus.Completed);

        Refusal(() => session.AddPlayer(null, "Marco", PlayerColor.Blue, _time, null))
            .ErrorCode.Should().Be(LiveSessionConflictCodes.SessionCompleted);
    }

    /// <summary>Save wants an ACTIVE game (Setup included): Created is «not started», Completed is «completed».</summary>
    [Theory]
    [InlineData(LiveSessionStatus.Created, LiveSessionConflictCodes.SessionNotStarted)]
    [InlineData(LiveSessionStatus.Completed, LiveSessionConflictCodes.SessionCompleted)]
    public void Save_OnAnInactiveGame_NamesTheCause(LiveSessionStatus status, string expectedCode)
    {
        var (session, _) = SessionIn(status);

        Refusal(() => session.Save(_time)).ErrorCode.Should().Be(expectedCode);
    }

    /// <summary>
    /// Every member of the enum has a cause. A new status (say, Abandoned) added without a line in the
    /// switch would turn every 409 on it into a 500 (the `_` arm throws): this test turns that into a
    /// red here instead.
    /// </summary>
    [Fact]
    public void ForRejectedStatus_CoversEveryDefinedStatus()
    {
        foreach (var status in Enum.GetValues<LiveSessionStatus>())
        {
            var act = () => LiveSessionConflictCodes.ForRejectedStatus(status, LiveSessionConflictCodes.SessionAlreadyStarted);
            act.Should().NotThrow($"{status} must map to a code");
        }
    }

    [Fact]
    public void ForRejectedStatus_InProgress_IsTheOnlyActionDependentCell()
    {
        LiveSessionConflictCodes.ForRejectedStatus(LiveSessionStatus.InProgress, "whatever_the_action_says")
            .Should().Be("whatever_the_action_says");

        var act = () => LiveSessionConflictCodes.ForRejectedStatus((LiveSessionStatus)99, "x");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
