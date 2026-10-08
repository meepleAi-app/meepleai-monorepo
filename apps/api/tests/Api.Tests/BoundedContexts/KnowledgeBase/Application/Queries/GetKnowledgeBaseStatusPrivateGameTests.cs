using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
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

    // Issue #4137: the handler now authorizes every read, so a test that wants a
    // result has to own the game it asks about. Before, these tests passed an id
    // with no identity at all, which is exactly the hole that was closed.
    private static readonly Guid OwnerId = Guid.NewGuid();

    public GetKnowledgeBaseStatusPrivateGameTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetKnowledgeBaseStatusQueryHandler(
            _dbContext,
            new RagAccessService(_dbContext),
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);
    }

    private static GetKnowledgeBaseStatusQuery OwnerQuery(Guid gameId, bool isPrivate = true)
        => new(gameId, OwnerId, "User", IsPrivateGame: isPrivate);

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_PrivateGame_ReadyPdf_ReturnsCompletedStatus()
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            Title = "Test Rulebook Game",
            OwnerId = OwnerId,
            Source = PrivateGameSource.Manual,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = PrivateGameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(OwnerQuery(PrivateGameId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
        result.GameName.Should().Be("Test Rulebook Game");
    }

    [Fact]
    public async Task Handle_PrivateGame_NoPdf_ReturnsPending()
    {
        // Issue #4137: this used to ask about a random id with no PrivateGame row at
        // all, and still expected "Pending". With authorization the game has to exist
        // and belong to the caller - which is also what the case is really about.
        var gameId = Guid.NewGuid();
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            Title = "Owned, no PDF",
            OwnerId = OwnerId,
            Source = PrivateGameSource.Manual,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(OwnerQuery(gameId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
    }

    // The regression guard for the IDOR itself: the endpoint authenticated and then
    // threw the user id away, so any logged-in user could read any private game's KB
    // status by id. A denial returns null, which the endpoint maps to 404 - not 403,
    // which would confirm that the id exists and belongs to someone else.
    [Fact]
    public async Task Handle_PrivateGame_NotOwnedByCaller_ReturnsNull()
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            Title = "Another user's game",
            OwnerId = Guid.NewGuid(),
            Source = PrivateGameSource.Manual,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(OwnerQuery(PrivateGameId), CancellationToken.None);

        result.Should().BeNull();
    }

    // And the other direction for the admin decision of #4137: an admin may read it.
    [Fact]
    public async Task Handle_PrivateGame_NotOwnedButAdmin_ReturnsStatus()
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            Title = "Another user's game",
            OwnerId = Guid.NewGuid(),
            Source = PrivateGameSource.Manual,
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKnowledgeBaseStatusQuery(PrivateGameId, Guid.NewGuid(), "Admin", IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_PrivateGame_DoesNotReturnSharedGamePdf()
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            Title = "Owned game",
            OwnerId = OwnerId,
            Source = PrivateGameSource.Manual,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = null,
            FileName = "shared.pdf",
            FilePath = "/uploads/shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(OwnerQuery(PrivateGameId), CancellationToken.None);

        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_SharedGame_IsPrivateGameFalse_ReturnsSharedPdf()
    {
        var sharedGameId = Guid.NewGuid();
        // Issue #4137: the shared-game path is authorized by the same rules, so the
        // row has to be reachable - IsRagPublic is the cheapest way to say that here.
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Public shared game",
            IsRagPublic = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId, // Handler filters PDFs by SharedGameId when IsPrivateGame=false
            PrivateGameId = null,
            FileName = "shared.pdf",
            FilePath = "/uploads/shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(OwnerQuery(sharedGameId, isPrivate: false), CancellationToken.None);

        result!.Status.Should().Be("Completed");
    }
}
