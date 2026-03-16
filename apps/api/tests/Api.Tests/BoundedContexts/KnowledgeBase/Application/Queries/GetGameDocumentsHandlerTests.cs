using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameDocuments;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Tests for GetGameDocumentsHandler.
/// Verifies document listing, status mapping, and ordering.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameDocumentsHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetGameDocumentsHandler _handler;

    public GetGameDocumentsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        var logger = Mock.Of<ILogger<GetGameDocumentsHandler>>();
        _handler = new GetGameDocumentsHandler(_dbContext, logger);
    }

    [Fact]
    public async Task Handle_ReturnsDocuments_ForValidGameId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var vectorDocId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        _dbContext.Games.Add(CreateGame(gameId));
        _dbContext.PdfDocuments.Add(CreatePdf(pdfId, gameId, userId, "rules.pdf", pageCount: 24));
        _dbContext.VectorDocuments.Add(CreateVectorDocument(vectorDocId, gameId, pdfId, "completed"));
        await _dbContext.SaveChangesAsync();

        var query = new GetGameDocumentsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(vectorDocId, result[0].Id);
        Assert.Equal("rules.pdf", result[0].Title);
        Assert.Equal("indexed", result[0].Status);
        Assert.Equal(24, result[0].PageCount);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_ForUnknownGameId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var unknownGameId = Guid.NewGuid();

        var query = new GetGameDocumentsQuery(unknownGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_MapsStatusCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        _dbContext.Games.Add(CreateGame(gameId));

        var pdfCompleted = Guid.NewGuid();
        var pdfProcessing = Guid.NewGuid();
        var pdfFailed = Guid.NewGuid();

        _dbContext.PdfDocuments.Add(CreatePdf(pdfCompleted, gameId, userId, "completed.pdf"));
        _dbContext.PdfDocuments.Add(CreatePdf(pdfProcessing, gameId, userId, "processing.pdf"));
        _dbContext.PdfDocuments.Add(CreatePdf(pdfFailed, gameId, userId, "failed.pdf"));

        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), gameId, pdfCompleted, "completed"));
        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), gameId, pdfProcessing, "processing"));
        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), gameId, pdfFailed, "failed"));

        await _dbContext.SaveChangesAsync();

        var query = new GetGameDocumentsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, d => d.Status == "indexed");
        Assert.Contains(result, d => d.Status == "processing");
        Assert.Contains(result, d => d.Status == "failed");
    }

    [Fact]
    public async Task Handle_ReturnsDocumentsOrderedByCreatedAtDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        _dbContext.Games.Add(CreateGame(gameId));

        var pdf1 = Guid.NewGuid();
        var pdf2 = Guid.NewGuid();
        var vd1 = Guid.NewGuid();
        var vd2 = Guid.NewGuid();

        _dbContext.PdfDocuments.Add(CreatePdf(pdf1, gameId, userId, "older.pdf"));
        _dbContext.PdfDocuments.Add(CreatePdf(pdf2, gameId, userId, "newer.pdf"));

        var olderDoc = CreateVectorDocument(vd1, gameId, pdf1, "completed");
        olderDoc.IndexedAt = DateTime.UtcNow.AddDays(-2);
        var newerDoc = CreateVectorDocument(vd2, gameId, pdf2, "completed");
        newerDoc.IndexedAt = DateTime.UtcNow.AddDays(-1);

        _dbContext.VectorDocuments.Add(olderDoc);
        _dbContext.VectorDocuments.Add(newerDoc);

        await _dbContext.SaveChangesAsync();

        var query = new GetGameDocumentsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("newer.pdf", result[0].Title);
        Assert.Equal("older.pdf", result[1].Title);
    }

    [Fact]
    public async Task Handle_ExcludesDocumentsFromOtherGames()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        _dbContext.Games.Add(CreateGame(gameId));
        _dbContext.Games.Add(CreateGame(otherGameId));

        var pdf1 = Guid.NewGuid();
        var pdf2 = Guid.NewGuid();

        _dbContext.PdfDocuments.Add(CreatePdf(pdf1, gameId, userId, "my-game.pdf"));
        _dbContext.PdfDocuments.Add(CreatePdf(pdf2, otherGameId, userId, "other-game.pdf"));

        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), gameId, pdf1, "completed"));
        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), otherGameId, pdf2, "completed"));

        await _dbContext.SaveChangesAsync();

        var query = new GetGameDocumentsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("my-game.pdf", result[0].Title);
    }

    [Fact]
    public async Task Handle_DefaultsPageCountToZero_WhenNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        _dbContext.Games.Add(CreateGame(gameId));
        _dbContext.PdfDocuments.Add(CreatePdf(pdfId, gameId, userId, "no-pages.pdf", pageCount: null));
        _dbContext.VectorDocuments.Add(CreateVectorDocument(Guid.NewGuid(), gameId, pdfId, "completed"));

        await _dbContext.SaveChangesAsync();

        var query = new GetGameDocumentsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(0, result[0].PageCount);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region Test Helpers

    private static UserEntity CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"user-{userId.ToString()[..8]}@test.com",
        DisplayName = $"user-{userId.ToString()[..8]}",
        PasswordHash = "hash",
        CreatedAt = DateTime.UtcNow
    };

    private static GameEntity CreateGame(Guid gameId) => new()
    {
        Id = gameId,
        Name = $"Game-{gameId.ToString()[..8]}",
        CreatedAt = DateTime.UtcNow
    };

    private static PdfDocumentEntity CreatePdf(
        Guid pdfId, Guid gameId, Guid userId, string fileName, int? pageCount = null) => new()
    {
        Id = pdfId,
        GameId = gameId,
        FileName = fileName,
        FilePath = $"/uploads/{fileName}",
        FileSizeBytes = 1024,
        UploadedByUserId = userId,
        UploadedAt = DateTime.UtcNow,
        PageCount = pageCount,
        ProcessingState = "Ready"
    };

    private static VectorDocumentEntity CreateVectorDocument(
        Guid id, Guid gameId, Guid pdfDocumentId, string indexingStatus) => new()
    {
        Id = id,
        GameId = gameId,
        PdfDocumentId = pdfDocumentId,
        ChunkCount = 10,
        TotalCharacters = 5000,
        IndexingStatus = indexingStatus,
        IndexedAt = DateTime.UtcNow,
        EmbeddingModel = "nomic-embed-text",
        EmbeddingDimensions = 768
    };

    #endregion
}
