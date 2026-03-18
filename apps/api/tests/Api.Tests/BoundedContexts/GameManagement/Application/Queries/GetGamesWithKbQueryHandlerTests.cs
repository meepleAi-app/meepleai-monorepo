using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Unit tests for GetGamesWithKbQueryHandler — cross-context EF join query
/// that returns games with KB-linked rulebooks via EntityLink (Game→KbCard).
/// Tests grouping, status mapping, and overall KB status aggregation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetGamesWithKbQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetGamesWithKbQueryHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public GetGamesWithKbQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetGamesWithKbQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    #region Empty Results

    [Fact]
    public async Task Handle_WhenUserHasNoLibraryEntries_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenUserHasGamesButNoEntityLinks_ReturnsEmptyList()
    {
        // Arrange — game in library but no EntityLink to any PDF
        var gameId = Guid.NewGuid();
        SeedGame(gameId, "Catan");
        SeedLibraryEntry(TestUserId, gameId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Single Game with Single Rulebook

    [Fact]
    public async Task Handle_WhenGameHasReadyRulebook_ReturnsGameWithReadyStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Catan", "https://img.example.com/catan.jpg");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "catan-rules.pdf", "Ready", DateTime.UtcNow);
        SeedEntityLink(gameId, pdfId, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);

        var game = result[0];
        game.GameId.Should().Be(gameId);
        game.Title.Should().Be("Catan");
        game.ImageUrl.Should().Be("https://img.example.com/catan.jpg");
        game.OverallKbStatus.Should().Be("ready");
        game.Rulebooks.Should().HaveCount(1);

        var rulebook = game.Rulebooks[0];
        rulebook.PdfDocumentId.Should().Be(pdfId);
        rulebook.FileName.Should().Be("catan-rules.pdf");
        rulebook.KbStatus.Should().Be("ready");
        rulebook.IndexedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenGameHasProcessingRulebook_ReturnsProcessingStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Wingspan");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "wingspan-rules.pdf", "Extracting");
        SeedEntityLink(gameId, pdfId, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].OverallKbStatus.Should().Be("processing");
        result[0].Rulebooks[0].KbStatus.Should().Be("processing");
        result[0].Rulebooks[0].IndexedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGameHasPendingRulebook_ReturnsPendingMappedStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Azul");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "azul-rules.pdf", "Pending");
        SeedEntityLink(gameId, pdfId, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].OverallKbStatus.Should().Be("processing"); // pending rolls up to processing
        result[0].Rulebooks[0].KbStatus.Should().Be("pending");
    }

    [Fact]
    public async Task Handle_WhenGameHasFailedRulebook_ReturnsFailedStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Gloomhaven");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "gloomhaven-rules.pdf", "Failed");
        SeedEntityLink(gameId, pdfId, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].OverallKbStatus.Should().Be("failed");
        result[0].Rulebooks[0].KbStatus.Should().Be("failed");
    }

    #endregion

    #region Multiple Rulebooks — Overall Status Aggregation

    [Fact]
    public async Task Handle_WhenGameHasReadyAndProcessing_OverallIsReady()
    {
        // Arrange — "ready" wins if any rulebook is ready
        var gameId = Guid.NewGuid();
        var pdfReady = Guid.NewGuid();
        var pdfProcessing = Guid.NewGuid();

        SeedGame(gameId, "Terraforming Mars");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfReady, "base-rules.pdf", "Ready", DateTime.UtcNow);
        SeedPdfDocument(pdfProcessing, "expansion-rules.pdf", "Embedding");
        SeedEntityLink(gameId, pdfReady, TestUserId);
        SeedEntityLink(gameId, pdfProcessing, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].OverallKbStatus.Should().Be("ready");
        result[0].Rulebooks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenGameHasProcessingAndFailed_OverallIsProcessing()
    {
        // Arrange — "processing" beats "failed"
        var gameId = Guid.NewGuid();
        var pdfProcessing = Guid.NewGuid();
        var pdfFailed = Guid.NewGuid();

        SeedGame(gameId, "Spirit Island");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfProcessing, "base.pdf", "Chunking");
        SeedPdfDocument(pdfFailed, "expansion.pdf", "Failed");
        SeedEntityLink(gameId, pdfProcessing, TestUserId);
        SeedEntityLink(gameId, pdfFailed, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].OverallKbStatus.Should().Be("processing");
    }

    #endregion

    #region Multiple Games

    [Fact]
    public async Task Handle_WhenUserHasMultipleGamesWithKb_ReturnsAll()
    {
        // Arrange
        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();
        var pdf1Id = Guid.NewGuid();
        var pdf2Id = Guid.NewGuid();

        SeedGame(game1Id, "Catan");
        SeedGame(game2Id, "Wingspan");
        SeedLibraryEntry(TestUserId, game1Id);
        SeedLibraryEntry(TestUserId, game2Id);
        SeedPdfDocument(pdf1Id, "catan-rules.pdf", "Ready", DateTime.UtcNow);
        SeedPdfDocument(pdf2Id, "wingspan-rules.pdf", "Ready", DateTime.UtcNow);
        SeedEntityLink(game1Id, pdf1Id, TestUserId);
        SeedEntityLink(game2Id, pdf2Id, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(g => g.Title).Should().Contain("Catan").And.Contain("Wingspan");
    }

    #endregion

    #region Filtering — Deleted Links and Other Users

    [Fact]
    public async Task Handle_WhenEntityLinkIsDeleted_ExcludesGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Catan");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "catan-rules.pdf", "Ready", DateTime.UtcNow);
        SeedEntityLink(gameId, pdfId, TestUserId, isDeleted: true);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenOtherUserHasGames_DoesNotReturnThem()
    {
        // Arrange — another user's game with KB
        var otherUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Catan");
        SeedLibraryEntry(otherUserId, gameId);
        SeedPdfDocument(pdfId, "catan-rules.pdf", "Ready", DateTime.UtcNow);
        SeedEntityLink(gameId, pdfId, otherUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Status Mapping — All Processing States

    [Theory]
    [InlineData("Pending", "pending")]
    [InlineData("Uploading", "pending")]
    [InlineData("Extracting", "processing")]
    [InlineData("Chunking", "processing")]
    [InlineData("Embedding", "processing")]
    [InlineData("Indexing", "processing")]
    [InlineData("Ready", "ready")]
    [InlineData("Failed", "failed")]
    public async Task Handle_MapsProcessingStateToKbStatusCorrectly(
        string processingState, string expectedKbStatus)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedGame(gameId, "Test Game");
        SeedLibraryEntry(TestUserId, gameId);
        SeedPdfDocument(pdfId, "rules.pdf", processingState,
            processingState == "Ready" ? DateTime.UtcNow : null);
        SeedEntityLink(gameId, pdfId, TestUserId);
        await _db.SaveChangesAsync();

        var query = new GetGamesWithKbQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Rulebooks[0].KbStatus.Should().Be(expectedKbStatus);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    #endregion

    #region Helpers

    private void SeedGame(Guid gameId, string name, string? imageUrl = null)
    {
        _db.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = name,
            ImageUrl = imageUrl
        });
    }

    private void SeedLibraryEntry(Guid userId, Guid gameId)
    {
        _db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = userId,
            SharedGameId = gameId
        });
    }

    private void SeedPdfDocument(Guid pdfId, string fileName, string processingState,
        DateTime? processedAt = null)
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = fileName,
            FilePath = $"/uploads/{pdfId:N}.pdf",
            ProcessingState = processingState,
            ProcessedAt = processedAt,
            UploadedByUserId = Guid.NewGuid()
        });
    }

    private void SeedEntityLink(Guid gameId, Guid pdfId, Guid ownerUserId,
        bool isDeleted = false)
    {
        var link = EntityLink.Create(
            sourceEntityType: MeepleEntityType.Game,
            sourceEntityId: gameId,
            targetEntityType: MeepleEntityType.KbCard,
            targetEntityId: pdfId,
            linkType: EntityLinkType.RelatedTo,
            scope: EntityLinkScope.User,
            ownerUserId: ownerUserId);

        if (isDeleted)
        {
            link.Delete(ownerUserId);
        }

        // Clear domain events to avoid side effects in tests
        link.PopDomainEvents();

        _db.EntityLinks.Add(link);
    }

    #endregion
}
