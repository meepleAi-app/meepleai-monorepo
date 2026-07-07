using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests proving that <c>game_night_events</c> enforces optimistic concurrency
/// via the PostgreSQL <c>xmin</c> system-column concurrency token — Issue #2703 (Umbrella #2697,
/// spec §4a US-INT-3 failure-mode #3, ADR-060).
///
/// Unlike the sibling <see cref="GameNightPlaylistRowVersionConcurrencyTests"/> (which checks at
/// the raw-entity level), these tests exercise the full <see cref="GameNightEventRepository"/>
/// round-trip: the repository loads with <c>AsNoTracking()</c> and writes with a detached
/// <c>.Update()</c> that re-maps a brand-new persistence entity. The xmin token must survive that
/// round-trip (loaded in <c>MapToDomain</c>, written back in <c>MapToPersistence</c>) for the
/// concurrency check to fire. Before #2703 the entity had no xmin token, so the detached UPDATE
/// emitted <c>WHERE id = @id</c> only → last-write-wins → the second writer silently overwrote.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2703")]
public sealed class GameNightEventConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightEventConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private GameNightEventRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, Mock.Of<IDomainEventCollector>(), Mock.Of<ILogger<GameNightEventRepository>>());

    private async Task<Guid> SeedEventAsync(string status = "Published")
    {
        var eventId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Original Title",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            GameIdsJson = "[]",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return eventId;
    }

    /// <summary>
    /// Two organisers load the same game night, both edit, the first commits (advancing the row's
    /// xmin), then the second commits holding the stale token — EF must raise
    /// <see cref="DbUpdateConcurrencyException"/> (0 rows affected). The global middleware maps this
    /// to 409 with <c>X-Warning-Code: concurrent-edit</c>.
    /// </summary>
    [Fact(DisplayName = "Concurrent GameNightEvent updates via repository throw DbUpdateConcurrencyException via xmin")]
    public async Task ConcurrentUpdates_ViaRepository_ThrowDbUpdateConcurrencyException()
    {
        // ── Arrange: seed one event, then load it from two independent contexts ──
        var eventId = await SeedEventAsync();

        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);
        var repoA = CreateRepository(dbA);
        var repoB = CreateRepository(dbB);

        var eventA = await repoA.GetByIdAsync(eventId, TestCancellationToken);
        var eventB = await repoB.GetByIdAsync(eventId, TestCancellationToken);
        eventA.Should().NotBeNull();
        eventB.Should().NotBeNull();

        // ── Act: A wins the race and commits first (xmin advances in Postgres) ──
        eventA!.Update("Updated by A", null, eventA.ScheduledAt, null, null, null);
        await repoA.UpdateAsync(eventA, TestCancellationToken);
        await dbA.SaveChangesAsync(TestCancellationToken);

        // B holds a stale xmin — its detached UPDATE must match 0 rows in Postgres.
        eventB!.Update("Updated by B", null, eventB.ScheduledAt, null, null, null);
        await repoB.UpdateAsync(eventB, TestCancellationToken);
        Func<Task> act = async () => await dbB.SaveChangesAsync(TestCancellationToken);

        // ── Assert ──────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "scope B holds a stale xmin after scope A committed — " +
            "EF optimistic concurrency must reject the second save");
    }

    /// <summary>
    /// Regression guard: a single load→edit→save round-trip through the repository must still
    /// persist the change. Protects against the xmin migration accidentally turning the detached
    /// update into a silent no-op (cfr. memory repo-updateasync-astracking-notracking).
    /// </summary>
    [Fact(DisplayName = "Single GameNightEvent update via repository persists the change")]
    public async Task SingleUpdate_ViaRepository_PersistsChange()
    {
        // ── Arrange ─────────────────────────────────────────────────────────────
        var eventId = await SeedEventAsync();

        await using var dbWrite = _fixture.CreateDbContext(_connectionString);
        var repo = CreateRepository(dbWrite);

        var evt = await repo.GetByIdAsync(eventId, TestCancellationToken);
        evt.Should().NotBeNull();

        // ── Act ─────────────────────────────────────────────────────────────────
        evt!.Update("Updated Title", "New description", evt.ScheduledAt, "New location", 8, null);
        await repo.UpdateAsync(evt, TestCancellationToken);
        await dbWrite.SaveChangesAsync(TestCancellationToken);

        // ── Assert: reload from a fresh context and confirm the scalar change stuck ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);
        var reloaded = await dbVerify.GameNightEvents
            .AsNoTracking()
            .FirstAsync(e => e.Id == eventId, TestCancellationToken);

        reloaded.Title.Should().Be("Updated Title");
        reloaded.Location.Should().Be("New location");
        reloaded.MaxPlayers.Should().Be(8);
    }
}
