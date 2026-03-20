using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for LlmRoutingDecision record.
/// Issue #107: G1 — UserRegion property for multi-region preparation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class LlmRoutingDecisionTests
{
    [Fact]
    public void UserRegion_DefaultsToNull_WhenUsingFactoryMethods()
    {
        // Arrange & Act
        var openRouterDecision = LlmRoutingDecision.OpenRouter("gpt-4", "test reason");
        var ollamaDecision = LlmRoutingDecision.Ollama("llama3:8b", "test reason");

        // Assert
        openRouterDecision.UserRegion.Should().BeNull();
        ollamaDecision.UserRegion.Should().BeNull();
    }

    [Fact]
    public void UserRegion_CanBeSetViaWithExpression()
    {
        // Arrange
        var decision = LlmRoutingDecision.OpenRouter("gpt-4", "Strategy: PRECISE, Tier: Admin");

        // Act
        var regionDecision = decision with { UserRegion = "eu-west" };

        // Assert
        regionDecision.UserRegion.Should().Be("eu-west");
        regionDecision.ProviderName.Should().Be(decision.ProviderName);
        regionDecision.ModelId.Should().Be(decision.ModelId);
        regionDecision.Reason.Should().Be(decision.Reason);
    }

    [Fact]
    public void UserRegion_DefaultsToNull_WhenUsingConstructor()
    {
        // Arrange & Act
        var decision = new LlmRoutingDecision("DeepSeek", "deepseek-chat", "direct routing");

        // Assert
        decision.UserRegion.Should().BeNull();
    }

    [Fact]
    public void UserRegion_CanBeSetViaInitializer()
    {
        // Arrange & Act
        var decision = new LlmRoutingDecision("OpenRouter", "gpt-4", "test")
        {
            UserRegion = "us-east"
        };

        // Assert
        decision.UserRegion.Should().Be("us-east");
    }
}
