using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

public class GameServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public GameServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Keep connection open for the lifetime of the test class
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
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

        game.Id.Should().Be("my-game-id");
        game.Name.Should().Be("My Game");

        var stored = await dbContext.Games.FirstAsync();
        stored.Id.Should().Be("my-game-id");
    }

    [Fact]
    public async Task CreateGameAsync_WhenDuplicateId_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "duplicate", Name = "Existing" });
        await dbContext.SaveChangesAsync();

        var service = new GameService(dbContext);

        var act = async () => service.CreateGameAsync("Another", "duplicate");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateGameAsync_WhenDuplicateName_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "existing", Name = "Existing Game" });
        await dbContext.SaveChangesAsync();

        var service = new GameService(dbContext);

        var act = async () => service.CreateGameAsync("Existing Game", "new-id");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateGameAsync_UsesTimeProviderForCreatedAt()
    {
        await using var dbContext = CreateInMemoryContext();
        var fixedTime = new DateTimeOffset(new DateTime(2024, 05, 01, 12, 0, 0, DateTimeKind.Utc));
        var timeProvider = new FixedTimeProvider(fixedTime);
        var service = new GameService(dbContext, timeProvider);

        var game = await service.CreateGameAsync("Timed Game", null);

        game.CreatedAt.Should().Be(fixedTime.UtcDateTime);
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

        games.Should().HaveCount(3);
        games.Select(g => g.Name).Should().BeEquivalentTo(new[] { "Alpha", "Bravo", "Charlie" });
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
