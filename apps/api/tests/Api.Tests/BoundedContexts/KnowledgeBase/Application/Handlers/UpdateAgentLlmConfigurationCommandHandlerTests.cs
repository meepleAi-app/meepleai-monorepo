using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for UpdateAgentLlmConfigurationCommandHandler.
/// Covers ownership, tier validation, partial update, and versioned config creation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateAgentLlmConfigurationCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _repository = new();
    private readonly Mock<IModelConfigurationService> _modelConfigService = new();
    private readonly Mock<ILogger<UpdateAgentLlmConfigurationCommandHandler>> _logger = new();
    private readonly MeepleAiDbContext _db;
    private readonly UpdateAgentLlmConfigurationCommandHandler _handler;

    public UpdateAgentLlmConfigurationCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new UpdateAgentLlmConfigurationCommandHandler(
            _repository.Object, _db, _modelConfigService.Object, _logger.Object);
    }

    private async Task<AgentConfigurationEntity> SeedConfig(Guid agentId, string model = "meta-llama/llama-3.3-70b-instruct:free")
    {
        var config = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = model,
            AgentMode = 0,
            SelectedDocumentIdsJson = "[]",
            Temperature = 0.3m,
            MaxTokens = 2048,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        _db.Set<AgentConfigurationEntity>().Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesNewConfigAndDeactivatesOld()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var oldConfig = await SeedConfig(agentId);

        var freeModel = ModelConfiguration.Create(
            "meta-llama/llama-3.1-70b-instruct:free", "Llama 3.1 Free", "meta-llama",
            ModelTier.Free, 0m, 0m, 8192);
        _modelConfigService.Setup(s => s.ValidateUserTierForModel(It.IsAny<ModelTier>(), "meta-llama/llama-3.1-70b-instruct:free"))
            .Returns(ModelTierValidationResult.Success(ModelTier.Free, ModelTier.Free, "meta-llama/llama-3.1-70b-instruct:free"));
        _modelConfigService.Setup(s => s.GetModelById("meta-llama/llama-3.1-70b-instruct:free"))
            .Returns(freeModel);

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            ModelId: "meta-llama/llama-3.1-70b-instruct:free",
            Temperature: 0.7m, MaxTokens: 4096, SelectedDocumentIds: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("meta-llama/llama-3.1-70b-instruct:free", result.LlmModel);
        Assert.Equal(0.7m, result.Temperature);
        Assert.Equal(4096, result.MaxTokens);
        Assert.True(result.IsCurrent);

        // Old config should be deactivated
        var oldInDb = await _db.Set<AgentConfigurationEntity>().FindAsync(oldConfig.Id);
        Assert.False(oldInDb!.IsCurrent);
    }

    [Fact]
    public async Task Handle_FreeTierWithPremiumModel_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        await SeedConfig(agentId);

        _modelConfigService.Setup(s => s.ValidateUserTierForModel(ModelTier.Free, "anthropic/claude-3.5-sonnet"))
            .Returns(ModelTierValidationResult.InsufficientTier(ModelTier.Free, ModelTier.Premium, "anthropic/claude-3.5-sonnet"));

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            ModelId: "anthropic/claude-3.5-sonnet", Temperature: null, MaxTokens: null, SelectedDocumentIds: null);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FreeTierWithFreeModel_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        await SeedConfig(agentId);

        var freeModel = ModelConfiguration.Create(
            "meta-llama/llama-3.3-70b-instruct:free", "Llama Free", "meta-llama",
            ModelTier.Free, 0m, 0m, 8192);
        _modelConfigService.Setup(s => s.ValidateUserTierForModel(ModelTier.Free, "meta-llama/llama-3.3-70b-instruct:free"))
            .Returns(ModelTierValidationResult.Success(ModelTier.Free, ModelTier.Free, "meta-llama/llama-3.3-70b-instruct:free"));
        _modelConfigService.Setup(s => s.GetModelById("meta-llama/llama-3.3-70b-instruct:free"))
            .Returns(freeModel);

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            ModelId: "meta-llama/llama-3.3-70b-instruct:free", Temperature: null, MaxTokens: null, SelectedDocumentIds: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", result.LlmModel);
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        await SeedConfig(agentId);

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: otherId, UserTier: "premium", UserRole: "User",
            ModelId: null, Temperature: 1.0m, MaxTokens: null, SelectedDocumentIds: null);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminBypass_Succeeds()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        await SeedConfig(agentId);

        var premiumModel = ModelConfiguration.Create(
            "anthropic/claude-3.5-sonnet", "Claude 3.5", "anthropic",
            ModelTier.Premium, 0.003m, 0.015m, 8192);
        _modelConfigService.Setup(s => s.GetModelById("anthropic/claude-3.5-sonnet"))
            .Returns(premiumModel);

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: adminId, UserTier: "free", UserRole: "Admin",
            ModelId: "anthropic/claude-3.5-sonnet", Temperature: 1.5m, MaxTokens: 4096, SelectedDocumentIds: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("anthropic/claude-3.5-sonnet", result.LlmModel);
        Assert.Equal(1.5m, result.Temperature);
        Assert.Equal(4096, result.MaxTokens);
    }

    [Fact]
    public async Task Handle_NoExistingConfig_ThrowsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        // No config seeded

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            ModelId: null, Temperature: 0.5m, MaxTokens: null, SelectedDocumentIds: null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PartialUpdate_OnlyOverridesProvidedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Test", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        await SeedConfig(agentId);

        // Only update temperature, leave model and maxTokens unchanged
        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            ModelId: null, Temperature: 1.2m, MaxTokens: null, SelectedDocumentIds: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — temperature changed, others kept defaults
        Assert.Equal(1.2m, result.Temperature);
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", result.LlmModel);
        Assert.Equal(2048, result.MaxTokens);
    }

    [Fact]
    public async Task Handle_AgentNotFound_ThrowsNotFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        var command = new UpdateAgentLlmConfigurationCommand(
            AgentId: agentId, UserId: Guid.NewGuid(), UserTier: "free", UserRole: "User",
            ModelId: null, Temperature: null, MaxTokens: null, SelectedDocumentIds: null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
