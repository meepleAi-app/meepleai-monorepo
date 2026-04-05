using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKnowledgeBaseStatusPrivateGameTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetKnowledgeBaseStatusQueryHandler _handler;
    private static readonly Guid PrivateGameId = Guid.NewGuid();

    public GetKnowledgeBaseStatusPrivateGameTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetKnowledgeBaseStatusQueryHandler(
            _dbContext,
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_PrivateGame_ReadyPdf_ReturnsCompletedStatus()
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            Title = "Test Rulebook Game",
            OwnerId = Guid.NewGuid(),
            Source = PrivateGameSource.Manual,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = PrivateGameId,
            GameId = null,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKnowledgeBaseStatusQuery(PrivateGameId, IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
        result.GameName.Should().Be("Test Rulebook Game");
    }

    [Fact]
    public async Task Handle_PrivateGame_NoPdf_ReturnsPending()
    {
        var query = new GetKnowledgeBaseStatusQuery(Guid.NewGuid(), IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_PrivateGame_DoesNotReturnSharedGamePdf()
    {
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = PrivateGameId,
            PrivateGameId = null,
            FileName = "shared.pdf",
            FilePath = "/uploads/shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKnowledgeBaseStatusQuery(PrivateGameId, IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_SharedGame_IsPrivateGameFalse_ReturnsSharedPdf()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId,
            PrivateGameId = null,
            FileName = "shared.pdf",
            FilePath = "/uploads/shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKnowledgeBaseStatusQuery(sharedGameId, IsPrivateGame: false);
        var result = await _handler.Handle(query, CancellationToken.None);

        result!.Status.Should().Be("Completed");
    }
}
