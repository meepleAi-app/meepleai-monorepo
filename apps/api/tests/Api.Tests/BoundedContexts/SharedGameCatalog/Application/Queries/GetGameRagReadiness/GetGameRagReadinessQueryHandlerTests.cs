using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;

/// <summary>
/// Unit tests for GetGameRagReadinessQueryHandler.
/// Tests cross-BC aggregation of RAG readiness status.
/// </summary>
public sealed class GetGameRagReadinessQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _gameRepositoryMock;
    private readonly Mock<ISharedGameDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _pdfDocumentRepositoryMock;
    private readonly Mock<IAgentDefinitionRepository> _agentDefinitionRepositoryMock;
    private readonly GetGameRagReadinessQueryHandler _sut;

    public GetGameRagReadinessQueryHandlerTests()
    {
        _gameRepositoryMock = new Mock<ISharedGameRepository>();
        _documentRepositoryMock = new Mock<ISharedGameDocumentRepository>();
        _pdfDocumentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _agentDefinitionRepositoryMock = new Mock<IAgentDefinitionRepository>();

        _sut = new GetGameRagReadinessQueryHandler(
            _gameRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _pdfDocumentRepositoryMock.Object,
            _agentDefinitionRepositoryMock.Object,
            NullLogger<GetGameRagReadinessQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameRagReadinessQuery(gameId);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act and Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(query, CancellationToken.None));

        Assert.Equal("SharedGame", exception.ResourceType);
        Assert.Equal(gameId.ToString(), exception.ResourceId);
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsNoDocumentsReadiness()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameRagReadinessQuery(gameId);

        var game = CreateSharedGame(gameId);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _documentRepositoryMock
            .Setup(x => x.GetBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument>());

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("no_documents", result.OverallReadiness);
        Assert.Equal(0, result.TotalDocuments);
        Assert.Empty(result.Documents);
        Assert.Null(result.LinkedAgent);
    }

    [Fact]
    public async Task Handle_ProcessingDocuments_ReturnsDocumentsProcessingReadiness()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var query = new GetGameRagReadinessQuery(gameId);

        var game = CreateSharedGame(gameId);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var sharedDoc = CreateSharedGameDocument(gameId, pdfId);
        _documentRepositoryMock
            .Setup(x => x.GetBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { sharedDoc });

        var pdfDoc = CreatePdfDocument(pdfId, gameId, PdfProcessingState.Extracting);
        _pdfDocumentRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(pdfId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument> { pdfDoc });

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("documents_processing", result.OverallReadiness);
        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(0, result.ReadyDocuments);
        Assert.Equal(1, result.ProcessingDocuments);
        Assert.Single(result.Documents);
        Assert.Equal("Extracting", result.Documents[0].ProcessingState);
    }

    [Fact]
    public async Task Handle_AllDocsReady_NoAgent_ReturnsReadyForAgent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var query = new GetGameRagReadinessQuery(gameId);

        var game = CreateSharedGame(gameId, agentDefinitionId: null);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var sharedDoc = CreateSharedGameDocument(gameId, pdfId);
        _documentRepositoryMock
            .Setup(x => x.GetBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { sharedDoc });

        var pdfDoc = CreatePdfDocument(pdfId, gameId, PdfProcessingState.Ready);
        _pdfDocumentRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(pdfId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument> { pdfDoc });

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("ready_for_agent", result.OverallReadiness);
        Assert.Equal(1, result.ReadyDocuments);
        Assert.Null(result.LinkedAgent);
    }

    [Fact]
    public async Task Handle_AllDocsReady_AgentLinked_ReturnsFullyOperational()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var query = new GetGameRagReadinessQuery(gameId);

        var game = CreateSharedGame(gameId, agentDefinitionId: agentId);
        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var sharedDoc = CreateSharedGameDocument(gameId, pdfId);
        _documentRepositoryMock
            .Setup(x => x.GetBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { sharedDoc });

        var pdfDoc = CreatePdfDocument(pdfId, gameId, PdfProcessingState.Ready);
        _pdfDocumentRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(pdfId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument> { pdfDoc });

        var agentDef = CreateAgentDefinition(agentId);
        _agentDefinitionRepositoryMock
            .Setup(x => x.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDef);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("fully_operational", result.OverallReadiness);
        Assert.Equal(1, result.ReadyDocuments);
        Assert.NotNull(result.LinkedAgent);
        Assert.Equal(agentId, result.LinkedAgent.AgentId);
        Assert.True(result.LinkedAgent.IsActive);
    }

    // ==========================================
    // Factory helpers
    // ==========================================

    private static SharedGame CreateSharedGame(Guid gameId, Guid? agentDefinitionId = null)
    {
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test board game for unit testing.",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());

        // Use reflection to set Id and AgentDefinitionId since they are private
        SetPrivateField(game, "_id", gameId);
        if (agentDefinitionId.HasValue)
        {
            SetPrivateField(game, "_agentDefinitionId", agentDefinitionId);
        }

        return game;
    }

    private static SharedGameDocument CreateSharedGameDocument(Guid sharedGameId, Guid pdfDocumentId)
    {
        return new SharedGameDocument(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            pdfDocumentId: pdfDocumentId,
            documentType: SharedGameDocumentType.Rulebook,
            version: "1.0",
            isActive: true,
            tags: null,
            createdAt: DateTime.UtcNow,
            createdBy: Guid.NewGuid());
    }

    private static PdfDocument CreatePdfDocument(Guid id, Guid gameId, PdfProcessingState state)
    {
        return PdfDocument.Reconstitute(
            id: id,
            gameId: gameId,
            fileName: new FileName("test-rulebook.pdf"),
            filePath: "/uploads/test-rulebook.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processedAt: state == PdfProcessingState.Ready ? DateTime.UtcNow : null,
            pageCount: state == PdfProcessingState.Ready ? 10 : null,
            processingError: null,
            language: LanguageCode.English,
            processingState: state,
            isActiveForRag: true);
    }

    private static AgentDefinition CreateAgentDefinition(Guid id)
    {
        var agent = AgentDefinition.Create(
            name: "Test Agent",
            description: "A test AI agent",
            type: AgentType.Custom("rag", "RAG Agent"),
            config: AgentDefinitionConfig.Create("gpt-4", 4096, 0.7f));

        // Set the Id via reflection since Create generates a new one
        SetPropertyValue(agent, "Id", id);
        return agent;
    }

    private static void SetPrivateField(object obj, string fieldName, object? value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    private static void SetPropertyValue(object obj, string propertyName, object? value)
    {
        var type = obj.GetType();
        while (type is not null)
        {
            var prop = type.GetProperty(propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop is not null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }
            type = type.BaseType;
        }
    }
}
