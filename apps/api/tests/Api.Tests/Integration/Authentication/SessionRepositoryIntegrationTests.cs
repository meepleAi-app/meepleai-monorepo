using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for SessionRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, session lifecycle, token validation, and revocation.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2307")]
public sealed class SessionRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ISessionRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;
    private TimeProvider? _timeProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestSessionId1 = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TestSessionId2 = new("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TestSessionId3 = new("20000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("10000000-0000-0000-0000-000000000002");

    public SessionRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_sessionrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<ISessionRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        _timeProvider = _serviceProvider.GetRequiredService<TimeProvider>();

        // Create database schema
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static User CreateTestUser(
        Guid id,
        string email,
        string displayName = "Test User",
        Role? role = null,
        UserTier? tier = null)
    {
        var user = new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
        return user;
    }

    private static Session CreateTestSession(
        Guid sessionId,
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        TimeSpan? lifetime = null,
        TimeProvider? timeProvider = null)
    {
        var token = SessionToken.FromStored(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        return new Session(
            id: sessionId,
            userId: userId,
            token: token,
            lifetime: lifetime,
            ipAddress: ipAddress,
            userAgent: userAgent,
            timeProvider: timeProvider ?? TimeProvider.System
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.UserSessions.RemoveRange(_dbContext.UserSessions);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task EnsureUserExistsAsync(Guid userId, string? email = null)
    {
        using var scope = _serviceProvider!.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingUser = await userRepo.GetByIdAsync(userId, TestCancellationToken);
        if (existingUser == null)
        {
            var user = CreateTestUser(userId, email ?? $"user{userId:N}@test.com");
            await userRepo.AddAsync(user, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        }
    }

    /// <summary>
    /// Execute action in isolated scope with fresh DbContext to avoid tracking conflicts
    /// </summary>
    private async Task<T> ExecuteInScopeAsync<T>(Func<ISessionRepository, IUnitOfWork, Task<T>> action)
    {
        var scopedRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingSession_ShouldReturnSession()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1, "user1@test.com");
        var session = CreateTestSession(TestSessionId1, TestUserId1, "192.168.1.1", "TestAgent/1.0");
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestSessionId1);
        result.UserId.Should().Be(TestUserId1);
        result.IpAddress.Should().Be("192.168.1.1");
        result.UserAgent.Should().Be("TestAgent/1.0");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingSession_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByTokenHashAsync Tests

    [Fact]
    public async Task GetByTokenHashAsync_ExistingToken_ShouldReturnSession()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1);
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByTokenHashAsync(session.TokenHash, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestSessionId1);
        result.TokenHash.Should().Be(session.TokenHash);
    }

    [Fact]
    public async Task GetByTokenHashAsync_NonExistingToken_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingHash = "nonexisting_hash_12345";

        // Act
        var result = await _repository!.GetByTokenHashAsync(nonExistingHash, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_MultipleSessions_ShouldReturnAllOrderedByCreatedAtDesc()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session1 = CreateTestSession(TestSessionId1, TestUserId1);
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken); // Ensure different CreatedAt
        var session2 = CreateTestSession(TestSessionId2, TestUserId1);
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken);
        var session3 = CreateTestSession(TestSessionId3, TestUserId1);

        await _repository!.AddAsync(session1, TestCancellationToken);
        await _repository.AddAsync(session2, TestCancellationToken);
        await _repository.AddAsync(session3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(s => s.CreatedAt);
    }

    [Fact]
    public async Task GetByUserIdAsync_NoSessions_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetActiveSessionsByUserIdAsync Tests

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_OnlyActiveSessions_ShouldReturnAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session1 = CreateTestSession(TestSessionId1, TestUserId1, lifetime: TimeSpan.FromDays(30));
        var session2 = CreateTestSession(TestSessionId2, TestUserId1, lifetime: TimeSpan.FromDays(30));

        await _repository!.AddAsync(session1, TestCancellationToken);
        await _repository.AddAsync(session2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveSessionsByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.IsValid(_timeProvider!));
    }

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_MixedSessions_ShouldReturnOnlyActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var activeSession = CreateTestSession(TestSessionId1, TestUserId1, lifetime: TimeSpan.FromDays(30));
        var expiredSession = CreateTestSession(TestSessionId2, TestUserId1, lifetime: TimeSpan.FromSeconds(-1));

        await _repository!.AddAsync(activeSession, TestCancellationToken);
        await _repository.AddAsync(expiredSession, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Revoke one session
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var session = await repo.GetByIdAsync(TestSessionId1, TestCancellationToken);
            session!.Revoke();
            await repo.UpdateAsync(session, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Act
        var result = await _repository!.GetActiveSessionsByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().BeEmpty(); // Both sessions are now inactive
    }

    [Fact]
    public async Task GetActiveSessionsByUserIdAsync_NoActiveSessions_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetActiveSessionsByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewSession_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1, "192.168.1.100", "Chrome/120.0");

        // Act
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(TestUserId1);
        persisted.IpAddress.Should().Be("192.168.1.100");
        persisted.UserAgent.Should().Be("Chrome/120.0");
    }

    [Fact]
    public async Task AddAsync_SessionWithLongUserAgent_ShouldTruncateTo256Chars()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var longUserAgent = new string('A', 300);
        var session = CreateTestSession(TestSessionId1, TestUserId1, userAgent: longUserAgent);

        // Act
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.UserAgent.Should().HaveLength(256);
    }

    [Fact]
    public async Task AddAsync_NullSession_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.AddAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ExistingSession_ShouldUpdateProperties()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1);
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedSession = await repo.GetByIdAsync(TestSessionId1, TestCancellationToken);
            loadedSession!.UpdateLastSeen();
            await repo.UpdateAsync(loadedSession, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        updated!.LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_RevokeSession_ShouldPersistRevocation()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1);
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Revoke in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedSession = await repo.GetByIdAsync(TestSessionId1, TestCancellationToken);
            loadedSession!.Revoke();
            await repo.UpdateAsync(loadedSession, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        updated!.RevokedAt.Should().NotBeNull();
        updated.IsRevoked().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NullSession_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.UpdateAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateLastSeenAsync Tests

    [Fact]
    public async Task UpdateLastSeenAsync_ExistingSession_ShouldUpdateTimestamp()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1);
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var newLastSeen = DateTime.UtcNow.AddMinutes(5);

        // Act
        await _repository.UpdateLastSeenAsync(TestSessionId1, newLastSeen, TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        updated!.LastSeenAt.Should().NotBeNull();
        updated.LastSeenAt!.Value.Should().BeCloseTo(newLastSeen, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateLastSeenAsync_NonExistingSession_ShouldNotThrow()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var act = async () => await _repository!.UpdateLastSeenAsync(nonExistingId, DateTime.UtcNow, TestCancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region RevokeAllUserSessionsAsync Tests

    [Fact]
    public async Task RevokeAllUserSessionsAsync_MultipleSessions_ShouldRevokeAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var session1 = CreateTestSession(TestSessionId1, TestUserId1);
        var session2 = CreateTestSession(TestSessionId2, TestUserId1);
        var session3 = CreateTestSession(TestSessionId3, TestUserId2); // Different user

        await _repository!.AddAsync(session1, TestCancellationToken);
        await _repository.AddAsync(session2, TestCancellationToken);
        await _repository.AddAsync(session3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        await _repository.RevokeAllUserSessionsAsync(TestUserId1, TestCancellationToken);

        // Assert
        var user1Sessions = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        user1Sessions.Should().HaveCount(2);
        user1Sessions.Should().OnlyContain(s => s.RevokedAt != null);

        var user2Sessions = await _repository.GetByUserIdAsync(TestUserId2, TestCancellationToken);
        user2Sessions.Should().HaveCount(1);
        user2Sessions[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_NoSessions_ShouldBeIdempotent()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.RevokeAllUserSessionsAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_AlreadyRevokedSessions_ShouldBeIdempotent()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var session = CreateTestSession(TestSessionId1, TestUserId1);
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Revoke once
        await _repository.RevokeAllUserSessionsAsync(TestUserId1, TestCancellationToken);

        // Act - Revoke again
        var act = async () => await _repository.RevokeAllUserSessionsAsync(TestUserId1, TestCancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_SessionLifecycle_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        // 1. Create session
        var session = CreateTestSession(TestSessionId1, TestUserId1, "192.168.1.1", "TestAgent/1.0");
        await _repository!.AddAsync(session, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // 2. Update last seen
        await _repository.UpdateLastSeenAsync(TestSessionId1, DateTime.UtcNow, TestCancellationToken);

        // 3. Verify session is active
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(TestUserId1, TestCancellationToken);
        activeSessions.Should().HaveCount(1);

        // 4. Revoke session
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedSession = await repo.GetByIdAsync(TestSessionId1, TestCancellationToken);
            loadedSession!.Revoke();
            await repo.UpdateAsync(loadedSession, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 5. Verify session is no longer active
        var activeAfterRevoke = await _repository.GetActiveSessionsByUserIdAsync(TestUserId1, TestCancellationToken);
        activeAfterRevoke.Should().BeEmpty();

        // Assert final state
        var final = await _repository.GetByIdAsync(TestSessionId1, TestCancellationToken);
        final.Should().NotBeNull();
        final!.IsRevoked().Should().BeTrue();
        final.LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexScenario_MultipleUsersAndSessions_ShouldIsolateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var user1Session1 = CreateTestSession(TestSessionId1, TestUserId1);
        var user1Session2 = CreateTestSession(TestSessionId2, TestUserId1);
        var user2Session1 = CreateTestSession(TestSessionId3, TestUserId2);

        await _repository!.AddAsync(user1Session1, TestCancellationToken);
        await _repository.AddAsync(user1Session2, TestCancellationToken);
        await _repository.AddAsync(user2Session1, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Revoke all User1 sessions
        await _repository.RevokeAllUserSessionsAsync(TestUserId1, TestCancellationToken);

        // Assert
        var user1Sessions = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        user1Sessions.Should().HaveCount(2);
        user1Sessions.Should().OnlyContain(s => s.RevokedAt != null);

        var user2Sessions = await _repository.GetByUserIdAsync(TestUserId2, TestCancellationToken);
        user2Sessions.Should().HaveCount(1);
        user2Sessions[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ComplexScenario_SessionExpiration_ShouldNotAppearInActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var expiredSession = CreateTestSession(TestSessionId1, TestUserId1, lifetime: TimeSpan.FromSeconds(-1));
        var activeSession = CreateTestSession(TestSessionId2, TestUserId1, lifetime: TimeSpan.FromDays(30));

        await _repository!.AddAsync(expiredSession, TestCancellationToken);
        await _repository.AddAsync(activeSession, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeSessions = await _repository.GetActiveSessionsByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        activeSessions.Should().HaveCount(1);
        activeSessions[0].Id.Should().Be(TestSessionId2);
    }

    #endregion
}
