using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
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
/// Integration tests for PlayRecord version history + restore.
/// Issue #2437-3: end-to-end snapshot-on-update, GET versions, restore, cap-5 enforcement.
///
/// Version semantics: a version snapshot is taken BEFORE each UpdateDetails call,
/// so each version holds the state that EXISTED BEFORE that particular update ran.
///
/// Scenarios:
/// 1. Two updates produce 2 versions; restore to version 1 sets record to version 1's values;
///    a third version is created (the pre-restore state), so after restore there are 3 versions.
/// 2. Six updates → only 5 versions retained (oldest pruned, cap enforcement).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public sealed class PlayRecordVersionsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    public PlayRecordVersionsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider not initialized.");

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"playrecord_versions_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<IPlayRecordVersionRepository, PlayRecordVersionRepository>();
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

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 1: two updates → GET versions → restore → verify undo-ability
    // ─────────────────────────────────────────────────────────────────────────
    //
    // Timeline:
    //   Create record (Notes = null)
    //   Update to "A"  → version 1 snaps  null  (the state before "A")
    //   Update to "B"  → version 2 snaps  "A"   (the state before "B")
    //   Record is now "B", versions: [v2="A", v1=null] DESC
    //   Restore to v2  → version 3 snaps  "B"  (the state before the restore)
    //                    record becomes "A"
    //   Versions after: [v3="B", v2="A", v1=null] DESC
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TwoUpdates_GetVersions_RestoreToV2_RecordHasV2Values_And_NewVersionCapturesPreRestoreState")]
    public async Task TwoUpdates_GetVersions_RestoreToV2_RecordHasV2Values_And_NewVersionCapturesPreRestoreState()
    {
        // ── Arrange: create record (Notes = null) ─────────────────────────────
        var userId = await SeedTestUserAsync();
        var recordId = await CreateBareRecordAsync(userId);

        // ── Act: two updates ──────────────────────────────────────────────────
        await SendInScopeAsync(new UpdatePlayRecordCommand(recordId, userId, Notes: "A")); // version 1 → null
        await SendInScopeAsync(new UpdatePlayRecordCommand(recordId, userId, Notes: "B")); // version 2 → "A"

        // ── Assert: GET versions returns 2 entries DESC ───────────────────────
        var versionsBeforeRestore = await SendInScopeAsync<IReadOnlyList<PlayRecordVersionDto>>(
            new GetPlayRecordVersionsQuery(recordId, userId));

        versionsBeforeRestore.Should().HaveCount(2, "two updates produce two version snapshots");
        versionsBeforeRestore[0].VersionNumber.Should().Be(2, "most-recent first");
        versionsBeforeRestore[0].Notes.Should().Be("A", "version 2 captured the 'A' state before the 'B' update");
        versionsBeforeRestore[1].VersionNumber.Should().Be(1);
        versionsBeforeRestore[1].Notes.Should().BeNull("version 1 captured the initial null Notes");

        // Record should currently be "B"
        {
            await using var db = _fixture.CreateDbContext(_connectionString);
            var rec = await db.PlayRecords.AsNoTracking()
                .FirstAsync(r => r.Id == recordId, TestCancellationToken);
            rec.Notes.Should().Be("B");
        }

        // ── Act: restore to version 2 (Notes = "A") ──────────────────────────
        await SendInScopeAsync(new RestorePlayRecordVersionCommand(recordId, 2, userId));

        // ── Assert: record now has Notes = "A" (version 2's value) ───────────
        {
            await using var db = _fixture.CreateDbContext(_connectionString);
            var rec = await db.PlayRecords.AsNoTracking()
                .FirstAsync(r => r.Id == recordId, TestCancellationToken);
            rec.Notes.Should().Be("A", "restore applied version 2's Notes value");
        }

        // ── Assert: a NEW version was created capturing the pre-restore "B" state
        var versionsAfterRestore = await SendInScopeAsync<IReadOnlyList<PlayRecordVersionDto>>(
            new GetPlayRecordVersionsQuery(recordId, userId));

        versionsAfterRestore.Should().HaveCount(3, "the restore itself creates a new snapshot (undo-able)");
        versionsAfterRestore[0].VersionNumber.Should().Be(3, "the pre-restore snapshot is newest");
        versionsAfterRestore[0].Notes.Should().Be("B", "version 3 captured the 'B' state before the restore");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 2: 6 updates → only 5 versions retained (cap enforcement)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "SixUpdates_VersionCapOf5_OldestVersionPruned")]
    public async Task SixUpdates_VersionCapOf5_OldestVersionPruned()
    {
        // ── Arrange: create record, then perform 6 updates ────────────────────
        var userId = await SeedTestUserAsync();
        var recordId = await CreateBareRecordAsync(userId);

        for (var i = 1; i <= 6; i++)
        {
            await SendInScopeAsync(new UpdatePlayRecordCommand(recordId, userId, Notes: $"Update {i}"));
        }

        // ── Assert: only 5 versions remain ────────────────────────────────────
        var versions = await SendInScopeAsync<IReadOnlyList<PlayRecordVersionDto>>(
            new GetPlayRecordVersionsQuery(recordId, userId));

        versions.Should().HaveCount(5, "cap is 5 — the oldest version (v1) is pruned after the 6th update");
        versions.Select(v => v.VersionNumber).Should().BeInDescendingOrder();

        // The 5 retained versions are v2–v6 (v1 was pruned as the oldest).
        versions.Max(v => v.VersionNumber).Should().Be(6);
        versions.Min(v => v.VersionNumber).Should().Be(2,
            "version 1 (the oldest) must have been pruned by the 6th update");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTestUserAsync()
    {
        var id = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"version-{id:N}@meepleai.test",
            DisplayName = "Version Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    /// <summary>
    /// Creates a play record with no initial notes/location, without triggering any version snapshots.
    /// The first UpdatePlayRecordCommand issued by the test will create version 1.
    /// </summary>
    private async Task<Guid> CreateBareRecordAsync(Guid creatorUserId)
    {
        var gameId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Version Test Game",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);

        var createCommand = new CreatePlayRecordCommand(
            creatorUserId,
            gameId,
            "Version Test Game",
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        return await SendInScopeAsync<Guid>(createCommand);
    }

    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(request, TestCancellationToken);
    }
}
