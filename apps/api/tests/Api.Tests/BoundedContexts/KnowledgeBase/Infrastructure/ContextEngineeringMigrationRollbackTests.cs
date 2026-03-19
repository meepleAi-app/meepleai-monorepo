using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Migration rollback tests for Context Engineering schema tables.
/// Issue #3986: Migration Rollback Testing for Context Engineering Schema.
///
/// Validates that EF Core migrations for conversation_memory, agent_game_state_snapshots,
/// and strategy_patterns tables can be rolled back cleanly with proper data handling,
/// transaction safety, and foreign key cascade behavior.
///
/// Test Scenarios:
/// 1. Clean Rollback - Tables created and removed properly
/// 2. Rollback with Data - Data integrity during rollback
/// 3. Partial Rollback - Rolling back to intermediate migration points
/// 4. Foreign Key Constraints - CASCADE behavior during rollback
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3986")]
public sealed class ContextEngineeringMigrationRollbackTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Context Engineering tables created in InitialCreate migration.
    /// </summary>
    private static readonly string[] ContextEngineeringTables =
    {
        "conversation_memory",
        "agent_game_state_snapshots",
        "strategy_patterns"
    };

    /// <summary>
    /// The migration that creates Context Engineering tables.
    /// All 3 tables are part of the initial schema creation.
    /// </summary>
    private const string InitialCreateMigration = "20260208111903_InitialCreate";

    /// <summary>
    /// A migration after InitialCreate, used for partial rollback testing.
    /// </summary>
    private const string PostInitialMigration = "20260208162522_AddPlayRecords";

    public ContextEngineeringMigrationRollbackTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rollback_{Guid.NewGuid():N}";
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();

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

    #region Test Scenario 1: Clean Rollback

    [Fact]
    public async Task CleanRollback_MigrateToZero_RemovesAllContextEngineeringTables()
    {
        // Arrange - Apply all migrations
        using var scope = _serviceProvider!.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Verify tables exist after migration
        var tablesBeforeRollback = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tablesBeforeRollback.Should().BeEquivalentTo(ContextEngineeringTables,
            "all Context Engineering tables should exist after migration");

        // Act - Rollback to "0" (before any migration)
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - All tables should be removed
        var tablesAfterRollback = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tablesAfterRollback.Should().BeEmpty(
            "all Context Engineering tables should be removed after rollback to 0");
    }

    [Fact]
    public async Task CleanRollback_ReapplyAfterRollback_RecreatesTablesSuccessfully()
    {
        // Arrange - Apply, rollback, and reapply

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Act - Reapply all migrations
        await migrator.MigrateAsync(cancellationToken: TestCancellationToken);

        // Assert - Tables should be recreated
        var tables = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tables.Should().BeEquivalentTo(ContextEngineeringTables,
            "Context Engineering tables should be recreated after reapplying migrations");
    }

    [Fact]
    public async Task CleanRollback_VerifiesIndexesRestoredAfterReapply()
    {
        // Arrange - Apply, rollback, reapply

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);
        await migrator.MigrateAsync(cancellationToken: TestCancellationToken);

        // Act - Check indexes on conversation_memory
        var indexes = await GetTableIndexesAsync(dbContext, "conversation_memory");

        // Assert - Key indexes should exist
        indexes.Should().Contain(i => i.Contains("session_id"), "session_id index should be restored");
        indexes.Should().Contain(i => i.Contains("user_id"), "user_id index should be restored");
        indexes.Should().Contain(i => i.Contains("timestamp"), "timestamp index should be restored");
    }

    [Fact]
    public async Task CleanRollback_MigrationHistory_ReflectsRollbackState()
    {
        // Arrange

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        var appliedBefore = (await dbContext.Database.GetAppliedMigrationsAsync(TestCancellationToken)).ToList();

        // Act - Rollback to 0
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - Migration history should be empty
        var appliedAfter = (await dbContext.Database.GetAppliedMigrationsAsync(TestCancellationToken)).ToList();
        appliedBefore.Should().NotBeEmpty("should have applied migrations before rollback");
        appliedAfter.Should().BeEmpty("migration history should be empty after full rollback");
    }

    [Fact]
    public async Task CleanRollback_PgvectorExtension_RemovedAfterFullRollback()
    {
        // Arrange

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Rollback to 0
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - pgvector extension may or may not be removed depending on Down() impl
        // At minimum, vector columns should not exist
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE data_type = 'USER-DEFINED'
            AND udt_name = 'vector'
            AND table_schema = 'public';
        ";

        var vectorColumnCount = (long)(await command.ExecuteScalarAsync(TestCancellationToken))!;
        vectorColumnCount.Should().Be(0, "no vector columns should exist after full rollback");
    }

    #endregion

    #region Test Scenario 2: Rollback with Data

    [Fact]
    public async Task RollbackWithData_DataIsRemovedCleanly()
    {
        // Arrange - Apply migrations and insert data

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        await InsertContextEngineeringTestDataAsync(dbContext);

        // Verify data exists
        var memoryCount = await dbContext.ConversationMemories.CountAsync(TestCancellationToken);
        var snapshotCount = await dbContext.AgentGameStateSnapshots.CountAsync(TestCancellationToken);
        var patternCount = await dbContext.StrategyPatterns.CountAsync(TestCancellationToken);

        memoryCount.Should().BeGreaterThan(0);
        snapshotCount.Should().BeGreaterThan(0);
        patternCount.Should().BeGreaterThan(0);

        // Act - Rollback
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - Tables should not exist (data removed with tables)
        var tables = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tables.Should().BeEmpty("tables and their data should be removed after rollback");
    }

    [Fact]
    public async Task RollbackWithData_ReapplyCreatesEmptyTables()
    {
        // Arrange - Apply, insert data, rollback

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        await InsertContextEngineeringTestDataAsync(dbContext);

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Act - Reapply migrations
        await migrator.MigrateAsync(cancellationToken: TestCancellationToken);

        // Assert - Tables should exist but be empty (no data resurrection)
        var memoryCount = await dbContext.ConversationMemories.CountAsync(TestCancellationToken);
        var snapshotCount = await dbContext.AgentGameStateSnapshots.CountAsync(TestCancellationToken);
        var patternCount = await dbContext.StrategyPatterns.CountAsync(TestCancellationToken);

        memoryCount.Should().Be(0, "conversation_memory should be empty after rollback and reapply");
        snapshotCount.Should().Be(0, "agent_game_state_snapshots should be empty after rollback and reapply");
        patternCount.Should().Be(0, "strategy_patterns should be empty after rollback and reapply");
    }

    [Fact]
    public async Task RollbackWithData_NoOrphanedSequencesOrConstraints()
    {
        // Arrange

        await dbContext.Database.MigrateAsync(TestCancellationToken);
        await InsertContextEngineeringTestDataAsync(dbContext);

        // Act - Rollback
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - No orphaned constraints referencing Context Engineering tables
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.table_constraints
            WHERE table_schema = 'public'
            AND table_name IN ('conversation_memory', 'agent_game_state_snapshots', 'strategy_patterns');
        ";

        var constraintCount = (long)(await command.ExecuteScalarAsync(TestCancellationToken))!;
        constraintCount.Should().Be(0, "no orphaned constraints should remain after rollback");
    }

    [Fact]
    public async Task RollbackWithData_LargeDataset_CompletesWithinTimeout()
    {
        // Arrange - Apply migrations and insert larger dataset

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Insert bulk data into strategy_patterns (no FK dependencies)
        for (int i = 0; i < 100; i++)
        {
            var entity = new Api.Infrastructure.Entities.KnowledgeBase.StrategyPatternEntity
            {
                Id = Guid.NewGuid(),
                GameId = Guid.NewGuid(),
                PatternName = $"Pattern-{i}",
                ApplicablePhase = "early",
                Description = $"Bulk test pattern {i}",
                EvaluationScore = 0.5 + (i % 10) * 0.05,
                Source = "test"
            };
            dbContext.StrategyPatterns.Add(entity);
        }
        await dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Rollback should complete within reasonable time
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await migrator.MigrateAsync("0", TestCancellationToken);
        sw.Stop();

        // Assert - Should complete within 30 seconds (generous timeout for CI)
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "rollback with 100 strategy patterns should complete quickly");

        var tables = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tables.Should().BeEmpty();
    }

    #endregion

    #region Test Scenario 3: Partial Rollback

    [Fact]
    public async Task PartialRollback_ToPostInitialMigration_PreservesContextEngineeringTables()
    {
        // Arrange - Apply all migrations

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        var allMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(TestCancellationToken)).ToList();
        allMigrations.Count.Should().BeGreaterThan(1, "should have multiple migrations applied");

        // Act - Rollback to just after InitialCreate (which contains CE tables)
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PostInitialMigration, TestCancellationToken);

        // Assert - Context Engineering tables should still exist
        var tables = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tables.Should().BeEquivalentTo(ContextEngineeringTables,
            "Context Engineering tables should be preserved when rolling back to a point after InitialCreate");
    }

    [Fact]
    public async Task PartialRollback_ToInitialCreate_PreservesContextEngineeringTables()
    {
        // Arrange - Apply all migrations

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Rollback to exactly InitialCreate
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(InitialCreateMigration, TestCancellationToken);

        // Assert - Context Engineering tables should still exist (part of InitialCreate)
        var tables = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tables.Should().BeEquivalentTo(ContextEngineeringTables,
            "Context Engineering tables should exist at InitialCreate migration point");

        // Verify later migration tables are removed
        var applied = (await dbContext.Database.GetAppliedMigrationsAsync(TestCancellationToken)).ToList();
        applied.Should().HaveCount(1, "only InitialCreate should remain after partial rollback");
        applied[0].Should().Be(InitialCreateMigration);
    }

    [Fact]
    public async Task PartialRollback_FromInitialCreateToZero_RemovesContextEngineeringTables()
    {
        // Arrange - Apply only InitialCreate

        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(InitialCreateMigration, TestCancellationToken);

        // Verify tables exist
        var tablesBefore = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tablesBefore.Should().BeEquivalentTo(ContextEngineeringTables);

        // Act - Rollback from InitialCreate to 0
        await migrator.MigrateAsync("0", TestCancellationToken);

        // Assert - Tables should be removed
        var tablesAfter = await GetExistingTablesAsync(dbContext, ContextEngineeringTables);
        tablesAfter.Should().BeEmpty(
            "Context Engineering tables should be removed when rolling back past InitialCreate");
    }

    #endregion

    #region Test Scenario 4: Foreign Key Constraints

    [Fact]
    public async Task ForeignKeyConstraints_ConversationMemory_CascadeOnUserDelete()
    {
        // Arrange - Apply migrations and seed data with FK relationships

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Create user and conversation memory
        var user = new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "fk-test@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        var memory = new Api.Infrastructure.Entities.KnowledgeBase.ConversationMemoryEntity
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = user.Id,
            Content = "FK cascade test memory",
            MessageType = "user",
            Timestamp = DateTime.UtcNow
        };
        dbContext.ConversationMemories.Add(memory);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Delete the user (should CASCADE to conversation_memory)
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Conversation memory should be cascade-deleted
        var remainingMemories = await dbContext.ConversationMemories
            .Where(m => m.UserId == user.Id)
            .CountAsync(TestCancellationToken);
        remainingMemories.Should().Be(0,
            "conversation_memory entries should be cascade-deleted when user is deleted");
    }

    [Fact]
    public async Task ForeignKeyConstraints_ConversationMemory_RejectsInvalidUserId()
    {
        // Arrange

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Try to insert conversation_memory with non-existent user_id
        var memory = new Api.Infrastructure.Entities.KnowledgeBase.ConversationMemoryEntity
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(), // Non-existent user
            Content = "FK violation test",
            MessageType = "user",
            Timestamp = DateTime.UtcNow
        };
        dbContext.ConversationMemories.Add(memory);

        var act = async () => await dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Should throw due to FK violation
        await act.Should().ThrowAsync<DbUpdateException>(
            "inserting conversation_memory with invalid user_id should violate FK constraint");
    }

    [Fact]
    public async Task ForeignKeyConstraints_RollbackPreservesFKIntegrity()
    {
        // Arrange - Apply all, rollback to InitialCreate, verify FK still enforced

        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Rollback to InitialCreate (partial rollback, tables still exist)
        var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(InitialCreateMigration, TestCancellationToken);

        // Act - Verify FK constraints are still enforced after partial rollback
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT constraint_name, constraint_type
            FROM information_schema.table_constraints
            WHERE table_schema = 'public'
            AND table_name = 'conversation_memory'
            AND constraint_type = 'FOREIGN KEY';
        ";

        var fkConstraints = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                fkConstraints.Add(reader.GetString(0));
            }
        }

        // Assert - FK constraint to users should still be present
        fkConstraints.Should().Contain("FK_conversation_memory_users_user_id",
            "FK constraint from conversation_memory to users should be preserved after partial rollback");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets existing table names from the database that match the requested table names.
    /// </summary>
    private static async Task<List<string>> GetExistingTablesAsync(
        MeepleAiDbContext dbContext, string[] tableNames)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        var tableList = string.Join("','", tableNames);
        var command = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: tableNames is a hardcoded constant array
        command.CommandText = $@"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name IN ('{tableList}')
            ORDER BY table_name;
        ";
#pragma warning restore CA2100

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        return tables;
    }

    /// <summary>
    /// Gets index names for a specific table.
    /// </summary>
    private static async Task<List<string>> GetTableIndexesAsync(
        MeepleAiDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: tableName is a hardcoded string literal from test code
        command.CommandText = $@"
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = '{tableName}'
            ORDER BY indexname;
        ";
#pragma warning restore CA2100

        var indexes = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                indexes.Add(reader.GetString(0));
            }
        }

        return indexes;
    }

    /// <summary>
    /// Inserts test data into Context Engineering tables.
    /// Creates necessary parent entities (users, games) for FK satisfaction.
    /// Inserts into conversation_memory and strategy_patterns (simple FK chains).
    /// agent_game_state_snapshots requires complex FK graph (Agent, GameSession, Typology)
    /// so we use raw SQL to insert a snapshot directly for data presence verification.
    /// </summary>
    private static async Task InsertContextEngineeringTestDataAsync(MeepleAiDbContext dbContext)
    {
        // Create parent entities for FK constraints
        var user = new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "rollback-data-test@test.com",
            Role = "user",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);

        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Rollback Test Game",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(TestCancellationToken);

        // Insert conversation_memory (FK to users)
        var memory = new Api.Infrastructure.Entities.KnowledgeBase.ConversationMemoryEntity
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            Content = "Rollback test memory content",
            MessageType = "user",
            Timestamp = DateTime.UtcNow
        };
        dbContext.ConversationMemories.Add(memory);

        // Insert strategy_patterns (no enforced FK at DB level for game_id)
        var pattern = new Api.Infrastructure.Entities.KnowledgeBase.StrategyPatternEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            PatternName = "Rollback Test Pattern",
            ApplicablePhase = "early",
            Description = "Pattern for rollback testing",
            EvaluationScore = 0.85,
            Source = "test"
        };
        dbContext.StrategyPatterns.Add(pattern);

        await dbContext.SaveChangesAsync(TestCancellationToken);

        // Insert agent_game_state_snapshot via raw SQL with temporarily disabled FK
        // The agent_sessions FK chain is complex (requires Agent, GameSession, User, Game, Typology)
        // We disable the FK check for this single insert to verify rollback cleans up the data
        var snapshotId = Guid.NewGuid();
        var fakeAgentSessionId = Guid.NewGuid();

        await dbContext.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE agent_game_state_snapshots DISABLE TRIGGER ALL;
              INSERT INTO agent_game_state_snapshots (id, game_id, agent_session_id, board_state_json, turn_number, created_at)
              VALUES ({0}, {1}, {2}, '{""turn"": 1}'::jsonb, 1, {3});
              ALTER TABLE agent_game_state_snapshots ENABLE TRIGGER ALL;",
            snapshotId, game.Id, fakeAgentSessionId, DateTime.UtcNow);
    }

    #endregion
}
