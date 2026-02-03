using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;
using AuthUser = Api.BoundedContexts.Authentication.Domain.Entities.User;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HybridAdaptiveRoutingStrategy.
/// Issue #3435: Validates strategy-based routing (strategy determines model, tier validates access).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class HybridAdaptiveRoutingStrategyTests
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IOptions<AiProviderSettings> _aiSettings;
    private readonly Mock<IStrategyModelMappingService> _mockStrategyMappingService;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ITierStrategyAccessService> _mockTierAccessService;
    private readonly Mock<ILlmModelOverrideService> _mockOverrideService;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public HybridAdaptiveRoutingStrategyTests()
    {
        _logger = Mock.Of<ILogger<HybridAdaptiveRoutingStrategy>>();

        // Default AI settings with both providers enabled
        _aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "", // Empty = use strategy-based routing
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["llama3:8b"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        });

        _mockStrategyMappingService = new Mock<IStrategyModelMappingService>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockTierAccessService = new Mock<ITierStrategyAccessService>();
        _mockOverrideService = new Mock<ILlmModelOverrideService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup scope factory to return scope with tier access service
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ITierStrategyAccessService)))
            .Returns(_mockTierAccessService.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);

        // Setup service scope factory to resolve ITierStrategyAccessService
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(p => p.GetService(typeof(ITierStrategyAccessService)))
            .Returns(_mockTierAccessService.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        // Default mock setup: all tiers have access to all strategies
        _mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(It.IsAny<LlmUserTier>(), It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default mock setup: return default mappings
        _mockStrategyMappingService
            .Setup(s => s.GetModelForStrategyAsync(It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RagStrategy strategy, CancellationToken _) =>
            {
                var mapping = StrategyModelMapping.Default(strategy);
                return (mapping.Provider, mapping.PrimaryModel);
            });

        _mockStrategyMappingService
            .Setup(s => s.GetFallbackModelsAsync(It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RagStrategy strategy, CancellationToken _) =>
            {
                var mapping = StrategyModelMapping.Default(strategy);
                return mapping.FallbackModels;
            });

        // Default mock setup: no budget mode
        _mockOverrideService.Setup(s => s.IsInBudgetMode()).Returns(false);
    }

    private HybridAdaptiveRoutingStrategy CreateStrategy(ILlmModelOverrideService? overrideService = null)
    {
        return new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockServiceScopeFactory.Object,
            _aiSettings,
            _logger,
            overrideService ?? _mockOverrideService.Object);
    }

    #region Strategy-Based Model Selection Tests

    [Theory]
    [InlineData(RagStrategy.Fast, "OpenRouter", "meta-llama/llama-3.3-70b-instruct:free")]
    [InlineData(RagStrategy.Balanced, "OpenRouter", "openai/gpt-4o-mini")]
    [InlineData(RagStrategy.Precise, "OpenRouter", "anthropic/claude-sonnet-4")]
    [InlineData(RagStrategy.Expert, "OpenRouter", "anthropic/claude-sonnet-4")]
    [InlineData(RagStrategy.Consensus, "OpenRouter", "anthropic/claude-sonnet-4")]
    public void SelectProvider_WithStrategy_ReturnsCorrectModelForStrategy(
        RagStrategy strategy,
        string expectedProvider,
        string expectedModel)
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.User);

        // Act
        var decision = sut.SelectProvider(user, strategy);

        // Assert
        Assert.Equal(expectedProvider, decision.ProviderName);
        Assert.Equal(expectedModel, decision.ModelId);
        Assert.Contains("Strategy:", decision.Reason);
    }

    [Fact]
    public void SelectProvider_AnonymousUser_UsesStrategyForModelSelection()
    {
        // Arrange
        var sut = CreateStrategy();

        // Act
        var decision = sut.SelectProvider(user: null, RagStrategy.Balanced);

        // Assert
        Assert.Equal("OpenRouter", decision.ProviderName);
        Assert.Equal("openai/gpt-4o-mini", decision.ModelId);
        Assert.Contains("Tier: Anonymous", decision.Reason);
    }

    [Theory]
    [InlineData("admin", LlmUserTier.Admin)]
    [InlineData("editor", LlmUserTier.Editor)]
    [InlineData("user", LlmUserTier.User)]
    public void SelectProvider_DifferentRoles_MapsToCorrectTier(string roleValue, LlmUserTier expectedTier)
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.Parse(roleValue));

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert
        Assert.Contains($"Tier: {expectedTier}", decision.Reason);
    }

    #endregion

    #region Tier Access Validation Tests

    [Fact]
    public void SelectProvider_TierDeniedAccess_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(LlmUserTier.User, RagStrategy.Expert, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockTierAccessService
            .Setup(s => s.GetAvailableStrategiesAsync(LlmUserTier.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RagStrategy.Fast, RagStrategy.Balanced });

        var sut = CreateStrategy();
        var user = CreateUser(Role.User);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => sut.SelectProvider(user, RagStrategy.Expert));

        Assert.Contains("User", exception.Message);
        Assert.Contains("EXPERT", exception.Message);
        Assert.Contains("Available strategies:", exception.Message);
    }

    [Fact]
    public void SelectProvider_AdminTier_HasAccessToAllStrategies()
    {
        // Arrange
        var sut = CreateStrategy();
        var admin = CreateUser(Role.Admin);

        // Act - Try all strategies
        var strategies = Enum.GetValues<RagStrategy>();
        var decisions = strategies.Select(s => sut.SelectProvider(admin, s)).ToList();

        // Assert - All should succeed
        Assert.Equal(strategies.Length, decisions.Count);
        Assert.All(decisions, d => Assert.Contains("Tier: Admin", d.Reason));
    }

    [Fact]
    public void SelectProvider_ValidationError_DeniesAccessSafely()
    {
        // Arrange
        _mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(It.IsAny<LlmUserTier>(), It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var sut = CreateStrategy();
        var user = CreateUser(Role.User);

        // Act & Assert - Should deny access on error (fail closed)
        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => sut.SelectProvider(user, RagStrategy.Balanced));
    }

    #endregion

    #region PreferredProvider Override Tests

    [Fact]
    public void SelectProvider_PreferredProviderSet_OverridesStrategyRouting()
    {
        // Arrange
        var preferredSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "Ollama",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["mistral"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            }
        });

        var sut = new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockServiceScopeFactory.Object,
            preferredSettings,
            _logger);

        var user = CreateUser(Role.Admin);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Expert);

        // Assert - Should use PreferredProvider instead of strategy routing
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("mistral", decision.ModelId);
        Assert.Contains("PreferredProvider override", decision.Reason);
    }

    [Fact]
    public void SelectProvider_PreferredProviderEmpty_UsesStrategyRouting()
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.Admin);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Expert);

        // Assert - Should use strategy routing
        Assert.DoesNotContain("PreferredProvider", decision.Reason);
        Assert.Contains("Strategy:", decision.Reason);
    }

    [Fact]
    public void SelectProvider_PreferredProviderDisabled_UsesStrategyRouting()
    {
        // Arrange
        var disabledPreferredSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "Ollama",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434", Models = ["mistral"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            }
        });

        var sut = new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockServiceScopeFactory.Object,
            disabledPreferredSettings,
            _logger);

        var user = CreateUser(Role.Admin);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Expert);

        // Assert - Should use strategy routing (preferred is disabled)
        Assert.DoesNotContain("PreferredProvider", decision.Reason);
    }

    #endregion

    #region Provider Fallback Tests

    [Fact]
    public void SelectProvider_PrimaryProviderDisabled_FallsBackToAlternative()
    {
        // Arrange
        var disabledOpenRouterSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" },
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["llama3:8b"] }
            }
        });

        _mockStrategyMappingService
            .Setup(s => s.GetFallbackModelsAsync(RagStrategy.Balanced, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "llama3:8b" });

        var sut = new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockServiceScopeFactory.Object,
            disabledOpenRouterSettings,
            _logger);

        var user = CreateUser(Role.User);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert - Should fallback to Ollama
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Contains("Fallback from OpenRouter", decision.Reason);
    }

    [Fact]
    public void SelectProvider_AllProvidersDisabled_ThrowsException()
    {
        // Arrange
        var allDisabledSettings = Options.Create(new AiProviderSettings
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = false, BaseUrl = "http://localhost:11434" },
                ["OpenRouter"] = new() { Enabled = false, BaseUrl = "https://openrouter.ai/api/v1" }
            }
        });

        var sut = new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockServiceScopeFactory.Object,
            allDisabledSettings,
            _logger);

        var user = CreateUser(Role.User);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => sut.SelectProvider(user, RagStrategy.Balanced));

        Assert.Contains("Both AI providers are disabled", exception.Message);
    }

    #endregion

    #region Budget Mode Override Tests

    [Fact]
    public void SelectProvider_BudgetModeActive_DowngradesExpensiveModel()
    {
        // Arrange
        var mockOverride = new Mock<ILlmModelOverrideService>();
        mockOverride.Setup(s => s.IsInBudgetMode()).Returns(true);
        mockOverride.Setup(s => s.GetOverrideModel("anthropic/claude-sonnet-4"))
            .Returns("meta-llama/llama-3.3-70b-instruct:free");

        var sut = CreateStrategy(mockOverride.Object);
        var user = CreateUser(Role.Admin);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Expert);

        // Assert
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", decision.ModelId);
        Assert.Contains("Budget mode", decision.Reason);
        mockOverride.Verify(s => s.IsInBudgetMode(), Times.Once);
        mockOverride.Verify(s => s.GetOverrideModel("anthropic/claude-sonnet-4"), Times.Once);
    }

    [Fact]
    public void SelectProvider_BudgetModeInactive_UsesNormalModel()
    {
        // Arrange
        var mockOverride = new Mock<ILlmModelOverrideService>();
        mockOverride.Setup(s => s.IsInBudgetMode()).Returns(false);

        var sut = CreateStrategy(mockOverride.Object);
        var user = CreateUser(Role.Admin);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Expert);

        // Assert
        Assert.Equal("anthropic/claude-sonnet-4", decision.ModelId);
        Assert.DoesNotContain("Budget mode", decision.Reason);
        mockOverride.Verify(s => s.GetOverrideModel(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SelectProvider_BudgetModeActive_NoMappingExists_KeepsOriginalModel()
    {
        // Arrange
        var mockOverride = new Mock<ILlmModelOverrideService>();
        mockOverride.Setup(s => s.IsInBudgetMode()).Returns(true);
        mockOverride.Setup(s => s.GetOverrideModel(It.IsAny<string>()))
            .Returns((string model) => model); // No mapping = return same model

        var sut = CreateStrategy(mockOverride.Object);
        var user = CreateUser(Role.User);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Fast);

        // Assert - Original model kept when no mapping exists
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", decision.ModelId);
        Assert.DoesNotContain("Budget mode:", decision.Reason); // No downgrade occurred
    }

    #endregion

    #region Strategy Model Mapping Integration Tests

    [Fact]
    public void SelectProvider_CustomStrategyMapping_UsesConfiguredModel()
    {
        // Arrange
        _mockStrategyMappingService
            .Setup(s => s.GetModelForStrategyAsync(RagStrategy.Balanced, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Ollama", "custom-local-model:latest"));

        var sut = CreateStrategy();
        var user = CreateUser(Role.User);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert
        Assert.Equal("Ollama", decision.ProviderName);
        Assert.Equal("custom-local-model:latest", decision.ModelId);
    }

    [Fact]
    public void SelectProvider_StrategyMappingFails_UsesDefaultMapping()
    {
        // Arrange
        _mockStrategyMappingService
            .Setup(s => s.GetModelForStrategyAsync(RagStrategy.Balanced, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var sut = CreateStrategy();
        var user = CreateUser(Role.User);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert - Should fall back to default
        Assert.Equal("OpenRouter", decision.ProviderName);
        Assert.Equal("openai/gpt-4o-mini", decision.ModelId);
    }

    #endregion

    #region Routing Decision Metadata Tests

    [Fact]
    public void SelectProvider_Decision_ContainsStrategyAndTierInfo()
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.Editor);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Precise);

        // Assert
        Assert.NotNull(decision.Reason);
        Assert.Contains("Strategy: PRECISE", decision.Reason);
        Assert.Contains("Tier: Editor", decision.Reason);
    }

    [Fact]
    public void SelectProvider_AnonymousUser_IncludesAnonymousTierInReason()
    {
        // Arrange
        var sut = CreateStrategy();

        // Act
        var decision = sut.SelectProvider(user: null, RagStrategy.Fast);

        // Assert
        Assert.Contains("Tier: Anonymous", decision.Reason);
    }

    #endregion

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
