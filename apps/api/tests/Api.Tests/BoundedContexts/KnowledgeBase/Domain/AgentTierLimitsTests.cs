using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Tests for AgentTierLimits shared configuration.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AgentTierLimitsTests
{
    [Theory]
    [InlineData("free", 3)]
    [InlineData("Free", 3)]
    [InlineData("FREE", 3)]
    [InlineData("normal", 10)]
    [InlineData("premium", 50)]
    [InlineData("pro", 50)]
    [InlineData("enterprise", 200)]
    public void GetMaxAgents_RegularUser_ReturnsCorrectLimit(string tier, int expected)
    {
        var result = AgentTierLimits.GetMaxAgents(tier, "user");
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    public void GetMaxAgents_AdminRole_ReturnsMaxValue(string role)
    {
        var result = AgentTierLimits.GetMaxAgents("free", role);
        result.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("platinum")]
    public void GetMaxAgents_UnknownTier_ReturnsDefault(string? tier)
    {
        var result = AgentTierLimits.GetMaxAgents(tier, "user");
        result.Should().Be(AgentTierLimits.DefaultMaxAgents);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Editor", true)]
    [InlineData("admin", true)]
    [InlineData("editor", true)]
    [InlineData("user", false)]
    [InlineData("User", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsAdminOrEditor_ReturnsCorrectly(string? role, bool expected)
    {
        var result = AgentTierLimits.IsAdminOrEditor(role);
        result.Should().Be(expected);
    }

    [Fact]
    public void MaxAgentsPerTier_ContainsAllExpectedTiers()
    {
        Assert.True(AgentTierLimits.MaxAgentsPerTier.ContainsKey("free"));
        Assert.True(AgentTierLimits.MaxAgentsPerTier.ContainsKey("normal"));
        Assert.True(AgentTierLimits.MaxAgentsPerTier.ContainsKey("premium"));
        Assert.True(AgentTierLimits.MaxAgentsPerTier.ContainsKey("pro"));
        Assert.True(AgentTierLimits.MaxAgentsPerTier.ContainsKey("enterprise"));
    }

    [Fact]
    public void MaxAgentsPerTier_IsCaseInsensitive()
    {
        Assert.Equal(
            AgentTierLimits.MaxAgentsPerTier["free"],
            AgentTierLimits.MaxAgentsPerTier["Free"]);
    }
}
