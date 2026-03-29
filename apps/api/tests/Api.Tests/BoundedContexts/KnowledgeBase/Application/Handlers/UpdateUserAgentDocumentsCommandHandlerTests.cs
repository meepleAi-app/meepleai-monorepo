using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for UpdateUserAgentDocumentsCommandHandler.
/// Validates user-facing document selection with Base-uniqueness constraint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class UpdateUserAgentDocumentsCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly UpdateUserAgentDocumentsCommandHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    public UpdateUserAgentDocumentsCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new UpdateUserAgentDocumentsCommandHandler(
            _dbContext,
            NullLogger<UpdateUserAgentDocumentsCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Should_Update_SelectedDocumentIds_Successfully()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var baseDocId = Guid.NewGuid();
        var expansionDocId = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SharedGameId = GameId,
            AddedAt = DateTime.UtcNow,
            IsFavorite = false
        });

        _dbContext.Agents.Add(new AgentEntity
        {
            Id = agentId,
            Name = "TestAgent",
            Type = "RagAgent",
            StrategyName = "default",
            StrategyParametersJson = "{}",
            GameId = GameId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.AgentConfigurations.Add(new AgentConfigurationEntity
        {
            Id = configId,
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = "[]",
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });

        _dbContext.PdfDocuments.AddRange(
            CreatePdfDocument(baseDocId, GameId, "rulebook.pdf", "base"),
            CreatePdfDocument(expansionDocId, GameId, "expansion.pdf", "expansion"));

        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            GameId, UserId, new List<Guid> { baseDocId, expansionDocId });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);

        var updatedConfig = await _dbContext.AgentConfigurations.FindAsync(configId);
        updatedConfig.Should().NotBeNull();

        var savedIds = JsonSerializer.Deserialize<List<Guid>>(updatedConfig!.SelectedDocumentIdsJson!);
        savedIds.Should().BeEquivalentTo(new[] { baseDocId, expansionDocId });
    }

    [Fact]
    public async Task Should_Reject_When_Multiple_Base_Documents_Selected()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var baseDoc1Id = Guid.NewGuid();
        var baseDoc2Id = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SharedGameId = GameId,
            AddedAt = DateTime.UtcNow,
            IsFavorite = false
        });

        _dbContext.Agents.Add(new AgentEntity
        {
            Id = agentId,
            Name = "TestAgent",
            Type = "RagAgent",
            StrategyName = "default",
            StrategyParametersJson = "{}",
            GameId = GameId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.AgentConfigurations.Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = "[]",
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });

        _dbContext.PdfDocuments.AddRange(
            CreatePdfDocument(baseDoc1Id, GameId, "rulebook-v1.pdf", "base"),
            CreatePdfDocument(baseDoc2Id, GameId, "rulebook-v2.pdf", "base"));

        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            GameId, UserId, new List<Guid> { baseDoc1Id, baseDoc2Id });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*regolamento base*");
    }

    [Fact]
    public async Task Should_Reject_When_Game_Not_In_User_Library()
    {
        // Arrange — no library entry seeded
        var command = new UpdateUserAgentDocumentsCommand(
            GameId, UserId, new List<Guid> { Guid.NewGuid() });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*libreria*");
    }

    [Fact]
    public async Task Should_Reject_When_Document_Not_Belonging_To_Game()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var foreignDocId = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SharedGameId = GameId,
            AddedAt = DateTime.UtcNow,
            IsFavorite = false
        });

        _dbContext.Agents.Add(new AgentEntity
        {
            Id = agentId,
            Name = "TestAgent",
            Type = "RagAgent",
            StrategyName = "default",
            StrategyParametersJson = "{}",
            GameId = GameId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.AgentConfigurations.Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = "[]",
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });

        // Document belongs to a DIFFERENT game
        _dbContext.PdfDocuments.Add(
            CreatePdfDocument(foreignDocId, otherGameId, "other-game.pdf", "base"));

        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            GameId, UserId, new List<Guid> { foreignDocId });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static PdfDocumentEntity CreatePdfDocument(
        Guid id, Guid gameId, string fileName, string documentType)
    {
        return new PdfDocumentEntity
        {
            Id = id,
            GameId = gameId,
            FileName = fileName,
            FilePath = $"/uploads/{fileName}",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = UserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            DocumentType = documentType,
            DocumentCategory = "Rulebook",
            Language = "it",
            IsActiveForRag = true
        };
    }
}
