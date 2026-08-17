using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Issue #2036 — Integration tests for <see cref="GetGameSessionContributorsQueryHandler"/>.
///
/// Runs against a Postgres Testcontainers fixture because the handler relies on
/// EF Core <c>GroupBy</c> + <c>OrderByDescending</c> + <c>Take</c> translation
/// against a navigation collection — EF InMemory translates the pipeline
/// differently and would mask shape issues on real Postgres.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupB")]
public class GetGameSessionContributorsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_getgamecontributors_{Guid.NewGuid():N}";

    public GetGameSessionContributorsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Handle_RanksContributorsBySessionCount_ForGivenGame()
    {
        // Arrange — 3 users, 1 game, sessions with distinct participation counts.
        var gameId = SeedSharedGame(_dbContext!, "Catan");
        var alice = SeedUser(_dbContext!, "alice@example.com", "Alice Adams");
        var bob = SeedUser(_dbContext!, "bob@example.com", "Bob Bishop");
        var carol = SeedUser(_dbContext!, "carol@example.com", "Carol Cipriani");

        // Alice participates in 3 finalized sessions; Bob in 2; Carol in 1.
        SeedFinalizedSession(_dbContext!, gameId, alice.Id, "Alice");
        SeedFinalizedSession(_dbContext!, gameId, alice.Id, "Alice", coParticipant: (bob.Id, "Bob"));
        SeedFinalizedSession(_dbContext!, gameId, alice.Id, "Alice", coParticipant: (bob.Id, "Bob"));
        SeedFinalizedSession(_dbContext!, gameId, carol.Id, "Carol");

        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetGameSessionContributorsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new GetGameSessionContributorsQuery(gameId, Limit: 8),
            TestContext.Current.CancellationToken);

        // Assert — ordering: Alice (3), Bob (2), Carol (1).
        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(alice.Id);
        result[0].DisplayName.Should().Be("Alice Adams");
        result[0].Initials.Should().Be("AA");
        result[0].SessionCount.Should().Be(3);

        result[1].UserId.Should().Be(bob.Id);
        result[1].Initials.Should().Be("BB");
        result[1].SessionCount.Should().Be(2);

        result[2].UserId.Should().Be(carol.Id);
        result[2].Initials.Should().Be("CC");
        result[2].SessionCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesGuests_AndActiveSessions_AndDeletedSessions()
    {
        // Arrange
        var gameId = SeedSharedGame(_dbContext!, "Pandemic");
        var alice = SeedUser(_dbContext!, "alice@example.com", "Alice");

        // Finalized session — counts.
        SeedFinalizedSession(_dbContext!, gameId, alice.Id, "Alice");

        // Active session — must NOT count.
        SeedSession(_dbContext!, gameId, alice.Id, "Alice", status: "Active", isDeleted: false);

        // Soft-deleted session — must NOT count.
        SeedSession(_dbContext!, gameId, alice.Id, "Alice", status: "Finalized", isDeleted: true);

        // Guest participant (UserId == null) in a finalized session — must NOT
        // appear in results.
        var guestSession = SeedSession(_dbContext!, gameId, alice.Id, "Alice", status: "Finalized", isDeleted: false);
        guestSession.Participants.Add(new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = guestSession.Id,
            UserId = null,
            DisplayName = "Anonymous Guest",
            JoinOrder = 2,
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetGameSessionContributorsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new GetGameSessionContributorsQuery(gameId, Limit: 8),
            TestContext.Current.CancellationToken);

        // Assert — only Alice (across 2 finalized non-deleted sessions: the
        // first SeedFinalizedSession + the guest one), guest excluded.
        result.Should().ContainSingle();
        result[0].UserId.Should().Be(alice.Id);
        result[0].SessionCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ScopesByGameId_AndRespectsLimit()
    {
        // Arrange
        var gameA = SeedSharedGame(_dbContext!, "Carcassonne");
        var gameB = SeedSharedGame(_dbContext!, "Splendor");
        var alice = SeedUser(_dbContext!, "alice@example.com", "Alice");
        var bob = SeedUser(_dbContext!, "bob@example.com", "Bob");
        var carol = SeedUser(_dbContext!, "carol@example.com", "Carol");

        SeedFinalizedSession(_dbContext!, gameA, alice.Id, "Alice");
        SeedFinalizedSession(_dbContext!, gameA, bob.Id, "Bob");
        SeedFinalizedSession(_dbContext!, gameA, carol.Id, "Carol");

        // Game B participation — must NOT leak into Game A query.
        SeedFinalizedSession(_dbContext!, gameB, alice.Id, "Alice");
        SeedFinalizedSession(_dbContext!, gameB, alice.Id, "Alice");

        await _dbContext!.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetGameSessionContributorsQueryHandler(_dbContext);

        // Act — limit=2, gameA scope.
        var result = await handler.Handle(
            new GetGameSessionContributorsQuery(gameA, Limit: 2),
            TestContext.Current.CancellationToken);

        // Assert — only 2 of the 3 contributors, all from gameA (Alice has 1
        // session, not the leaked 3 from gameB).
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.SessionCount.Should().Be(1));
        result.Select(c => c.UserId).Should().NotContain(_ => false); // sanity: all userIds in expected set
        result.Select(c => c.UserId).Should().BeSubsetOf(new[] { alice.Id, bob.Id, carol.Id });
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_ForUnknownGame_OrEmptyGuid()
    {
        var handler = new GetGameSessionContributorsQueryHandler(_dbContext!);

        var empty = await handler.Handle(
            new GetGameSessionContributorsQuery(Guid.Empty, Limit: 8),
            TestContext.Current.CancellationToken);
        empty.Should().BeEmpty();

        var noSessions = await handler.Handle(
            new GetGameSessionContributorsQuery(Guid.NewGuid(), Limit: 8),
            TestContext.Current.CancellationToken);
        noSessions.Should().BeEmpty();
    }

    private static Guid SeedSharedGame(MeepleAiDbContext db, string title)
    {
        var id = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            Status = 1, // Published — `int` here, not the GameStatus enum (= 2 for Published)
            GameDataStatus = 5, // Complete
            CreatedAt = DateTime.UtcNow,
        });
        return id;
    }

    private static UserEntity SeedUser(MeepleAiDbContext db, string email, string displayName)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        return user;
    }

    private static SessionEntity SeedFinalizedSession(
        MeepleAiDbContext db,
        Guid gameId,
        Guid ownerUserId,
        string ownerDisplayName,
        (Guid UserId, string DisplayName)? coParticipant = null)
    {
        var session = SeedSession(db, gameId, ownerUserId, ownerDisplayName, status: "Finalized", isDeleted: false);
        if (coParticipant.HasValue)
        {
            session.Participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                UserId = coParticipant.Value.UserId,
                DisplayName = coParticipant.Value.DisplayName,
                JoinOrder = 2,
                CreatedAt = DateTime.UtcNow,
            });
        }
        return session;
    }

    private static SessionEntity SeedSession(
        MeepleAiDbContext db,
        Guid gameId,
        Guid ownerUserId,
        string ownerDisplayName,
        string status,
        bool isDeleted)
    {
        var sessionId = Guid.NewGuid();
        var session = new SessionEntity
        {
            Id = sessionId,
            UserId = ownerUserId,
            GameId = gameId,
            SessionCode = NextCode(),
            SessionType = "Standard",
            Status = status,
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = ownerUserId,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            FinalizedAt = status == "Finalized" ? DateTime.UtcNow : null,
        };
        session.Participants.Add(new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = ownerUserId,
            DisplayName = ownerDisplayName,
            IsOwner = true,
            JoinOrder = 1,
            CreatedAt = DateTime.UtcNow,
        });
        db.SessionTrackingSessions.Add(session);
        return session;
    }

    private static int _codeCounter;
    private static string NextCode() =>
        $"T{Interlocked.Increment(ref _codeCounter):D5}";
}
