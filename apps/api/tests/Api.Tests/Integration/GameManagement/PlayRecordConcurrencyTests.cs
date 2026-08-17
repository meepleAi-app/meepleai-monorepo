using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for PlayRecord optimistic concurrency via PostgreSQL xmin.
/// Issue #2437-1: end-to-end stale-form 409.
///
/// Two scenarios:
/// 1. Stale xmin → DbUpdateConcurrencyException (the middleware maps this to 409).
/// 2. Null xmin  → fresh-load fallback, no concurrency check, succeeds.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public sealed class PlayRecordConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    public PlayRecordConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"playrecord_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Also build DI container for handler-level tests
        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// When the client holds a stale xmin (another writer committed in between),
    /// the UPDATE matches 0 rows → EF raises DbUpdateConcurrencyException.
    /// The global middleware maps this to 409 with X-Warning-Code: concurrent-edit.
    /// </summary>
    [Fact(DisplayName = "Update_WithStaleXmin_ThrowsDbUpdateConcurrencyException")]
    public async Task Update_WithStaleXmin_ThrowsDbUpdateConcurrencyException()
    {
        // ── Arrange: seed a play record row ────────────────────────────────────
        var userId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(userId);

        // Capture the current xmin by reading the row from Postgres
        await using var dbA = _fixture.CreateDbContext(_connectionString);
        var rowA = await dbA.PlayRecords.FirstAsync(r => r.Id == recordId, TestCancellationToken);
        var staleXmin = rowA.Xmin;

        // ── Act (part 1): first writer updates the row (xmin advances in DB) ──
        var firstUpdate = new UpdatePlayRecordCommand(
            recordId,
            userId,
            Notes: "First writer wins",
            Xmin: null);  // no xmin check — just mutate to advance xmin
        await SendInScopeAsync(firstUpdate);

        // ── Act (part 2): second writer uses the stale xmin → must throw ──────
        var staleUpdate = new UpdatePlayRecordCommand(
            recordId,
            userId,
            Notes: "Stale form submit",
            Xmin: staleXmin);  // #2437-1: push stale token → EF rejects

        Func<Task> act = async () => await SendInScopeAsync(staleUpdate);

        // ── Assert ─────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the second writer holds a stale xmin — " +
            "EF optimistic concurrency must reject the update (0 rows affected)");
    }

    /// <summary>
    /// When Xmin is null (omitted / non-versioned caller), the handler skips SetXmin
    /// → fresh-load behaviour → always succeeds even after a concurrent write.
    /// </summary>
    [Fact(DisplayName = "Update_WithoutXmin_SucceedsFreshLoad")]
    public async Task Update_WithoutXmin_SucceedsFreshLoad()
    {
        // ── Arrange ─────────────────────────────────────────────────────────────
        var userId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(userId);

        // Advance xmin by performing a first update
        await SendInScopeAsync(new UpdatePlayRecordCommand(recordId, userId, Notes: "First write"));

        // ── Act: second update with Xmin == null (no concurrency check) ────────
        Func<Task> act = async () => await SendInScopeAsync(
            new UpdatePlayRecordCommand(recordId, userId, Notes: "Second write, no xmin"));

        // ── Assert ─────────────────────────────────────────────────────────────
        await act.Should().NotThrowAsync(
            "null Xmin means fresh-load behaviour — no concurrency check is applied");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTestUserAsync()
    {
        var id = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"xmin-{id:N}@meepleai.test",
            DisplayName = "Xmin Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> CreateTestRecordAsync(Guid creatorUserId)
    {
        var gameId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Xmin Test Game",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);

        var command = new CreatePlayRecordCommand(
            creatorUserId,
            gameId,
            "Xmin Test Game",
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        return await SendInScopeAsync(command);
    }

    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest command)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, TestCancellationToken);
    }
}
