using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetAvailableModelsQueryHandler
/// Issue #3377: Models Tier Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetAvailableModelsQueryHandlerTests
{
    private readonly Mock<IModelConfigurationService> _mockService;
    private readonly GetAvailableModelsQueryHandler _handler;

    public GetAvailableModelsQueryHandlerTests()
    {
        _mockService = new Mock<IModelConfigurationService>();
        var logger = Mock.Of<ILogger<GetAvailableModelsQueryHandler>>();
        _handler = new GetAvailableModelsQueryHandler(_mockService.Object, logger);
    }

    [Fact]
    public async Task Handle_NoTierFilter_CallsGetAllModels()
    {
        // Arrange
        var models = CreateTestModels();
        _mockService.Setup(s => s.GetAllModels()).Returns(models);

        var query = new GetAvailableModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.GetAllModels(), Times.Once);
        _mockService.Verify(s => s.GetModelsByTier(It.IsAny<ModelTier>()), Times.Never);
        Assert.Equal(models.Count, result.Models.Count);
    }

    [Theory]
    [InlineData("free", ModelTier.Free)]
    [InlineData("Free", ModelTier.Free)]
    [InlineData("FREE", ModelTier.Free)]
    [InlineData("normal", ModelTier.Normal)]
    [InlineData("premium", ModelTier.Premium)]
    [InlineData("custom", ModelTier.Custom)]
    public async Task Handle_WithTierFilter_CallsGetModelsByTier(string tierString, ModelTier expectedTier)
    {
        // Arrange
        var models = CreateTestModels().Where(m => m.Tier <= expectedTier).ToList();
        _mockService.Setup(s => s.GetModelsByTier(expectedTier)).Returns(models);

        var query = new GetAvailableModelsQuery(tierString);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.GetModelsByTier(expectedTier), Times.Once);
        _mockService.Verify(s => s.GetAllModels(), Times.Never);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("enterprise")]
    [InlineData("basic")]
    public async Task Handle_InvalidTierFilter_ReturnsEmptyList(string invalidTier)
    {
        // Arrange
        var query = new GetAvailableModelsQuery(invalidTier);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Models);
        _mockService.Verify(s => s.GetAllModels(), Times.Never);
        _mockService.Verify(s => s.GetModelsByTier(It.IsAny<ModelTier>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyTier_ReturnsAllModels()
    {
        // Arrange - empty string is treated as "no filter" (return all)
        var models = CreateTestModels();
        _mockService.Setup(s => s.GetAllModels()).Returns(models);

        var query = new GetAvailableModelsQuery("");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(models.Count, result.Models.Count);
        _mockService.Verify(s => s.GetAllModels(), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsModelConfigurationToDto()
    {
        // Arrange
        var models = new List<ModelConfiguration>
        {
            ModelConfiguration.Create(
                id: "test/model",
                name: "Test Model",
                provider: "test",
                tier: ModelTier.Premium,
                costPer1kInput: 0.001m,
                costPer1kOutput: 0.002m,
                maxTokens: 8192,
                supportsStreaming: true,
                description: "Test description")
        };
        _mockService.Setup(s => s.GetAllModels()).Returns(models);

        var query = new GetAvailableModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Models);
        var dto = result.Models[0];
        Assert.Equal("test/model", dto.Id);
        Assert.Equal("Test Model", dto.Name);
        Assert.Equal("test", dto.Provider);
        Assert.Equal("premium", dto.Tier);
        Assert.Equal(0.001m, dto.CostPer1kInputTokens);
        Assert.Equal(0.002m, dto.CostPer1kOutputTokens);
        Assert.Equal(8192, dto.MaxTokens);
        Assert.True(dto.SupportsStreaming);
        Assert.Equal("Test description", dto.Description);
    }

    [Fact]
    public async Task Handle_TierInResponse_IsLowercase()
    {
        // Arrange
        var models = CreateTestModels();
        _mockService.Setup(s => s.GetAllModels()).Returns(models);

        var query = new GetAvailableModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.All(result.Models, m =>
        {
            Assert.Equal(m.Tier, m.Tier.ToLowerInvariant());
        });
    }

    [Fact]
    public async Task Handle_WhitespaceOnlyTier_ReturnsAllModels()
    {
        // Arrange - whitespace is treated as "no filter" (return all)
        var models = CreateTestModels();
        _mockService.Setup(s => s.GetAllModels()).Returns(models);

        var query = new GetAvailableModelsQuery("   ");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(models.Count, result.Models.Count);
        _mockService.Verify(s => s.GetAllModels(), Times.Once);
    }

    private static List<ModelConfiguration> CreateTestModels()
    {
        return new List<ModelConfiguration>
        {
            ModelConfiguration.Create(
                id: "free-model",
                name: "Free Model",
                provider: "test",
                tier: ModelTier.Free,
                costPer1kInput: 0m,
                costPer1kOutput: 0m,
                maxTokens: 4096,
                supportsStreaming: true),
            ModelConfiguration.Create(
                id: "normal-model",
                name: "Normal Model",
                provider: "test",
                tier: ModelTier.Normal,
                costPer1kInput: 0.001m,
                costPer1kOutput: 0.002m,
                maxTokens: 8192,
                supportsStreaming: true),
            ModelConfiguration.Create(
                id: "premium-model",
                name: "Premium Model",
                provider: "test",
                tier: ModelTier.Premium,
                costPer1kInput: 0.005m,
                costPer1kOutput: 0.015m,
                maxTokens: 16384,
                supportsStreaming: true),
            ModelConfiguration.Create(
                id: "custom-model",
                name: "Custom Model",
                provider: "test",
                tier: ModelTier.Custom,
                costPer1kInput: 0.015m,
                costPer1kOutput: 0.075m,
                maxTokens: 4096,
                supportsStreaming: true)
        };
    }
}
