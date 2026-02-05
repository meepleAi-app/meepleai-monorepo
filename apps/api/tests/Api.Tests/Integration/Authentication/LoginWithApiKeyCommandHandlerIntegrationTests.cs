using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using System.Security.Cryptography;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for LoginWithApiKeyCommandHandler (Issue #2643).
/// Tests API key authentication with real database and services.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2643")]
public sealed class LoginWithApiKeyCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private LoginWithApiKeyCommandHandler? _handler;
    private IPasswordHashingService? _passwordHashingService;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public LoginWithApiKeyCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_loginapikey_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository, Api.BoundedContexts.Authentication.Infrastructure.Persistence.UserRepository>();
        services.AddScoped<ApiKeyAuthenticationService>();

        var mockEmailService = new Mock<IEmailService>();
        services.AddSingleton(mockEmailService.Object);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _passwordHashingService = serviceProvider.GetRequiredService<IPasswordHashingService>();

        var userRepository = serviceProvider.GetRequiredService<Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository>();
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

        _handler = new LoginWithApiKeyCommandHandler(authService, userRepository);
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

    /// <summary>
    /// Clears database state to ensure test isolation.
    /// </summary>
    private async Task CleanDatabaseStateAsync()
    {
        _dbContext!.ApiKeys.RemoveRange(_dbContext.ApiKeys);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidApiKey_ReturnsUserProfile()
    {
        // FIX: Clear database state to prevent interference from previous tests
        await CleanDatabaseStateAsync();

        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "test@example.com");
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var (apiKey, plaintextKey) = CreateTestApiKey(userId);
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new LoginWithApiKeyCommand(plaintextKey);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Id.Should().Be(userId);
        result.User.Email.Should().Be("test@example.com");
        result.ApiKeyId.Should().Be(apiKey.Id.ToString());
        result.Message.Should().Contain("authenticated successfully");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithInvalidApiKey_ThrowsDomainException()
    {
        // FIX: Clear database state to prevent interference from previous tests
        await CleanDatabaseStateAsync();

        // Arrange
        var command = new LoginWithApiKeyCommand("mpl_invalid_key_12345678901234567890");

        // Act & Assert
        var act = async () => await _handler!.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Invalid*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithExpiredApiKey_ThrowsDomainException()
    {
        // FIX: Clear database state to prevent interference from previous tests
        await CleanDatabaseStateAsync();

        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "expired@test.com");
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var (apiKey, plaintextKey) = CreateTestApiKey(userId, expiresAt: DateTime.UtcNow.AddDays(-1));
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new LoginWithApiKeyCommand(plaintextKey);

        // Act & Assert
        var act = async () => await _handler!.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Invalid*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithRevokedApiKey_ThrowsDomainException()
    {
        // FIX: Clear database state to prevent interference from previous tests
        await CleanDatabaseStateAsync();

        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "revoked@test.com");
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var (apiKey, plaintextKey) = CreateTestApiKey(userId);
        apiKey.IsActive = false;
        apiKey.RevokedAt = DateTime.UtcNow;
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new LoginWithApiKeyCommand(plaintextKey);

        // Act & Assert
        var act = async () => await _handler!.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert (no database cleanup needed for null check)
        var act = async () => await _handler!.Handle(null!, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static UserEntity CreateTestUser(Guid userId, string email)
    {
        return new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "hashed_password",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
    }

    private (ApiKeyEntity apiKey, string plaintextKey) CreateTestApiKey(Guid userId, DateTime? expiresAt = null)
    {
        // Generate real key
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var plaintextKey = Convert.ToBase64String(keyBytes);
        var keyPrefix = plaintextKey[..8];

        // Hash with real service
        var keyHash = _passwordHashingService!.HashSecret(plaintextKey);

        var apiKey = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyName = "Test API Key",
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scopes = "read,write",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            User = null!
        };

        return (apiKey, plaintextKey);
    }
}