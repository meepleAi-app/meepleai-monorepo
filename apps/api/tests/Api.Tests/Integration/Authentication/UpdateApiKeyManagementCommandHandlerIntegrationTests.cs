using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
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
[Trait("Issue", "2643")]
public sealed class UpdateApiKeyManagementCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private UpdateApiKeyManagementCommandHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpdateApiKeyManagementCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_updateapikeymgmt_{Guid.NewGuid():N}";
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
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        var logger = new Mock<ILogger<UpdateApiKeyManagementCommandHandler>>();
        _handler = new UpdateApiKeyManagementCommandHandler(_dbContext, logger.Object);
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
    public async Task Handle_UpdatesKeyName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId, "Old Name");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new UpdateApiKeyRequest { KeyName = "New Name" };
        var command = new UpdateApiKeyManagementCommand(apiKey.Id.ToString(), userId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.KeyName.Should().Be("New Name");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_UpdatesScopes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId, "Test Key");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new UpdateApiKeyRequest { Scopes = new[] { "read", "write", "admin" } };
        var command = new UpdateApiKeyManagementCommand(apiKey.Id.ToString(), userId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Scopes.Should().Contain("admin");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_UpdatesIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);

        var apiKey = CreateTestApiKey(userId, "Test Key");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new UpdateApiKeyRequest { IsActive = false };
        var command = new UpdateApiKeyManagementCommand(apiKey.Id.ToString(), userId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var request = new UpdateApiKeyRequest { KeyName = "New Name" };
        var command = new UpdateApiKeyManagementCommand(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

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

        var apiKey = CreateTestApiKey(ownerId, "Test Key");
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new UpdateApiKeyRequest { KeyName = "Hacked Name" };
        var command = new UpdateApiKeyManagementCommand(apiKey.Id.ToString(), otherUserId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeNull();
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
            Scopes = "read",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            User = null!
        };
    }
}
