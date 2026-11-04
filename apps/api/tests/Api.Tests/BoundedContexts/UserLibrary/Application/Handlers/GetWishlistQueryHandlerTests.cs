using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetWishlistQueryHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepoMock = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock = new();
    private readonly GetWishlistQueryHandler _handler;

    public GetWishlistQueryHandlerTests()
    {
        // #4227: senza un gioco configurato il catalogo risponde vuoto.
        _sharedGameRepoMock
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new GetWishlistQueryHandler(_wishlistRepoMock.Object, _sharedGameRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WithItems_ReturnsMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<WishlistItem>
        {
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.High, 49.99m, "Must have"),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Low)
        };

        _wishlistRepoMock.Setup(r => r.GetUserWishlistAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var query = new GetWishlistQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("HIGH", result[0].Priority);
        Assert.Equal(49.99m, result[0].TargetPrice);
        Assert.Equal("Must have", result[0].Notes);
        Assert.Equal("LOW", result[1].Priority);
    }

    [Fact]
    public async Task Handle_EmptyWishlist_ReturnsEmptyList()
    {
        // Arrange
        _wishlistRepoMock.Setup(r => r.GetUserWishlistAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WishlistItem>());

        var query = new GetWishlistQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    // === #4227: il nome del gioco viaggia nel DTO ===

    [Fact]
    public async Task Handle_CarriesTheGameName_ReadInOneBatchFromTheCatalog()
    {
        // Prima il DTO non portava il nome: la pagina lo cercava in una pagina della libreria, poi
        // con una richiesta di dettaglio per gioco, e l'ordinamento alfabetico non funzionava.
        var userId = Guid.NewGuid();
        var wingspanId = Guid.NewGuid();
        var items = new List<WishlistItem>
        {
            WishlistItem.Create(userId, wingspanId, WishlistPriority.High),
            WishlistItem.Create(userId, Guid.NewGuid(), WishlistPriority.Low), // gioco non piu' nel catalogo
        };
        _wishlistRepoMock.Setup(r => r.GetUserWishlistAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(items);
        _sharedGameRepoMock
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [wingspanId] = "Wingspan" });

        var result = await _handler.Handle(new GetWishlistQuery(userId), CancellationToken.None);

        Assert.Equal("Wingspan", result[0].GameName);
        Assert.Null(result[1].GameName);
        _sharedGameRepoMock.Verify(
            r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Highlights_CarryTheGameName_Too()
    {
        var userId = Guid.NewGuid();
        var wingspanId = Guid.NewGuid();
        var highlightsRepo = new Mock<IWishlistRepository>();
        highlightsRepo
            .Setup(r => r.GetHighlightsAsync(userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WishlistItem> { WishlistItem.Create(userId, wingspanId, WishlistPriority.High) });
        var catalog = new Mock<ISharedGameRepository>();
        catalog
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [wingspanId] = "Wingspan" });
        var handler = new GetWishlistHighlightsQueryHandler(highlightsRepo.Object, catalog.Object);

        var result = await handler.Handle(new GetWishlistHighlightsQuery(userId), CancellationToken.None);

        Assert.Equal("Wingspan", Assert.Single(result).GameName);
    }

    [Fact]
    public void Dto_OmitsAMissingGameName_InsteadOfWritingNull()
    {
        // Un client che dichiara gameName facoltativo ma non nullable rifiuterebbe un null:
        // per questo il campo assente non viene scritto (opzioni come ConfigureHttpJsonOptions).
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        var dto = new Api.BoundedContexts.UserLibrary.Application.DTOs.WishlistItemDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HIGH", null, null, DateTime.UtcNow, null, "PRIVATE");

        Assert.DoesNotContain("gameName", System.Text.Json.JsonSerializer.Serialize(dto, options));
        Assert.Contains("\"gameName\":\"Wingspan\"",
            System.Text.Json.JsonSerializer.Serialize(dto with { GameName = "Wingspan" }, options));
    }
}
