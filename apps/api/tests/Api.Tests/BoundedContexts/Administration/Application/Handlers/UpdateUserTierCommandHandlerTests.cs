using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Integration tests for UpdateUserTierCommandHandler.
/// Verifies that tier changes are actually persisted to the database.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public class UpdateUserTierCommandHandlerTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => CancellationToken.None;

    public UpdateUserTierCommandHandlerTests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing UpdateUserTier integration test infrastructure...");

        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "meepleai_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=meepleai_test;Username=postgres;Password=postgres;";

        _output($"PostgreSQL started at localhost:{containerPort}");

        // Setup dependency injection
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register DbContext with PostgreSQL
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register domain services
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Infrastructure.DomainEventCollector>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        _output("Database migrations applied");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }

        _output("Test infrastructure disposed");
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_UpdateUserTier_PersistsChangesToDatabase()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        // Create admin user
        var adminUser = new UserBuilder()
            .WithEmail("admin@test.com")
            .AsAdmin()
            .Build();

        // Create regular user with Free tier
        var regularUser = new UserBuilder()
            .WithEmail("user@test.com")
            .WithTier(UserTier.Free)
            .Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await userRepository.AddAsync(regularUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            userRepository,
            unitOfWork,
            dbContext,
            logger);

        var command = new UpdateUserTierCommand(
            RequesterUserId: adminUser.Id,
            UserId: regularUser.Id,
            NewTier: UserTier.Premium.Value);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - verify response
        Assert.NotNull(result);
        Assert.Equal(regularUser.Id.ToString(), result.Id);
        Assert.Equal("user@test.com", result.Email);

        // CRITICAL: Verify the tier was actually persisted to the database
        var persistedUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == regularUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Premium.Value, persistedUser.Tier);

        scope.Dispose();
    }

    [Fact]
    public async Task Handle_UpgradeTierFromFreeToNormal_Persists()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        var adminUser = new UserBuilder().WithEmail("admin2@test.com").AsAdmin().Build();
        var regularUser = new UserBuilder().WithEmail("user2@test.com").WithTier(UserTier.Free).Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await userRepository.AddAsync(regularUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(userRepository, unitOfWork, dbContext, logger);
        var command = new UpdateUserTierCommand(adminUser.Id, regularUser.Id, UserTier.Normal.Value);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var persistedUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == regularUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Normal.Value, persistedUser.Tier);

        scope.Dispose();
    }

    [Fact]
    public async Task Handle_DowngradeTierFromPremiumToFree_Persists()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        var adminUser = new UserBuilder().WithEmail("admin3@test.com").AsAdmin().Build();
        var premiumUser = new UserBuilder().WithEmail("premium@test.com").WithTier(UserTier.Premium).Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await userRepository.AddAsync(premiumUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(userRepository, unitOfWork, dbContext, logger);
        var command = new UpdateUserTierCommand(adminUser.Id, premiumUser.Id, UserTier.Free.Value);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var persistedUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == premiumUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier);

        scope.Dispose();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Handle_NonAdminRequester_ThrowsDomainException()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        // Create non-admin user
        var regularRequester = new UserBuilder()
            .WithEmail("regular@test.com")
            .Build(); // Default is User role

        var targetUser = new UserBuilder()
            .WithEmail("target@test.com")
            .WithTier(UserTier.Free)
            .Build();

        await userRepository.AddAsync(regularRequester, TestCancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(userRepository, unitOfWork, dbContext, logger);
        var command = new UpdateUserTierCommand(
            RequesterUserId: regularRequester.Id,
            UserId: targetUser.Id,
            NewTier: UserTier.Premium.Value);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        Assert.Contains("Only administrators can change user tiers", exception.Message);

        // Verify tier was NOT changed
        var persistedUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier); // Should still be Free

        scope.Dispose();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_InvalidTierValue_ThrowsDomainException()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        var adminUser = new UserBuilder().WithEmail("admin4@test.com").AsAdmin().Build();
        var targetUser = new UserBuilder().WithEmail("target2@test.com").WithTier(UserTier.Free).Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(userRepository, unitOfWork, dbContext, logger);
        var command = new UpdateUserTierCommand(adminUser.Id, targetUser.Id, "invalid-tier");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        Assert.Contains("Invalid tier value", exception.Message);
        Assert.Contains("free, normal, premium", exception.Message);

        scope.Dispose();
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsDomainException()
    {
        // Arrange
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>();

        var adminUser = new UserBuilder().WithEmail("admin5@test.com").AsAdmin().Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(userRepository, unitOfWork, dbContext, logger);
        var nonExistentUserId = Guid.NewGuid();
        var command = new UpdateUserTierCommand(adminUser.Id, nonExistentUserId, UserTier.Premium.Value);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        Assert.Contains("not found", exception.Message);

        scope.Dispose();
    }

    #endregion
}
