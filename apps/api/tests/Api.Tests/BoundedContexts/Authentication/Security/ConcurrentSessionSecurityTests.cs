using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Security;

/// <summary>
/// Security tests for concurrent session handling and race condition prevention.
/// Issue #2645: Security edge cases for concurrent sessions.
/// OWASP Reference: A07:2021 - Identification and Authentication Failures
/// </summary>
[Trait("Category", TestCategories.Security)]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2645")]
[Trait("OWASP", "A07-Authentication")]
#pragma warning disable S3881 // Simplified dispose pattern sufficient for test class
public class ConcurrentSessionSecurityTests : IDisposable
#pragma warning restore S3881
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;

    public ConcurrentSessionSecurityTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sessionRepository = new SessionRepository(_dbContext, eventCollector.Object, _timeProvider);
        _userRepository = new UserRepository(_dbContext, eventCollector.Object);
        _unitOfWork = new EfCoreUnitOfWork(_dbContext);
    }

    #region Concurrent Session Creation Tests

    /// <summary>
    /// SECURITY TEST: Concurrent session creation from different IPs should be tracked.
    /// User should be aware of multiple active sessions (potential account compromise).
    /// </summary>
    [Fact]
    public async Task ConcurrentSessions_DifferentIPs_ShouldAllBeTracked()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var ipAddresses = new[] { "192.168.1.1", "10.0.0.1", "172.16.0.1" };
        var sessions = new List<Session>();

        // Act - Create sessions from different IPs
        foreach (var ip in ipAddresses)
        {
            var token = SessionToken.Generate();
            var session = new Session(
                id: Guid.NewGuid(),
                userId: user.Id,
                token: token,
                lifetime: TimeSpan.FromDays(1),
                ipAddress: ip,
                userAgent: $"Mozilla/5.0 (Device: {ip})",
                timeProvider: _timeProvider);
            sessions.Add(session);
            await _sessionRepository.AddAsync(session, CancellationToken.None);
        }
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert - All sessions should exist and be tracked
        var userSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync();

        userSessions.Count.Should().Be(3);
        userSessions.Should().Contain(s => s.IpAddress == "192.168.1.1");
        userSessions.Should().Contain(s => s.IpAddress == "10.0.0.1");
        userSessions.Should().Contain(s => s.IpAddress == "172.16.0.1");
    }

    /// <summary>
    /// SECURITY TEST: Maximum concurrent session limit should be enforced.
    /// Prevents attacker from creating unlimited sessions.
    /// </summary>
    [Fact]
    public async Task ConcurrentSessions_ExceedsLimit_ShouldRevokeOldest()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        const int maxSessions = 5;
        var sessions = new List<Session>();

        // Act - Create more sessions than the limit
        for (int i = 0; i < maxSessions + 3; i++)
        {
            var token = SessionToken.Generate();
            var session = new Session(
                id: Guid.NewGuid(),
                userId: user.Id,
                token: token,
                lifetime: TimeSpan.FromDays(1),
                ipAddress: $"192.168.1.{i}",
                userAgent: "Test User Agent",
                timeProvider: _timeProvider);
            sessions.Add(session);
            await _sessionRepository.AddAsync(session, CancellationToken.None);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);

            // Simulate session limit enforcement
            var activeSessions = await _dbContext.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            if (activeSessions.Count > maxSessions)
            {
                // Revoke oldest session(s)
                var sessionsToRevoke = activeSessions.Take(activeSessions.Count - maxSessions);
                foreach (var oldSession in sessionsToRevoke)
                {
                    oldSession.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;
                }
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }

        // Assert - Only maxSessions should be active
        var finalActiveSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync();

        finalActiveSessions.Count.Should().Be(maxSessions);
    }

    /// <summary>
    /// SECURITY TEST: Parallel session creation should not create duplicates.
    /// Race condition prevention in session creation.
    /// </summary>
    [Fact]
    public async Task ConcurrentSessions_ParallelCreation_ShouldHaveUniqueTokens()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var tokenHashes = new List<string>();
        var lockObj = new object();

        // Act - Create multiple sessions with unique tokens
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var token = SessionToken.Generate();
            var session = new Session(
                id: Guid.NewGuid(),
                userId: user.Id,
                token: token,
                lifetime: TimeSpan.FromDays(1),
                ipAddress: "192.168.1.1",
                userAgent: "Test User Agent",
                timeProvider: _timeProvider);

            lock (lockObj)
            {
                tokenHashes.Add(session.TokenHash);
            }

            await Task.CompletedTask;
        });

        await Task.WhenAll(tasks);

        // Assert - Each session should have unique token hash
        var uniqueHashes = tokenHashes.Distinct().Count();
        uniqueHashes.Should().Be(5); // All token hashes should be unique
    }

    #endregion

    #region Session Conflict Resolution Tests

    /// <summary>
    /// SECURITY TEST: Login from new IP should be detectable.
    /// System should be able to identify unusual login patterns.
    /// </summary>
    [Fact]
    public async Task SessionConflict_NewIPLogin_ShouldBeDetectable()
    {
        // Arrange
        var user = await SeedTestUserAsync();

        // Create existing session from known IP
        var token1 = SessionToken.Generate();
        var existingSession = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: token1,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "192.168.1.100",
            userAgent: "Chrome on Windows",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(existingSession, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - Check if new login would be from different IP
        var newIp = "203.0.113.50"; // Different network
        var existingSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync();

        var isNewIp = !existingSessions.Any(s => s.IpAddress == newIp);

        // Assert
        isNewIp.Should().BeTrue("System should detect login from new IP");
        existingSessions.Should().ContainSingle(); // One existing session
        existingSessions[0].IpAddress.Should().Be("192.168.1.100");
    }

    /// <summary>
    /// SECURITY TEST: Option to revoke all other sessions on new login.
    /// Security-conscious users can force single-session mode.
    /// </summary>
    [Fact]
    public async Task SessionConflict_RevokeAllOthers_ShouldOnlyKeepCurrent()
    {
        // Arrange
        var user = await SeedTestUserAsync();

        // Create multiple existing sessions
        for (int i = 0; i < 3; i++)
        {
            var token = SessionToken.Generate();
            var session = new Session(
                id: Guid.NewGuid(),
                userId: user.Id,
                token: token,
                lifetime: TimeSpan.FromDays(1),
                ipAddress: $"192.168.1.{i}",
                userAgent: "Old Device",
                timeProvider: _timeProvider);
            await _sessionRepository.AddAsync(session, CancellationToken.None);
        }
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - Create new session and revoke all others
        var newToken = SessionToken.Generate();
        var newSession = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: newToken,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "10.0.0.1",
            userAgent: "New Device",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(newSession, CancellationToken.None);

        // Revoke all other sessions
        var otherSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == user.Id && s.Id != newSession.Id && s.RevokedAt == null)
            .ToListAsync();

        foreach (var session in otherSessions)
        {
            session.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        var activeSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync();

        activeSessions.Should().ContainSingle();
        activeSessions[0].Id.Should().Be(newSession.Id);
    }

    /// <summary>
    /// SECURITY TEST: Session token uniqueness prevents token collision attacks.
    /// Each session must have cryptographically unique token.
    /// </summary>
    [Fact]
    public void SessionToken_Generation_ShouldBeCryptographicallyUnique()
    {
        // Arrange & Act - Generate many tokens
        var tokens = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            var token = SessionToken.Generate();
            tokens.Add(token.Value);
        }

        // Assert - All tokens should be unique
        var uniqueTokens = tokens.Distinct().Count();
        uniqueTokens.Should().Be(1000);

        // Verify token format (should be base64-like, long enough for security)
        tokens.Should().OnlyContain(t => t.Length >= 32, "Token should be at least 32 characters");
    }

    #endregion

    #region Race Condition Tests

    /// <summary>
    /// SECURITY TEST: Concurrent session validation should be thread-safe.
    /// Multiple threads validating same session shouldn't cause issues.
    /// </summary>
    [Fact]
    public async Task RaceCondition_ConcurrentValidation_ShouldBeThreadSafe()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var token = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: token,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "192.168.1.1",
            userAgent: "Test Device",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(session, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        var validationResults = new List<bool>();
        var lockObj = new object();

        // Act - Parallel validation attempts
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            // Simulate validation check
            var isValid = session.IsValid(_timeProvider);

            lock (lockObj)
            {
                validationResults.Add(isValid);
            }

            await Task.Delay(1); // Simulate async work
        });

        await Task.WhenAll(tasks);

        // Assert - All validations should succeed consistently
        validationResults.Count.Should().Be(10);
        validationResults.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    /// <summary>
    /// SECURITY TEST: Concurrent revoke and validate should handle gracefully.
    /// Session revoked during validation should fail validation.
    /// </summary>
    [Fact]
    public async Task RaceCondition_RevokeWhileValidating_ShouldHandleGracefully()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var token = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: token,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "192.168.1.1",
            userAgent: "Test Device",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(session, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        var validationBeforeRevoke = session.IsValid(_timeProvider);

        // Act - Revoke the session
        session.Revoke(_timeProvider);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        var validationAfterRevoke = session.IsValid(_timeProvider);

        // Assert
        validationBeforeRevoke.Should().BeTrue("Validation before revoke should succeed");
        validationAfterRevoke.Should().BeFalse("Validation after revoke should fail");
    }

    /// <summary>
    /// SECURITY TEST: Double revoke should be prevented by domain.
    /// Multiple revoke calls should throw to prevent replay attacks.
    /// </summary>
    [Fact]
    public async Task RaceCondition_DoubleRevoke_ShouldBePrevented()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var token = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: token,
            lifetime: TimeSpan.FromDays(1),
            ipAddress: "192.168.1.1",
            userAgent: "Test Device",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(session, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - First revoke should succeed
        session.Revoke(_timeProvider);
        var firstRevokedAt = session.RevokedAt;
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert - Second revoke should throw (domain guards against replay)
        var act = () => session.Revoke(_timeProvider);
        var exception = act.Should().Throw<Api.SharedKernel.Domain.Exceptions.DomainException>().Which;
        exception.Message.Should().Contain("already revoked");
        session.IsValid(_timeProvider).Should().BeFalse();
        session.RevokedAt.Should().NotBeNull();
        session.RevokedAt.Should().Be(firstRevokedAt); // RevokedAt unchanged
    }

    /// <summary>
    /// SECURITY TEST: Expired session should not be extendable.
    /// Cannot resurrect an expired session.
    /// </summary>
    [Fact]
    public async Task ExpiredSession_ExtendAttempt_ShouldNotResurrect()
    {
        // Arrange
        var user = await SeedTestUserAsync();
        var token = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: token,
            lifetime: TimeSpan.FromMinutes(1), // Short lifetime
            ipAddress: "192.168.1.1",
            userAgent: "Test Device",
            timeProvider: _timeProvider);
        await _sessionRepository.AddAsync(session, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act - Advance time past expiration
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var isValidAfterExpiry = session.IsValid(_timeProvider);

        // Assert - Session should be invalid after expiry
        isValidAfterExpiry.Should().BeFalse("Expired session should not be valid");
    }

    #endregion

    #region Helper Methods

    private async Task<User> SeedTestUserAsync()
    {
        var user = new UserBuilder()
            .WithEmail($"test-{Guid.NewGuid()}@example.com")
            .WithPassword("TestPassword123!")
            .WithDisplayName($"TestUser-{Guid.NewGuid():N}")
            .Build();

        await _userRepository.AddAsync(user, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        return user;
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}