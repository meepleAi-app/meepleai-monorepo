using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
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
/// Integration tests proving the <see cref="GameNightEventRepository"/> round-trips candidate
/// votes (Issue #2700) through the detached-Update full-remap, and that an unrelated update
/// does NOT silently wipe votes (the queries feeding a mutation path must Include the Votes
/// owned collection — same class of bug as the xmin round-trip in #2703).
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2700")]
public sealed class GameNightVotingRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightVotingRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_votes_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private GameNightEventRepository Repo(MeepleAiDbContext db) =>
        new(db, Mock.Of<IDomainEventCollector>(), Mock.Of<ILogger<GameNightEventRepository>>());

    private async Task<(Guid EventId, Guid Voter, Guid GameA, Guid GameB)> SeedPublishedEventAsync()
    {
        var eventId = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Voting Night",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(2),
            GameIdsJson = JsonSerializer.Serialize(new List<Guid> { gameA, gameB }),
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            Rsvps =
            {
                new GameNightRsvpEntity
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    UserId = voter,
                    Status = "Accepted",
                    CreatedAt = DateTimeOffset.UtcNow
                }
            }
        });
        await _dbContext.SaveChangesAsync(Ct);
        return (eventId, voter, gameA, gameB);
    }

    [Fact(DisplayName = "Cast votes round-trip through the repository and tally correctly")]
    public async Task CastVotes_RoundTripThroughRepository()
    {
        var (eventId, voter, gameA, _) = await SeedPublishedEventAsync();

        // Load (for the domain guards), cast a vote, persist via the tracked AddVote path.
        await using (var dbWrite = _fixture.CreateDbContext(_connectionString))
        {
            var repo = Repo(dbWrite);
            var evt = await repo.GetByIdAsync(eventId, Ct);
            var vote = evt!.CastVote(voter, gameA, DateTimeOffset.UtcNow);
            await repo.AddVoteAsync(vote!, Ct);
            await dbWrite.SaveChangesAsync(Ct);
        }

        // Reload from a fresh context and assert the vote survived + tally is right.
        await using var dbRead = _fixture.CreateDbContext(_connectionString);
        var reloaded = await Repo(dbRead).GetByIdAsync(eventId, Ct);

        reloaded!.Votes.Should().ContainSingle(v => v.VoterUserId == voter && v.CandidateGameId == gameA);
        reloaded.TallyVotes().CountsByCandidate[gameA].Should().Be(1);
    }

    [Fact(DisplayName = "Duplicate (event,voter,candidate) vote is rejected by the unique index")]
    public async Task DuplicateVote_ViolatesUniqueIndex()
    {
        var (eventId, voter, gameA, _) = await SeedPublishedEventAsync();

        await using var db = _fixture.CreateDbContext(_connectionString);
        var repo = Repo(db);

        var evt = await repo.GetByIdAsync(eventId, Ct);
        var first = evt!.CastVote(voter, gameA, DateTimeOffset.UtcNow);
        await repo.AddVoteAsync(first!, Ct);
        await db.SaveChangesAsync(Ct);

        // A distinct row (new id) for the same (event, voter, candidate) tuple must be rejected —
        // this is the DB guard the CastVote handler relies on to convert a concurrent race into
        // an idempotent success.
        var duplicate = GameNightVote.Create(eventId, voter, gameA);
        await repo.AddVoteAsync(duplicate, Ct);
        Func<Task> act = async () => await db.SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "An unrelated update does NOT wipe previously-cast votes")]
    public async Task UnrelatedUpdate_DoesNotWipeVotes()
    {
        var (eventId, voter, gameA, _) = await SeedPublishedEventAsync();

        await using (var dbVote = _fixture.CreateDbContext(_connectionString))
        {
            var repo = Repo(dbVote);
            var evt = await repo.GetByIdAsync(eventId, Ct);
            var vote = evt!.CastVote(voter, gameA, DateTimeOffset.UtcNow);
            await repo.AddVoteAsync(vote!, Ct);
            await dbVote.SaveChangesAsync(Ct);
        }

        // Mutate an unrelated field (title) through the detached-Update path — which
        // re-maps existing votes as Modified UPDATEs, so they must survive.
        await using (var dbEdit = _fixture.CreateDbContext(_connectionString))
        {
            var repo = Repo(dbEdit);
            var evt = await repo.GetByIdAsync(eventId, Ct);
            evt!.Update("Renamed Night", evt.Description, evt.ScheduledAt, evt.Location, evt.MaxPlayers, null);
            await repo.UpdateAsync(evt, Ct);
            await dbEdit.SaveChangesAsync(Ct);
        }

        // The vote must still be there — GetByIdAsync includes Votes, so the remap preserves it.
        await using var dbRead = _fixture.CreateDbContext(_connectionString);
        var reloaded = await Repo(dbRead).GetByIdAsync(eventId, Ct);

        reloaded!.Title.Should().Be("Renamed Night");
        reloaded.Votes.Should().ContainSingle(v => v.CandidateGameId == gameA);
    }
}
