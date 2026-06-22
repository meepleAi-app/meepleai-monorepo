using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Unit tests for GetAllGamesQueryHandler.
/// Tests game catalog retrieval and DTO mapping against InMemory DbContext.
/// Issue #1320 (P2c): Migrated from IGameRepository mock to SharedGames EF query.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAllGamesQueryHandlerTests
{
    private static MeepleAiDbContext CreateContext() =>
        TestDbContextFactory.CreateInMemoryDbContext();

    private static GetAllGamesQueryHandler CreateHandler(MeepleAiDbContext context) =>
        new(context);

    private static SharedGameEntity MakeSharedGame(
        string title,
        int yearPublished = 2020,
        int minPlayers = 2,
        int maxPlayers = 4,
        int? bggId = null,
        string? imageUrl = null)
    {
        return new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = yearPublished,
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            BggId = bggId,
            ImageUrl = imageUrl ?? string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_WithMultipleGames_ReturnsAllMappedDtos()
    {
        // Arrange
        await using var context = CreateContext();
        context.SharedGames.AddRange(
            MakeSharedGame("Catan", 1995, 3, 4),
            MakeSharedGame("Pandemic", 2008, 2, 4),
            MakeSharedGame("Ticket to Ride", 2004, 2, 5));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Games.Count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithSingleGame_ReturnsSingleElementList()
    {
        // Arrange
        await using var context = CreateContext();
        context.SharedGames.Add(MakeSharedGame("Chess"));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Games.Should().ContainSingle();
        result.Games[0].Title.Should().Be("Chess");
    }

    [Fact]
    public async Task Handle_WithNoGames_ReturnsEmptyList()
    {
        // Arrange
        await using var context = CreateContext();
        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Games.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsAllPropertiesToDto()
    {
        // Arrange
        await using var context = CreateContext();
        var gameId = Guid.NewGuid();
        context.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Wingspan",
            YearPublished = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 70,
            BggId = 266192,
            ImageUrl = "https://example.com/wingspan.jpg",
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            MinAge = 10,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var dto = result.Games[0];
        dto.Id.Should().Be(gameId);
        dto.Title.Should().Be("Wingspan");
        dto.YearPublished.Should().Be(2019);
        dto.MinPlayers.Should().Be(1);
        dto.MaxPlayers.Should().Be(5);
        dto.BggId.Should().Be(266192);
        dto.ImageUrl.Should().Be("https://example.com/wingspan.jpg");
        dto.CreatedAt.Should().NotBe(default(DateTime));
    }

    [Fact]
    public async Task Handle_WithBggLinkedGames_IncludesBggIds()
    {
        // Arrange
        await using var context = CreateContext();
        context.SharedGames.AddRange(
            MakeSharedGame("Catan", bggId: 13),
            MakeSharedGame("Pandemic", bggId: 30549),
            MakeSharedGame("Chess")); // No BGG link
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Games.Count.Should().Be(3);
        // Games are ordered by title alphabetically
        var catan = result.Games.First(g => g.Title == "Catan");
        var pandemic = result.Games.First(g => g.Title == "Pandemic");
        var chess = result.Games.First(g => g.Title == "Chess");
        catan.BggId.Should().Be(13);
        pandemic.BggId.Should().Be(30549);
        chess.BggId.Should().BeNull();
    }

    // NOTE: Handle_WithSearchFilter_ReturnsMatchingGames lives in
    // Api.Tests.Integration.GameManagement.GetAllGamesQueryHandlerIntegrationTests
    // because EF.Functions.ILike is PostgreSQL-specific and InMemory cannot translate it.

    [Fact]
    public async Task Handle_PaginationReturnsCorrectPage()
    {
        // Arrange
        await using var context = CreateContext();
        for (int i = 1; i <= 10; i++)
        {
            context.SharedGames.Add(MakeSharedGame($"Game {i:D2}"));
        }
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var query = new GetAllGamesQuery(Page: 2, PageSize: 3);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(10);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.Games.Count.Should().Be(3);
        result.TotalPages.Should().Be(4); // ceil(10/3) = 4
    }
}
