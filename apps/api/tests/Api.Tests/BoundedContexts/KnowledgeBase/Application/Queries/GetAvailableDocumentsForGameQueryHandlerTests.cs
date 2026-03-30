using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetAvailableDocumentsForGameQueryHandler.
/// Verifies document retrieval split by type with agent selection state.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAvailableDocumentsForGameQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetAvailableDocumentsForGameQueryHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    public GetAvailableDocumentsForGameQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetAvailableDocumentsForGameQueryHandler(
            _dbContext,
            NullLogger<GetAvailableDocumentsForGameQueryHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Should_Return_Documents_Split_By_Type()
    {
        // Arrange
        var baseDocId = Guid.NewGuid();
        var expansionDocId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(CreateLibraryEntry(UserId, GameId));
        _dbContext.PdfDocuments.AddRange(
            CreatePdfDocument(baseDocId, GameId, "rulebook.pdf", "base", "Ready"),
            CreatePdfDocument(expansionDocId, GameId, "expansion.pdf", "expansion", "Ready"));

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
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { baseDocId }),
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        result.AgentId.Should().Be(agentId);
        result.BaseDocuments.Should().HaveCount(1);
        result.BaseDocuments[0].DocumentId.Should().Be(baseDocId);
        result.BaseDocuments[0].IsSelected.Should().BeTrue();
        result.BaseDocuments[0].IsPrivate.Should().BeFalse();

        result.AdditionalDocuments.Should().HaveCount(1);
        result.AdditionalDocuments[0].DocumentId.Should().Be(expansionDocId);
        result.AdditionalDocuments[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Return_Null_AgentId_When_No_Agent()
    {
        // Arrange
        _dbContext.UserLibraryEntries.Add(CreateLibraryEntry(UserId, GameId));
        _dbContext.PdfDocuments.Add(
            CreatePdfDocument(Guid.NewGuid(), GameId, "rulebook.pdf", "base", "Ready"));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        result.AgentId.Should().BeNull();
        result.BaseDocuments.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Include_Private_Pdf_With_IsPrivate_Flag()
    {
        // Arrange
        var privateDocId = Guid.NewGuid();
        var doc = CreatePdfDocument(privateDocId, GameId, "house-rules.pdf", "homerule", "Extracting");
        doc.PrivateGameId = Guid.NewGuid(); // marks it as private

        _dbContext.UserLibraryEntries.Add(CreateLibraryEntry(UserId, GameId));
        _dbContext.PdfDocuments.Add(doc);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        result.AdditionalDocuments.Should().HaveCount(1);
        var privateDoc = result.AdditionalDocuments[0];
        privateDoc.IsPrivate.Should().BeTrue();
        privateDoc.ProcessingState.Should().Be("Extracting");
        privateDoc.DocumentId.Should().Be(privateDocId);
    }

    [Fact]
    public async Task Should_Throw_ForbiddenException_When_Game_Not_In_Library()
    {
        // Arrange — no library entry seeded
        _dbContext.PdfDocuments.Add(
            CreatePdfDocument(Guid.NewGuid(), GameId, "rulebook.pdf", "base", "Ready"));
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ForbiddenException>();
    }

    [Fact]
    public async Task Should_Return_IsSelected_False_When_Agent_Has_No_Config()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(CreateLibraryEntry(UserId, GameId));
        _dbContext.PdfDocuments.Add(
            CreatePdfDocument(docId, GameId, "rulebook.pdf", "base", "Ready"));
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
        // No AgentConfiguration seeded

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        result.AgentId.Should().Be(agentId);
        result.BaseDocuments.Should().HaveCount(1);
        result.BaseDocuments[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Exclude_Documents_Not_Active_For_Rag()
    {
        // Arrange
        var activeDocId = Guid.NewGuid();
        var inactiveDocId = Guid.NewGuid();

        _dbContext.UserLibraryEntries.Add(CreateLibraryEntry(UserId, GameId));
        _dbContext.PdfDocuments.AddRange(
            CreatePdfDocument(activeDocId, GameId, "rules.pdf", "base", "Ready"),
            new PdfDocumentEntity
            {
                Id = inactiveDocId,
                GameId = GameId,
                FileName = "draft.pdf",
                FilePath = "/uploads/draft.pdf",
                FileSizeBytes = 512,
                ContentType = "application/pdf",
                UploadedByUserId = UserId,
                UploadedAt = DateTime.UtcNow,
                ProcessingState = "Ready",
                DocumentType = "base",
                DocumentCategory = "Rulebook",
                Language = "it",
                IsActiveForRag = false // excluded
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAvailableDocumentsForGameQuery(GameId, UserId),
            CancellationToken.None);

        // Assert
        result.BaseDocuments.Should().HaveCount(1);
        result.BaseDocuments[0].DocumentId.Should().Be(activeDocId);
    }

    private static UserLibraryEntryEntity CreateLibraryEntry(Guid userId, Guid gameId) =>
        new() { UserId = userId, SharedGameId = gameId, AddedAt = DateTime.UtcNow };

    private static PdfDocumentEntity CreatePdfDocument(
        Guid id, Guid gameId, string fileName, string documentType, string processingState,
        Guid? privateGameId = null)
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
            ProcessingState = processingState,
            DocumentType = documentType,
            DocumentCategory = "Rulebook",
            Language = "it",
            PrivateGameId = privateGameId,
            IsActiveForRag = true
        };
    }
}
