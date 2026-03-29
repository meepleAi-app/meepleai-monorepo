using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for PrivatePdfAssociatedEventHandler.
/// Verifies auto-add of private PDF to agent SelectedDocumentIds on upload.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PrivatePdfAssociatedEventHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly PrivatePdfAssociatedEventHandler _handler;

    private static readonly Guid LibraryEntryId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid PdfDocumentId = Guid.NewGuid();

    public PrivatePdfAssociatedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockAgentRepo = new Mock<IAgentRepository>();

        _handler = new PrivatePdfAssociatedEventHandler(
            _mockAgentRepo.Object,
            _dbContext,
            NullLogger<PrivatePdfAssociatedEventHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Should_Add_PdfId_To_Agent_SelectedDocuments_When_Agent_Exists()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var existingDocId = Guid.NewGuid();
        var agent = new Agent(
            agentId,
            "TestAgent",
            AgentType.RagAgent,
            AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
            true,
            gameId: GameId);

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { agent });

        var configId = Guid.NewGuid();
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = configId,
            AgentId = agentId,
            IsCurrent = true,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { existingDocId }),
            LlmModel = "test-model",
            Temperature = 0.7m,
            MaxTokens = 2048,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });
        await _dbContext.SaveChangesAsync();

        var domainEvent = new PrivatePdfAssociatedEvent(LibraryEntryId, UserId, GameId, PdfDocumentId);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        var updatedConfig = await _dbContext.AgentConfigurations
            .FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(updatedConfig.SelectedDocumentIdsJson!);
        selectedIds.Should().HaveCount(2);
        selectedIds.Should().Contain(existingDocId);
        selectedIds.Should().Contain(PdfDocumentId);
    }

    [Fact]
    public async Task Should_Noop_When_No_Agent_Exists_For_Game()
    {
        // Arrange
        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var domainEvent = new PrivatePdfAssociatedEvent(LibraryEntryId, UserId, GameId, PdfDocumentId);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — no configs should exist, no exceptions thrown
        var configs = await _dbContext.AgentConfigurations.ToListAsync();
        configs.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Be_Idempotent_When_PdfId_Already_Selected()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(
            agentId,
            "TestAgent",
            AgentType.RagAgent,
            AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
            true,
            gameId: GameId);

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { agent });

        var configId = Guid.NewGuid();
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = configId,
            AgentId = agentId,
            IsCurrent = true,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { PdfDocumentId }),
            LlmModel = "test-model",
            Temperature = 0.7m,
            MaxTokens = 2048,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });
        await _dbContext.SaveChangesAsync();

        var domainEvent = new PrivatePdfAssociatedEvent(LibraryEntryId, UserId, GameId, PdfDocumentId);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        var updatedConfig = await _dbContext.AgentConfigurations
            .FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(updatedConfig.SelectedDocumentIdsJson!);
        selectedIds.Should().HaveCount(1);
        selectedIds.Should().Contain(PdfDocumentId);
    }
}
