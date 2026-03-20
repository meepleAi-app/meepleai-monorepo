using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for ModelConfigurationService
/// Issue #3377: Models Tier Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelConfigurationServiceTests
{
    private readonly IModelConfigurationService _service;

    public ModelConfigurationServiceTests()
    {
        var logger = Mock.Of<ILogger<ModelConfigurationService>>();
        _service = new ModelConfigurationService(logger);
    }

    #region GetAllModels Tests

    [Fact]
    public void GetAllModels_ReturnsNonEmptyList()
    {
        // Act
        var models = _service.GetAllModels();

        // Assert
        models.Should().NotBeEmpty();
        (models.Count > 10).Should().BeTrue("Should have at least 10 models configured");
    }

    [Fact]
    public void GetAllModels_ContainsModelsFromAllTiers()
    {
        // Act
        var models = _service.GetAllModels();

        // Assert
        models.Should().Contain(m => m.Tier == ModelTier.Free);
        models.Should().Contain(m => m.Tier == ModelTier.Normal);
        models.Should().Contain(m => m.Tier == ModelTier.Premium);
        models.Should().Contain(m => m.Tier == ModelTier.Custom);
    }

    [Fact]
    public void GetAllModels_AllModelsHaveRequiredFields()
    {
        // Act
        var models = _service.GetAllModels();

        // Assert
        foreach (var model in models)
        {
            string.IsNullOrWhiteSpace(model.Id).Should().BeFalse($"Model should have Id");
            string.IsNullOrWhiteSpace(model.Name).Should().BeFalse($"Model {model.Id} should have Name");
            string.IsNullOrWhiteSpace(model.Provider).Should().BeFalse($"Model {model.Id} should have Provider");
            (model.MaxTokens > 0).Should().BeTrue($"Model {model.Id} should have positive MaxTokens");
        }
    }

    #endregion

    #region GetModelsByTier Tests

    [Fact]
    public void GetModelsByTier_Free_ReturnsOnlyFreeModels()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Free);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().AllSatisfy(m => m.Tier.Should().Be(ModelTier.Free));
    }

    [Fact]
    public void GetModelsByTier_Normal_ReturnsFreeAndNormalModels()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Normal);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().AllSatisfy(m => (m.Tier <= ModelTier.Normal).Should().BeTrue());
        models.Should().Contain(m => m.Tier == ModelTier.Free);
        models.Should().Contain(m => m.Tier == ModelTier.Normal);
    }

    [Fact]
    public void GetModelsByTier_Premium_ReturnsUpToPremiumModels()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Premium);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().AllSatisfy(m => (m.Tier <= ModelTier.Premium).Should().BeTrue());
        models.Should().NotContain(m => m.Tier == ModelTier.Custom);
    }

    [Fact]
    public void GetModelsByTier_Custom_ReturnsAllModels()
    {
        // Arrange
        var allModels = _service.GetAllModels();

        // Act
        var customTierModels = _service.GetModelsByTier(ModelTier.Custom);

        // Assert
        customTierModels.Count.Should().Be(allModels.Count);
    }

    [Fact]
    public void GetModelsByTier_ResultsAreOrderedByTierThenProviderThenName()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Premium);

        // Assert — ordering is: Tier asc → Cloud before Ollama → Name asc
        for (int i = 1; i < models.Count; i++)
        {
            var prev = models[i - 1];
            var curr = models[i];

            if (prev.Tier != curr.Tier)
            {
                // Different tier: lower tier should come first
                (prev.Tier < curr.Tier).Should().BeTrue($"Lower tier should come first: {prev.Tier} vs {curr.Tier}");
            }
            else
            {
                // Same tier: cloud providers (sort key 0) before ollama (sort key 1)
                var prevIsOllama = string.Equals(prev.Provider, "ollama", StringComparison.Ordinal);
                var currIsOllama = string.Equals(curr.Provider, "ollama", StringComparison.Ordinal);

                if (prevIsOllama != currIsOllama)
                {
                    // Cloud before Ollama
                    prevIsOllama.Should().BeFalse($"Cloud models should come before Ollama within same tier: {prev.Name} ({prev.Provider}) vs {curr.Name} ({curr.Provider})");
                }
                else
                {
                    // Same provider group: should be ordered by name
                    (string.Compare(prev.Name, curr.Name, StringComparison.Ordinal) <= 0).Should().BeTrue($"Models with same tier and provider group should be ordered by name: {prev.Name} vs {curr.Name}");
                }
            }
        }
    }

    #endregion

    #region GetModelById Tests

    [Fact]
    public void GetModelById_ExistingModel_ReturnsModel()
    {
        // Arrange
        var modelId = "meta-llama/llama-3.3-70b-instruct:free";

        // Act
        var model = _service.GetModelById(modelId);

        // Assert
        model.Should().NotBeNull();
        model.Id.Should().Be(modelId);
        model.Tier.Should().Be(ModelTier.Free);
    }

    [Fact]
    public void GetModelById_NonExistingModel_ReturnsNull()
    {
        // Arrange
        var modelId = "non-existent/model";

        // Act
        var model = _service.GetModelById(modelId);

        // Assert
        model.Should().BeNull();
    }

    [Theory]
    [InlineData("anthropic/claude-3.5-haiku", ModelTier.Premium)]
    [InlineData("openai/gpt-4o-mini", ModelTier.Premium)]
    [InlineData("deepseek/deepseek-chat", ModelTier.Normal)]
    [InlineData("llama3:8b", ModelTier.Free)]
    public void GetModelById_VariousModels_ReturnsCorrectTier(string modelId, ModelTier expectedTier)
    {
        // Act
        var model = _service.GetModelById(modelId);

        // Assert
        model.Should().NotBeNull();
        model.Tier.Should().Be(expectedTier);
    }

    #endregion

    #region ValidateUserTierForModel Tests

    [Theory]
    [InlineData(ModelTier.Free, "meta-llama/llama-3.3-70b-instruct:free", true)]
    [InlineData(ModelTier.Free, "deepseek/deepseek-chat", false)]
    [InlineData(ModelTier.Free, "anthropic/claude-3.5-haiku", false)]
    [InlineData(ModelTier.Normal, "meta-llama/llama-3.3-70b-instruct:free", true)]
    [InlineData(ModelTier.Normal, "deepseek/deepseek-chat", true)]
    [InlineData(ModelTier.Normal, "anthropic/claude-3.5-haiku", false)]
    [InlineData(ModelTier.Premium, "meta-llama/llama-3.3-70b-instruct:free", true)]
    [InlineData(ModelTier.Premium, "deepseek/deepseek-chat", true)]
    [InlineData(ModelTier.Premium, "anthropic/claude-3.5-haiku", true)]
    [InlineData(ModelTier.Custom, "anthropic/claude-3-opus", true)]
    public void ValidateUserTierForModel_VariousCombinations_ReturnsExpectedResult(
        ModelTier userTier, string modelId, bool expectedValid)
    {
        // Act
        var result = _service.ValidateUserTierForModel(userTier, modelId);

        // Assert
        result.IsValid.Should().Be(expectedValid);
        result.UserTier.Should().Be(userTier);
        result.ModelId.Should().Be(modelId);
    }

    [Fact]
    public void ValidateUserTierForModel_NonExistentModel_ReturnsInvalid()
    {
        // Arrange
        var modelId = "non-existent/model";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Custom, modelId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.RequiredTier.Should().BeNull();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public void ValidateUserTierForModel_Success_ReturnsCorrectRequiredTier()
    {
        // Arrange
        var modelId = "anthropic/claude-3.5-haiku";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Premium, modelId);

        // Assert
        result.IsValid.Should().BeTrue();
        result.RequiredTier.Should().Be(ModelTier.Premium);
        result.Message.Should().Be("Access granted");
    }

    [Fact]
    public void ValidateUserTierForModel_Failure_ReturnsDescriptiveMessage()
    {
        // Arrange
        var modelId = "anthropic/claude-3.5-haiku";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Free, modelId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("Premium");
        result.Message.Should().Contain("Free");
        result.Message.Should().Contain("Access denied");
    }

    #endregion

    #region Model Pricing Tests

    [Fact]
    public void GetAllModels_FreeModels_HaveZeroCost()
    {
        // Act
        var freeModels = _service.GetModelsByTier(ModelTier.Free)
            .Where(m => m.Tier == ModelTier.Free);

        // Assert
        freeModels.Should().AllSatisfy(m =>
        {
            m.CostPer1kInputTokens.Should().Be(0m);
            m.CostPer1kOutputTokens.Should().Be(0m);
            m.IsFree.Should().BeTrue();
        });
    }

    [Fact]
    public void GetAllModels_PaidModels_HavePositiveCost()
    {
        // Act
        var paidModels = _service.GetAllModels()
            .Where(m => m.Tier > ModelTier.Free && m.Provider != "ollama");

        // Assert
        paidModels.Should().AllSatisfy(m =>
        {
            (m.CostPer1kInputTokens > 0 || m.CostPer1kOutputTokens > 0).Should().BeTrue($"Paid model {m.Id} should have positive cost");
        });
    }

    #endregion
}
