using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Npgsql-backed counterpart to <see cref="MeepleAiDbContextModelCanaryTests"/>. Catches
/// provider-specific failures that the InMemory canary cannot — e.g. Postgres-only type
/// converters, pgvector mappings, JSON column mappings, schema/migration mismatches
/// (Issue #889).
///
/// <para>
/// Runs once per CI shard: spins up a Postgres+pgvector container, applies all
/// migrations, and asserts the model is consistent end-to-end. Cost: ~10–20s startup;
/// pays for itself the first time a Postgres-only converter regresses.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "889")]
public sealed class MeepleAiDbContextNpgsqlCanaryTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private string _connectionString = null!;

    public async ValueTask InitializeAsync()
    {
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = "model_canary_db",
                SslMode = SslMode.Disable,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 2,
                Timeout = 30,
                CommandTimeout = 60,
            };
            _connectionString = builder.ConnectionString;
        }
        else
        {
            _postgresContainer = new ContainerBuilder()
                .WithImage("pgvector/pgvector:pg16")
                .WithEnvironment("POSTGRES_USER", "testuser")
                .WithEnvironment("POSTGRES_PASSWORD", "testpass")
                .WithEnvironment("POSTGRES_DB", "model_canary_db")
                .WithPortBinding(5432, assignRandomHostPort: true)
                .WithCleanUp(true)
                .Build();

            await _postgresContainer.StartAsync();
            var port = _postgresContainer.GetMappedPublicPort(5432);
            _connectionString =
                $"Host=localhost;Port={port};Database=model_canary_db;Username=testuser;Password=testpass;"
              + "Ssl Mode=Disable;Pooling=true;MinPoolSize=1;MaxPoolSize=5;Timeout=30;CommandTimeout=60";

            await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        }
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private MeepleAiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString, o => o.UseVector())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new MeepleAiDbContext(
            options,
            TestDbContextFactory.CreateMockMediator().Object,
            TestDbContextFactory.CreateMockEventCollector().Object);
    }

    [Fact]
    public async Task Migrations_Apply_Cleanly_Against_Postgres()
    {
        await using var context = CreateContext();

        // EnsureDeletedAsync + MigrateAsync surfaces:
        //  - Provider-specific type-mapping errors (text[] ↔ string[] mismatches,
        //    pgvector dimension errors, JSON converter conflicts on jsonb columns)
        //  - Schema-migration drift the InMemory canary can't detect
        //  - Composite-key/FK constraint violations during table creation
        Func<Task> migrate = async () =>
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        };

        await migrate.Should().NotThrowAsync(
            because: "Postgres migrations must apply against pgvector:pg16 without "
                   + "type-mapping or DDL errors; this catches the class of bug that the "
                   + "InMemory canary cannot (Issue #889)");
    }
}
