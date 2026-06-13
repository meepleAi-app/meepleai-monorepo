using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Integration tests for <see cref="GetUserLibraryQueryHandler"/> that exercise the
/// PostgreSQL-specific <c>EF.Functions.ILike</c> case-insensitive search filter.
///
/// Lives here (not next to the Unit class) because EF InMemory provider does not
/// translate <c>ILike</c>, so the search-filter scenario cannot run against InMemory.
///
/// Issue #2089 — Task 3.5: BE search param coverage for GamePicker unification.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
[Collection("Integration-GroupC")]
public class GetUserLibraryQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_getuserlibrary_ilike_{Guid.NewGuid():N}";

    public GetUserLibraryQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
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
    public async Task GetUserLibraryPaginated_WithSearchParam_ReturnsCaseInsensitiveMatches()
    {
        // Arrange
        var userId = await SeedUserAndLibraryAsync(new[] { "Wingspan", "Catan", "Wingspan: Oceania", "Pandemic" });

        var repository = new UserLibraryRepository(
            _dbContext!,
            Mock.Of<IDomainEventCollector>(),
            NullLogger<UserLibraryRepository>.Instance);

        // Act — Postgres provider translates EF.Functions.ILike to SQL ILIKE.
        // ILike is case-insensitive so "wingspan" matches both "Wingspan" and "Wingspan: Oceania".
        var (entries, total) = await repository.GetUserLibraryPaginatedAsync(
            userId,
            search: "wingspan",
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        total.Should().Be(2);
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserLibraryPaginated_WithSearchParam_ReturnsPartialMatch()
    {
        // Arrange
        var userId = await SeedUserAndLibraryAsync(new[] { "Catan", "Catan: Cities & Knights", "Pandemic" });

        var repository = new UserLibraryRepository(
            _dbContext!,
            Mock.Of<IDomainEventCollector>(),
            NullLogger<UserLibraryRepository>.Instance);

        // Act — partial match "cat" should hit both Catan variants but NOT Pandemic
        var (entries, total) = await repository.GetUserLibraryPaginatedAsync(
            userId,
            search: "cat",
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        total.Should().Be(2);
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserLibraryPaginated_WithSearchParam_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var userId = await SeedUserAndLibraryAsync(new[] { "Catan", "Pandemic" });

        var repository = new UserLibraryRepository(
            _dbContext!,
            Mock.Of<IDomainEventCollector>(),
            NullLogger<UserLibraryRepository>.Instance);

        // Act
        var (entries, total) = await repository.GetUserLibraryPaginatedAsync(
            userId,
            search: "nonexistent",
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        total.Should().Be(0);
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserLibraryPaginated_WithNullSearch_ReturnsAllEntries()
    {
        // Arrange
        var userId = await SeedUserAndLibraryAsync(new[] { "Wingspan", "Catan", "Pandemic" });

        var repository = new UserLibraryRepository(
            _dbContext!,
            Mock.Of<IDomainEventCollector>(),
            NullLogger<UserLibraryRepository>.Instance);

        // Act — null search => no filter
        var (entries, total) = await repository.GetUserLibraryPaginatedAsync(
            userId,
            search: null,
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        total.Should().Be(3);
        entries.Should().HaveCount(3);
    }

    private async Task<Guid> SeedUserAndLibraryAsync(string[] gameTitles)
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

        for (var i = 0; i < gameTitles.Length; i++)
        {
            var gameId = Guid.NewGuid();
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = gameId,
                Title = gameTitles[i],
                YearPublished = 2020,
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                ImageUrl = string.Empty,
                ThumbnailUrl = string.Empty,
                Description = string.Empty,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });

            _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = gameId,
                CurrentState = (int)GameStateType.Owned,
                StateChangedAt = DateTime.UtcNow,
                AddedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();
        return userId;
    }
}
