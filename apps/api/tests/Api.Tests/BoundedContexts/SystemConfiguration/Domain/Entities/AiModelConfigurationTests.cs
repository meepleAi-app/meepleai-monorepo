using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Tests for the AiModelConfiguration entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 30
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiModelConfigurationTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsConfiguration()
    {
        // Arrange
        var modelId = "gpt-4o";
        var displayName = "GPT-4 Omni";
        var provider = "openrouter";
        var priority = 1;
        var settings = ModelSettings.Default;

        // Act
        var config = AiModelConfiguration.Create(modelId, displayName, provider, priority, settings);

        // Assert
        config.Id.Should().NotBe(Guid.Empty);
        config.ModelId.Should().Be(modelId);
        config.DisplayName.Should().Be(displayName);
        config.Provider.Should().Be(provider);
        config.Priority.Should().Be(priority);
        config.Settings.Should().Be(settings);
        config.IsActive.Should().BeTrue();
        config.IsPrimary.Should().BeFalse();
        config.ApplicableTier.Should().BeNull();
        config.EnvironmentType.Should().Be(LlmEnvironmentType.Production);
        config.IsDefaultForTier.Should().BeFalse();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeNull();
        config.Usage.Should().Be(UsageStats.Empty);
    }

    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var settings = new ModelSettings(2048, 0.5m, new ModelPricing(2.5m, 10m));

        // Act
        var config = AiModelConfiguration.Create(
            modelId: "claude-3-sonnet",
            displayName: "Claude 3 Sonnet",
            provider: "anthropic",
            priority: 2,
            settings: settings,
            isActive: false,
            isPrimary: true,
            applicableTier: LlmUserTier.Premium,
            environmentType: LlmEnvironmentType.Test,
            isDefaultForTier: true);

        // Assert
        config.IsActive.Should().BeFalse();
        config.IsPrimary.Should().BeTrue();
        config.ApplicableTier.Should().Be(LlmUserTier.Premium);
        config.EnvironmentType.Should().Be(LlmEnvironmentType.Test);
        config.IsDefaultForTier.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyModelId_ThrowsArgumentException(string? modelId)
    {
        // Act
        var action = () => AiModelConfiguration.Create(
            modelId!,
            "Display Name",
            "provider",
            1,
            ModelSettings.Default);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ModelId cannot be empty*")
            .WithParameterName("modelId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDisplayName_ThrowsArgumentException(string? displayName)
    {
        // Act
        var action = () => AiModelConfiguration.Create(
            "model-id",
            displayName!,
            "provider",
            1,
            ModelSettings.Default);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DisplayName cannot be empty*")
            .WithParameterName("displayName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyProvider_ThrowsArgumentException(string? provider)
    {
        // Act
        var action = () => AiModelConfiguration.Create(
            "model-id",
            "Display Name",
            provider!,
            1,
            ModelSettings.Default);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Provider cannot be empty*")
            .WithParameterName("provider");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidPriority_ThrowsArgumentException(int priority)
    {
        // Act
        var action = () => AiModelConfiguration.Create(
            "model-id",
            "Display Name",
            "provider",
            priority,
            ModelSettings.Default);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Priority must be >= 1*")
            .WithParameterName("priority");
    }

    [Fact]
    public void Create_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AiModelConfiguration.Create(
            "model-id",
            "Display Name",
            "provider",
            1,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SetTierRouting Tests

    [Fact]
    public void SetTierRouting_WithValidData_UpdatesTierRouting()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.SetTierRouting(LlmUserTier.Admin, LlmEnvironmentType.Test, true);

        // Assert
        config.ApplicableTier.Should().Be(LlmUserTier.Admin);
        config.EnvironmentType.Should().Be(LlmEnvironmentType.Test);
        config.IsDefaultForTier.Should().BeTrue();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetTierRouting_WithNullTier_SetsGlobalConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.SetTierRouting(LlmUserTier.Premium, LlmEnvironmentType.Production, true);

        // Act
        config.SetTierRouting(null, LlmEnvironmentType.Production, false);

        // Assert
        config.ApplicableTier.Should().BeNull();
        config.IsDefaultForTier.Should().BeFalse();
    }

    #endregion

    #region SetAsDefaultForTier Tests

    [Fact]
    public void SetAsDefaultForTier_WithTrue_SetsDefault()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.SetAsDefaultForTier(true);

        // Assert
        config.IsDefaultForTier.Should().BeTrue();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetAsDefaultForTier_WithFalse_ClearsDefault()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.SetAsDefaultForTier(true);

        // Act
        config.SetAsDefaultForTier(false);

        // Assert
        config.IsDefaultForTier.Should().BeFalse();
    }

    #endregion

    #region UpdatePriority Tests

    [Fact]
    public void UpdatePriority_WithValidPriority_UpdatesPriority()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.UpdatePriority(5);

        // Assert
        config.Priority.Should().Be(5);
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void UpdatePriority_WithInvalidPriority_ThrowsArgumentException(int priority)
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdatePriority(priority);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Priority must be >= 1*")
            .WithParameterName("newPriority");
    }

    #endregion

    #region SetActive Tests

    [Fact]
    public void SetActive_WithTrue_SetsActive()
    {
        // Arrange
        var config = AiModelConfiguration.Create(
            "model-id", "Name", "provider", 1, ModelSettings.Default, isActive: false);

        // Act
        config.SetActive(true);

        // Assert
        config.IsActive.Should().BeTrue();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetActive_WithFalse_SetsInactive()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.SetActive(false);

        // Assert
        config.IsActive.Should().BeFalse();
    }

    #endregion

    #region SetPrimary Tests

    [Fact]
    public void SetPrimary_WithTrue_SetsPrimary()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.SetPrimary(true);

        // Assert
        config.IsPrimary.Should().BeTrue();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetPrimary_WithFalse_ClearsPrimary()
    {
        // Arrange
        var config = AiModelConfiguration.Create(
            "model-id", "Name", "provider", 1, ModelSettings.Default, isPrimary: true);

        // Act
        config.SetPrimary(false);

        // Assert
        config.IsPrimary.Should().BeFalse();
    }

    #endregion

    #region UpdateSettings Tests

    [Fact]
    public void UpdateSettings_WithValidData_CreatesNewSettings()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var originalPricing = config.Settings.Pricing;

        // Act
        config.UpdateSettings(2048, 0.9m);

        // Assert
        config.Settings.MaxTokens.Should().Be(2048);
        config.Settings.Temperature.Should().Be(0.9m);
        config.Settings.Pricing.Should().Be(originalPricing); // Pricing preserved
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(511)]
    [InlineData(8193)]
    public void UpdateSettings_WithInvalidMaxTokens_ThrowsArgumentException(int maxTokens)
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdateSettings(maxTokens, 0.7m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTokens must be between 512 and 8192*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void UpdateSettings_WithInvalidTemperature_ThrowsArgumentException(decimal temperature)
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdateSettings(4096, temperature);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    #endregion

    #region UpdatePricing Tests

    [Fact]
    public void UpdatePricing_WithValidData_UpdatesPricing()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var originalMaxTokens = config.Settings.MaxTokens;
        var originalTemp = config.Settings.Temperature;

        // Act
        config.UpdatePricing(5.0m, 15.0m);

        // Assert
        config.Settings.Pricing.InputPricePerMillion.Should().Be(5.0m);
        config.Settings.Pricing.OutputPricePerMillion.Should().Be(15.0m);
        config.Settings.MaxTokens.Should().Be(originalMaxTokens); // Preserved
        config.Settings.Temperature.Should().Be(originalTemp); // Preserved
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdatePricing_WithNegativeInputPrice_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdatePricing(-1.0m, 10.0m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Input price cannot be negative*");
    }

    [Fact]
    public void UpdatePricing_WithNegativeOutputPrice_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdatePricing(5.0m, -1.0m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Output price cannot be negative*");
    }

    #endregion

    #region TrackUsage Tests

    [Fact]
    public void TrackUsage_WithValidData_UpdatesUsageStats()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.TrackUsage(1000, 500, 0.025m);

        // Assert
        config.Usage.TotalRequests.Should().Be(1);
        config.Usage.TotalInputTokens.Should().Be(1000);
        config.Usage.TotalOutputTokens.Should().Be(500);
        config.Usage.TotalTokensUsed.Should().Be(1500);
        config.Usage.TotalCostUsd.Should().Be(0.025m);
        config.Usage.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TrackUsage_MultipleTimes_AccumulatesStats()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.TrackUsage(1000, 500, 0.025m);
        config.TrackUsage(2000, 1000, 0.05m);
        config.TrackUsage(500, 250, 0.01m);

        // Assert
        config.Usage.TotalRequests.Should().Be(3);
        config.Usage.TotalInputTokens.Should().Be(3500);
        config.Usage.TotalOutputTokens.Should().Be(1750);
        config.Usage.TotalTokensUsed.Should().Be(5250);
        config.Usage.TotalCostUsd.Should().Be(0.085m);
    }

    [Fact]
    public void TrackUsage_WithNegativeInputTokens_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.TrackUsage(-100, 500, 0.01m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*InputTokens cannot be negative*")
            .WithParameterName("inputTokens");
    }

    [Fact]
    public void TrackUsage_WithNegativeOutputTokens_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.TrackUsage(1000, -100, 0.01m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*OutputTokens cannot be negative*")
            .WithParameterName("outputTokens");
    }

    [Fact]
    public void TrackUsage_WithNegativeCost_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.TrackUsage(1000, 500, -0.01m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CostUsd cannot be negative*")
            .WithParameterName("costUsd");
    }

    [Fact]
    public void TrackUsage_WithZeroTokens_Works()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.TrackUsage(0, 0, 0m);

        // Assert
        config.Usage.TotalRequests.Should().Be(1);
        config.Usage.TotalInputTokens.Should().Be(0);
        config.Usage.TotalOutputTokens.Should().Be(0);
        config.Usage.TotalTokensUsed.Should().Be(0);
        config.Usage.TotalCostUsd.Should().Be(0m);
    }

    #endregion

    #region Helper Methods

    private static AiModelConfiguration CreateValidConfiguration()
    {
        return AiModelConfiguration.Create(
            "gpt-4o",
            "GPT-4 Omni",
            "openrouter",
            1,
            ModelSettings.Default);
    }

    #endregion
}
