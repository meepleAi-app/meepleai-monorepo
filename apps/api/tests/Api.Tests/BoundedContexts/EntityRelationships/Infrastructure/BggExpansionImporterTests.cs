using System.Net;
using System.Text;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Infrastructure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class BggExpansionImporterTests
{
    private static readonly Guid _adminUserId = Guid.NewGuid();
    private const int BaseBggId = 1234;
    private const int ExpansionBggId = 5678;

    // Real SharedGame instances — SharedGame is sealed, cannot be mocked with Moq
    private readonly SharedGame _baseGame;
    private readonly SharedGame _expansionGame;

    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock;
    private readonly Mock<IEntityLinkRepository> _entityLinkRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public BggExpansionImporterTests()
    {
        _baseGame = CreateTestGame(BaseBggId);
        _expansionGame = CreateTestGame(ExpansionBggId);

        _sharedGameRepoMock = new Mock<ISharedGameRepository>();
        _entityLinkRepoMock = new Mock<IEntityLinkRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    private static SharedGame CreateTestGame(int? bggId = null) =>
        SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "A test game for unit testing.",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/image.png",
            thumbnailUrl: "https://example.com/thumb.png",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: bggId);

    private BggExpansionImporter CreateImporterWithXml(string xml)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/"),
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient("BggApi"))
            .Returns(httpClient);

        return new BggExpansionImporter(
            httpClientFactoryMock.Object,
            _sharedGameRepoMock.Object,
            _entityLinkRepoMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<BggExpansionImporter>>().Object);
    }

    private static string BuildBggXml(int bggId, IEnumerable<(int linkedId, string type, bool inbound)> links)
    {
        var linkElements = string.Concat(links.Select(l =>
            $"<link type=\"{l.type}\" id=\"{l.linkedId}\" value=\"Game {l.linkedId}\"{(l.inbound ? " inbound=\"true\"" : "")} />"));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
              <item type="boardgame" id="{bggId}">
                <name type="primary" sortindex="1" value="Test Game" />
                {linkElements}
              </item>
            </items>
            """;
    }

    private BggExpansionImporter CreateImporterWithHttpStatus(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/"),
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient("BggApi"))
            .Returns(httpClient);

        return new BggExpansionImporter(
            httpClientFactoryMock.Object,
            _sharedGameRepoMock.Object,
            _entityLinkRepoMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<BggExpansionImporter>>().Object);
    }

    // ── Happy paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportExpansionsAsync_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var importer = CreateImporterWithXml("");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId));
        _entityLinkRepoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportExpansionsAsync_NoBggId_ReturnsZero()
    {
        // Arrange — real SharedGame without BggId
        var gameWithoutBgg = CreateTestGame(bggId: null);
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(gameWithoutBgg.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameWithoutBgg);

        var importer = CreateImporterWithXml("");

        // Act
        var result = await importer.ImportExpansionsAsync(gameWithoutBgg.Id, _adminUserId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ImportExpansionsAsync_ExpansionOutbound_CreatesExpansionOfLink()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(ExpansionBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_expansionGame);

        _entityLinkRepoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgameexpansion", false) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(1, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(
            It.Is<EntityLink>(el =>
                el.LinkType == EntityLinkType.ExpansionOf &&
                el.SourceEntityId == _expansionGame.Id &&
                el.TargetEntityId == _baseGame.Id &&
                el.IsBggImported &&
                el.IsAdminApproved),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportExpansionsAsync_ExpansionInbound_CreatesExpansionOfLinkReversed()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(ExpansionBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_expansionGame);

        _entityLinkRepoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // inbound=true: currentGame (_baseGame) IS the expansion OF linkedGame (_expansionGame)
        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgameexpansion", true) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(1, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(
            It.Is<EntityLink>(el =>
                el.LinkType == EntityLinkType.ExpansionOf &&
                el.SourceEntityId == _baseGame.Id &&
                el.TargetEntityId == _expansionGame.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportExpansionsAsync_ReimplementsInbound_CreatesReimplementsLink()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(ExpansionBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_expansionGame);

        _entityLinkRepoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // inbound=true: currentGame reimplements linkedGame
        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgameimplementation", true) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(1, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(
            It.Is<EntityLink>(el =>
                el.LinkType == EntityLinkType.Reimplements &&
                el.SourceEntityId == _baseGame.Id &&
                el.TargetEntityId == _expansionGame.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportExpansionsAsync_DuplicateLink_SkipsAndReturnsZero()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(ExpansionBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_expansionGame);

        // BR-08: Link already exists
        _entityLinkRepoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgameexpansion", false) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(0, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportExpansionsAsync_LinkedGameNotInCatalog_SkipsLink()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        // Linked game is NOT in our catalog
        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(ExpansionBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgameexpansion", false) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(0, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportExpansionsAsync_NoLinksInXml_ReturnsZero()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        var emptyLinks = Array.Empty<(int, string, bool)>();
        var xml = BuildBggXml(BaseBggId, emptyLinks);
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ImportExpansionsAsync_IgnoresNonExpansionLinkTypes()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        // Non-expansion link type (boardgamemechanic, boardgamecategory, etc.)
        var xml = BuildBggXml(BaseBggId, new[] { (ExpansionBggId, "boardgamemechanic", false) });
        var importer = CreateImporterWithXml(xml);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert
        Assert.Equal(0, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ImportExpansionsAsync_BggApiNonSuccessStatus_ReturnsZero(HttpStatusCode statusCode)
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(_baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_baseGame);

        var importer = CreateImporterWithHttpStatus(statusCode);

        // Act
        var result = await importer.ImportExpansionsAsync(_baseGame.Id, _adminUserId);

        // Assert — BGG API failures degrade gracefully, no links created
        Assert.Equal(0, result);
        _entityLinkRepoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
