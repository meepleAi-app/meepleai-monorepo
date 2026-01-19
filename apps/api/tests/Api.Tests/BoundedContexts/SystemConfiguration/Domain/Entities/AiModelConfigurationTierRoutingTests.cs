using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Domain tests for AiModelConfiguration tier routing functionality.
/// Issue #2596: LLM tier routing with test/production model separation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class AiModelConfigurationTierRoutingTests
{
    [Fact]
    public void SetTierRouting_WithValidValues_SetsAllProperties()
    {
        // Arrange
        var model = AiModelConfiguration.Create("gpt-4o", "GPT-4o", "OpenRouter", 1, ModelSettings.Default);

        // Act
        model.SetTierRouting(LlmUserTier.Admin, LlmEnvironmentType.Production, true);

        // Assert
        model.ApplicableTier.Should().Be(LlmUserTier.Admin);
        model.EnvironmentType.Should().Be(LlmEnvironmentType.Production);
        model.IsDefaultForTier.Should().BeTrue();
    }

    [Fact]
    public void SetTierRouting_ForTestEnvironment_SetsCorrectEnvironment()
    {
        // Arrange
        var model = AiModelConfiguration.Create("llama3:8b", "Llama 3 8B", "Ollama", 1, ModelSettings.Default);

        // Act
        model.SetTierRouting(LlmUserTier.Editor, LlmEnvironmentType.Test, true);

        // Assert
        model.EnvironmentType.Should().Be(LlmEnvironmentType.Test);
        model.ApplicableTier.Should().Be(LlmUserTier.Editor);
    }

    [Fact]
    public void SetAsDefaultForTier_True_SetsIsDefaultForTier()
    {
        // Arrange
        var model = AiModelConfiguration.Create("gpt-4o-mini", "GPT-4o Mini", "OpenRouter", 1, ModelSettings.Default);
        model.SetTierRouting(LlmUserTier.User, LlmEnvironmentType.Production, false);

        // Act
        model.SetAsDefaultForTier(true);

        // Assert
        model.IsDefaultForTier.Should().BeTrue();
    }

    [Fact]
    public void SetAsDefaultForTier_False_RemovesDefaultStatus()
    {
        // Arrange
        var model = AiModelConfiguration.Create("gpt-4o", "GPT-4o", "OpenRouter", 1, ModelSettings.Default);
        model.SetTierRouting(LlmUserTier.Admin, LlmEnvironmentType.Production, true);

        // Act
        model.SetAsDefaultForTier(false);

        // Assert
        model.IsDefaultForTier.Should().BeFalse();
    }

    [Theory]
    [InlineData(LlmUserTier.Anonymous)]
    [InlineData(LlmUserTier.User)]
    [InlineData(LlmUserTier.Editor)]
    [InlineData(LlmUserTier.Admin)]
    [InlineData(LlmUserTier.Premium)]
    public void SetTierRouting_AllUserTiers_AreSupported(LlmUserTier tier)
    {
        // Arrange
        var model = AiModelConfiguration.Create("test-model", "Test Model", "OpenRouter", 1, ModelSettings.Default);

        // Act
        model.SetTierRouting(tier, LlmEnvironmentType.Production, true);

        // Assert
        model.ApplicableTier.Should().Be(tier);
    }

    [Theory]
    [InlineData(LlmEnvironmentType.Production)]
    [InlineData(LlmEnvironmentType.Test)]
    public void SetTierRouting_BothEnvironments_AreSupported(LlmEnvironmentType environment)
    {
        // Arrange
        var model = AiModelConfiguration.Create("test-model", "Test Model", "OpenRouter", 1, ModelSettings.Default);

        // Act
        model.SetTierRouting(LlmUserTier.User, environment, true);

        // Assert
        model.EnvironmentType.Should().Be(environment);
    }

    [Fact]
    public void Create_WithTierRouting_InitializesAllFields()
    {
        // Arrange & Act
        var model = AiModelConfiguration.Create(
            "gpt-4o",
            "GPT-4o",
            "OpenRouter",
            1,
            ModelSettings.Default,
            isActive: true,
            isPrimary: false,
            applicableTier: LlmUserTier.Admin,
            environmentType: LlmEnvironmentType.Production,
            isDefaultForTier: true);

        // Assert
        model.ModelId.Should().Be("gpt-4o");
        model.DisplayName.Should().Be("GPT-4o");
        model.Provider.Should().Be("OpenRouter");
        model.Priority.Should().Be(1);
        model.ApplicableTier.Should().Be(LlmUserTier.Admin);
        model.EnvironmentType.Should().Be(LlmEnvironmentType.Production);
        model.IsDefaultForTier.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutTierRouting_HasNullApplicableTier()
    {
        // Arrange & Act
        var model = AiModelConfiguration.Create("gpt-4o", "GPT-4o", "OpenRouter", 1, ModelSettings.Default);

        // Assert
        model.ApplicableTier.Should().BeNull();
        model.EnvironmentType.Should().Be(LlmEnvironmentType.Production);
        model.IsDefaultForTier.Should().BeFalse();
    }
}
