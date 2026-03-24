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
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2644")]
public sealed class GetAllSessionsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private GetAllSessionsQueryHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetAllSessionsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_getallsessions_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

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

        _handler = new GetAllSessionsQueryHandler(_dbContext);
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
    public async Task Handle_ReturnsAllSessions()
    {
        // Arrange
        var user1 = CreateTestUser(Guid.NewGuid());
        var user2 = CreateTestUser(Guid.NewGuid());
        _dbContext!.Users.AddRange(user1, user2);

        var session1 = CreateTestSession(user1.Id);
        var session2 = CreateTestSession(user2.Id);
        _dbContext.UserSessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetAllSessionsQuery(null, 100);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_FiltersByUserId_WhenProvided()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user1 = CreateTestUser(user1Id);
        var user2 = CreateTestUser(user2Id);
        _dbContext!.Users.AddRange(user1, user2);

        var session1 = CreateTestSession(user1Id);
        var session2 = CreateTestSession(user2Id);
        _dbContext.UserSessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetAllSessionsQuery(user1Id, 100);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be(user1Id.ToString());
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_RespectsLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        for (int i = 0; i < 50; i++)
        {
            _dbContext.UserSessions.Add(CreateTestSession(userId));
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetAllSessionsQuery(null, 25);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().HaveCount(25);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidLimit_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetAllSessionsQuery(null, 0);

        // Act & Assert
        var act = async () => await _handler!.Handle(query, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Limit must be between 1 and 1000*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithLimitTooHigh_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetAllSessionsQuery(null, 1001);

        // Act & Assert
        var act = async () => await _handler!.Handle(query, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
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

    private static UserSessionEntity CreateTestSession(Guid userId)
    {
        return new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = $"hash-{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IpAddress = "127.0.0.1",
            UserAgent = "Test Browser",
            User = null!
        };
    }
}
