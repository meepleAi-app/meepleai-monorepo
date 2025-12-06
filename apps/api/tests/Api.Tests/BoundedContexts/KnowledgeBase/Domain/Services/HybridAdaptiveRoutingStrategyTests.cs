using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;
using AuthUser = Api.BoundedContexts.Authentication.Domain.Entities.User;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HybridAdaptiveRoutingStrategy
/// ISSUE-958: Validates user-tier routing and traffic split logic
/// BGAI-022: Validates AI:Provider configuration integration
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class HybridAdaptiveRoutingStrategyTests
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<AiProviderSettings> _aiSettings;

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

        // BGAI-022: Default AI settings with both providers enabled (backward compatible)
        _aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "", // Empty = use user-tier routing
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["llama3:8b"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        });
    }

    [Fact]
    public void AnonymousUser_RoutesFreeModel()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);

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
    public void UserRole_Routes80PercentFree()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
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
    public void EditorRole_Routes50PercentOpenRouter()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
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
    public void AdminRole_Routes80PercentPremium()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
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
    public void RoutingDecision_ContainsReason()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert
        Assert.NotNull(decision.Reason);
        Assert.Contains(Role.User.Value, decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            decision.Reason.Contains("Traffic split") || decision.Reason.Contains("Cost-optimized"),
            "Reason should explain routing logic");
    }

    [Fact]
    public void FreeModelSelection_UsesOpenRouterProvider()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
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
    public void LocalOllamaModel_UsesOllamaProvider()
    {
        // Arrange
        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, _aiSettings);
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
    public void ConfigurationOverride_UsesCustomModels()
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

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, _aiSettings);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert
        Assert.Equal("custom-model:latest", decision.ModelId);
        Assert.Equal("Ollama", decision.ProviderName); // No '/' means local Ollama
    }

    [Fact]
    public void ZeroPercentOpenRouter_AlwaysUsesOllama()
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

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, _aiSettings);
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
    public void HundredPercentOpenRouter_AlwaysUsesOpenRouter()
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

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, _aiSettings);
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
    [Fact]
    public void PreferredProvider_OverridesUserTierRouting()
    {
        // Arrange - PreferredProvider set to Ollama
        var preferredSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "Ollama",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["mistral"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, preferredSettings);
        var admin = CreateUser(Role.Admin); // Admin would normally get 80% OpenRouter

        // Act
        var decision = strategy.SelectProvider(admin);

        // Assert - Should use PreferredProvider (Ollama) instead of user-tier routing
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("mistral", decision.ModelId);
        Assert.Contains("PreferredProvider override", decision.Reason);
    }

    [Fact]
    public void DisabledProvider_FallsBackToAlternative()
    {
        // Arrange - Ollama disabled, OpenRouter enabled
        // Use 0% OpenRouter to force Ollama selection, then verify fallback
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "0" // Force Ollama selection
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var disabledSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, disabledSettings);
        var editor = CreateUser(Role.Editor);

        // Act - Run 20 times
        var decisions = Enumerable.Range(0, 20)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - All should fallback to OpenRouter (Ollama disabled)
        Assert.All(decisions, d =>
        {
            Assert.Equal("OpenRouter", d.ProviderName);
            Assert.Contains("Fallback from Ollama", d.Reason);
        });
    }

    [Fact]
    public void AllProvidersDisabled_ThrowsException()
    {
        // Arrange - Both providers disabled
        var allDisabledSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, allDisabledSettings);
        var user = CreateUser(Role.User);

        // Act & Assert - Should throw when all providers disabled
        var exception = Assert.Throws<InvalidOperationException>(() => strategy.SelectProvider(user));
        Assert.Contains("Both AI providers are disabled", exception.Message);
    }

    [Fact]
    public void EmptyPreferredProvider_UsesUserTierRouting()
    {
        // Arrange - Empty PreferredProvider (Option C backward compatibility)
        var emptyPreferredSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "", // Empty = use user-tier routing
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, emptyPreferredSettings);
        var admin = CreateUser(Role.Admin);

        // Act - Run 100 times to test traffic split
        var decisions = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectProvider(admin))
            .ToList();

        // Assert - Should use existing user-tier routing (Admin = 80% OpenRouter)
        var openRouterCount = decisions.Count(d => d.ProviderName == "OpenRouter");
        Assert.True(openRouterCount > 60, $"Admin should get ~80% OpenRouter, got {openRouterCount}%");
        Assert.All(decisions, d => Assert.DoesNotContain("PreferredProvider", d.Reason));
    }

    [Fact]
    public void MissingAiSection_UsesUserTierRouting()
    {
        // Arrange - Empty providers (backward compatibility for legacy deployments)
        var emptySettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>() // Empty
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, emptySettings);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert - Should work normally with user-tier routing (no AI config interference)
        Assert.NotNull(decision);
        Assert.True(
            decision.ModelId == "meta-llama/llama-3.3-70b-instruct:free" ||
            decision.ModelId == "openai/gpt-4o-mini");
    }
    [Fact]
    public void BothProvidersMissing_UsesDefaults()
    {
        // Arrange - Empty providers dictionary (both missing)
        var emptySettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>() // Both missing
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, _configuration, emptySettings);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert - Should work with default configuration (no crash)
        Assert.NotNull(decision);
        Assert.True(
            decision.ModelId == "meta-llama/llama-3.3-70b-instruct:free" ||
            decision.ModelId == "openai/gpt-4o-mini");
    }

    [Fact]
    public void OneProviderDisabled_OtherMissing_FallbackWorks()
    {
        // Arrange - OpenRouter disabled, Ollama missing (should fallback to Ollama defaults)
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "100" // Force OpenRouter selection
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var oneDisabledSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
                // Ollama missing (not in dictionary)
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, oneDisabledSettings);
        var editor = CreateUser(Role.Editor);

        // Act - Run 10 times
        var decisions = Enumerable.Range(0, 10)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - Should fallback to Ollama (missing = implicitly enabled)
        Assert.All(decisions, d =>
        {
            Assert.Equal("Ollama", d.ProviderName);
            Assert.Equal("llama3:8b", d.ModelId); // Default Ollama model
            Assert.Contains("Fallback from OpenRouter", d.Reason);
        });
    }

    [Fact]
    public void BothProvidersExplicitlyDisabled_ThrowsException()
    {
        // Arrange - Both providers explicitly disabled
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:UserModel"] = "llama3:8b",
            ["LlmRouting:UserOpenRouterPercent"] = "50"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var bothDisabledSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, bothDisabledSettings);
        var user = CreateUser(Role.User);

        // Act & Assert - Should throw when both providers explicitly disabled
        var exception = Assert.Throws<InvalidOperationException>(() => strategy.SelectProvider(user));
        Assert.Contains("Both AI providers are disabled", exception.Message);
    }

    [Fact]
    public void OneProviderDisabled_OtherExplicitlyEnabled_FallbackWorks()
    {
        // Arrange - Ollama disabled, OpenRouter explicitly enabled
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "0" // Force Ollama selection
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var oneEnabledSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4o"] }
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, oneEnabledSettings);
        var editor = CreateUser(Role.Editor);

        // Act - Run 10 times
        var decisions = Enumerable.Range(0, 10)
            .Select(_ => strategy.SelectProvider(editor))
            .ToList();

        // Assert - Should fallback to OpenRouter (explicitly enabled)
        Assert.All(decisions, d =>
        {
            Assert.Equal("OpenRouter", d.ProviderName);
            Assert.Equal("gpt-4o", d.ModelId);
            Assert.Contains("Fallback from Ollama", d.Reason);
        });
    }

    [Fact]
    public void MissingProvider_UsesDefaultModel()
    {
        // Arrange - Only OpenRouter configured, Ollama missing
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:UserModel"] = "llama3:8b",
            ["LlmRouting:UserOpenRouterPercent"] = "100" // Force OpenRouter
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var partialSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
                // Ollama missing - should use GetDefaultModelForProvider
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, partialSettings);
        var user = CreateUser(Role.User);

        // Act
        var decision = strategy.SelectProvider(user);

        // Assert - Should use default Ollama model (llama3:8b)
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("llama3:8b", decision.ModelId);
    }

    [Fact]
    public void AdminUser_FallbackToMissingProvider_UsesAdminModel()
    {
        // Arrange - OpenRouter disabled, Ollama missing (admin should get Claude)
        var customConfig = new Dictionary<string, string>
        {
            ["LlmRouting:AdminModel"] = "llama3:8b",
            ["LlmRouting:AdminOpenRouterPercent"] = "100", // Force OpenRouter
            ["LlmRouting:PremiumModel"] = "anthropic/claude-3.5-haiku"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(customConfig!)
            .Build();

        var adminFallbackSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
                // Ollama missing
            }
        });

        var strategy = new HybridAdaptiveRoutingStrategy(_logger, config, adminFallbackSettings);
        var admin = CreateUser(Role.Admin);

        // Act
        var decision = strategy.SelectProvider(admin);

        // Assert - Admin fallback to Ollama should still use admin-tier model
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("llama3:8b", decision.ModelId); // Ollama default
    }
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
}

