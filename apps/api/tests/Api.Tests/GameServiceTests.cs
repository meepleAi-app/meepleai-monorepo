using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GameServiceTests
{
    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task CreateGameAsync_WhenRequestedIdProvided_NormalizesAndPersists()
    {
        await using var dbContext = CreateInMemoryContext();
        var service = new GameService(dbContext);

        var game = await service.CreateGameAsync("  My Game  ", "My Game.ID  ");

        Assert.Equal("my-game-id", game.Id);
        Assert.Equal("My Game", game.Name);

        var stored = await dbContext.Games.FirstAsync();
        Assert.Equal("my-game-id", stored.Id);
    }

    [Fact]
    public async Task CreateGameAsync_WhenDuplicateId_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "duplicate", Name = "Existing" });
        await dbContext.SaveChangesAsync();

        var service = new GameService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGameAsync("Another", "duplicate"));
    }

    [Fact]
    public async Task CreateGameAsync_WhenDuplicateName_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "existing", Name = "Existing Game" });
        await dbContext.SaveChangesAsync();

        var service = new GameService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGameAsync("Existing Game", "new-id"));
    }

    [Fact]
    public async Task CreateGameAsync_UsesTimeProviderForCreatedAt()
    {
        await using var dbContext = CreateInMemoryContext();
        var fixedTime = new DateTimeOffset(new DateTime(2024, 05, 01, 12, 0, 0, DateTimeKind.Utc));
        var timeProvider = new FixedTimeProvider(fixedTime);
        var service = new GameService(dbContext, timeProvider);

        var game = await service.CreateGameAsync("Timed Game", null);

        Assert.Equal(fixedTime.UtcDateTime, game.CreatedAt);
    }

    [Fact]
    public async Task GetGamesAsync_ReturnsAlphabetical()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.AddRange(
            new GameEntity { Id = "b", Name = "Bravo" },
            new GameEntity { Id = "c", Name = "Charlie" },
            new GameEntity { Id = "a", Name = "Alpha" });
        await dbContext.SaveChangesAsync();

        var service = new GameService(dbContext);

        var games = await service.GetGamesAsync();

        Assert.Collection(
            games,
            g => Assert.Equal("Alpha", g.Name),
            g => Assert.Equal("Bravo", g.Name),
            g => Assert.Equal("Charlie", g.Name));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;

        public FixedTimeProvider(DateTimeOffset value)
        {
            _value = value;
        }

        public override DateTimeOffset GetUtcNow() => _value;
    }
}
