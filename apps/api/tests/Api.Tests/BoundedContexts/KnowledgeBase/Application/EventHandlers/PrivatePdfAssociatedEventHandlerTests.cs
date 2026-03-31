using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
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
/// The handler is intentionally a no-op: at PDF upload time the VectorDocument does not
/// exist yet, so writing PdfDocument.Id into SelectedDocumentIdsJson would cause a type
/// mismatch with SendAgentMessageCommandHandler (which reads VectorDocument.Id values).
/// Auto-add after indexing is performed by AgentDocumentSyncOnReadyHandler.
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
    private static readonly Guid AgentId = Guid.NewGuid();

    public PrivatePdfAssociatedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockAgentRepo = new Mock<IAgentRepository>();

        _handler = new PrivatePdfAssociatedEventHandler(
            NullLogger<PrivatePdfAssociatedEventHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task HandleAsync_IsNoOp_DoesNotWriteToSelectedDocumentIds()
    {
        // Arrange — agent with an existing config
        var existingVectorDocId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = configId,
            AgentId = AgentId,
            IsCurrent = true,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { existingVectorDocId }),
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

        // Assert — config must be unchanged (PdfDocumentId must NOT appear in SelectedDocumentIds)
        var config = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        selectedIds.Should().HaveCount(1);
        selectedIds.Should().Contain(existingVectorDocId);
        selectedIds.Should().NotContain(PdfDocumentId,
            because: "PdfDocument.Id must never be written to SelectedDocumentIdsJson; " +
                     "only VectorDocument.Id values are valid (resolved after indexing)");
    }

    [Fact]
    public async Task HandleAsync_IsNoOp_WhenNoAgentExistsForGame()
    {
        // Arrange — no agents, no configs
        var domainEvent = new PrivatePdfAssociatedEvent(LibraryEntryId, UserId, GameId, PdfDocumentId);

        // Act — must not throw
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — still no configs
        var configs = await _dbContext.AgentConfigurations.ToListAsync();
        configs.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DoesNotCallAgentRepository()
    {
        // The handler should not touch the agent repository at all
        var domainEvent = new PrivatePdfAssociatedEvent(LibraryEntryId, UserId, GameId, PdfDocumentId);

        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // The handler is a no-op and must not interact with agent state
        _mockAgentRepo.Verify(
            r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
