using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.ToolState;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class ToolStateDomainTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _toolkitId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_CreatesToolState()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Main Dice", ToolType.Dice, "{\"diceType\":\"D6\"}");

        ts.SessionId.Should().Be(_sessionId);
        ts.ToolkitId.Should().Be(_toolkitId);
        ts.ToolName.Should().Be("Main Dice");
        ts.ToolType.Should().Be(ToolType.Dice);
        ts.StateDataJson.Should().Be("{\"diceType\":\"D6\"}");
        Assert.True(ts.CreatedAt <= DateTime.UtcNow);
        Assert.True(ts.LastUpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullInitialState_DefaultsToEmptyJson()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Counter", ToolType.Counter, null!);

        ts.StateDataJson.Should().Be("{}");
    }

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), Guid.Empty, _toolkitId, "Dice", ToolType.Dice, "{}"));
    }

    [Fact]
    public void Constructor_WithEmptyToolkitId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), _sessionId, Guid.Empty, "Dice", ToolType.Dice, "{}"));
    }

    [Fact]
    public void Constructor_WithEmptyToolName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), _sessionId, _toolkitId, "", ToolType.Dice, "{}"));
    }

    [Fact]
    public void Constructor_WithWhitespaceToolName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
                Guid.NewGuid(), _sessionId, _toolkitId, "   ", ToolType.Dice, "{}"));
    }

    [Fact]
    public void Constructor_TrimsToolName()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "  My Dice  ", ToolType.Dice, "{}");

        ts.ToolName.Should().Be("My Dice");
    }

    [Fact]
    public void UpdateState_WithValidJson_UpdatesStateAndTimestamp()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Dice", ToolType.Dice, "{}");
        var originalTimestamp = ts.LastUpdatedAt;

        // Small delay to ensure timestamp differs
        Thread.Sleep(1);

        ts.UpdateState("{\"lastRoll\":[3,5]}");

        ts.StateDataJson.Should().Be("{\"lastRoll\":[3,5]}");
        Assert.True(ts.LastUpdatedAt >= originalTimestamp);
    }

    [Fact]
    public void UpdateState_WithNullJson_ThrowsArgumentNullException()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Dice", ToolType.Dice, "{}");

        ((Action)(() => ts.UpdateState(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithDiceToolType_Succeeds()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Tool", ToolType.Dice, "{}");
        ts.ToolType.Should().Be(ToolType.Dice);
    }

    [Fact]
    public void Constructor_WithCounterToolType_Succeeds()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Tool", ToolType.Counter, "{}");
        ts.ToolType.Should().Be(ToolType.Counter);
    }

    [Fact]
    public void Constructor_WithCardToolType_Succeeds()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Tool", ToolType.Card, "{}");
        ts.ToolType.Should().Be(ToolType.Card);
    }

    [Fact]
    public void Constructor_WithTimerToolType_Succeeds()
    {
        var ts = new Api.BoundedContexts.GameManagement.Domain.Entities.ToolState.ToolState(
            Guid.NewGuid(), _sessionId, _toolkitId, "Tool", ToolType.Timer, "{}");
        ts.ToolType.Should().Be(ToolType.Timer);
    }
}
