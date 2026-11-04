using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.Integration.AgentMemory;

/// <summary>
/// #4839, option C, on a real PostgreSQL: the migration adds the two <c>processed_session_ids</c> jsonb columns, the
/// repositories write and read them, and the statistics handler — real repositories, real unit of work, a fresh
/// context for each delivery as each request has its own — counts a session once even when the redelivery comes after
/// another session of the same player and group.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4839")]
public sealed class ProcessedSessionsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public ProcessedSessionsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"agentmem_sessions_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await using var db = _fixture.CreateDbContext(_connectionString);
        await db.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static readonly Mock<IFeatureFlagService> Flags = CreateFlags();

    private static Mock<IFeatureFlagService> CreateFlags()
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null)).ReturnsAsync(true);
        return flags;
    }

    /// <summary>One delivery on its own context, as a request or an outbox batch would have.</summary>
    private async Task DeliverAsync(LiveSessionCompletedEvent @event)
    {
        await using var db = _fixture.CreateDbContext(_connectionString);
        var collector = new DomainEventCollector();
        var handler = new OnSessionCompletedUpdateStatsHandler(
            new PlayerMemoryRepository(db, collector),
            new GroupMemoryRepository(db, collector),
            new UnitOfWork(db),
            Flags.Object,
            NullLogger<OnSessionCompletedUpdateStatsHandler>.Instance);
        await handler.Handle(@event, Ct);
    }

    private static LiveSessionCompletedEvent Completed(Guid gameId, Guid groupId, Guid userId, int rank)
    {
        var now = DateTime.UtcNow;
        return new LiveSessionCompletedEvent(
            sessionId: Guid.NewGuid(),
            completedAt: now,
            totalTurns: 5,
            gameId: gameId,
            gameName: "Catan",
            createdByUserId: userId,
            visibility: PlayRecordVisibility.Private,
            groupId: groupId,
            sessionDate: now.AddHours(-1),
            startedAt: now.AddHours(-1),
            notes: null,
            players: [new CompletedPlayerSnapshot(Guid.NewGuid(), userId, "Alice", 10, rank)],
            scores: []);
    }

    private static LiveSessionCompletedEvent Rebuilt(LiveSessionCompletedEvent original) =>
        JsonSerializer.Deserialize<LiveSessionCompletedEvent>(
            JsonSerializer.Serialize(original, DomainEventJsonOptions.Default), DomainEventJsonOptions.Default)!;

    [Fact]
    public async Task Migration_AddsTheProcessedSessionColumns_AsNullableJsonb()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        var columns = await db.Database
            .SqlQuery<string>($"""
                SELECT table_name || ':' || data_type || ':' || is_nullable AS "Value"
                FROM information_schema.columns
                WHERE column_name = 'processed_session_ids' AND table_name IN ('player_memories', 'group_memories')
                ORDER BY table_name
                """)
            .ToListAsync(Ct);

        columns.Should().Equal("group_memories:jsonb:YES", "player_memories:jsonb:YES");
    }

    [Fact]
    public async Task PlayerAndGroupMemory_ProcessedSessions_RoundTripThroughPostgres()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var player = PlayerMemory.CreateForUser(userId);
        player.MarkProcessed(Guid.NewGuid(), sessionId);
        var group = GroupMemory.Create(userId, "Giovedi");
        group.MarkProcessed(Guid.NewGuid(), sessionId);
        var untouched = PlayerMemory.CreateForGuest("Marco");

        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            var collector = new DomainEventCollector();
            await new PlayerMemoryRepository(db, collector).AddAsync(player, Ct);
            await new PlayerMemoryRepository(db, collector).AddAsync(untouched, Ct);
            await new GroupMemoryRepository(db, collector).AddAsync(group, Ct);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = _fixture.CreateDbContext(_connectionString);
        var players = new PlayerMemoryRepository(read, new DomainEventCollector());
        (await players.GetByUserIdAsync(userId, Ct))!.HasProcessedSession(sessionId).Should().BeTrue();
        (await players.GetByGuestNameAsync("Marco", Ct))!.ProcessedSessionIds.Should().BeEmpty("a row with no column value reads as none");
        (await new GroupMemoryRepository(read, new DomainEventCollector()).GetByIdAsync(group.Id, Ct))!
            .HasProcessedSession(sessionId).Should().BeTrue();
        (await read.Database.SqlQuery<string>($"""SELECT processed_session_ids::text AS "Value" FROM player_memories WHERE user_id = {userId}""")
            .SingleAsync(Ct)).Should().Be($"[\"{sessionId}\"]");
    }

    [Fact]
    public async Task Handler_ARedeliveryAfterAnotherSessionOfTheSamePlayerAndGroup_CountsTheSessionOnce()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seededGroup = GroupMemory.Create(userId, "Giovedi");
        var groupId = seededGroup.Id;
        await using (var seed = _fixture.CreateDbContext(_connectionString))
        {
            await new GroupMemoryRepository(seed, new DomainEventCollector()).AddAsync(seededGroup, Ct);
            await seed.SaveChangesAsync(Ct);
        }

        var first = Completed(gameId, groupId, userId, rank: 1);
        var second = Completed(gameId, groupId, userId, rank: 2);

        await DeliverAsync(first);
        await DeliverAsync(second);
        await DeliverAsync(Rebuilt(first));
        await DeliverAsync(Rebuilt(second));

        await using var read = _fixture.CreateDbContext(_connectionString);
        var player = await new PlayerMemoryRepository(read, new DomainEventCollector()).GetByUserIdAsync(userId, Ct);
        var stats = player!.GameStats.Should().ContainSingle().Subject;
        stats.TotalPlayed.Should().Be(2, "two different sessions, each delivered twice");
        stats.Wins.Should().Be(1);
        stats.Losses.Should().Be(1);
        var group = await new GroupMemoryRepository(read, new DomainEventCollector()).GetByIdAsync(groupId, Ct);
        group!.Stats.TotalSessions.Should().Be(2);
        group.Stats.GamePlayCounts[gameId].Should().Be(2);
        player.ProcessedSessionIds.Should().Equal(first.SessionId, second.SessionId);
    }
}
