using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2644")]
public sealed class GetUserSessionsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private GetUserSessionsQueryHandler? _handler;
    private readonly FakeTimeProvider _timeProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetUserSessionsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 19, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_getusersessions_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        var mockEmailService = new Mock<IEmailService>();
        services.AddSingleton(mockEmailService.Object);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

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

        _handler = new GetUserSessionsQueryHandler(_dbContext, _timeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { }
        }
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsActiveSessionsForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var session1 = CreateTestSession(userId, "Session 1");
        var session2 = CreateTestSession(userId, "Session 2");
        _dbContext.UserSessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserSessionsQuery(userId);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ExcludesExpiredSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var activeSession = CreateTestSession(userId, "Active", _timeProvider.GetUtcNow().AddHours(1).UtcDateTime);
        var expiredSession = CreateTestSession(userId, "Expired", _timeProvider.GetUtcNow().AddHours(-1).UtcDateTime);
        _dbContext.UserSessions.AddRange(activeSession, expiredSession);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserSessionsQuery(userId);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result.First().CreatedAt.Should().Be(activeSession.CreatedAt);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ExcludesRevokedSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var activeSession = CreateTestSession(userId, "Active");
        var revokedSession = CreateTestSession(userId, "Revoked");
        revokedSession.RevokedAt = DateTime.UtcNow;
        _dbContext.UserSessions.AddRange(activeSession, revokedSession);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserSessionsQuery(userId);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNoSessions_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetUserSessionsQuery(Guid.NewGuid());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_OrdersByLastSeenDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var session1 = CreateTestSession(userId, "Session 1");
        session1.LastSeenAt = _timeProvider.GetUtcNow().AddHours(-2).UtcDateTime;

        var session2 = CreateTestSession(userId, "Session 2");
        session2.LastSeenAt = _timeProvider.GetUtcNow().AddHours(-1).UtcDateTime;

        _dbContext.UserSessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserSessionsQuery(userId);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.First().LastSeenAt.Should().Be(session2.LastSeenAt);
    }

    private static UserEntity CreateTestUser(Guid userId)
    {
        return new UserEntity
        {
            Id = userId,
            Email = $"test-{userId}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed_password",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
    }

    private UserSessionEntity CreateTestSession(Guid userId, string userAgent, DateTime? expiresAt = null)
    {
        return new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = $"hash-{Guid.NewGuid()}",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAt = expiresAt ?? _timeProvider.GetUtcNow().AddDays(30).UtcDateTime,
            IpAddress = "127.0.0.1",
            UserAgent = userAgent,
            User = null!
        };
    }
}
