using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AuthUser = Api.BoundedContexts.Authentication.Domain.Entities.User;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HybridAdaptiveRoutingStrategy
/// ISSUE-958: Validates user-tier routing and traffic split logic
/// </summary>
public class HybridAdaptiveRoutingStrategyTests
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IConfiguration _configuration;

    public HybridAdaptiveRoutingStrategyTests()
    {
        _logger = Mock.Of<ILogger<HybridAdaptiveRoutingStrategy>>();

        // Default configuration for testing
        var configData = new Dictionary<string, string>
        {
            ["LlmRouting:AnonymousModel"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["LlmRouting:AnonymousOpenRouterPercent"] = "20",
            ["LlmRouting:UserModel"] = "meta-llama/llama-3.3-70b-instruct:free",
            ["LlmRouting:UserOpenRouterPercent"] = "20",
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "50",
            ["LlmRouting:AdminModel"] = "llama3:8b",
            ["LlmRouting:AdminOpenRouterPercent"] = "80",
            ["LlmRouting:PremiumModel"] = "anthropic/claude-3.5-haiku"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    [Fact]
    public void Test01_AnonymousUser_RoutesFreeModel()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);

        // Act - Run multiple times to test traffic split
        var decisions = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectProvider(user: null))
            .ToList();

        // Assert - Should route to AnonymousModel config (not UserModel)
        Assert.All(decisions, d =>
        {
            Assert.True(
                d.ModelId == "meta-llama/llama-3.3-70b-instruct:free" ||
                d.ModelId == "openai/gpt-4o-mini",
                $"Anonymous should use AnonymousModel config, got: {d.ModelId}");
        });

        // Verify traffic split roughly 80/20 (AnonymousOpenRouterPercent = 20)
        var freeModelCount = decisions.Count(d => d.ModelId == "meta-llama/llama-3.3-70b-instruct:free");
        var paidModelCount = decisions.Count(d => d.ModelId == "openai/gpt-4o-mini");

        Assert.InRange(freeModelCount, 60, 95); // Allow variance (target 80%)
        Assert.InRange(paidModelCount, 5, 40); // Allow variance (target 20%)

        // P1: Verify anonymous config is honored (not User config)
        var allUseConfiguredModels = decisions.All(d =>
            d.ModelId == "meta-llama/llama-3.3-70b-instruct:free" || // AnonymousModel
            d.ModelId == "openai/gpt-4o-mini"); // AnonymousOpenRouterModel fallback

        Assert.True(allUseConfiguredModels, "Anonymous should use AnonymousModel/AnonymousOpenRouterModel");
    }

    [Fact]
    public void Test02_UserRole_Routes80PercentFree()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var user = CreateUser(Role.User);

        // Act - Run 100 times
        var decisions = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectProvider(user))
            .ToList();

        // Assert - ~80% free tier, ~20% paid
        var freeCount = decisions.Count(d => d.ModelId == "meta-llama/llama-3.3-70b-instruct:free");
        var paidCount = decisions.Count(d => d.ModelId == "openai/gpt-4o-mini");

        Assert.InRange(freeCount, 60, 95);
        Assert.InRange(paidCount, 5, 40);
    }

    [Fact]
    public void Test03_EditorRole_Routes50PercentOpenRouter()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var editor = CreateUser(Role.Editor);

        // Act - Run 100 times
        var decisions = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - Balanced split (50/50)
        var ollamaCount = decisions.Count(d => d.ModelId == "llama3:8b");
        var openRouterCount = decisions.Count(d => d.ModelId == "openai/gpt-4o-mini");

        Assert.InRange(ollamaCount, 30, 70); // Allow variance around 50%
        Assert.InRange(openRouterCount, 30, 70);

        // Verify provider routing
        var ollamaProviderCount = decisions.Count(d => d.ProviderName == "Ollama");
        Assert.InRange(ollamaProviderCount, 30, 70);
    }

    [Fact]
    public void Test04_AdminRole_Routes80PercentPremium()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var admin = CreateUser(Role.Admin);

        // Act - Run 100 times
        var decisions = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectProvider(admin))
            .ToList();

        // Assert - ~80% premium (Claude Haiku), ~20% local Ollama
        var premiumCount = decisions.Count(d => d.ModelId == "anthropic/claude-3.5-haiku");
        var localCount = decisions.Count(d => d.ModelId == "llama3:8b");

        Assert.InRange(premiumCount, 60, 95); // Target 80%
        Assert.InRange(localCount, 5, 40); // Target 20%

        // Verify most are OpenRouter provider
        var openRouterCount = decisions.Count(d => d.ProviderName == "OpenRouter");
        Assert.InRange(openRouterCount, 60, 95);
    }

    [Fact]
    public void Test05_RoutingDecision_ContainsReason()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert
        Assert.NotNull(decision.Reason);
        Assert.Contains("user", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            decision.Reason.Contains("Traffic split") || decision.Reason.Contains("Cost-optimized"),
            "Reason should explain routing logic");
    }

    [Fact]
    public void Test06_FreeModelSelection_UsesOpenRouterProvider()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var user = CreateUser(Role.User);

        // Act - Get 50 decisions
        var decisions = Enumerable.Range(0, 50)
            .Select(_ => strategy.SelectProvider(user))
            .ToList();

        // Assert - Free tier models (meta-llama/*) should route to OpenRouter provider
        var freeModelDecisions = decisions.Where(d => d.ModelId.StartsWith("meta-llama/")).ToList();

        if (freeModelDecisions.Any())
        {
            Assert.All(freeModelDecisions, d =>
                Assert.Equal("OpenRouter", d.ProviderName));
        }
    }

    [Fact]
    public void Test07_LocalOllamaModel_UsesOllamaProvider()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration);
        var editor = CreateUser(Role.Editor);

        // Act - Get 50 decisions
        var decisions = Enumerable.Range(0, 50)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - Local models (llama3:8b) should route to Ollama provider
        var localModelDecisions = decisions.Where(d => d.ModelId == "llama3:8b").ToList();

        if (localModelDecisions.Any())
        {
            Assert.All(localModelDecisions, d =>
                Assert.Equal("Ollama", d.ProviderName));
        }
    }

    [Fact]
    public void Test08_ConfigurationOverride_UsesCustomModels()
    {
        // Arrange - Custom configuration
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:UserModel"] = "custom-model:latest",
            ["LlmRouting:UserOpenRouterPercent"] = "0" // Always use primary
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert
        Assert.Equal("custom-model:latest", decision.ModelId);
        Assert.Equal("Ollama", decision.ProviderName); // No '/' means local Ollama
    }

    [Fact]
    public void Test09_ZeroPercentOpenRouter_AlwaysUsesOllama()
    {
        // Arrange - 0% OpenRouter traffic
        var zeroConfig = new Dictionary<string, string>
        {
            ["LlmRouting:UserModel"] = "llama3:8b",
            ["LlmRouting:UserOpenRouterPercent"] = "0"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(zeroConfig!)
            .Build();

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config);
        var user = CreateUser(Role.User);

        // Act - Run 20 times
        var decisions = Enumerable.Range(0, 20)
            .Select(_ => strategy.SelectProvider(user))
            .ToList();

        // Assert - All should use Ollama
        Assert.All(decisions, d =>
        {
            Assert.Equal("llama3:8b", d.ModelId);
            Assert.Equal("Ollama", d.ProviderName);
        });
    }

    [Fact]
    public void Test10_HundredPercentOpenRouter_AlwaysUsesOpenRouter()
    {
        // Arrange - 100% OpenRouter traffic
        var fullConfig = new Dictionary<string, string>
        {
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "100",
            ["LlmRouting:EditorOpenRouterModel"] = "openai/gpt-4o"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(fullConfig!)
            .Build();

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config);
        var editor = CreateUser(Role.Editor);

        // Act - Run 20 times
        var decisions = Enumerable.Range(0, 20)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - All should use OpenRouter
        Assert.All(decisions, d =>
        {
            Assert.Equal("openai/gpt-4o", d.ModelId);
            Assert.Equal("OpenRouter", d.ProviderName);
        });
    }

    #region Helper Methods

    private static AuthUser CreateUser(Role role)
    {
        var email = Email.Parse($"test.{role.Value}@meepleai.dev");
        var password = PasswordHash.Create("TestPass123!");

        return new AuthUser(
            Guid.NewGuid(),
            email,
            $"Test {role.Value}",
            password,
            role);
    }

    #endregion
}
