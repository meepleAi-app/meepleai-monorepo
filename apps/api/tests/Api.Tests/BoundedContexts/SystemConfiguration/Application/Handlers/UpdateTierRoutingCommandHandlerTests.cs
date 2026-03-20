using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Unit tests for UpdateTierRoutingCommandHandler.
/// Issue #2596: LLM tier routing update with cache invalidation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class UpdateTierRoutingCommandHandlerTests : IDisposable
{
    private readonly Mock<IAiModelConfigurationRepository> _repositoryMock;
    private readonly Mock<ILlmTierRoutingService> _tierRoutingServiceMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<UpdateTierRoutingCommandHandler>> _loggerMock;
    private readonly UpdateTierRoutingCommandHandler _handler;

    public UpdateTierRoutingCommandHandlerTests()
    {
        _repositoryMock = new Mock<IAiModelConfigurationRepository>();
        _tierRoutingServiceMock = new Mock<ILlmTierRoutingService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<UpdateTierRoutingCommandHandler>>();

        _handler = new UpdateTierRoutingCommandHandler(
            _repositoryMock.Object,
            _tierRoutingServiceMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesTierRoutingAndInvalidatesCache()
    {
        // Arrange
        var productionModel = CreateTestModel("gpt-4o", "GPT-4o", "OpenRouter");
        var testModel = CreateTestModel("llama3:8b", "Llama 3 8B", "Ollama");

        var command = new UpdateTierRoutingCommand(
            LlmUserTier.Admin,
            "gpt-4o",
            "llama3:8b");

        _repositoryMock.Setup(r => r.GetByModelIdAsync("gpt-4o", It.IsAny<CancellationToken>()))
            .ReturnsAsync(productionModel);
        _repositoryMock.Setup(r => r.GetByModelIdAsync("llama3:8b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testModel);
        _repositoryMock.Setup(r => r.GetByTierAsync(LlmUserTier.Admin, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiModelConfiguration>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Tier.Should().Be(LlmUserTier.Admin);
        result.ProductionModelId.Should().Be("gpt-4o");
        result.TestModelId.Should().Be("llama3:8b");

        _tierRoutingServiceMock.Verify(s => s.InvalidateCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductionModelNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new UpdateTierRoutingCommand(
            LlmUserTier.User,
            "nonexistent-model",
            "llama3:8b");

        _repositoryMock.Setup(r => r.GetByModelIdAsync("nonexistent-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelConfiguration?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain("nonexistent-model");
    }

    [Fact]
    public async Task Handle_TestModelNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var productionModel = CreateTestModel("gpt-4o", "GPT-4o", "OpenRouter");

        var command = new UpdateTierRoutingCommand(
            LlmUserTier.Editor,
            "gpt-4o",
            "nonexistent-test-model");

        _repositoryMock.Setup(r => r.GetByModelIdAsync("gpt-4o", It.IsAny<CancellationToken>()))
            .ReturnsAsync(productionModel);
        _repositoryMock.Setup(r => r.GetByModelIdAsync("nonexistent-test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelConfiguration?)null);

        // Act & Assert
        var act2 = async () => await _handler.Handle(command, CancellationToken.None);
        var exception = (await act2.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain("nonexistent-test-model");
    }

    [Fact]
    public async Task Handle_ExistingDefaultModel_RemovesOldDefaultAndSetsNew()
    {
        // Arrange
        var oldDefaultModel = CreateTestModel("old-model", "Old Model", "Ollama");
        oldDefaultModel.SetTierRouting(LlmUserTier.Anonymous, LlmEnvironmentType.Production, true);

        var newProductionModel = CreateTestModel("new-model", "New Model", "OpenRouter");
        var testModel = CreateTestModel("test-model", "Test Model", "Ollama");

        var command = new UpdateTierRoutingCommand(
            LlmUserTier.Anonymous,
            "new-model",
            "test-model");

        _repositoryMock.Setup(r => r.GetByModelIdAsync("new-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newProductionModel);
        _repositoryMock.Setup(r => r.GetByModelIdAsync("test-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testModel);
        _repositoryMock.Setup(r => r.GetByTierAsync(LlmUserTier.Anonymous, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiModelConfiguration> { oldDefaultModel });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AiModelConfiguration>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    private static AiModelConfiguration CreateTestModel(string modelId, string displayName, string provider)
    {
        return AiModelConfiguration.Create(modelId, displayName, provider, 1, ModelSettings.Default);
    }
}
