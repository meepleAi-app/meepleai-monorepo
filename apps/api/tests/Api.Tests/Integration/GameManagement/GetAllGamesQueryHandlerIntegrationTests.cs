using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for <see cref="GetAllGamesQueryHandler"/> that exercise the
/// PostgreSQL-specific <c>EF.Functions.ILike</c> case-insensitive search.
///
/// Lives here (not next to the Unit class) because EF InMemory provider does not
/// translate <c>ILike</c>, so the search-filter scenario cannot run against InMemory.
/// All other handler behaviors remain covered by the Unit test class.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupC")]
public class GetAllGamesQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_getallgames_ilike_{Guid.NewGuid():N}";

    public GetAllGamesQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
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
    public async Task Handle_WithSearchFilter_ReturnsMatchingGames()
    {
        // Arrange
        _dbContext!.SharedGames.AddRange(
            MakeSharedGame("Catan"),
            MakeSharedGame("Pandemic"),
            MakeSharedGame("Carcassonne"));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetAllGamesQueryHandler(_dbContext);
        var query = new GetAllGamesQuery(Search: "Catan");

        // Act — Postgres provider translates EF.Functions.ILike to SQL ILIKE.
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Games.Should().Contain(g => g.Title == "Catan");
        result.Games.Should().HaveCount(1);
    }

    private static SharedGameEntity MakeSharedGame(string title) => new()
    {
        Id = Guid.NewGuid(),
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
    };
}
