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
/// The contract of the structured dispute v2 (#4936) on a real PostgreSQL. #4960 drops <c>rule_disputes</c>; #4973
/// drops <c>game_memories.last_processed_event_id</c> and the shadow property that still put it in every SELECT of the
/// table. The same column on <c>player_memories</c> and <c>group_memories</c> stays:
/// <c>OnSessionCompletedUpdateStatsHandler</c> reads and writes it.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4960")]
[Trait("Issue", "4973")]
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
    public async Task Migrations_DropTheGameMemoryColumn_AndKeepItOnPlayerAndGroupMemories()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        var tables = await db.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value" FROM information_schema.columns
                WHERE table_schema = 'public' AND column_name = 'last_processed_event_id'
                  AND table_name IN ('game_memories', 'player_memories', 'group_memories')
                ORDER BY table_name
                """)
            .ToListAsync(Ct);

        tables.Should().Equal(["group_memories", "player_memories"],
            "#4973 drops the column from game_memories only; the session-stats handler still uses the other two");
    }

    [Fact]
    public async Task GameMemory_RoundTripsThroughPostgres_OnTheMigratedSchema()
    {
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("Si pesca una carta in più al primo turno", HouseRuleSource.UserAdded);

        // A model that still maps a dropped column fails here, on the INSERT and on every read, with 42703.
        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            await new GameMemoryRepository(db, new DomainEventCollector()).AddAsync(memory, Ct);
            await db.SaveChangesAsync(Ct);
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
