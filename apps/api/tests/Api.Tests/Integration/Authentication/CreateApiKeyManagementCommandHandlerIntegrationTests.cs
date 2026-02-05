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
public sealed class CreateApiKeyManagementCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private CreateApiKeyManagementCommandHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CreateApiKeyManagementCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_createapikeymgmt_{Guid.NewGuid():N}";
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
        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<ApiKeyAuthenticationService>();

        var mockEmailService = new Mock<IEmailService>();
        services.AddSingleton(mockEmailService.Object);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

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

        var logger = new Mock<ILogger<CreateApiKeyManagementCommandHandler>>();
        _handler = new CreateApiKeyManagementCommandHandler(_dbContext, authService, logger.Object);
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
    public async Task Handle_WithValidRequest_CreatesApiKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new CreateApiKeyRequest
        {
            KeyName = "Production Key",
            Scopes = new[] { "read", "write" },
            Environment = "live",
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            MaxRequestsPerDay = 1000
        };
        var command = new CreateApiKeyManagementCommand(userId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ApiKey.Should().NotBeNull();
        result.PlaintextKey.Should().NotBeNullOrEmpty();
        result.ApiKey.KeyName.Should().Be("Production Key");
        result.ApiKey.Scopes.Should().Contain("read");
        result.ApiKey.Scopes.Should().Contain("write");

        var savedKey = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id.ToString() == result.ApiKey.Id, TestCancellationToken);
        savedKey.Should().NotBeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithQuota_StoresMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var request = new CreateApiKeyRequest
        {
            KeyName = "Quota Key",
            Scopes = new[] { "read" },
            MaxRequestsPerDay = 500,
            MaxRequestsPerHour = 50
        };
        var command = new CreateApiKeyManagementCommand(userId.ToString(), request);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.ApiKey.Quota.Should().NotBeNull();
        result.ApiKey.Quota!.MaxRequestsPerDay.Should().Be(500);
        result.ApiKey.Quota.MaxRequestsPerHour.Should().Be(50);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyKeyName_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateApiKeyRequest { KeyName = "", Scopes = new[] { "read" } };
        var command = new CreateApiKeyManagementCommand(Guid.NewGuid().ToString(), request);

        // Act & Assert
        var act = async () => await _handler!.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Key name is required*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _handler!.Handle(null!, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
