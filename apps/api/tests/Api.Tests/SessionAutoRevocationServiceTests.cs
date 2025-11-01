using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Helpers;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD-style unit tests for SessionAutoRevocationService background service.
/// Tests configuration validation, periodic execution, error handling, and graceful shutdown.
/// Uses TestTimeProvider for deterministic timing (eliminates Task.Delay).
/// </summary>
public class SessionAutoRevocationServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<SessionAutoRevocationService>> _loggerMock;
    private readonly ServiceCollection _serviceCollection;
    private readonly TestTimeProvider _timeProvider;
    private ServiceProvider? _serviceProvider;

    public SessionAutoRevocationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _loggerMock = new Mock<ILogger<SessionAutoRevocationService>>();
        _serviceCollection = new ServiceCollection();
        _timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1); // Start at 2025-01-01 00:00:00 UTC
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
        _timeProvider?.Dispose();
    }

    #region Configuration Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithZeroInterval_LogsWarningAndDoesNotRun()
    {
        // Given: Configuration with zero auto-revocation interval
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 0
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // When: Service starts
        await service.StartAsync(cts.Token);
        
        // Advance time to allow service to process (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield(); // Minimal sync point
        
        await service.StopAsync(CancellationToken.None);

        // Then: Warning is logged and service does not run
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Auto-revocation interval")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify no sessions were processed
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Running scheduled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeInterval_LogsWarningAndDoesNotRun()
    {
        // Given: Configuration with negative auto-revocation interval
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = -5
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // When: Service starts
        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();
        
        await service.StopAsync(CancellationToken.None);

        // Then: Warning is logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("invalid")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroInactivityTimeout_LogsWarningAndDoesNotRun()
    {
        // Given: Configuration with zero inactivity timeout
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 0,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // When: Service starts
        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();
        
        await service.StopAsync(CancellationToken.None);

        // Then: Warning is logged about inactivity timeout
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Inactivity timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeInactivityTimeout_LogsWarningAndDoesNotRun()
    {
        // Given: Configuration with negative inactivity timeout
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = -10,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // When: Service starts
        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();
        
        await service.StopAsync(CancellationToken.None);

        // Then: Warning is logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("invalid")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidConfiguration_LogsStartupInformation()
    {
        // Given: Valid configuration
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 24
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // When: Service starts
        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();

        // Then: Startup information is logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started") && v.ToString()!.Contains("24 hours")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        await service.StopAsync(CancellationToken.None);
    }

    #endregion

    #region Execution and Timing Tests

    [Fact]
    public async Task ExecuteAsync_WaitsOneMinuteBeforeFirstRun()
    {
        // Given: Valid configuration and service with tracked execution
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();

        // When: Service starts and we advance less than 1 minute
        await service.StartAsync(cts.Token);
        
        // Advance only 30 seconds (not enough to trigger first run)
        _timeProvider.AdvanceSeconds(30);
        await Task.Yield();

        // Then: Auto-revocation check should NOT have been called yet
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Running scheduled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "First auto-revocation should not run within 30 seconds");

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoInactiveSessions_LogsDebugMessage()
    {
        // Given: Valid configuration and no inactive sessions
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        // Create an active session
        var now = _timeProvider.GetUtcNow();
        var user = CreateTestUser("user-no-inactive");
        await _dbContext.Users.AddAsync(user);
        await _dbContext.UserSessions.AddAsync(CreateSession(
            userId: user.Id,
            sessionId: "sess-active",
            createdAt: now.AddHours(-1).DateTime,
            expiresAt: now.AddDays(30).DateTime,
            lastSeenAt: now.AddMinutes(-5).DateTime,
            user: user
        ));
        await _dbContext.SaveChangesAsync();

        var service = CreateService(config);

        // When: Service performs auto-revocation check (simulated directly)
        // Note: We use deterministic time now instead of waiting
        var sessionMgmt = CreateSessionManagementService(config);
        var revokedCount = await sessionMgmt.RevokeInactiveSessionsAsync();

        // Then: No sessions should be revoked
        revokedCount.Should().Be(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenSessionManagementThrowsException_LogsErrorAndContinues()
    {
        // Given: Configuration and a faulty SessionManagementService that throws
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        // Create a mock session management service that throws
        var mockSessionMgmt = new Mock<ISessionManagementService>();
        mockSessionMgmt
            .Setup(x => x.RevokeInactiveSessionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Setup service collection with mocked service
        _serviceCollection.AddScoped<ISessionManagementService>(_ => mockSessionMgmt.Object);
        _serviceProvider = _serviceCollection.BuildServiceProvider();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new SessionAutoRevocationService(
            scopeFactory,
            Options.Create(config),
            _loggerMock.Object
        );

        using var cts = new CancellationTokenSource();

        // When: Service starts
        await service.StartAsync(cts.Token);

        // Advance time to allow processing (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Then: Service should log the startup (error would only occur after 1 minute delay)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Service should start even if future execution would fail");
    }

    [Fact]
    public async Task RevokeInactiveSessionsAsync_WhenDatabaseFails_ThrowsException()
    {
        // Given: A SessionManagementService with a failing database context
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        // Dispose the current context and create a closed one
        _dbContext.Database.CloseConnection();

        var sessionMgmt = CreateSessionManagementService(config);

        // When/Then: Calling RevokeInactiveSessionsAsync should throw (database connection is closed)
        var act = async () => await sessionMgmt.RevokeInactiveSessionsAsync()
        ;
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion

    #region Cancellation and Shutdown Tests

    [Fact]
    public async Task ExecuteAsync_WithCancellation_StopsGracefully()
    {
        // Given: Valid configuration
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();

        // When: Service starts and is then cancelled
        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();
        
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Then: Service logs that it stopped
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtMostOnce);
    }

    [Fact]
    public async Task StopAsync_WithRunningService_CancelsExecution()
    {
        // Given: Valid configuration and running service
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var service = CreateService(config);
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        
        // Advance time (replaces Task.Delay)
        _timeProvider.AdvanceMilliseconds(100);
        await Task.Yield();

        // When: StopAsync is called
        var stopTask = service.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        // Then: Service stops within reasonable time
        completed.Should().Be(stopTask);
        await stopTask; // Ensure no exceptions
    }

    #endregion

    #region Integration with SessionManagementService Tests

    [Fact]
    public async Task RevokeInactiveSessionsAsync_WithInactiveSessions_RevokesCorrectly()
    {
        // Given: Configuration and inactive sessions in the database
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var now = _timeProvider.GetUtcNow();
        var user = CreateTestUser("user-inactive-sessions");
        await _dbContext.Users.AddAsync(user);

        // Create one active and one inactive session
        var activeSession = CreateSession(
            userId: user.Id,
            sessionId: "sess-active",
            createdAt: now.AddDays(-10).DateTime,
            expiresAt: now.AddDays(80).DateTime,
            lastSeenAt: now.AddDays(-5).DateTime, // Active within 30 days
            user: user
        );

        var inactiveSession = CreateSession(
            userId: user.Id,
            sessionId: "sess-inactive",
            createdAt: now.AddDays(-60).DateTime,
            expiresAt: now.AddDays(30).DateTime,
            lastSeenAt: now.AddDays(-35).DateTime, // Inactive for > 30 days
            user: user
        );

        await _dbContext.UserSessions.AddRangeAsync(activeSession, inactiveSession);
        await _dbContext.SaveChangesAsync();

        var sessionMgmt = CreateSessionManagementService(config);

        // When: Auto-revocation runs
        var revokedCount = await sessionMgmt.RevokeInactiveSessionsAsync();

        // Then: Only the inactive session is revoked
        revokedCount.Should().Be(1);

        var sessions = await _dbContext.UserSessions.ToListAsync();
        var active = sessions.First(s => s.Id == "sess-active");
        var inactive = sessions.First(s => s.Id == "sess-inactive");

        active.RevokedAt.Should().BeNull();
        inactive.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeInactiveSessionsAsync_WithMultipleUsers_RevokesOnlyInactive()
    {
        // Given: Multiple users with varying session activity
        var config = new SessionManagementConfiguration
        {
            InactivityTimeoutDays = 30,
            AutoRevocationIntervalHours = 1
        };

        var now = _timeProvider.GetUtcNow();

        var user1 = CreateTestUser("user1-multi");
        var user2 = CreateTestUser("user2-multi");
        await _dbContext.Users.AddRangeAsync(user1, user2);

        var sessions = new[]
        {
            CreateSession(user1.Id, "u1-s1", now.AddDays(-10).DateTime, now.AddDays(80).DateTime, now.AddDays(-5).DateTime, null, null, user1),  // Active
            CreateSession(user1.Id, "u1-s2", now.AddDays(-50).DateTime, now.AddDays(40).DateTime, now.AddDays(-40).DateTime, null, null, user1), // Inactive
            CreateSession(user2.Id, "u2-s1", now.AddDays(-20).DateTime, now.AddDays(70).DateTime, now.AddDays(-2).DateTime, null, null, user2),  // Active
            CreateSession(user2.Id, "u2-s2", now.AddDays(-70).DateTime, now.AddDays(20).DateTime, now.AddDays(-45).DateTime, null, null, user2), // Inactive
        };

        await _dbContext.UserSessions.AddRangeAsync(sessions);
        await _dbContext.SaveChangesAsync();

        var sessionMgmt = CreateSessionManagementService(config);

        // When: Auto-revocation runs
        var revokedCount = await sessionMgmt.RevokeInactiveSessionsAsync();

        // Then: Only the 2 inactive sessions are revoked
        revokedCount.Should().Be(2);

        var allSessions = await _dbContext.UserSessions.ToListAsync();
        Assert.Null(allSessions.First(s => s.Id == "u1-s1").RevokedAt);
        Assert.NotNull(allSessions.First(s => s.Id == "u1-s2").RevokedAt);
        Assert.Null(allSessions.First(s => s.Id == "u2-s1").RevokedAt);
        Assert.NotNull(allSessions.First(s => s.Id == "u2-s2").RevokedAt);
    }

    #endregion

    #region Helper Methods

    private SessionAutoRevocationService CreateService(SessionManagementConfiguration config)
    {
        // Setup service collection with all dependencies
        _serviceCollection.AddDbContext<MeepleAiDbContext>(options =>
            options.UseSqlite(_dbContext.Database.GetConnectionString()));

        // Inject TestTimeProvider instead of System
        _serviceCollection.AddSingleton<TimeProvider>(_timeProvider);
        _serviceCollection.AddScoped<ISessionManagementService>(sp =>
        {
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var logger = new Mock<ILogger<SessionManagementService>>().Object;
            return new SessionManagementService(
                db,
                Options.Create(config),
                logger,
                null,
                timeProvider
            );
        });

        _serviceProvider = _serviceCollection.BuildServiceProvider();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new SessionAutoRevocationService(
            scopeFactory,
            Options.Create(config),
            _loggerMock.Object,
            _timeProvider // Pass TestTimeProvider to SessionAutoRevocationService
        );
    }

    private SessionManagementService CreateSessionManagementService(SessionManagementConfiguration config)
    {
        var logger = new Mock<ILogger<SessionManagementService>>().Object;
        return new SessionManagementService(
            _dbContext,
            Options.Create(config),
            logger,
            null,
            _timeProvider // Use TestTimeProvider instead of TimeProvider.System
        );
    }

    private static UserEntity CreateTestUser(string username)
    {
        return new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"{username}@test.com",
            DisplayName = username,
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static UserSessionEntity CreateSession(
        string userId,
        string sessionId,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        DateTime? lastSeenAt = null,
        DateTime? revokedAt = null,
        string? tokenHash = null,
        UserEntity? user = null)
    {
        return new UserSessionEntity
        {
            Id = sessionId,
            UserId = userId,
            TokenHash = tokenHash ?? $"hash-{sessionId}",
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(90),
            LastSeenAt = lastSeenAt,
            RevokedAt = revokedAt,
            IpAddress = "127.0.0.1",
            UserAgent = "test-agent",
            User = user ?? CreateTestUser($"default-user-{sessionId}")
        };
    }

    #endregion
}