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

/// <summary>
/// Integration tests for DeleteApiKeyCommandHandler (Issue #2643).
/// Tests API key deletion with real PostgreSQL database.
/// Uses SharedTestcontainersFixture for Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2643")]
public sealed class DeleteApiKeyCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private DeleteApiKeyCommandHandler? _handler;
    private Mock<ILogger<DeleteApiKeyCommandHandler>>? _mockLogger;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DeleteApiKeyCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_deleteapikey_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Required services for MeepleAiDbContext
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Mock IEmailService (required by event handlers)
        var mockEmailService = new Mock<IEmailService>();
        services.AddSingleton(mockEmailService.Object);

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create database schema with retry logic
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

        _mockLogger = new Mock<ILogger<DeleteApiKeyCommandHandler>>();
        _handler = new DeleteApiKeyCommandHandler(_dbContext, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

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

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidKeyId_DeletesApiKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var apiKey = CreateTestApiKey(userId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new DeleteApiKeyCommand(apiKey.Id.ToString(), userId.ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeTrue();
        var deletedKey = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id, TestCancellationToken);
        deletedKey.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentKeyId_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new DeleteApiKeyCommand(nonExistentId.ToString(), Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidGuidFormat_ReturnsFalse()
    {
        // Arrange
        var command = new DeleteApiKeyCommand("not-a-valid-guid", Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_LogsWarning_WhenKeyNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new DeleteApiKeyCommand(nonExistentId.ToString(), Guid.NewGuid().ToString());

        // Act
        await _handler!.Handle(command, TestCancellationToken);

        // Assert
        _mockLogger!.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("API key deletion failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_LogsInformation_WhenKeyDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var apiKey = CreateTestApiKey(userId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new DeleteApiKeyCommand(apiKey.Id.ToString(), userId.ToString());

        // Act
        await _handler!.Handle(command, TestCancellationToken);

        // Assert
        _mockLogger!.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("API key deleted")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyKeyId_ReturnsFalse()
    {
        // Arrange
        var command = new DeleteApiKeyCommand(string.Empty, Guid.NewGuid().ToString());

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_PermanentlyRemoves_NoSoftDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var apiKey = CreateTestApiKey(userId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new DeleteApiKeyCommand(apiKey.Id.ToString(), userId.ToString());

        // Act
        await _handler!.Handle(command, TestCancellationToken);

        // Assert - Verify hard delete, not soft delete
        var allKeys = await _dbContext.ApiKeys.IgnoreQueryFilters().ToListAsync(TestCancellationToken);
        allKeys.Should().NotContain(k => k.Id == apiKey.Id);
    }

    private static ApiKeyEntity CreateTestApiKey(Guid userId)
    {
        return new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyName = "Test API Key",
            KeyHash = "test-hash",
            KeyPrefix = "mpl_test",
            Scopes = "read,write",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            User = null! // Navigation property not needed for deletion tests
        };
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
}
