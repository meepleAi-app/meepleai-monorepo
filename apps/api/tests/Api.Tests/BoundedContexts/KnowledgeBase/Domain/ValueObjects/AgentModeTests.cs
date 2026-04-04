using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the AgentMode enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class AgentModeTests
{
    #region Enum Value Tests

    [Fact]
    public void Chat_HasCorrectValue()
    {
        ((int)AgentMode.Chat).Should().Be(0);
    }

    [Fact]
    public void Player_HasCorrectValue()
    {
        ((int)AgentMode.Player).Should().Be(1);
    }

    [Fact]
    public void Ledger_HasCorrectValue()
    {
        ((int)AgentMode.Ledger).Should().Be(2);
    }

    [Fact]
    public void Arbiter_HasCorrectValue()
    {
        ((int)AgentMode.Arbiter).Should().Be(3);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void AgentMode_HasFourValues()
    {
        var values = Enum.GetValues<AgentMode>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void AgentMode_AllValuesCanBeParsed()
    {
        var names = new[] { "Chat", "Player", "Ledger", "Arbiter" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<AgentMode>(name);
            parsed.Should().BeOneOf(Enum.GetValues<AgentMode>());
        }
    }

    [Fact]
    public void AgentMode_ToString_ReturnsExpectedNames()
    {
        AgentMode.Chat.ToString().Should().Be("Chat");
        AgentMode.Player.ToString().Should().Be("Player");
        AgentMode.Ledger.ToString().Should().Be("Ledger");
        AgentMode.Arbiter.ToString().Should().Be("Arbiter");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void Chat_IsDefaultMode()
    {
        // Chat = 0 is the default Q&A mode
        var defaultMode = default(AgentMode);
        defaultMode.Should().Be(AgentMode.Chat);
    }

    [Fact]
    public void ModeValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<AgentMode>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected mode at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, AgentMode.Chat)]
    [InlineData(1, AgentMode.Player)]
    [InlineData(2, AgentMode.Ledger)]
    [InlineData(3, AgentMode.Arbiter)]
    public void AgentMode_CastFromInt_ReturnsCorrectValue(int value, AgentMode expected)
    {
        ((AgentMode)value).Should().Be(expected);
    }

    [Fact]
    public void AgentMode_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(AgentMode), 0).Should().BeTrue();
        Enum.IsDefined(typeof(AgentMode), 1).Should().BeTrue();
        Enum.IsDefined(typeof(AgentMode), 2).Should().BeTrue();
        Enum.IsDefined(typeof(AgentMode), 3).Should().BeTrue();
    }

    [Fact]
    public void AgentMode_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(AgentMode), 4).Should().BeFalse();
        Enum.IsDefined(typeof(AgentMode), -1).Should().BeFalse();
    }

    #endregion
}
