using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public class ScoreEntryRepositoryTests : SharedDatabaseTestBase<ScoreEntryRepository>
{
    private SessionRepository _sessionRepository = null!;

    public ScoreEntryRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override ScoreEntryRepository CreateRepository(MeepleAiDbContext dbContext)
    {
        _sessionRepository = new SessionRepository(dbContext);
        return new ScoreEntryRepository(dbContext);
    }

    private async Task<Guid> CreateTestUserAsync(string email = "score-test@example.com")
    {
        var userId = Guid.NewGuid();
        await DbContext.Users.AddAsync(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = $"Score Test User",
            PasswordHash = "test-hash",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false
        });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userId;
    }

    private async Task<Guid> CreateTestGameAsync()
    {
        var gameId = Guid.NewGuid();
        DbContext.Games.Add(new Api.Infrastructure.Entities.GameEntity
        {
            Id = gameId,
            Name = $"Test Game {gameId:N}",
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gameId;
    }

    private async Task<(Session Session, Participant Participant)> CreateTestSessionAsync()
    {
        var userId = await CreateTestUserAsync($"session-{Guid.NewGuid():N}@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        var participantInfo = ParticipantInfo.Create("Test Player", false, 2);
        session.AddParticipant(participantInfo);

        await _sessionRepository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();

        return (session, session.Participants.Last());
    }

    [Fact]
    public async Task AddAsync_ShouldPersistScoreEntry()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();
        var scoreEntry = ScoreEntry.Create(session.Id, participant.Id, 25.5m, Guid.NewGuid(), roundNumber: 1);

        // Act
        await Repository.AddAsync(scoreEntry, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var scores = await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken);
        scores.Should().ContainSingle();
        scores.First().ScoreValue.Should().Be(25.5m);
    }

    [Fact]
    public async Task GetBySessionIdAsync_ShouldReturnAllScoresOrdered()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();

        var score1 = ScoreEntry.Create(session.Id, participant.Id, 10m, Guid.NewGuid(), roundNumber: 1);
        Thread.Sleep(10);
        var score2 = ScoreEntry.Create(session.Id, participant.Id, 20m, Guid.NewGuid(), roundNumber: 2);
        Thread.Sleep(10);
        var score3 = ScoreEntry.Create(session.Id, participant.Id, 30m, Guid.NewGuid(), roundNumber: 3);

        await Repository.AddAsync(score3, TestContext.Current.CancellationToken); // Add in reverse order
        await Repository.AddAsync(score1, TestContext.Current.CancellationToken);
        await Repository.AddAsync(score2, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var scores = (await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken)).ToList();

        // Assert
        scores.Should().HaveCount(3);
        scores[0].RoundNumber.Should().Be(1); // Ordered by timestamp
        scores[1].RoundNumber.Should().Be(2);
        scores[2].RoundNumber.Should().Be(3);
    }

    [Fact]
    public async Task GetByParticipantAsync_ShouldFilterByParticipant()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();
        var owner = session.Participants.First(p => p.IsOwner);

        var score1 = ScoreEntry.Create(session.Id, owner.Id, 10m, Guid.NewGuid(), roundNumber: 1);
        var score2 = ScoreEntry.Create(session.Id, participant.Id, 20m, Guid.NewGuid(), roundNumber: 1);

        await Repository.AddAsync(score1, TestContext.Current.CancellationToken);
        await Repository.AddAsync(score2, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var participantScores = await Repository.GetByParticipantAsync(
            session.Id,
            participant.Id,
            TestContext.Current.CancellationToken);

        // Assert
        participantScores.Should().ContainSingle();
        participantScores.First().ScoreValue.Should().Be(20m);
    }

    [Fact]
    public async Task AddBatchAsync_ShouldInsertMultipleScores()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();

        var scores = new[]
        {
            ScoreEntry.Create(session.Id, participant.Id, 10m, Guid.NewGuid(), roundNumber: 1),
            ScoreEntry.Create(session.Id, participant.Id, 20m, Guid.NewGuid(), roundNumber: 2),
            ScoreEntry.Create(session.Id, participant.Id, 30m, Guid.NewGuid(), roundNumber: 3)
        };

        // Act
        await Repository.AddBatchAsync(scores, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var allScores = await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken);
        allScores.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddBatchAsync_WithEmptyCollection_ShouldNotFail()
    {
        // Arrange
        await ResetDatabaseAsync();
        var emptyScores = Array.Empty<ScoreEntry>();

        // Act & Assert - should not throw
        await Repository.AddBatchAsync(emptyScores, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyScoreEntry()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();

        var scoreEntry = ScoreEntry.Create(session.Id, participant.Id, 10m, Guid.NewGuid(), roundNumber: 1);
        await Repository.AddAsync(scoreEntry, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var retrieved = (await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken)).First();
        retrieved.UpdateScore(50m);

        // Act
        await Repository.UpdateAsync(retrieved, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var updated = (await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken)).First();
        updated.ScoreValue.Should().Be(50m);
    }

    [Fact]
    public async Task ScoreEntry_DecimalPrecision_ShouldBePreserved()
    {
        // Arrange
        await ResetDatabaseAsync();
        var (session, participant) = await CreateTestSessionAsync();

        var preciseScore = 123.45m;
        var scoreEntry = ScoreEntry.Create(session.Id, participant.Id, preciseScore, Guid.NewGuid(), roundNumber: 1);

        // Act
        await Repository.AddAsync(scoreEntry, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = (await Repository.GetBySessionIdAsync(session.Id, TestContext.Current.CancellationToken)).First();
        retrieved.ScoreValue.Should().Be(123.45m);
    }
}
