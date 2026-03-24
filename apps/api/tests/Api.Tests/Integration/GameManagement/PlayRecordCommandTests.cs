using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddScoped<IGameRepository, GameRepository>();
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
        var game = await CreateTestGameAsync();
        var userId = await SeedTestUserAsync();
        var command = new CreatePlayRecordCommand(
            userId,
            game.Id,
            game.Title.Value,
            _timeProvider.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private);

        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var recordId = await mediator.Send(command, TestCancellationToken);

        // Assert
        recordId.Should().NotBeEmpty();
        var record = await DbContext.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.GameId.Should().Be(game.Id);
        record.GameName.Should().Be(game.Title.Value);
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

        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var recordId = await mediator.Send(command, TestCancellationToken);

        // Assert
        recordId.Should().NotBeEmpty();
        var record = await DbContext.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
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

        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act & Assert
        var act = async () => () =>
            mediator.Send(command, TestCancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region AddPlayerToRecordCommand Tests

    [Fact]
    public async Task AddPlayerToRecordCommand_RegisteredUser_AddsSuccessfully()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var userId = Guid.NewGuid();
        var command = new AddPlayerToRecordCommand(recordId, userId, "Alice");
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var players = await DbContext.RecordPlayers
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
        var recordId = await CreateTestRecordAsync();
        var command = new AddPlayerToRecordCommand(recordId, null, "Bob");
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var players = await DbContext.RecordPlayers
            .Where(p => p.PlayRecordId == recordId)
            .ToListAsync(TestCancellationToken);

        players.Should().HaveCount(1);
        players[0].UserId.Should().BeNull();
        players[0].DisplayName.Should().Be("Bob");
    }

    #endregion

    #region RecordScoreCommand Tests

    [Fact]
    public async Task RecordScoreCommand_ValidScore_RecordsSuccessfully()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var playerId = await AddTestPlayerAsync(recordId);

        var command = new RecordScoreCommand(recordId, playerId, "points", 42, "pts");
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var scores = await DbContext.RecordScores
            .Where(s => s.RecordPlayerId == playerId)
            .ToListAsync(TestCancellationToken);

        scores.Should().HaveCount(1);
        scores[0].Dimension.Should().Be("points");
        scores[0].Value.Should().Be(42);
    }

    #endregion

    #region StartPlayRecordCommand Tests

    [Fact]
    public async Task StartPlayRecordCommand_PlannedRecord_StartsSuccessfully()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var command = new StartPlayRecordCommand(recordId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var record = await DbContext.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.Status.Should().Be((int)PlayRecordStatus.InProgress);
        record.StartTime.Should().NotBeNull();
    }

    [Fact]
    public async Task StartPlayRecordCommand_NonPlannedRecord_ThrowsConflictException()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var startCommand = new StartPlayRecordCommand(recordId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(startCommand, TestCancellationToken);

        // Act & Assert
        var act = async () => () =>
            mediator.Send(startCommand, TestCancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }

    #endregion

    #region CompletePlayRecordCommand Tests

    [Fact]
    public async Task CompletePlayRecordCommand_WithManualDuration_CompletesSuccessfully()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var duration = TimeSpan.FromHours(2);
        var command = new CompletePlayRecordCommand(recordId, duration);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var record = await DbContext.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.Status.Should().Be((int)PlayRecordStatus.Completed);
        record.Duration.Should().Be(duration);
    }

    #endregion

    #region UpdatePlayRecordCommand Tests

    [Fact]
    public async Task UpdatePlayRecordCommand_ValidData_UpdatesSuccessfully()
    {
        // Arrange
        var recordId = await CreateTestRecordAsync();
        var newDate = _timeProvider.UtcNow.AddDays(-2);
        var command = new UpdatePlayRecordCommand(
            recordId,
            SessionDate: newDate,
            Notes: "Great game!",
            Location: "Home");

        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(command, TestCancellationToken);

        // Assert
        var record = await DbContext.PlayRecords.FirstOrDefaultAsync(r => r.Id == recordId, TestCancellationToken);
        record.Should().NotBeNull();
        record!.SessionDate.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
        record.Notes.Should().Be("Great game!");
        record.Location.Should().Be("Home");
    }

    #endregion

    #region Helper Methods

    private async Task<Game> CreateTestGameAsync()
    {
        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Test Game"),
            new Publisher("Test Publisher"));

        var repository = ServiceProvider.GetRequiredService<IGameRepository>();
        await repository.AddAsync(game, TestCancellationToken);

        var unitOfWork = ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        return game;
    }

    /// <summary>
    /// Seeds a UserEntity in the database to satisfy FK constraints on play_records.CreatedByUserId.
    /// </summary>
    private async Task<Guid> SeedTestUserAsync(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        DbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"test-{id:N}@meepleai.dev",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> CreateTestRecordAsync()
    {
        var game = await CreateTestGameAsync();
        var userId = await SeedTestUserAsync();

        var command = new CreatePlayRecordCommand(
            userId,
            game.Id,
            game.Title.Value,
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        var mediator = ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command, TestCancellationToken);
    }

    private async Task<Guid> AddTestPlayerAsync(Guid recordId)
    {
        var command = new AddPlayerToRecordCommand(recordId, Guid.NewGuid(), "TestPlayer");
        var mediator = ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, TestCancellationToken);

        var player = await DbContext.RecordPlayers
            .Where(p => p.PlayRecordId == recordId)
            .FirstAsync(TestCancellationToken);

        return player.Id;
    }

    #endregion
}
