using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.AgentMemory;

/// <summary>
/// #4960 on a real PostgreSQL: the contract of the structured dispute v2 (#4936) drops <c>rule_disputes</c>, and
/// only expands <c>game_memories.last_processed_event_id</c>. That column left the aggregate and the entity but stays
/// in the schema, because the release before #4960 still maps it and would fail every read of a game memory without
/// it. Its DROP COLUMN is #4973; until then the model reads and writes the table with the column still there.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4960")]
public sealed class RetiredDisputeSchemaIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public RetiredDisputeSchemaIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"agentmem_dispute_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await using var db = _fixture.CreateDbContext(_connectionString);
        await db.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Migrations_DropRuleDisputes()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        var tables = await db.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'rule_disputes'
                """)
            .ToListAsync(Ct);

        tables.Should().BeEmpty();
    }

    [Fact]
    public async Task Migrations_KeepTheGameMemoryColumn_UntilItsOwnContract()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        var columns = await db.Database
            .SqlQuery<string>($"""
                SELECT data_type || ':' || is_nullable AS "Value" FROM information_schema.columns
                WHERE table_name = 'game_memories' AND column_name = 'last_processed_event_id'
                """)
            .ToListAsync(Ct);

        columns.Should().Equal(["uuid:YES"], "the release before #4960 still selects it; it goes in #4973");
    }

    [Fact]
    public async Task GameMemory_RoundTripsThroughPostgres_OnARowTheEarlierReleaseWrote()
    {
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("Si pesca una carta in più al primo turno", HouseRuleSource.UserAdded);

        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            await new GameMemoryRepository(db, new DomainEventCollector()).AddAsync(memory, Ct);
            await db.SaveChangesAsync(Ct);

            // A row as the dispute-override handler left it, before #4936 retired it.
            var processed = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync(
                $"UPDATE game_memories SET last_processed_event_id = {processed} WHERE id = {memory.Id}", Ct);
        }

        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            var repository = new GameMemoryRepository(db, new DomainEventCollector());
            var read = await repository.GetByGameAndOwnerAsync(gameId, ownerId, Ct);
            read!.AddNote("Nota dopo il ritiro", addedByUserId: ownerId);
            await repository.UpdateAsync(read, Ct);
            await db.SaveChangesAsync(Ct);
        }

        await using var check = _fixture.CreateDbContext(_connectionString);
        var stored = await new GameMemoryRepository(check, new DomainEventCollector()).GetByIdAsync(memory.Id, Ct);
        stored!.HouseRules.Should().ContainSingle().Which.Description.Should().Be("Si pesca una carta in più al primo turno");
        stored.Notes.Should().ContainSingle().Which.Content.Should().Be("Nota dopo il ritiro");
    }
}
