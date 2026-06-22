using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for PlayRecord commands.
/// Tests CQRS command handlers with database persistence.
/// Issue #3889: CQRS commands for play records.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "3889")]
public sealed class PlayRecordCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    public PlayRecordCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private MeepleAiDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_playrecord_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();
        // #2349: IGameCoreDataProvider is required by CreatePlayRecordCommandHandler.
        // Register the real SharedGameRepository (only needs DbContext + IDomainEventCollector,
        // both provided by CreateBase) and the real GameCoreDataProvider.
        // IPrivateGameRepository is already mocked in IntegrationServiceCollectionBuilder.CreateBase.
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await DbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        // Fixture handles database cleanup automatically
    }

    #region CreatePlayRecordCommand Tests

    [Fact]
    public async Task CreatePlayRecordCommand_WithCatalogGame_CreatesSuccessfully()
    {
        // Arrange
        var (gameId, gameTitle) = await CreateTestGameAsync();
        var userId = await SeedTestUserAsync();
        var command = new CreatePlayRecordCommand(
            userId,
            gameId,
            gameTitle,
            _timeProvider.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private);

        // Act
        var recordId = await SendInScopeAsync(command);

        // Assert
        recordId.Should().NotBeEmpty();
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.GameId.Should().Be(gameId);
        record.GameName.Should().Be(gameTitle);
        record.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreatePlayRecordCommand_FreeForm_CreatesSuccessfully()
    {
        // Arrange
        var userId = await SeedTestUserAsync();
        var scoringDimensions = new List<string> { "points", "ranking" };
        var dimensionUnits = new Dictionary<string, string>
        {
            ["points"] = "pts",
            ["ranking"] = "#"
        };

        var command = new CreatePlayRecordCommand(
            userId,
            null,
            "Poker",
            _timeProvider.UtcNow.AddHours(-2),
            PlayRecordVisibility.Private,
            ScoringDimensions: scoringDimensions,
            DimensionUnits: dimensionUnits);

        // Act
        var recordId = await SendInScopeAsync(command);

        // Assert
        recordId.Should().NotBeEmpty();
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.GameId.Should().BeNull();
        record.GameName.Should().Be("Poker");
    }

    [Fact]
    public async Task CreatePlayRecordCommand_NonExistentGame_ThrowsNotFoundException()
    {
        // Arrange
        var command = new CreatePlayRecordCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "NonExistent",
            _timeProvider.UtcNow,
            PlayRecordVisibility.Private);

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region AddPlayerToRecordCommand Tests

    [Fact]
    public async Task AddPlayerToRecordCommand_RegisteredUser_AddsSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var userId = await SeedTestUserAsync(); // FK constraint: record_players.UserId references users
        var command = new AddPlayerToRecordCommand(recordId, creatorId, userId, "Alice");

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var players = await db.RecordPlayers
            .Where(p => p.PlayRecordId == recordId)
            .ToListAsync(TestCancellationToken);

        players.Should().HaveCount(1);
        players[0].UserId.Should().Be(userId);
        players[0].DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task AddPlayerToRecordCommand_Guest_AddsSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new AddPlayerToRecordCommand(recordId, creatorId, null, "Bob");

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var players = await db.RecordPlayers
            .Where(p => p.PlayRecordId == recordId)
            .ToListAsync(TestCancellationToken);

        players.Should().HaveCount(1);
        players[0].UserId.Should().BeNull();
        players[0].DisplayName.Should().Be("Bob");
    }

    [Fact]
    public async Task AddPlayerToRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, add-player attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new AddPlayerToRecordCommand(recordId, otherUserId, null, "Hijack");

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region RecordScoreCommand Tests

    [Fact]
    public async Task RecordScoreCommand_ValidScore_RecordsSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var playerId = await AddTestPlayerAsync(recordId, creatorId);

        var command = new RecordScoreCommand(recordId, creatorId, playerId, "points", 42, "pts");

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var scores = await db.RecordScores
            .Where(s => s.RecordPlayerId == playerId)
            .ToListAsync(TestCancellationToken);

        scores.Should().HaveCount(1);
        scores[0].Dimension.Should().Be("points");
        scores[0].Value.Should().Be(42);
    }

    [Fact]
    public async Task RecordScoreCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, score attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var playerId = await AddTestPlayerAsync(recordId, creatorId);
        var command = new RecordScoreCommand(recordId, otherUserId, playerId, "points", 99);

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region StartPlayRecordCommand Tests

    [Fact]
    public async Task StartPlayRecordCommand_PlannedRecord_StartsSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new StartPlayRecordCommand(recordId, creatorId);

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.Status.Should().Be((int)PlayRecordStatus.InProgress);
        record.StartTime.Should().NotBeNull();
    }

    [Fact]
    public async Task StartPlayRecordCommand_NonPlannedRecord_ThrowsConflictException()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var startCommand = new StartPlayRecordCommand(recordId, creatorId);

        await SendInScopeAsync(startCommand);

        // Act & Assert
        var act = () => SendInScopeAsync(startCommand);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task StartPlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, start attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new StartPlayRecordCommand(recordId, otherUserId);

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region CompletePlayRecordCommand Tests

    [Fact]
    public async Task CompletePlayRecordCommand_WithManualDuration_CompletesSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var duration = TimeSpan.FromHours(2);
        var command = new CompletePlayRecordCommand(recordId, creatorId, duration);

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.Status.Should().Be((int)PlayRecordStatus.Completed);
        record.Duration.Should().Be(duration);
    }

    #endregion

    #region UpdatePlayRecordCommand Tests

    [Fact]
    public async Task UpdatePlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, update attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new UpdatePlayRecordCommand(recordId, otherUserId, Notes: "hijack");

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CompletePlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new CompletePlayRecordCommand(recordId, otherUserId);

        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdatePlayRecordCommand_ValidData_UpdatesSuccessfully()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var newDate = _timeProvider.UtcNow.AddDays(-2);
        var command = new UpdatePlayRecordCommand(
            recordId,
            creatorId,
            SessionDate: newDate,
            Notes: "Great game!",
            Location: "Home");

        // Act
        await SendInScopeAsync(command);

        // Assert
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.SessionDate.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
        record.Notes.Should().Be("Great game!");
        record.Location.Should().Be("Home");
    }

    #endregion

    #region DeletePlayRecordCommand Tests

    [Fact]
    public async Task DeletePlayRecordCommand_AsCreator_SoftDeletesAndHidesRecord()
    {
        // Arrange
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);

        // Act
        await SendInScopeAsync(new DeletePlayRecordCommand(recordId, creatorId));

        // Assert — hidden by query filter, but persisted with is_deleted=true
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var visible = await db.PlayRecords
            .FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        visible.Should().BeNull("the soft-delete query filter must hide the record");

        var raw = await db.PlayRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        raw.Should().NotBeNull();
        raw!.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeletePlayRecordCommand_UserIsNotCreator_ThrowsForbiddenException()
    {
        // Arrange — record created by user A, delete attempted by user B
        var creatorId = await SeedTestUserAsync();
        var otherUserId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        var command = new DeletePlayRecordCommand(recordId, otherUserId);

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeletePlayRecordCommand_NonExistentRecord_ThrowsNotFoundException()
    {
        // Arrange
        var command = new DeletePlayRecordCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => SendInScopeAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeletePlayRecordCommand_AlreadyDeleted_ThrowsNotFoundException()
    {
        // Arrange — a second delete resolves to NotFound because the query filter hides the row
        var creatorId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(creatorId);
        await SendInScopeAsync(new DeletePlayRecordCommand(recordId, creatorId));

        // Act & Assert
        var act = () => SendInScopeAsync(new DeletePlayRecordCommand(recordId, creatorId));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds a SharedGameEntity row directly via MeepleAiDbContext (IGameRepository removed by #1320 P2c).
    /// Returns (Id, Title) tuple for use by play-record commands.
    /// </summary>
    private async Task<(Guid Id, string Title)> CreateTestGameAsync()
    {
        var id = Guid.NewGuid();
        const string title = "Test Game";

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);

        return (id, title);
    }

    /// <summary>
    /// Seeds a UserEntity in the database to satisfy FK constraints on play_records.CreatedByUserId.
    /// </summary>
    private async Task<Guid> SeedTestUserAsync(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"test-{id:N}@meepleai.dev",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    /// <summary>
    /// Sends a MediatR command using a fresh DI scope to avoid DbContext tracking conflicts
    /// when multiple commands operate on the same entities.
    /// </summary>
    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, TestCancellationToken);
    }

    private async Task<Guid> CreateTestRecordAsync()
    {
        var (gameId, gameTitle) = await CreateTestGameAsync();
        var userId = await SeedTestUserAsync();

        var command = new CreatePlayRecordCommand(
            userId,
            gameId,
            gameTitle,
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        return await SendInScopeAsync(command);
    }

    private async Task<Guid> CreateTestRecordAsync(Guid creatorUserId)
    {
        var (gameId, gameTitle) = await CreateTestGameAsync();

        var command = new CreatePlayRecordCommand(
            creatorUserId,
            gameId,
            gameTitle,
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        return await SendInScopeAsync(command);
    }

    private async Task<Guid> AddTestPlayerAsync(Guid recordId, Guid callerUserId)
    {
        var playerUserId = await SeedTestUserAsync(); // FK constraint: record_players.UserId references users
        var command = new AddPlayerToRecordCommand(recordId, callerUserId, playerUserId, "TestPlayer");
        await SendInScopeAsync(command);

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var player = await db.RecordPlayers
            .Where(p => p.PlayRecordId == recordId)
            .FirstAsync(TestCancellationToken);

        return player.Id;
    }

    #endregion
}
