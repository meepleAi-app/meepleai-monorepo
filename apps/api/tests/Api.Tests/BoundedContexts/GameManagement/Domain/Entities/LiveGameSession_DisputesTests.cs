using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSession_DisputesTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _now;

    public LiveGameSession_DisputesTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
        _now = _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Creates a session in InProgress state (Created → Start requires players).
    /// </summary>
    private LiveGameSession CreateInProgressSession()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Catan",
            _timeProvider);

        session.AddPlayer(null, "Alice", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);

        return session;
    }

    private static RuleDisputeEntry CreateTestDispute(string playerName = "Marco") =>
        new RuleDisputeEntry(
            Guid.NewGuid(),
            "Can I play 2 cards per turn?",
            "No, rule says 1 card per turn.",
            new List<string> { "Page 5", "Page 12" },
            playerName,
            DateTime.UtcNow);

    #region AddDispute

    [Fact]
    public void AddDispute_ShouldAddToDisputesList()
    {
        // Arrange
        var session = CreateInProgressSession();
        var dispute = CreateTestDispute("Marco");

        // Act
        session.AddDispute(dispute);

        // Assert
        session.Disputes.Should().ContainSingle();
        session.Disputes[0].RaisedByPlayerName.Should().Be("Marco");
        session.Disputes[0].Description.Should().Be("Can I play 2 cards per turn?");
        Assert.Equal("No, rule says 1 card per turn.", session.Disputes[0].Verdict);
    }

    [Fact]
    public void AddDispute_MultipleDisputes_ShouldAllBePresent()
    {
        // Arrange
        var session = CreateInProgressSession();
        var dispute1 = CreateTestDispute("Marco");
        var dispute2 = CreateTestDispute("Luca");

        // Act
        session.AddDispute(dispute1);
        session.AddDispute(dispute2);

        // Assert
        session.Disputes.Count.Should().Be(2);
        Assert.Contains(session.Disputes, d => d.RaisedByPlayerName == "Marco");
        Assert.Contains(session.Disputes, d => d.RaisedByPlayerName == "Luca");
    }

    [Fact]
    public void AddDispute_NullDispute_ShouldThrowArgumentNullException()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act & Assert
        ((Action)(() => session.AddDispute(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Disputes_ShouldBeReadOnly()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act & Assert
        Assert.IsAssignableFrom<IReadOnlyList<RuleDisputeEntry>>(session.Disputes);
    }

    [Fact]
    public void Disputes_InitiallyEmpty()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Assert
        Assert.Empty(session.Disputes);
    }

    [Fact]
    public void AddDispute_PreservesRuleReferences()
    {
        // Arrange
        var session = CreateInProgressSession();
        var references = new List<string> { "Page 5", "Section 3.2" };
        var dispute = new RuleDisputeEntry(
            Guid.NewGuid(),
            "Is this allowed?",
            "Yes, per rule 3.2",
            references,
            "Player",
            DateTime.UtcNow);

        // Act
        session.AddDispute(dispute);

        // Assert
        session.Disputes[0].RuleReferences.Count.Should().Be(2);
        session.Disputes[0].RuleReferences.Should().Contain("Page 5");
        session.Disputes[0].RuleReferences.Should().Contain("Section 3.2");
    }

    #endregion

    #region Pause

    [Fact]
    public void Pause_WhenInProgress_ShouldSetStatusToPaused()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.Pause(_timeProvider);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.Paused);
    }

    [Fact]
    public void Pause_WhenInProgress_ShouldSetPausedAt()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.Pause(_timeProvider);

        // Assert
        session.PausedAt.Should().Be(_now);
    }

    [Fact]
    public void Pause_WhenNotInProgress_ShouldThrow()
    {
        // Arrange
        var session = CreateInProgressSession();
        session.Pause(_timeProvider); // now Paused

        // Act & Assert
        ((Action)(() => session.Pause(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Pause_WhenCreated_ShouldThrow()
    {
        // Arrange
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider);

        // Act & Assert
        ((Action)(() => session.Pause(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Pause_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var session = CreateInProgressSession();
        session.Complete(_timeProvider);

        // Act & Assert
        ((Action)(() => session.Pause(_timeProvider))).Should().Throw<ConflictException>();
    }

    #endregion

    #region Resume

    [Fact]
    public void Resume_WhenPaused_ShouldSetStatusToInProgress()
    {
        // Arrange
        var session = CreateInProgressSession();
        session.Pause(_timeProvider);

        // Act
        session.Resume(_timeProvider);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.InProgress);
    }

    [Fact]
    public void Resume_WhenPaused_ShouldClearPausedAt()
    {
        // Arrange
        var session = CreateInProgressSession();
        session.Pause(_timeProvider);

        // Act
        session.Resume(_timeProvider);

        // Assert
        Assert.Null(session.PausedAt);
    }

    [Fact]
    public void Resume_WhenNotPaused_ShouldThrow()
    {
        // Arrange
        var session = CreateInProgressSession(); // InProgress, not Paused

        // Act & Assert
        ((Action)(() => session.Resume(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Resume_WhenCreated_ShouldThrow()
    {
        // Arrange
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider);

        // Act & Assert
        ((Action)(() => session.Resume(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Resume_WhenCompleted_ShouldThrow()
    {
        // Arrange
        var session = CreateInProgressSession();
        session.Complete(_timeProvider);

        // Act & Assert
        ((Action)(() => session.Resume(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void PauseAndResume_Cycle_ShouldWorkCorrectly()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act & Assert - full cycle
        session.Pause(_timeProvider);
        session.Status.Should().Be(LiveSessionStatus.Paused);
        Assert.NotNull(session.PausedAt);

        session.Resume(_timeProvider);
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        Assert.Null(session.PausedAt);

        // Can pause again
        session.Pause(_timeProvider);
        session.Status.Should().Be(LiveSessionStatus.Paused);
    }

    #endregion
}
