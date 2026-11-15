using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.AgentMemory;

/// <summary>
/// The contract of the per-player stats (#4876) on a real PostgreSQL. The expand (PR #5032) retired the code that read
/// and wrote <c>player_memories</c>; <c>DropPlayerMemories</c> drops the table, with the values of
/// <c>game_stats_json</c> it held.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4876")]
public sealed class RetiredPlayerMemorySchemaIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public RetiredPlayerMemorySchemaIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"agentmem_player_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await using var db = _fixture.CreateDbContext(_connectionString);
        await db.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Migrations_DropPlayerMemories()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        var tables = await db.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'player_memories'
                """)
            .ToListAsync(Ct);

        tables.Should().BeEmpty("#4876 retired the per-player stats, and nothing reads or writes the table");
    }
}
