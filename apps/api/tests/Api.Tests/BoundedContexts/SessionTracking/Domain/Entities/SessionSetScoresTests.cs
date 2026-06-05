using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="Session.ScoringType"/>, <see cref="Session.ScoreData"/>,
/// and <see cref="Session.SetScores"/>.
///
/// <para>Asse A semantic alignment #1896 (T9, DEC-1): validates the polymorphic
/// scoring fields (default = Points/"{}") and the <see cref="SessionScoresUpdatedEvent"/>
/// raised by <see cref="Session.SetScores"/>. T10 will integrate this domain method
/// with the <c>SaveSessionCommandHandler</c> + per-type FluentValidation.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionSetScoresTests
{
    [Fact]
    public void NewSession_DefaultsToPointsScoringType_EmptyJsonScoreData()
    {
        var session = CreateValidSession();

        session.ScoringType.Should().Be(ScoreType.Points,
            because: "DEC-1 — default ScoringType for a new session is Points (~50% catalog coverage)");
        session.ScoreData.Should().Be("{}",
            because: "DEC-1 — default ScoreData is an empty JSON object until SetScores is called");
    }

    [Fact]
    public void SetScores_UpdatesBothFields_RaisesDomainEvent()
    {
        var session = CreateValidSession();
        session.ClearDomainEvents(); // discard the Create-time event(s)
        var json = """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":42}]}""";

        session.SetScores(ScoreType.Points, json);

        session.ScoringType.Should().Be(ScoreType.Points);
        session.ScoreData.Should().Be(json,
            because: "SetScores must overwrite ScoreData with the new JSON payload");

        var domainEvent = session.DomainEvents
            .OfType<SessionScoresUpdatedEvent>()
            .Should().ContainSingle(
                because: "SetScores must enqueue exactly one SessionScoresUpdatedEvent")
            .Subject;

        domainEvent.SessionId.Should().Be(session.Id,
            because: "the event SessionId must match the session whose scores were updated");
        domainEvent.ScoringType.Should().Be(ScoreType.Points);
        domainEvent.ScoreData.Should().Be(json);
        domainEvent.AggregateId.Should().Be(session.Id,
            because: "AggregateId aliases SessionId for the DomainEventLog mapper");
        domainEvent.EventId.Should().NotBe(Guid.Empty,
            because: "each domain event must carry a unique identifier for idempotency");
    }

    [Fact]
    public void SetScores_WithBinaryWinScoringType_PersistsType()
    {
        var session = CreateValidSession();
        var json = """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true}]}""";

        session.SetScores(ScoreType.BinaryWin, json);

        session.ScoringType.Should().Be(ScoreType.BinaryWin,
            because: "DEC-1 — BinaryWin is a first-class ScoringType for cooperative games (Pandemic, Forbidden Island)");
        session.ScoreData.Should().Be(json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetScores_WithEmptyOrWhitespaceScoreData_Throws(string? scoreData)
    {
        var session = CreateValidSession();

        var act = () => session.SetScores(ScoreType.Points, scoreData!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScoreData cannot be empty*",
                because: "DEC-1 — SetScores must guard against empty/whitespace JSON payloads (richer per-type validation happens in T10)");
    }

    [Fact]
    public void SetScores_CalledTwice_OverwritesAndRaisesSecondEvent()
    {
        var session = CreateValidSession();
        session.SetScores(ScoreType.Points, "{\"first\":true}");
        session.ClearDomainEvents();

        var newJson = """{"scores":[]}""";
        session.SetScores(ScoreType.Ranking, newJson);

        session.ScoringType.Should().Be(ScoreType.Ranking,
            because: "DEC-1 — SetScores supersedes any prior ScoringType");
        session.ScoreData.Should().Be(newJson,
            because: "DEC-1 — SetScores supersedes any prior ScoreData");
        session.DomainEvents
            .OfType<SessionScoresUpdatedEvent>()
            .Should().ContainSingle(
                because: "each SetScores invocation must enqueue exactly one event (idempotency at the call site)");
    }

    private static Session CreateValidSession()
    {
        return Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.Generic);
    }
}
