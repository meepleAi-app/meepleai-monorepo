using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.IntegrationEvents;
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
/// Unit tests for AgentDocumentSyncOnReadyHandler.
/// Verifies that the VectorDocument.Id (NOT PdfDocument.Id) is added to
/// SelectedDocumentIdsJson after a private PDF is successfully indexed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentDocumentSyncOnReadyHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly AgentDocumentSyncOnReadyHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid PdfDocumentId = Guid.NewGuid();
    private static readonly Guid VectorDocumentId = Guid.NewGuid();
    private static readonly Guid PrivateGameId = Guid.NewGuid();

    public AgentDocumentSyncOnReadyHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockAgentRepo = new Mock<IAgentRepository>();

        _handler = new AgentDocumentSyncOnReadyHandler(
            _mockAgentRepo.Object,
            _dbContext,
            NullLogger<AgentDocumentSyncOnReadyHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private VectorDocumentReadyIntegrationEvent CreateEvent(Guid? vectorDocId = null, Guid? pdfDocId = null) =>
        new()
        {
            DocumentId = vectorDocId ?? VectorDocumentId,
            GameId = GameId,
            PdfDocumentId = pdfDocId ?? PdfDocumentId,
            ChunkCount = 42,
            UploadedByUserId = UserId,
            FileName = "test.pdf",
            CurrentProcessingState = "Indexing"
        };

    private Agent CreateAgent(Guid? agentId = null) =>
        new(agentId ?? Guid.NewGuid(), "TestAgent", AgentType.RagAgent,
            AgentStrategy.HybridSearch(), true, gameId: GameId);

    private async Task<Guid> SeedPrivatePdfAsync(Guid? pdfId = null)
    {
        var id = pdfId ?? PdfDocumentId;
        _dbContext.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = id,
            GameId = GameId,
            PrivateGameId = PrivateGameId,
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedSharedPdfAsync(Guid? pdfId = null)
    {
        var id = pdfId ?? PdfDocumentId;
        _dbContext.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = id,
            GameId = GameId,
            PrivateGameId = null, // shared
            FileName = "shared-rulebook.pdf",
            FilePath = "/uploads/shared-rulebook.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedAgentConfigAsync(Guid agentId, List<Guid>? existingDocIds = null)
    {
        var configId = Guid.NewGuid();
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = configId,
            AgentId = agentId,
            IsCurrent = true,
            SelectedDocumentIdsJson = existingDocIds != null
                ? JsonSerializer.Serialize(existingDocIds)
                : "[]",
            LlmModel = "gpt-4o",
            Temperature = 0.7m,
            MaxTokens = 2048,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });
        await _dbContext.SaveChangesAsync();
        return configId;
    }

    #region Private PDF auto-add

    [Fact]
    public async Task HandleAsync_PrivatePdf_AddsVectorDocumentIdToSelectedDocs()
    {
        // Arrange
        await SeedPrivatePdfAsync();
        var agentId = Guid.NewGuid();
        var configId = await SeedAgentConfigAsync(agentId, new List<Guid>());

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { CreateAgent(agentId) });

        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — VectorDocument.Id (not PdfDocument.Id) must be stored
        var config = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        selectedIds.Should().ContainSingle()
            .Which.Should().Be(VectorDocumentId,
                because: "SelectedDocumentIdsJson must store VectorDocument.Id values");
        selectedIds.Should().NotContain(PdfDocumentId,
            because: "PdfDocument.Id must never be stored in SelectedDocumentIdsJson");
    }

    [Fact]
    public async Task HandleAsync_PrivatePdf_AppendsToExistingSelectedDocs()
    {
        // Arrange
        await SeedPrivatePdfAsync();
        var agentId = Guid.NewGuid();
        var existingVectorDocId = Guid.NewGuid();
        var configId = await SeedAgentConfigAsync(agentId, new List<Guid> { existingVectorDocId });

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { CreateAgent(agentId) });

        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        var config = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        selectedIds.Should().HaveCount(2);
        selectedIds.Should().Contain(existingVectorDocId);
        selectedIds.Should().Contain(VectorDocumentId);
    }

    [Fact]
    public async Task HandleAsync_PrivatePdf_IsIdempotent_WhenVectorDocAlreadySelected()
    {
        // Arrange
        await SeedPrivatePdfAsync();
        var agentId = Guid.NewGuid();
        var configId = await SeedAgentConfigAsync(agentId, new List<Guid> { VectorDocumentId });

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { CreateAgent(agentId) });

        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — no duplicate
        var config = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        selectedIds.Should().ContainSingle()
            .Which.Should().Be(VectorDocumentId);
    }

    #endregion

    #region Shared PDF — no auto-add

    [Fact]
    public async Task HandleAsync_SharedPdf_DoesNotModifySelectedDocs()
    {
        // Arrange — PDF has no PrivateGameId (shared/base document)
        await SeedSharedPdfAsync();
        var agentId = Guid.NewGuid();
        var configId = await SeedAgentConfigAsync(agentId, new List<Guid>());

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { CreateAgent(agentId) });

        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — shared documents are selected explicitly via UpdateAgentDocumentsCommand
        var config = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId);
        var selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        selectedIds.Should().BeEmpty(
            because: "shared PDFs are not auto-added; only private PDFs are");
    }

    [Fact]
    public async Task HandleAsync_SharedPdf_DoesNotCallAgentRepository()
    {
        // Arrange
        await SeedSharedPdfAsync();
        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        // Agent lookup is skipped when the PDF is not private
        _mockAgentRepo.Verify(
            r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region No agent for game

    [Fact]
    public async Task HandleAsync_PrivatePdf_NoAgentForGame_IsNoOp()
    {
        // Arrange
        await SeedPrivatePdfAsync();
        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var evt = CreateEvent();

        // Act — must not throw
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — no configs modified
        var configs = await _dbContext.AgentConfigurations.ToListAsync();
        configs.Should().BeEmpty();
    }

    #endregion

    #region Multiple agents

    [Fact]
    public async Task HandleAsync_PrivatePdf_AddsToAllAgentsForGame()
    {
        // Arrange
        await SeedPrivatePdfAsync();
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var configId1 = await SeedAgentConfigAsync(agentId1, new List<Guid>());
        var configId2 = await SeedAgentConfigAsync(agentId2, new List<Guid>());

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>
            {
                CreateAgent(agentId1),
                CreateAgent(agentId2)
            });

        var evt = CreateEvent();

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — both configs updated
        var config1 = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId1);
        var config2 = await _dbContext.AgentConfigurations.FirstAsync(c => c.Id == configId2);
        JsonSerializer.Deserialize<List<Guid>>(config1.SelectedDocumentIdsJson!)!
            .Should().Contain(VectorDocumentId);
        JsonSerializer.Deserialize<List<Guid>>(config2.SelectedDocumentIdsJson!)!
            .Should().Contain(VectorDocumentId);
    }

    #endregion
}
