using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
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
/// Integration test reproducing the persistence path of Issue #2835: publishing a game night
/// that has a pre-invited user must NOT create a duplicate <c>game_night_rsvps</c> row for that
/// user.
///
/// <para>The bug: <c>CreateGameNightCommandHandler</c> calls <c>PreInvite(...)</c> at create time
/// (persisting RSVP #1), then <c>PublishGameNightCommandHandler</c> reads the existing invited IDs
/// back off the RSVPs and passes them to <c>Publish(...)</c>, which — before the fix — created a
/// SECOND RSVP for the same <c>(event_id, user_id)</c>. Two rows with the same unique-index key in
/// one <c>SaveChanges</c> made EF Core throw
/// <c>InvalidOperationException: ... circular dependency was detected</c> (Multigraph.ThrowCycle)
/// → HTTP 500.</para>
///
/// <para>This test exercises the full repository round-trip (AddAsync → SaveChanges →
/// GetByIdAsync → Publish → UpdateAsync → SaveChanges) against a real Postgres container.
/// It fails (Multigraph cycle) on origin/main-dev before the dedup guard is added to
/// <see cref="GameNightEvent.Publish"/>, and passes after.</para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2835")]
public sealed class GameNightEventPublishRsvpIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightEventPublishRsvpIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_publish_rsvp_{Guid.NewGuid():N}";
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

    /// <summary>
    /// Full path: create a Draft night with a pre-invited user, persist it, reload it,
    /// publish it (passing the same invited user id — exactly what the real command handler
    /// does), and save. Before #2835 the second SaveChanges throws the Multigraph cycle; after
    /// the fix it succeeds and leaves exactly ONE rsvp row for (event, user) with the night
    /// Published.
    /// </summary>
    [Fact(DisplayName = "Publishing a pre-invited game night persists exactly one RSVP (no Multigraph cycle) — #2835")]
    public async Task Publish_WithPreInvitedUser_PersistsSingleRsvp()
    {
        // ── Arrange: create a draft with one pre-invited user and persist it ──
        var invitedUserId = Guid.NewGuid();

        await using (var dbCreate = _fixture.CreateDbContext(_connectionString))
        {
            var repoCreate = CreateRepository(dbCreate);
            var draft = GameNightEvent.Create(
                organizerId: Guid.NewGuid(),
                title: "Boardgame Night",
                scheduledAt: DateTimeOffset.UtcNow.AddDays(3));
            draft.PreInvite(new List<Guid> { invitedUserId });

            await repoCreate.AddAsync(draft, TestCancellationToken);
            await dbCreate.SaveChangesAsync(TestCancellationToken);

            _eventId = draft.Id;
        }

        // Sanity: the pre-invite produced exactly one persisted RSVP row.
        await using (var dbSanity = _fixture.CreateDbContext(_connectionString))
        {
            var preInviteRows = await dbSanity.GameNightRsvps
                .AsNoTracking()
                .CountAsync(r => r.EventId == _eventId && r.UserId == invitedUserId, TestCancellationToken);
            preInviteRows.Should().Be(1, "PreInvite persists exactly one RSVP for the invited user");
        }

        // ── Act: reload, publish (handler passes existing invited ids), save ──
        await using (var dbPublish = _fixture.CreateDbContext(_connectionString))
        {
            var repoPublish = CreateRepository(dbPublish);

            var night = await repoPublish.GetByIdAsync(_eventId, TestCancellationToken);
            night.Should().NotBeNull();

            // Mirror PublishGameNightCommandHandler: invited ids come from the existing RSVPs.
            var existingInvitedIds = night!.Rsvps.Select(r => r.UserId).ToList();
            night.Publish(existingInvitedIds);

            await repoPublish.UpdateAsync(night, TestCancellationToken);

            // This SaveChanges throws the Multigraph "circular dependency" cycle pre-fix.
            Func<Task> act = async () => await dbPublish.SaveChangesAsync(TestCancellationToken);
            await act.Should().NotThrowAsync(
                "publishing a pre-invited night must not persist a duplicate (event, user) RSVP (#2835)");
        }

        // ── Assert: exactly one RSVP row survives and the night is Published ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);

        var rsvpCount = await dbVerify.GameNightRsvps
            .AsNoTracking()
            .CountAsync(r => r.EventId == _eventId && r.UserId == invitedUserId, TestCancellationToken);
        rsvpCount.Should().Be(1, "no duplicate RSVP row for the (event, user) unique key after publish");

        var reloaded = await dbVerify.GameNightEvents
            .AsNoTracking()
            .FirstAsync(e => e.Id == _eventId, TestCancellationToken);
        reloaded.Status.Should().Be("Published");
    }

    private Guid _eventId;
}
