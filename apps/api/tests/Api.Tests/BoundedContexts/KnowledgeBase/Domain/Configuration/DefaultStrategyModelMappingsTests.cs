using Api.BoundedContexts.KnowledgeBase.Domain.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Configuration;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class DefaultStrategyModelMappingsTests
{
    [Fact]
    public void Mappings_ContainsAllRagStrategies()
    {
        // Arrange
        var allStrategies = Enum.GetValues<RagStrategy>();

        // Act & Assert
        foreach (var strategy in allStrategies)
        {
            DefaultStrategyModelMappings.Mappings.Should().ContainKey(strategy,
                $"because all RagStrategy values must have default mappings");
        }
    }

    [Fact]
    public void Mappings_Fast_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Fast];

        // Assert
        mapping.Provider.Should().Be("OpenRouter");
        mapping.PrimaryModel.Should().Be("meta-llama/llama-3.3-70b-instruct");
        mapping.FallbackModels.Should().BeEmpty();
        mapping.IsCustomizable.Should().BeFalse();
        mapping.EstimatedCostPer1KTokens.Should().Be(0.001m);
    }

    [Fact]
    public void Mappings_Balanced_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Balanced];

        // Assert
        mapping.Provider.Should().Be("DeepSeek");
        mapping.PrimaryModel.Should().Be("deepseek-flash");
        mapping.FallbackModels.Should().ContainSingle()
            .Which.Should().Be("meta-llama/llama-3.3-70b-instruct");
        mapping.IsCustomizable.Should().BeFalse();
        mapping.EstimatedCostPer1KTokens.Should().Be(0.01m);
    }

    [Fact]
    public void Mappings_Precise_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Precise];

        // Assert
        mapping.Provider.Should().Be("Anthropic");
        mapping.PrimaryModel.Should().Be("anthropic/claude-sonnet-4.5");
        mapping.FallbackModels.Should().ContainSingle()
            .Which.Should().Be("openai/gpt-4o-mini");
        mapping.IsCustomizable.Should().BeFalse();
        mapping.EstimatedCostPer1KTokens.Should().Be(0.13m);
    }

    [Fact]
    public void Mappings_Expert_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Expert];

        // Assert
        mapping.Provider.Should().Be("Anthropic");
        mapping.PrimaryModel.Should().Be("anthropic/claude-sonnet-4.5");
        mapping.FallbackModels.Should().ContainSingle()
            .Which.Should().Be("openai/gpt-4o");
        mapping.IsCustomizable.Should().BeFalse();
        mapping.EstimatedCostPer1KTokens.Should().Be(0.10m);
    }

    [Fact]
    public void Mappings_Consensus_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Consensus];

        // Assert
        mapping.Provider.Should().Be("Mixed");
        mapping.PrimaryModel.Should().Be("anthropic/claude-sonnet-4.5");
        mapping.FallbackModels.Should().ContainSingle()
            .Which.Should().Be("openai/gpt-4o");
        mapping.IsCustomizable.Should().BeFalse();
        mapping.EstimatedCostPer1KTokens.Should().Be(0.09m);
    }

    [Fact]
    public void Mappings_Custom_HasCorrectConfiguration()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Custom];

        // Assert
        mapping.Provider.Should().Be("Anthropic");
        mapping.PrimaryModel.Should().Be("anthropic/claude-haiku-4.5");
        mapping.FallbackModels.Should().BeEmpty();
        mapping.IsCustomizable.Should().BeTrue();
        mapping.EstimatedCostPer1KTokens.Should().BeNull("because custom strategy has variable cost");
    }

    [Fact]
    public void GetMapping_WithValidStrategy_ReturnsMapping()
    {
        // Arrange
        var strategy = RagStrategy.Precise;

        // Act
        var mapping = DefaultStrategyModelMappings.GetMapping(strategy);

        // Assert
        mapping.Should().NotBeNull();
        mapping.Provider.Should().Be("Anthropic");
        mapping.PrimaryModel.Should().Be("anthropic/claude-sonnet-4.5");
    }

    [Theory]
    [InlineData(RagStrategy.Fast)]
    [InlineData(RagStrategy.Balanced)]
    [InlineData(RagStrategy.Precise)]
    [InlineData(RagStrategy.Expert)]
    [InlineData(RagStrategy.Consensus)]
    [InlineData(RagStrategy.Custom)]
    public void GetMapping_WithAnyStrategy_ReturnsValidMapping(RagStrategy strategy)
    {
        // Act
        var mapping = DefaultStrategyModelMappings.GetMapping(strategy);

        // Assert
        mapping.Should().NotBeNull();
        mapping.Provider.Should().NotBeNullOrWhiteSpace();
        mapping.PrimaryModel.Should().NotBeNullOrWhiteSpace();
        mapping.FallbackModels.Should().NotBeNull();
    }

    [Fact]
    public void HasMapping_WithValidStrategy_ReturnsTrue()
    {
        // Act & Assert
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Fast).Should().BeTrue();
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Balanced).Should().BeTrue();
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Precise).Should().BeTrue();
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Expert).Should().BeTrue();
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Consensus).Should().BeTrue();
        DefaultStrategyModelMappings.HasMapping(RagStrategy.Custom).Should().BeTrue();
    }

    [Fact]
    public void Mappings_AllNonCustomStrategies_AreNotCustomizable()
    {
        // Arrange
        var nonCustomStrategies = new[]
        {
            RagStrategy.Fast,
            RagStrategy.Balanced,
            RagStrategy.Precise,
            RagStrategy.Expert,
            RagStrategy.Consensus
        };

        // Act & Assert
        foreach (var strategy in nonCustomStrategies)
        {
            var mapping = DefaultStrategyModelMappings.Mappings[strategy];
            mapping.IsCustomizable.Should().BeFalse(
                $"because {strategy} is a predefined strategy");
        }
    }

    [Fact]
    public void Mappings_Custom_IsCustomizable()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Custom];

        // Assert
        mapping.IsCustomizable.Should().BeTrue();
    }

    [Fact]
    public void Mappings_Fast_IsCheaperThanBalanced()
    {
        // Act
        var fast = DefaultStrategyModelMappings.Mappings[RagStrategy.Fast];
        var balanced = DefaultStrategyModelMappings.Mappings[RagStrategy.Balanced];

        // Assert — #4179: le varianti «:free» non esistono più, FAST usa il modello a pagamento più economico
        fast.EstimatedCostPer1KTokens.Should().BeLessThan(balanced.EstimatedCostPer1KTokens!.Value);
    }

    [Fact]
    public void Mappings_AllStrategies_HaveValidProviders()
    {
        // Arrange
        var validProviders = new[] { "OpenRouter", "Anthropic", "DeepSeek", "Mixed" };

        // Act & Assert
        foreach (var kvp in DefaultStrategyModelMappings.Mappings)
        {
            kvp.Value.Provider.Should().BeOneOf(validProviders,
                $"because {kvp.Key} must use a valid provider");
        }
    }

    [Fact]
    public void Mappings_Balanced_UsesDeepSeek()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Balanced];

        // Assert
        mapping.Provider.Should().Be("DeepSeek",
            "because BALANCED strategy is optimized for cost-efficiency with DeepSeek");
        mapping.PrimaryModel.Should().Contain("deepseek");
    }

    [Fact]
    public void Mappings_PreciseAndExpert_UseAnthropic()
    {
        // Act
        var preciseMapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Precise];
        var expertMapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Expert];

        // Assert
        preciseMapping.Provider.Should().Be("Anthropic");
        expertMapping.Provider.Should().Be("Anthropic");
        preciseMapping.PrimaryModel.Should().Contain("claude-sonnet-4.5");
        expertMapping.PrimaryModel.Should().Contain("claude-sonnet-4.5");
    }

    /// <summary>
    /// #4179: nessuna mappa predefinita nomina un modello che il provider ha dismesso. Le fonti
    /// sono quelle della issue: il changelog di DeepSeek per <c>deepseek-chat</c>, il listino di
    /// OpenRouter del 2026-10-10 per gli altri.
    /// </summary>
    [Fact]
    public void Mappings_NameNoModelTheProviderRetired()
    {
        string[] retired =
        [
            "deepseek-chat",
            "google/gemini-pro",
            .. Api.BoundedContexts.SystemConfiguration.Application.Commands.SeedAiModelsCommandHandler.RetiredModels
                .Select(m => m.ModelId)
        ];

        var named = DefaultStrategyModelMappings.Mappings
            .SelectMany(kvp => kvp.Value.FallbackModels.Prepend(kvp.Value.PrimaryModel)
                .Select(model => (Strategy: kvp.Key, Model: model)));

        named.Where(n => retired.Contains(n.Model, StringComparer.Ordinal))
            .Should().BeEmpty("una chiamata a un modello che il provider non elenca più non può riuscire");
    }

    [Fact]
    public void Mappings_Custom_UsesHaiku()
    {
        // Act
        var mapping = DefaultStrategyModelMappings.Mappings[RagStrategy.Custom];

        // Assert
        mapping.PrimaryModel.Should().Contain("claude-haiku-4.5",
            "because CUSTOM strategy prioritizes cost-efficiency with Haiku");
    }
}
