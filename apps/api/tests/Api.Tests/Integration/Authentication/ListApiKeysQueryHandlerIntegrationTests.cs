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

[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2644")]
public sealed class ListApiKeysQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ListApiKeysQueryHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ListApiKeysQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_listapikeys_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
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

        _handler = new ListApiKeysQueryHandler(_dbContext);
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
    public async Task Handle_ReturnsUserApiKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var key1 = CreateTestApiKey(userId, "Key 1");
        var key2 = CreateTestApiKey(userId, "Key 2");
        _dbContext.ApiKeys.AddRange(key1, key2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new ListApiKeysQuery(userId.ToString(), false, 1, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Keys.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        for (int i = 0; i < 15; i++)
        {
            _dbContext.ApiKeys.Add(CreateTestApiKey(userId, $"Key {i}"));
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new ListApiKeysQuery(userId.ToString(), false, 2, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Keys.Should().HaveCount(5); // Page 2 of 15 items with pageSize=10
        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ExcludesRevokedKeys_ByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var activeKey = CreateTestApiKey(userId, "Active Key");
        var revokedKey = CreateTestApiKey(userId, "Revoked Key");
        revokedKey.RevokedAt = DateTime.UtcNow;
        _dbContext.ApiKeys.AddRange(activeKey, revokedKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new ListApiKeysQuery(userId.ToString(), false, 1, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Keys.Should().HaveCount(1);
        result.Keys.First().KeyName.Should().Be("Active Key");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_IncludesRevokedKeys_WhenRequested()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var activeKey = CreateTestApiKey(userId, "Active Key");
        var revokedKey = CreateTestApiKey(userId, "Revoked Key");
        revokedKey.RevokedAt = DateTime.UtcNow;
        _dbContext.ApiKeys.AddRange(activeKey, revokedKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new ListApiKeysQuery(userId.ToString(), true, 1, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Keys.Should().HaveCount(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidUserId_ReturnsEmptyList()
    {
        // Arrange
        var query = new ListApiKeysQuery("not-a-guid", false, 1, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Keys.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNoKeys_ReturnsEmptyList()
    {
        // Arrange
        var query = new ListApiKeysQuery(Guid.NewGuid().ToString(), false, 1, 10);

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Keys.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
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

    private static ApiKeyEntity CreateTestApiKey(Guid userId, string keyName)
    {
        return new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyName = keyName,
            KeyHash = $"test-hash-{Guid.NewGuid()}", // Unique hash for each key
            KeyPrefix = "mpl_test",
            Scopes = "read,write",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            User = null!
        };
    }
}
