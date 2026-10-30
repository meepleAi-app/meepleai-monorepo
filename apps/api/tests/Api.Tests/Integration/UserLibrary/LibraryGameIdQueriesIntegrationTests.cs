using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// #4749 — two handlers filtered library entries on <c>UserLibraryEntryEntity.GameId</c>, the
/// compatibility property #3662 left outside the EF model (<c>builder.Ignore(e =&gt; e.GameId)</c>).
/// EF cannot translate a member it does not map, so every call answered 500. Lives here because only
/// a real PostgreSQL provider builds the SQL: the translation failure never shows on InMemory.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
[Collection("Integration-GroupC")]
public class LibraryGameIdQueriesIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private readonly string _databaseName = $"test_library_gameid_{Guid.NewGuid():N}";

    public LibraryGameIdQueriesIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task BatchCheck_ReportsWhichRequestedGamesAreInTheCallersLibrary()
    {
        var viewer = await SeedUserAsync();
        var stranger = await SeedUserAsync();
        var favourite = await SeedGameAsync("Catan");
        var plain = await SeedGameAsync("Azul");
        var notInLibrary = await SeedGameAsync("Pandemic");
        await AddToLibraryAsync(viewer, favourite, isFavorite: true);
        await AddToLibraryAsync(viewer, plain);
        await AddToLibraryAsync(stranger, notInLibrary);
        var handler = new BatchCheckGamesInLibraryQueryHandler(_dbContext!);

        var result = await handler.Handle(
            new BatchCheckGamesInLibraryQuery(viewer, [favourite, plain, notInLibrary]),
            TestContext.Current.CancellationToken);

        result.TotalChecked.Should().Be(3);
        result.Results[favourite].InLibrary.Should().BeTrue();
        result.Results[favourite].IsFavorite.Should().BeTrue();
        result.Results[plain].InLibrary.Should().BeTrue();
        result.Results[plain].IsFavorite.Should().BeFalse();
        result.Results[notInLibrary].InLibrary.Should().BeFalse("only the caller's own library counts, not another user's entry for the same game");
    }

    [Fact]
    public async Task BulkAssociatedData_CountsOnlyTheCallersEntriesForTheRequestedGames()
    {
        var viewer = await SeedUserAsync();
        var stranger = await SeedUserAsync();
        var first = await SeedGameAsync("Catan");
        var second = await SeedGameAsync("Azul");
        var strangers = await SeedGameAsync("Pandemic");
        await AddToLibraryAsync(viewer, first, checklistItems: 2);
        await AddToLibraryAsync(viewer, second, checklistItems: 1);
        await AddToLibraryAsync(stranger, strangers, checklistItems: 5);
        var handler = new GetBulkCollectionAssociatedDataQueryHandler(_dbContext!);

        var both = await handler.Handle(
            new GetBulkCollectionAssociatedDataQuery(viewer, EntityType.Game, [first, second]),
            TestContext.Current.CancellationToken);
        var withAStrangersGame = await handler.Handle(
            new GetBulkCollectionAssociatedDataQuery(viewer, EntityType.Game, [first, strangers]),
            TestContext.Current.CancellationToken);

        both.TotalChecklistItems.Should().Be(3);
        withAStrangersGame.TotalChecklistItems.Should().Be(2, "the stranger's entry is not the caller's");
    }

    private async Task<Guid> SeedUserAsync()
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test_{Guid.NewGuid():N}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userId;
    }

    private async Task<Guid> SeedGameAsync(string title)
    {
        var gameId = Guid.NewGuid();
        _dbContext!.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = title,
            YearPublished = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gameId;
    }

    private async Task AddToLibraryAsync(Guid userId, Guid gameId, bool isFavorite = false, int checklistItems = 0)
    {
        var entry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = gameId,
            CurrentState = (int)GameStateType.Owned,
            StateChangedAt = DateTime.UtcNow,
            AddedAt = DateTime.UtcNow,
            IsFavorite = isFavorite
        };
        for (var i = 0; i < checklistItems; i++)
        {
            entry.Checklist.Add(new UserGameChecklistEntity
            {
                UserLibraryEntryId = entry.Id,
                Description = $"Step {i}",
                DisplayOrder = i
            });
        }

        _dbContext!.UserLibraryEntries.Add(entry);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();
    }
}
