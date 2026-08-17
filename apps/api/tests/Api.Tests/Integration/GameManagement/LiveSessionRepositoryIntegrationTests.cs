using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// ADR-060 Phase 3 integration tests — 5 acceptance criteria for EF-backed live session persistence.
/// Issue #2097.
///
/// AC-1..AC-5 prove: create-persists, restart-safe, multi-instance, optimistic-concurrency, 100-update-restart.
///
/// Test grouping:
///   - AC-1 / AC-3 / AC-4: use SharedTestcontainersFixture (GroupC) + IntegrationWebApplicationFactory.
///   - AC-2 / AC-5: use a self-contained IContainer to support stop/start without disrupting the shared fixture
///     used by other concurrent test classes.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;

    // Per-test isolated DB names (shared fixture, isolated DB per AC)
    private string _dbAC1 = null!;
    private string _dbAC3 = null!;
    private string _dbAC4 = null!;

    // WebApplicationFactory instances backed by the shared Postgres container
    private WebApplicationFactory<Program> _factoryAC1 = null!;
    private WebApplicationFactory<Program> _factoryAC3 = null!;
    private WebApplicationFactory<Program> _factoryAC4 = null!;

    // Self-contained container + factory for restart tests (AC-2, AC-5)
    // Each test owns its own container so stop/start doesn't affect others.
    private IContainer? _containerAC2;
    private WebApplicationFactory<Program>? _factoryAC2;
    private string _connStrAC2 = null!;

    private IContainer? _containerAC5;
    private WebApplicationFactory<Program>? _factoryAC5;
    private string _connStrAC5 = null!;

    public LiveSessionRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public async ValueTask InitializeAsync()
    {
        // Provision isolated DBs on the shared container (AC-1/3/4)
        _dbAC1 = $"live_session_ac1_{Guid.NewGuid():N}";
        _dbAC3 = $"live_session_ac3_{Guid.NewGuid():N}";
        _dbAC4 = $"live_session_ac4_{Guid.NewGuid():N}";

        var connAC1 = await _fixture.CreateIsolatedDatabaseAsync(_dbAC1);
        var connAC3 = await _fixture.CreateIsolatedDatabaseAsync(_dbAC3);
        var connAC4 = await _fixture.CreateIsolatedDatabaseAsync(_dbAC4);

        _factoryAC1 = CreateMigratedFactory(connAC1);
        _factoryAC3 = CreateMigratedFactory(connAC3);
        _factoryAC4 = CreateMigratedFactory(connAC4);

        // Spin up self-contained containers for restart tests (AC-2, AC-5)
        _containerAC2 = BuildPrivateContainer();
        _containerAC5 = BuildPrivateContainer();

        await Task.WhenAll(
            _containerAC2.StartAsync(),
            _containerAC5.StartAsync());

        _connStrAC2 = BuildConnStr(_containerAC2);
        _connStrAC5 = BuildConnStr(_containerAC5);

        // Wait for Postgres inside private containers to finish initializing, then migrate.
        // maxRetries=30 at 2s = 90s max. Private containers (no tmpfs) need time for
        // Postgres to initialize its data directory on first start.
        await Task.WhenAll(
            TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connStrAC2, maxRetries: 30, initialDelayMs: 2000),
            TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connStrAC5, maxRetries: 30, initialDelayMs: 2000));

        _factoryAC2 = CreateMigratedFactory(_connStrAC2);
        _factoryAC5 = CreateMigratedFactory(_connStrAC5);
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose factories first (before dropping DBs)
        if (_factoryAC1 != null) await _factoryAC1.DisposeAsync();
        if (_factoryAC3 != null) await _factoryAC3.DisposeAsync();
        if (_factoryAC4 != null) await _factoryAC4.DisposeAsync();
        if (_factoryAC2 != null) await _factoryAC2.DisposeAsync();
        if (_factoryAC5 != null) await _factoryAC5.DisposeAsync();

        // Drop shared-fixture-backed databases
        // (SharedTestcontainersFixture.DropIsolatedDatabaseAsync already calls
        // NpgsqlConnection.ClearAllPools() internally per its own cleanup contract;
        // calling it AGAIN here would risk dropping live pools held by sibling
        // tests running in the same xunit collection.)
        await Task.WhenAll(
            _fixture.DropIsolatedDatabaseAsync(_dbAC1),
            _fixture.DropIsolatedDatabaseAsync(_dbAC3),
            _fixture.DropIsolatedDatabaseAsync(_dbAC4));

        // Dispose private containers
        if (_containerAC2 != null) await _containerAC2.DisposeAsync();
        if (_containerAC5 != null) await _containerAC5.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AC-1 — Create persists row in live_game_sessions
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AC-1: Create live session persists row in live_game_sessions")]
    public async Task AC1_Create_PersistsRow()
    {
        await using var scope = _factoryAC1.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = await SeedUserAsync(db);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Mage Knight"));

        sessionId.Should().NotBeEmpty();

        var row = await db.LiveGameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == sessionId);

        row.Should().NotBeNull("the handler must have saved the session via IUnitOfWork.SaveChangesAsync");
        row!.GameName.Should().Be("Mage Knight");
        row.CreatedByUserId.Should().Be(userId);
        row.Status.Should().Be((int)LiveSessionStatus.Created);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AC-2 — Session state survives Postgres container restart
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AC-2: Session state survives Postgres container restart")]
    public async Task AC2_RestartSafe_StateSurvives()
    {
        Guid sessionId;
        Guid playerId;

        // Set up — create session + add player in one scope, then start in a FRESH scope.
        // Using separate scopes for Create/AddPlayer vs Start avoids EF change-tracker
        // accumulation issues when multiple handlers share the same DbContext instance.
        Guid userId;
        await using (var scope1 = _factoryAC2!.Services.CreateAsyncScope())
        {
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            var db = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            userId = await SeedUserAsync(db);

            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                UserId: userId,
                GameName: "Restart Test Game"));

            playerId = await mediator.Send(new AddPlayerToLiveSessionCommand(
                SessionId: sessionId,
                DisplayName: "Aaron",
                Color: PlayerColor.Red,
                UserId: userId));

        }

        // Start in a fresh scope so the change tracker is clean
        await using (var scope2 = _factoryAC2.Services.CreateAsyncScope())
        {
            var mediator = scope2.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new StartLiveSessionCommand(sessionId, userId, UserTier.Free, Role.User));

        }

        // Simulate container restart — stop then start the private Postgres container
        await _containerAC2!.StopAsync();

        // Release stale Npgsql pool connections so the reconnect goes through fresh TCP sockets
        NpgsqlConnection.ClearAllPools();

        await _containerAC2.StartAsync();

        // After restart, re-derive the connection string from the container's CURRENT mapped port
        // (Testcontainers may assign a different host port after start — the connection string
        // cached at InitializeAsync time may no longer be valid).
        _connStrAC2 = BuildConnStr(_containerAC2);

        // After a stop/start the Postgres process needs time to perform WAL recovery;
        // use a generous maxRetries=30 with 2s initial delay to allow up to ~90s total wait.
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(
            _connStrAC2, maxRetries: 30, initialDelayMs: 2000);

        // Re-create the factory with the new connection string (if port changed)
        await _factoryAC2!.DisposeAsync();
        _factoryAC2 = CreateMigratedFactory(_connStrAC2);

        // Verify — fresh scope, read back after restart
        await using var verifyScope = _factoryAC2.Services.CreateAsyncScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repo.GetByIdAsync(sessionId);

        session.Should().NotBeNull("session row must survive the container restart");
        session!.GameName.Should().Be("Restart Test Game");
        session.Status.Should().Be(LiveSessionStatus.InProgress,
            "Start was persisted before the restart");
        session.Players.Should().ContainSingle(p => p.DisplayName == "Aaron" && p.Id == playerId,
            "player row must survive the container restart");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AC-3 — Multi-instance: session created on scope A is readable on scope B
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AC-3: Multi-instance — session created on scope A is readable on scope B")]
    public async Task AC3_MultiInstance_StateShared()
    {
        // Two independent scopes on the SAME factory / DB — simulates two API instances
        // sharing the same Postgres without shared in-memory state.
        await using var scopeA = _factoryAC3.Services.CreateAsyncScope();
        await using var scopeB = _factoryAC3.Services.CreateAsyncScope();

        var mediatorA = scopeA.ServiceProvider.GetRequiredService<IMediator>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = await SeedUserAsync(dbA);

        var sessionId = await mediatorA.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Multi-instance Test"));

        // Scope B has its own DbContext (independent change tracker) — this models
        // a second API pod reading state that was written by the first pod.
        var repoB = scopeB.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var fromB = await repoB.GetByIdAsync(sessionId);

        fromB.Should().NotBeNull("scope B must read the committed state from scope A");
        fromB!.GameName.Should().Be("Multi-instance Test");
        fromB.Status.Should().Be(LiveSessionStatus.Created);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AC-4 — Concurrent updates → DbUpdateConcurrencyException via RowVersion
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AC-4: Concurrent updates throw DbUpdateConcurrencyException (optimistic concurrency)")]
    public async Task AC4_ConcurrentUpdates_ThrowsConcurrencyException()
    {
        Guid sessionId;

        // Seed: create session
        await using (var setupScope = _factoryAC4.Services.CreateAsyncScope())
        {
            var mediator = setupScope.ServiceProvider.GetRequiredService<IMediator>();
            var db = setupScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = await SeedUserAsync(db);

            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                UserId: userId,
                GameName: "Concurrency Test"));
        }

        // Two independent scopes load the same session — each gets its own DbContext
        // and therefore its own change tracker + its own stale copy of RowVersion.
        await using var scopeA = _factoryAC4.Services.CreateAsyncScope();
        await using var scopeB = _factoryAC4.Services.CreateAsyncScope();

        var repoA = scopeA.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var uowA = scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var uowB = scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Both read the session before any update
        var sessionA = await repoA.GetByIdAsync(sessionId);
        var sessionB = await repoB.GetByIdAsync(sessionId);

        sessionA.Should().NotBeNull();
        sessionB.Should().NotBeNull();

        // Mutate both domain objects (notes update is a lightweight mutation)
        sessionA!.UpdateNotes("From A");
        sessionB!.UpdateNotes("From B");

        // A wins the race — commits first
        await repoA.UpdateAsync(sessionA);
        await uowA.SaveChangesAsync();

        // B holds a stale RowVersion — must throw DbUpdateConcurrencyException
        await repoB.UpdateAsync(sessionB);
        Func<Task> act = () => uowB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the RowVersion on scope B's entity is stale after scope A committed — EF optimistic concurrency must reject the second save");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AC-5 — 100 score updates + container restart → all 100 RoundScores persist
    // ─────────────────────────────────────────────────────────────────────────

    // Timeout=600s (10 min) covers worst-case Docker Desktop load: 100 sequential
    // RecordLiveSessionScoreCommand sends (each ~300ms under load) + container restart
    // wait (max 90s) + EF migration replay + verify GET. Without this, the default
    // xunit method timeout (180s in xunit.runner.json) would kill the test under load
    // and cascade into sibling test failures.
    [Fact(Timeout = 600_000, DisplayName = "AC-5: 100 score updates + container restart → all 100 RoundScores persist")]
    public async Task AC5_HighFrequencyUpdates_RestartSafe()
    {
        Guid sessionId;
        Guid playerId;

        // Set up — create session, add player, start, then record 100 scores
        await using (var setupScope = _factoryAC5!.Services.CreateAsyncScope())
        {
            var mediator = setupScope.ServiceProvider.GetRequiredService<IMediator>();
            var db = setupScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = await SeedUserAsync(db);

            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                UserId: userId,
                GameName: "100-score Game",
                ScoringDimensions: new List<string> { "points" }));

            playerId = await mediator.Send(new AddPlayerToLiveSessionCommand(
                SessionId: sessionId,
                DisplayName: "Aaron",
                Color: PlayerColor.Red,
                UserId: userId));

            await mediator.Send(new StartLiveSessionCommand(sessionId, userId, UserTier.Free, Role.User));

            // 100 sequential score records — proves all writes are transactionally durable
            for (int round = 1; round <= 100; round++)
            {
                await mediator.Send(new RecordLiveSessionScoreCommand(
                    SessionId: sessionId,
                    PlayerId: playerId,
                    Round: round,
                    Dimension: "points",
                    Value: round * 10));
            }
        }

        // Container restart to prove durability
        await _containerAC5!.StopAsync();
        NpgsqlConnection.ClearAllPools();
        await _containerAC5.StartAsync();

        // Re-derive the connection string with the current mapped port after restart
        _connStrAC5 = BuildConnStr(_containerAC5);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(
            _connStrAC5, maxRetries: 30, initialDelayMs: 2000);

        // Re-create factory pointing at the restarted container
        await _factoryAC5!.DisposeAsync();
        _factoryAC5 = CreateMigratedFactory(_connStrAC5);

        // Verify — all 100 RoundScore rows must survive the restart
        await using var verifyScope = _factoryAC5.Services.CreateAsyncScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repo.GetByIdAsync(sessionId);

        session.Should().NotBeNull("session row must survive the container restart");
        session!.RoundScores.Should().HaveCount(100,
            "all 100 score writes were committed via IUnitOfWork.SaveChangesAsync before restart");

        // Sum 1*10 + 2*10 + ... + 100*10 = 10 * (100*101/2) = 50500
        var expectedSum = 10 * (100 * 101 / 2);
        session.RoundScores.Sum(s => s.Value).Should().Be(expectedSum,
            "each of the 100 rounds had Value = round * 10");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a minimal user entity required by CreateLiveSessionCommand.
    /// </summary>
    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@ac-test.local",
            DisplayName = "AC Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }

    /// <summary>
    /// Builds a private (non-shared) Postgres container for restart tests.
    /// Uses a random host port. Data is stored in the container's overlay FS (NOT tmpfs)
    /// so that it survives container stop/start (simulating a crash-recovery scenario for AC-2/5).
    /// </summary>
    private static IContainer BuildPrivateContainer()
    {
        return new ContainerBuilder()
            .WithImage(TestcontainersConfiguration.PostgresImage)
            .WithEnvironment("POSTGRES_USER", TestcontainersConfiguration.PostgresUsername)
            .WithEnvironment("POSTGRES_PASSWORD", TestcontainersConfiguration.PostgresPassword)
            .WithEnvironment("POSTGRES_DB", "live_session_restart_test")
            .WithPortBinding(5432, assignRandomHostPort: true)
            // Note: NO tmpfs mount — data must survive container stop/start for AC-2/AC-5
            .WithCommand(
                "-c", "max_connections=50",
                "-c", "shared_buffers=64MB")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Builds a connection string for a private container (no-pool, restart-safe settings).
    /// </summary>
    private static string BuildConnStr(IContainer container)
    {
        var port = container.GetMappedPublicPort(5432);
        return $"Host=localhost;Port={port};Database=live_session_restart_test;" +
               $"Username={TestcontainersConfiguration.PostgresUsername};" +
               $"Password={TestcontainersConfiguration.PostgresPassword};" +
               $"Ssl Mode=Disable;Trust Server Certificate=true;" +
               $"KeepAlive=10;Pooling=true;MinPoolSize=0;MaxPoolSize=5;" +
               $"Timeout=60;CommandTimeout=60;ConnectionIdleLifetime=5;ConnectionPruningInterval=3;";
    }

    /// <summary>
    /// Creates a <see cref="WebApplicationFactory{Program}"/> pointed at <paramref name="connectionString"/>
    /// and runs EF migrations before returning it.
    /// </summary>
    private static WebApplicationFactory<Program> CreateMigratedFactory(string connectionString)
    {
        var factory = IntegrationWebApplicationFactory.Create(connectionString);

        // Apply EF migrations so live_game_sessions table (and all required tables) exist
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        dbContext.Database.Migrate();

        return factory;
    }
}
