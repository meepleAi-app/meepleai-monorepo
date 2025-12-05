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
using Npgsql;
using Xunit;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

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
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider _serviceProvider = null!;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Helper record to hold resolved test services.
    /// </summary>
    private record TestServices(
        MeepleAiDbContext DbContext,
        IUserRepository UserRepository,
        IUnitOfWork UnitOfWork,
        ILogger<UpdateUserTierCommandHandler> Logger);

    /// <summary>
    /// Resolves all required services from a scope for handler instantiation.
    /// </summary>
    private TestServices GetServices(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
        scope.ServiceProvider.GetRequiredService<IUserRepository>(),
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
        scope.ServiceProvider.GetRequiredService<ILogger<UpdateUserTierCommandHandler>>());

    public UpdateUserTierCommandHandlerTests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing UpdateUserTier integration test infrastructure...");

        // Prefer existing infra if provided
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        string connectionString;

        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = "meepleai_test",
                SslMode = Npgsql.SslMode.Disable,
                KeepAlive = 30,
                Pooling = false
            };
            connectionString = builder.ConnectionString;
            _output("Using external TEST_POSTGRES_CONNSTRING for UpdateUserTier tests");
        }
        else
        {
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
            connectionString = $"Host=localhost;Port={containerPort};Database=meepleai_test;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;";

            _output($"PostgreSQL started at localhost:{containerPort}");
        }

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
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UpdateUserTierCommandHandler).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations with simple retry to handle transient container readiness
        await MigrateWithRetry(_dbContext);
        _output("Database migrations applied");
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose all resources even if individual disposals fail
        // Capture first exception but ensure all cleanup is attempted
        Exception? firstException = null;

        try
        {
            await _dbContext.DisposeAsync();
        }
        catch (Exception ex)
        {
            firstException ??= ex;
            _output($"Warning: DbContext disposal failed: {ex.Message}");
        }

        try
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            firstException ??= ex;
            _output($"Warning: ServiceProvider disposal failed: {ex.Message}");
        }

        try
        {
            if (_postgresContainer != null)
            {
                await _postgresContainer.StopAsync(TestCancellationToken);
            }
        }
        catch (Exception ex)
        {
            firstException ??= ex;
            _output($"Warning: PostgreSQL container stop failed: {ex.Message}");
        }

        try
        {
            if (_postgresContainer != null)
            {
                await _postgresContainer.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            firstException ??= ex;
            _output($"Warning: PostgreSQL container disposal failed: {ex.Message}");
        }

        _output("Test infrastructure disposed");

        // Re-throw first exception if any disposal failed
        if (firstException != null)
        {
            throw new AggregateException(
                "One or more errors occurred during test cleanup. See inner exception for details.",
                firstException);
        }
    }
    [Fact]
    public async Task Handle_UpdateUserTier_PersistsChangesToDatabase()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

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

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UserRepository.AddAsync(regularUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        // NOTE: DbContext is passed to handler for direct query operations alongside repository pattern
        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

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
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == regularUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Premium.Value, persistedUser.Tier);
    }

    [Fact]
    public async Task Handle_UpgradeTierFromFreeToNormal_Persists()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var adminUser = new UserBuilder().WithEmail("admin2@test.com").AsAdmin().Build();
        var regularUser = new UserBuilder().WithEmail("user2@test.com").WithTier(UserTier.Free).Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UserRepository.AddAsync(regularUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var command = new UpdateUserTierCommand(regularUser.Id, UserTier.Normal.Value, adminUser.Id);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == regularUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Normal.Value, persistedUser.Tier);
    }

    [Fact]
    public async Task Handle_DowngradeTierFromPremiumToFree_Persists()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var adminUser = new UserBuilder().WithEmail("admin3@test.com").AsAdmin().Build();
        var premiumUser = new UserBuilder().WithEmail("premium@test.com").WithTier(UserTier.Premium).Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UserRepository.AddAsync(premiumUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var command = new UpdateUserTierCommand(premiumUser.Id, UserTier.Free.Value, adminUser.Id);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == premiumUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier);
    }
    [Fact]
    public async Task Handle_NonAdminRequester_ThrowsDomainException()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        // Create non-admin user
        var regularRequester = new UserBuilder()
            .WithEmail("regular@test.com")
            .Build(); // Default is User role

        var targetUser = new UserBuilder()
            .WithEmail("target@test.com")
            .WithTier(UserTier.Free)
            .Build();

        await services.UserRepository.AddAsync(regularRequester, TestCancellationToken);
        await services.UserRepository.AddAsync(targetUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var command = new UpdateUserTierCommand(
            RequesterUserId: regularRequester.Id,
            UserId: targetUser.Id,
            NewTier: UserTier.Premium.Value);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        // NOTE: Testing exact error message intentionally - this is user-facing validation text
        Assert.Contains("Only administrators can change user tiers", exception.Message);

        // Verify tier was NOT changed
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier); // Should still be Free
    }
    [Fact]
    public async Task Handle_InvalidTierValue_ThrowsDomainException()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var adminUser = new UserBuilder().WithEmail("admin4@test.com").AsAdmin().Build();
        var targetUser = new UserBuilder().WithEmail("target2@test.com").WithTier(UserTier.Free).Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UserRepository.AddAsync(targetUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var command = new UpdateUserTierCommand(targetUser.Id, "invalid-tier", adminUser.Id);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        // NOTE: Testing exact error messages intentionally - these are user-facing validation texts
        Assert.Contains("Invalid tier value", exception.Message);
        Assert.Contains("free, normal, premium", exception.Message);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsDomainException()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var adminUser = new UserBuilder().WithEmail("admin5@test.com").AsAdmin().Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var nonExistentUserId = Guid.NewGuid();
        var command = new UpdateUserTierCommand(nonExistentUserId, UserTier.Premium.Value, adminUser.Id);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        // NOTE: Testing exact error message intentionally - this is user-facing validation text
        Assert.Contains("not found", exception.Message);
    }
    [Fact]
    public async Task Handle_NonExistentRequester_ThrowsDomainException()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var targetUser = new UserBuilder().WithEmail("target3@test.com").WithTier(UserTier.Free).Build();

        await services.UserRepository.AddAsync(targetUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var nonExistentRequesterId = Guid.NewGuid();
        var command = new UpdateUserTierCommand(targetUser.Id, UserTier.Premium.Value, nonExistentRequesterId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        // NOTE: Testing exact error message intentionally - this is user-facing validation text
        Assert.Contains("not found", exception.Message);

        // Verify tier was NOT changed
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier); // Should still be Free
    }

    [Fact]
    public async Task Handle_AdminChangingOwnTier_SucceedsIfAllowed()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        // Create admin user with Normal tier (unusual but possible scenario)
        var adminUser = new UserBuilder()
            .WithEmail("selfmodify@test.com")
            .AsAdmin()
            .WithTier(UserTier.Normal)
            .Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        // Admin trying to change their own tier
        var command = new UpdateUserTierCommand(adminUser.Id, UserTier.Premium.Value, adminUser.Id);

        // Act - This test verifies the actual behavior (could succeed or fail based on business rules)
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - Verify the change was persisted
        Assert.NotNull(result);
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Premium.Value, persistedUser.Tier);
    }

    [Fact]
    public async Task Handle_SettingSameTier_IsIdempotent()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        var adminUser = new UserBuilder().WithEmail("admin6@test.com").AsAdmin().Build();
        var premiumUser = new UserBuilder().WithEmail("alreadypremium@test.com").WithTier(UserTier.Premium).Build();

        await services.UserRepository.AddAsync(adminUser, TestCancellationToken);
        await services.UserRepository.AddAsync(premiumUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        // User is already Premium, setting to Premium again (idempotent operation)
        var command = new UpdateUserTierCommand(premiumUser.Id, UserTier.Premium.Value, adminUser.Id);

        // Act - Should succeed as idempotent operation
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - Verify operation succeeded
        Assert.NotNull(result);
        Assert.Equal(premiumUser.Id.ToString(), result.Id);

        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == premiumUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Premium.Value, persistedUser.Tier); // Still Premium
    }

    [Fact]
    public async Task Handle_EditorRequester_ThrowsDomainException()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var services = GetServices(scope);

        // Create editor user (per CLAUDE.md: admin/editor/user roles exist)
        var editorUser = new UserBuilder()
            .WithEmail("editor@test.com")
            .WithRole(AuthRole.Editor)
            .Build();

        var targetUser = new UserBuilder()
            .WithEmail("edittarget@test.com")
            .WithTier(UserTier.Free)
            .Build();

        await services.UserRepository.AddAsync(editorUser, TestCancellationToken);
        await services.UserRepository.AddAsync(targetUser, TestCancellationToken);
        await services.UnitOfWork.SaveChangesAsync(TestCancellationToken);

        var handler = new UpdateUserTierCommandHandler(
            services.UserRepository,
            services.UnitOfWork,
            services.DbContext,
            services.Logger);

        var command = new UpdateUserTierCommand(
            RequesterUserId: editorUser.Id,
            UserId: targetUser.Id,
            NewTier: UserTier.Premium.Value);

        // Act & Assert - Editors should NOT be able to change user tiers
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command, TestCancellationToken));

        // NOTE: Testing exact error message intentionally - this is user-facing validation text
        Assert.Contains("Only administrators can change user tiers", exception.Message);

        // Verify tier was NOT changed
        var persistedUser = await services.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUser.Id, TestCancellationToken);

        Assert.NotNull(persistedUser);
        Assert.Equal(UserTier.Free.Value, persistedUser.Tier); // Should still be Free
    }
    private static async Task MigrateWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
}

