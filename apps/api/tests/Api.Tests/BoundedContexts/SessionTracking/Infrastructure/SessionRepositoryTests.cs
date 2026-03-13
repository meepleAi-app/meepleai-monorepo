using Api.BoundedContexts.SessionTracking.Domain.Entities;
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
public class SessionRepositoryTests : SharedDatabaseTestBase<SessionRepository>
{
    public SessionRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override SessionRepository CreateRepository(MeepleAiDbContext dbContext)
        => new SessionRepository(dbContext);

    private async Task<Guid> CreateTestUserAsync(string email = "test@example.com")
    {
        var userId = Guid.NewGuid();
        await DbContext.Users.AddAsync(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = $"Test User {email}",
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

    [Fact]
    public async Task AddAsync_ShouldPersistSession()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);

        // Act
        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = await Repository.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.SessionCode.Should().Be(session.SessionCode);
        retrieved.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task GetByCodeAsync_ShouldFindSession()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test1@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var retrieved = await Repository.GetByCodeAsync(session.SessionCode, TestContext.Current.CancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetByCodeAsync_WithLowercaseCode_ShouldFind()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test2@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var retrieved = await Repository.GetByCodeAsync(
            session.SessionCode.ToLowerInvariant(),
            TestContext.Current.CancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldReturnOnlyActiveAndPaused()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test3@example.com");
        var gameId1 = await CreateTestGameAsync();
        var gameId2 = await CreateTestGameAsync();
        var gameId3 = await CreateTestGameAsync();
        var session1 = Session.Create(userId, gameId1, SessionType.Generic);
        var session2 = Session.Create(userId, gameId2, SessionType.Generic);
        session2.Pause();
        var session3 = Session.Create(userId, gameId3, SessionType.Generic);
        session3.Finalize();

        await Repository.AddAsync(session1, TestContext.Current.CancellationToken);
        await Repository.AddAsync(session2, TestContext.Current.CancellationToken);
        await Repository.AddAsync(session3, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var activeSessions = await Repository.GetActiveByUserIdAsync(userId, TestContext.Current.CancellationToken);

        // Assert
        var sessionList = activeSessions.ToList();
        sessionList.Should().HaveCount(2);
        sessionList.Should().Contain(s => s.Id == session1.Id);
        sessionList.Should().Contain(s => s.Id == session2.Id);
        sessionList.Should().NotContain(s => s.Id == session3.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifySession()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test4@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var retrieved = await Repository.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        retrieved!.Pause();

        // Act
        await Repository.UpdateAsync(retrieved, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var updated = await Repository.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        updated!.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDelete()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test5@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await Repository.DeleteAsync(session.Id, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var retrieved = await Repository.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        retrieved.Should().BeNull(); // Soft delete query filter hides deleted records
    }

    [Fact]
    public async Task AddAsync_WithDuplicateSessionCode_ShouldThrow()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test6@example.com");
        var gameId1 = await CreateTestGameAsync();
        var gameId2 = await CreateTestGameAsync();
        var session1 = Session.Create(userId, gameId1, SessionType.Generic);
        await Repository.AddAsync(session1, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();

        // Create session2 with same code (simulate collision)
        var session2 = Session.Create(userId, gameId2, SessionType.Generic);
        typeof(Session).GetProperty("SessionCode")!.SetValue(session2, session1.SessionCode);

        // Act
        var act = async () =>
        {
            await Repository.AddAsync(session2, TestContext.Current.CancellationToken);
            await DbContext.SaveChangesAsync();
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Session code*already exists*");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldIncludeParticipants()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync("test7@example.com");
        var gameId = await CreateTestGameAsync();
        var session = Session.Create(userId, gameId, SessionType.Generic);
        var participantInfo = Api.BoundedContexts.SessionTracking.Domain.ValueObjects.ParticipantInfo.Create("Player 2", false, 2);
        session.AddParticipant(participantInfo);

        await Repository.AddAsync(session, TestContext.Current.CancellationToken);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var retrieved = await Repository.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Participants.Should().HaveCount(2); // Owner + Player 2
    }
}
