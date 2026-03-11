using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

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
        Assert.NotEmpty(models);
        Assert.True(models.Count > 10, "Should have at least 10 models configured");
    }

    [Fact]
    public void GetAllModels_ContainsModelsFromAllTiers()
    {
        // Act
        var models = _service.GetAllModels();

        // Assert
        Assert.Contains(models, m => m.Tier == ModelTier.Free);
        Assert.Contains(models, m => m.Tier == ModelTier.Normal);
        Assert.Contains(models, m => m.Tier == ModelTier.Premium);
        Assert.Contains(models, m => m.Tier == ModelTier.Custom);
    }

    [Fact]
    public void GetAllModels_AllModelsHaveRequiredFields()
    {
        // Act
        var models = _service.GetAllModels();

        // Assert
        foreach (var model in models)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id), $"Model should have Id");
            Assert.False(string.IsNullOrWhiteSpace(model.Name), $"Model {model.Id} should have Name");
            Assert.False(string.IsNullOrWhiteSpace(model.Provider), $"Model {model.Id} should have Provider");
            Assert.True(model.MaxTokens > 0, $"Model {model.Id} should have positive MaxTokens");
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
        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.Equal(ModelTier.Free, m.Tier));
    }

    [Fact]
    public void GetModelsByTier_Normal_ReturnsFreeAndNormalModels()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Normal);

        // Assert
        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.True(m.Tier <= ModelTier.Normal));
        Assert.Contains(models, m => m.Tier == ModelTier.Free);
        Assert.Contains(models, m => m.Tier == ModelTier.Normal);
    }

    [Fact]
    public void GetModelsByTier_Premium_ReturnsUpToPremiumModels()
    {
        // Act
        var models = _service.GetModelsByTier(ModelTier.Premium);

        // Assert
        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.True(m.Tier <= ModelTier.Premium));
        Assert.DoesNotContain(models, m => m.Tier == ModelTier.Custom);
    }

    [Fact]
    public void GetModelsByTier_Custom_ReturnsAllModels()
    {
        // Arrange
        var allModels = _service.GetAllModels();

        // Act
        var customTierModels = _service.GetModelsByTier(ModelTier.Custom);

        // Assert
        Assert.Equal(allModels.Count, customTierModels.Count);
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
                Assert.True(prev.Tier < curr.Tier,
                    $"Lower tier should come first: {prev.Tier} vs {curr.Tier}");
            }
            else
            {
                // Same tier: cloud providers (sort key 0) before ollama (sort key 1)
                var prevIsOllama = string.Equals(prev.Provider, "ollama", StringComparison.Ordinal);
                var currIsOllama = string.Equals(curr.Provider, "ollama", StringComparison.Ordinal);

                if (prevIsOllama != currIsOllama)
                {
                    // Cloud before Ollama
                    Assert.False(prevIsOllama,
                        $"Cloud models should come before Ollama within same tier: {prev.Name} ({prev.Provider}) vs {curr.Name} ({curr.Provider})");
                }
                else
                {
                    // Same provider group: should be ordered by name
                    Assert.True(
                        string.Compare(prev.Name, curr.Name, StringComparison.Ordinal) <= 0,
                        $"Models with same tier and provider group should be ordered by name: {prev.Name} vs {curr.Name}");
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
        Assert.NotNull(model);
        Assert.Equal(modelId, model.Id);
        Assert.Equal(ModelTier.Free, model.Tier);
    }

    [Fact]
    public void GetModelById_NonExistingModel_ReturnsNull()
    {
        // Arrange
        var modelId = "non-existent/model";

        // Act
        var model = _service.GetModelById(modelId);

        // Assert
        Assert.Null(model);
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
        Assert.NotNull(model);
        Assert.Equal(expectedTier, model.Tier);
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
        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(userTier, result.UserTier);
        Assert.Equal(modelId, result.ModelId);
    }

    [Fact]
    public void ValidateUserTierForModel_NonExistentModel_ReturnsInvalid()
    {
        // Arrange
        var modelId = "non-existent/model";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Custom, modelId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.RequiredTier);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public void ValidateUserTierForModel_Success_ReturnsCorrectRequiredTier()
    {
        // Arrange
        var modelId = "anthropic/claude-3.5-haiku";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Premium, modelId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ModelTier.Premium, result.RequiredTier);
        Assert.Equal("Access granted", result.Message);
    }

    [Fact]
    public void ValidateUserTierForModel_Failure_ReturnsDescriptiveMessage()
    {
        // Arrange
        var modelId = "anthropic/claude-3.5-haiku";

        // Act
        var result = _service.ValidateUserTierForModel(ModelTier.Free, modelId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Premium", result.Message);
        Assert.Contains("Free", result.Message);
        Assert.Contains("Access denied", result.Message);
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
        Assert.All(freeModels, m =>
        {
            Assert.Equal(0m, m.CostPer1kInputTokens);
            Assert.Equal(0m, m.CostPer1kOutputTokens);
            Assert.True(m.IsFree);
        });
    }

    [Fact]
    public void GetAllModels_PaidModels_HavePositiveCost()
    {
        // Act
        var paidModels = _service.GetAllModels()
            .Where(m => m.Tier > ModelTier.Free && m.Provider != "ollama");

        // Assert
        Assert.All(paidModels, m =>
        {
            Assert.True(
                m.CostPer1kInputTokens > 0 || m.CostPer1kOutputTokens > 0,
                $"Paid model {m.Id} should have positive cost");
        });
    }

    #endregion
}
