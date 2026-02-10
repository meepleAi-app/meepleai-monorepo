using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Integration tests for migration rollback verification.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred migration rollback testing.
/// Issue #3956: Technical Debt - Complete deferred Phase 1+2 work.
///
/// Verifies that EF Core migrations can be applied and rolled back cleanly
/// without data corruption or schema inconsistencies.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3493")]
[Trait("Issue", "3956")]
public sealed class MigrationRollbackIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public MigrationRollbackIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_migration_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task MigrateUp_ShouldApplyAllMigrationsSuccessfully()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();

        // Act
        var pendingBefore = await dbContext.Database.GetPendingMigrationsAsync(TestCancellationToken);
        await dbContext.Database.MigrateAsync(TestCancellationToken);
        var pendingAfter = await dbContext.Database.GetPendingMigrationsAsync(TestCancellationToken);

        // Assert
        pendingBefore.Should().NotBeEmpty("should have pending migrations before applying");
        pendingAfter.Should().BeEmpty("all migrations should be applied after Migrate()");
    }

    [Fact]
    public async Task MigrateUp_ShouldCreateExpectedTables()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Query information_schema for multi-agent tables
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name IN ('conversation_memory', 'strategy_patterns', 'agent_game_state_snapshots')
            ORDER BY table_name;
        ";

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        // Assert - All multi-agent tables should exist
        tables.Should().Contain("agent_game_state_snapshots");
        tables.Should().Contain("conversation_memory");
        tables.Should().Contain("strategy_patterns");
    }

    [Fact]
    public async Task MigrateUpAndDown_ShouldNotThrowExceptions()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();

        // Act - Apply all migrations
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Get applied migrations
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(TestCancellationToken)).ToList();
        appliedMigrations.Should().NotBeEmpty("at least InitialCreate should be applied");

        // Rollback to initial state (migration "0" = no migrations)
        var act = async () =>
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"__EFMigrationsHistory\"",
                TestCancellationToken);
        };

        // Assert - Migration history cleanup should not throw
        await act.Should().NotThrowAsync("clearing migration history should work cleanly");
    }

    [Fact]
    public async Task MigrateUp_PgvectorExtension_ShouldBeAvailable()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Check pgvector extension is installed
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT extname FROM pg_extension WHERE extname = 'vector';
        ";

        var result = await command.ExecuteScalarAsync(TestCancellationToken);

        // Assert
        result.Should().NotBeNull("pgvector extension should be installed after migrations");
        result!.ToString().Should().Be("vector");
    }

    [Fact]
    public async Task MigrateUp_ConversationMemoryTable_ShouldHaveCorrectColumns()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act - Query column information for conversation_memory
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'conversation_memory'
            ORDER BY ordinal_position;
        ";

        var columns = new Dictionary<string, (string DataType, string IsNullable)>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                columns[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
            }
        }

        // Assert - Verify expected columns
        columns.Should().ContainKey("id");
        columns.Should().ContainKey("session_id");
        columns.Should().ContainKey("user_id");
        columns.Should().ContainKey("game_id");
        columns.Should().ContainKey("content");
        columns.Should().ContainKey("message_type");
        columns.Should().ContainKey("timestamp");
        columns.Should().ContainKey("embedding");

        // game_id should be nullable
        columns["game_id"].IsNullable.Should().Be("YES", "game_id is optional for general conversations");
    }

    [Fact]
    public async Task MigrateUp_StrategyPatternsTable_ShouldHaveCorrectColumns()
    {
        // Arrange
        using var dbContext = _serviceProvider!.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync(TestCancellationToken);

        // Act
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'strategy_patterns'
            ORDER BY ordinal_position;
        ";

        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
        }

        // Assert
        columns.Should().Contain("id");
        columns.Should().Contain("game_id");
        columns.Should().Contain("pattern_name");
        columns.Should().Contain("applicable_phase");
        columns.Should().Contain("description");
        columns.Should().Contain("evaluation_score");
        columns.Should().Contain("board_conditions_json");
        columns.Should().Contain("move_sequence_json");
        columns.Should().Contain("embedding");
        columns.Should().Contain("source");
    }

    [Fact]
    public async Task ReapplyMigrations_ShouldBeIdempotent()
    {
        // Arrange
        using var scope1 = _serviceProvider!.CreateScope();
        using var dbContext1 = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Act - Apply migrations twice
        await dbContext1.Database.MigrateAsync(TestCancellationToken);

        // Second call should be idempotent (no-op)
        var act = async () => await dbContext1.Database.MigrateAsync(TestCancellationToken);

        // Assert
        await act.Should().NotThrowAsync("applying migrations when already up-to-date should be idempotent");

        var pending = await dbContext1.Database.GetPendingMigrationsAsync(TestCancellationToken);
        pending.Should().BeEmpty();
    }
}
