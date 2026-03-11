using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Null(openRouterDecision.UserRegion);
        Assert.Null(ollamaDecision.UserRegion);
    }

    [Fact]
    public void UserRegion_CanBeSetViaWithExpression()
    {
        // Arrange
        var decision = LlmRoutingDecision.OpenRouter("gpt-4", "Strategy: PRECISE, Tier: Admin");

        // Act
        var regionDecision = decision with { UserRegion = "eu-west" };

        // Assert
        Assert.Equal("eu-west", regionDecision.UserRegion);
        Assert.Equal(decision.ProviderName, regionDecision.ProviderName);
        Assert.Equal(decision.ModelId, regionDecision.ModelId);
        Assert.Equal(decision.Reason, regionDecision.Reason);
    }

    [Fact]
    public void UserRegion_DefaultsToNull_WhenUsingConstructor()
    {
        // Arrange & Act
        var decision = new LlmRoutingDecision("DeepSeek", "deepseek-chat", "direct routing");

        // Assert
        Assert.Null(decision.UserRegion);
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
        Assert.Equal("us-east", decision.UserRegion);
    }
}
