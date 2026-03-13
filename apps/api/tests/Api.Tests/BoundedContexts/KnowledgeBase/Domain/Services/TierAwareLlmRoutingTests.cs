using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
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
/// Unit tests for E4-2: Tier-Aware LLM Routing.
/// Validates that user subscription tier (free/premium/enterprise) is considered
/// when mapping users to LlmUserTier for model routing decisions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class TierAwareLlmRoutingTests
{
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _logger;
    private readonly IOptions<AiProviderSettings> _aiSettings;
    private readonly Mock<IStrategyModelMappingService> _mockStrategyMappingService;
    private readonly Mock<ITierStrategyAccessService> _mockTierAccessService;
    private readonly Mock<ILlmModelOverrideService> _mockOverrideService;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public TierAwareLlmRoutingTests()
    {
        _logger = Mock.Of<ILogger<HybridAdaptiveRoutingStrategy>>();

        _aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://localhost:11434", Models = ["llama3:8b"] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["gpt-4"] }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        });

        _mockStrategyMappingService = new Mock<IStrategyModelMappingService>();
        _mockTierAccessService = new Mock<ITierStrategyAccessService>();
        _mockOverrideService = new Mock<ILlmModelOverrideService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ITierStrategyAccessService)))
            .Returns(_mockTierAccessService.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);

        // Default: all tiers have access to all strategies
        _mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(It.IsAny<LlmUserTier>(), It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default model mappings
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

        _mockOverrideService.Setup(s => s.IsInBudgetMode()).Returns(false);
    }

    private HybridAdaptiveRoutingStrategy CreateStrategy()
    {
        return new HybridAdaptiveRoutingStrategy(
            _mockStrategyMappingService.Object,
            _mockScopeFactory.Object,
            _aiSettings,
            _logger,
            _mockOverrideService.Object);
    }

    // ─── MapUserToLlmTier direct tests ──────────────────────────────────────

    [Fact]
    public void MapUserToLlmTier_FreeTierUser_ReturnsUserTier()
    {
        // Arrange
        var user = CreateUser(Role.User, UserTier.Free);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.User, result);
    }

    [Fact]
    public void MapUserToLlmTier_PremiumTierUser_ReturnsPremiumTier()
    {
        // Arrange
        var user = CreateUser(Role.User, UserTier.Premium);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.Premium, result);
    }

    [Fact]
    public void MapUserToLlmTier_ProTierUser_ReturnsPremiumTier()
    {
        // Arrange — "pro" is an alias for "premium"
        var user = CreateUser(Role.User, UserTier.Pro);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.Premium, result);
    }

    [Fact]
    public void MapUserToLlmTier_EnterpriseTierUser_ReturnsPremiumTier()
    {
        // Arrange
        var user = CreateUser(Role.User, UserTier.Enterprise);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.Premium, result);
    }

    [Fact]
    public void MapUserToLlmTier_AdminRole_AlwaysReturnsAdminTier()
    {
        // Arrange — admin role always wins, regardless of subscription tier
        var user = CreateUser(Role.Admin, UserTier.Free);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.Admin, result);
    }

    [Fact]
    public void MapUserToLlmTier_EditorRole_ReturnsEditorTier()
    {
        // Arrange
        var user = CreateUser(Role.Editor, UserTier.Free);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.Editor, result);
    }

    [Fact]
    public void MapUserToLlmTier_NullUser_DefaultsToUserTier()
    {
        // Arrange & Act — null user is internal pipeline call
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(null);

        // Assert
        Assert.Equal(LlmUserTier.User, result);
    }

    [Fact]
    public void MapUserToLlmTier_NormalTierUser_ReturnsUserTier()
    {
        // Arrange — "normal" tier maps to User LLM tier (not Premium)
        var user = CreateUser(Role.User, UserTier.Normal);

        // Act
        var result = HybridAdaptiveRoutingStrategy.MapUserToLlmTier(user);

        // Assert
        Assert.Equal(LlmUserTier.User, result);
    }

    // ─── Integration via SelectProvider ──────────────────────────────────────

    [Fact]
    public void SelectProvider_FreeTierUser_RoutesWithUserTier()
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.User, UserTier.Free);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert
        Assert.Contains("Tier: User", decision.Reason);
    }

    [Fact]
    public void SelectProvider_PremiumTierUser_RoutesWithPremiumTier()
    {
        // Arrange
        var sut = CreateStrategy();
        var user = CreateUser(Role.User, UserTier.Premium);

        // Act
        var decision = sut.SelectProvider(user, RagStrategy.Balanced);

        // Assert
        Assert.Contains("Tier: Premium", decision.Reason);
    }

    [Fact]
    public void SelectProvider_AdminAlwaysAdmin_RegardlessOfSubscriptionTier()
    {
        // Arrange — admin with free subscription still gets Admin LLM tier
        var sut = CreateStrategy();
        var admin = CreateUser(Role.Admin, UserTier.Free);

        // Act
        var decision = sut.SelectProvider(admin, RagStrategy.Expert);

        // Assert
        Assert.Contains("Tier: Admin", decision.Reason);
    }

    [Fact]
    public void SelectProvider_PremiumTierDeniedExpertStrategy_ThrowsWithAvailableStrategies()
    {
        // Arrange — Premium tier denied access to Expert strategy
        _mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(LlmUserTier.Premium, RagStrategy.Expert, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockTierAccessService
            .Setup(s => s.GetAvailableStrategiesAsync(LlmUserTier.Premium, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise });

        var sut = CreateStrategy();
        var user = CreateUser(Role.User, UserTier.Premium);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => sut.SelectProvider(user, RagStrategy.Expert));

        Assert.Contains("Premium", exception.Message);
        Assert.Contains("EXPERT", exception.Message);
    }

    // ─── Helper ─────────────────────────────────────────────────────────────

    private static AuthUser CreateUser(Role role, UserTier? tier = null)
    {
        var email = Email.Parse($"test.{role.Value}@meepleai.dev");
        var password = PasswordHash.Create("TestPass123!");

        return new AuthUser(
            Guid.NewGuid(),
            email,
            $"Test {role.Value}",
            password,
            role,
            tier);
    }
}
