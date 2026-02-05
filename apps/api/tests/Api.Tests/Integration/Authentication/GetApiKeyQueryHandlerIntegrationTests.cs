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
public sealed class GetApiKeyQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private GetApiKeyQueryHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetApiKeyQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_getapikey_{Guid.NewGuid():N}";
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

        _handler = new GetApiKeyQueryHandler(_dbContext);
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
    public async Task Handle_WithValidKeyAndUser_ReturnsApiKeyDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId, "Test Key");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetApiKeyQuery(apiKey.Id.ToString(), userId.ToString());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(apiKey.Id.ToString());
        result.KeyName.Should().Be("Test Key");
        result.KeyPrefix.Should().Be("mpl_test");
        result.Scopes.Should().Contain("read");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var query = new GetApiKeyQuery(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithWrongUserId_ReturnsNull()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var owner = CreateTestUser(ownerId);
        _dbContext!.Users.Add(owner);

        var apiKey = CreateTestApiKey(ownerId, "Owner's Key");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetApiKeyQuery(apiKey.Id.ToString(), otherUserId.ToString());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidKeyIdFormat_ReturnsNull()
    {
        // Arrange
        var query = new GetApiKeyQuery("not-a-guid", Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidUserIdFormat_ReturnsNull()
    {
        // Arrange
        var query = new GetApiKeyQuery(Guid.NewGuid().ToString(), "not-a-guid");

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _handler!.Handle(null!, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_MapsQuotaMetadata_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId, "Quota Key");
        apiKey.Metadata = "{\"maxRequestsPerDay\":1000,\"maxRequestsPerHour\":100}";
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetApiKeyQuery(apiKey.Id.ToString(), userId.ToString());

        // Act
        var result = await _handler!.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Quota.Should().NotBeNull();
        result.Quota!.MaxRequestsPerDay.Should().Be(1000);
        result.Quota.MaxRequestsPerHour.Should().Be(100);
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
            KeyHash = "test-hash",
            KeyPrefix = "mpl_test",
            Scopes = "read,write",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            User = null!
        };
    }
}
