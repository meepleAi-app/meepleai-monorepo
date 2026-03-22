using Api.BoundedContexts.Authentication.Application.Commands;
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
[Trait("Issue", "2643")]
public sealed class RevokeApiKeyManagementCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private RevokeApiKeyManagementCommandHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RevokeApiKeyManagementCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_revokeapikeymgmt_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<ApiKeyAuthenticationService>();

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        var authService = serviceProvider.GetRequiredService<ApiKeyAuthenticationService>();

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

        var logger = new Mock<ILogger<RevokeApiKeyManagementCommandHandler>>();
        _handler = new RevokeApiKeyManagementCommandHandler(_dbContext, authService, logger.Object);
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
    public async Task Handle_WithValidKey_RevokesApiKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new RevokeApiKeyManagementCommand(apiKey.Id.ToString(), userId.ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeTrue();

        var revokedKey = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id, TestCancellationToken);
        revokedKey.Should().NotBeNull();
        revokedKey!.RevokedAt.Should().NotBeNull();
        revokedKey.RevokedBy.Should().Be(userId);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var command = new RevokeApiKeyManagementCommand(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithWrongUserId_ReturnsFalse()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var owner = CreateTestUser(ownerId);
        _dbContext!.Users.Add(owner);

        var apiKey = CreateTestApiKey(ownerId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new RevokeApiKeyManagementCommand(apiKey.Id.ToString(), otherUserId.ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidGuid_ReturnsFalse()
    {
        // Arrange
        var command = new RevokeApiKeyManagementCommand("not-a-guid", Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
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

    private static ApiKeyEntity CreateTestApiKey(Guid userId)
    {
        return new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyName = "Test Key",
            KeyHash = "test-hash",
            KeyPrefix = "mpl_test",
            Scopes = "read",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            User = null!
        };
    }
}
