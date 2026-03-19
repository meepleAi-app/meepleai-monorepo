using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Unit tests for GetGamePdfsQueryHandler.
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGamePdfsQueryHandlerTests
{
    private readonly MeepleAiDbContext _db;
    private readonly GetGamePdfsQueryHandler _handler;

    public GetGamePdfsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        var mockLogger = new Mock<ILogger<GetGamePdfsQueryHandler>>();
        _handler = new GetGamePdfsQueryHandler(_db, mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenNoPdfsExist_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenPdfExistsForGame_ReturnsPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var game = new GameEntity { Id = gameId, Name = "Test Game" };
        _db.Games.Add(game);

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = "TestRules.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1_000_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            PageCount = 10,
            Language = "EN"
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TestRules");
        result[0].FileSizeBytes.Should().Be(1_000_000);
        result[0].Source.Should().Be("Catalog");
        result[0].Language.Should().Be("EN");
    }

    [Fact]
    public async Task Handle_WhenPdfExistsForSharedGame_ReturnsPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "SharedRules.pdf",
            FilePath = "/test/shared.pdf",
            FileSizeBytes = 500_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Language = "IT"
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(sharedGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SharedRules");
        result[0].Language.Should().Be("IT");
    }

    [Fact]
    public async Task Handle_ReturnsPdfsOrderedByUploadDateDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var olderPdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "OlderRules.pdf",
            FilePath = "/test/older.pdf",
            FileSizeBytes = 100_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddDays(-1),
            Language = "EN"
        };
        var newerPdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "NewerRules.pdf",
            FilePath = "/test/newer.pdf",
            FileSizeBytes = 200_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Language = "IT"
        };
        _db.PdfDocuments.AddRange(olderPdf, newerPdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(sharedGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("NewerRules");
        result[1].Name.Should().Be("OlderRules");
    }
}
