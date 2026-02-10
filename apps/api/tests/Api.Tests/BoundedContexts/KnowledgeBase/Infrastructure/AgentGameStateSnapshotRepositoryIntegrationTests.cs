using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Integration tests for AgentGameStateSnapshotRepository with real PostgreSQL + pgvector.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred integration tests.
/// Issue #3985: Integration Tests for Context Engineering Repositories.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3493")]
[Trait("Issue", "3985")]
public sealed class AgentGameStateSnapshotRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAgentGameStateSnapshotRepository? _repository;
    private IServiceProvider? _serviceProvider;
    private Guid _gameId;
    private Guid _agentSessionId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AgentGameStateSnapshotRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_snapshot_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new AgentGameStateSnapshotRepository(_dbContext, eventCollector);

        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedTestDataAsync()
    {
        // Create minimal test data
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _gameId = game.Id;
        _dbContext!.Games.Add(game);

        // Create fake agent session ID (not actually creating AgentSession to simplify)
        _agentSessionId = Guid.NewGuid();

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidSnapshot_PersistsToDatabase()
    {
        // Arrange
        var boardState = new { turn = 1, players = new[] { "Alice", "Bob" } };
        var snapshot = new AgentGameStateSnapshot(
            id: Guid.NewGuid(),
            gameId: _gameId,
            agentSessionId: _agentSessionId,
            boardStateJson: System.Text.Json.JsonSerializer.Serialize(boardState),
            turnNumber: 1);

        // Act
        await _repository!.AddAsync(snapshot, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var entity = await _dbContext.AgentGameStateSnapshots.FirstOrDefaultAsync(
            s => s.Id == snapshot.Id,
            TestCancellationToken);

        entity.Should().NotBeNull();
        entity!.GameId.Should().Be(_gameId);
        entity.TurnNumber.Should().Be(1);
        entity.BoardStateJson.Should().Contain("Alice");
    }

    [Fact]
    public async Task AddAsync_WithJSONBoardState_QueryableViaJsonOperations()
    {
        // Arrange
        var boardState = """{"turn": 5, "activePlayer": "Alice"}""";
        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, boardState, 5);

        await _repository!.AddAsync(snapshot, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Query using JSONB operations
        var result = await _dbContext.AgentGameStateSnapshots
            .FirstOrDefaultAsync(
                s => EF.Functions.JsonContains(s.BoardStateJson, """{"activePlayer":"Alice"}"""),
                TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(snapshot.Id);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetByGameIdAsync_ReturnsSnapshotsInTemporalOrder()
    {
        // Arrange
        var snapshot1 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 1}""", 1);
        var snapshot2 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 2}""", 2);
        var snapshot3 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 3}""", 3);

        await _repository!.AddAsync(snapshot1, TestCancellationToken);
        await _repository.AddAsync(snapshot2, TestCancellationToken);
        await _repository.AddAsync(snapshot3, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByGameIdAsync(_gameId, limit: 10, TestCancellationToken);

        // Assert
        results.Should().HaveCount(3);
        // Ordered by turn number descending (latest first)
        results[0].TurnNumber.Should().Be(3);
        results[1].TurnNumber.Should().Be(2);
        results[2].TurnNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetLatestByGameIdAsync_ReturnsLatestSnapshot()
    {
        // Arrange
        var snapshot1 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 1}""", 1);
        var snapshot2 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 5}""", 5);

        await _repository!.AddAsync(snapshot1, TestCancellationToken);
        await _repository.AddAsync(snapshot2, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetLatestByGameIdAsync(_gameId, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TurnNumber.Should().Be(5);
        result.BoardStateJson.Should().Contain("\"turn\": 5");
    }

    #endregion

    #region Index Verification Tests

    [Fact]
    public async Task Indexes_Exist_OnAllRequiredColumns()
    {
        // Arrange & Act
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'agent_game_state_snapshots'
            ORDER BY indexname;
        ";

        var indexes = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                indexes.Add(reader.GetString(0));
            }
        }

        // Assert
        indexes.Should().Contain("ix_agent_game_state_snapshots_game_id");
        indexes.Should().Contain("ix_agent_game_state_snapshots_agent_session_id");
        indexes.Should().Contain("ix_agent_game_state_snapshots_game_id_turn_number");
        indexes.Should().Contain("ix_agent_game_state_snapshots_created_at");
        indexes.Should().Contain("pk_agent_game_state_snapshots");
    }

    #endregion
}
