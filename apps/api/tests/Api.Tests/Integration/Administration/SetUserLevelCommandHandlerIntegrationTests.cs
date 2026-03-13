using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for SetUserLevelCommandHandler (Issue #3141).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3141")]
public sealed class SetUserLevelCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private SetUserLevelCommandHandler? _handler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SetUserLevelCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_setuserlevel_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Run migrations with retry
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

        var logger = new Mock<ILogger<SetUserLevelCommandHandler>>();
        _handler = new SetUserLevelCommandHandler(userRepository, unitOfWork, _dbContext, logger.Object);
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
    public async Task Handle_WithValidCommand_UpdatesUserLevel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEntity = CreateTestUserEntity(userId, initialLevel: 1);
        _dbContext!.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        var command = new SetUserLevelCommand(userId, 10);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Level.Should().Be(10);
        result.Id.Should().Be(userId);

        // Verify persistence
        var savedUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestCancellationToken);
        savedUser.Should().NotBeNull();
        savedUser!.Level.Should().Be(10);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithZeroLevel_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEntity = CreateTestUserEntity(userId, initialLevel: 5);
        _dbContext!.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        var command = new SetUserLevelCommand(userId, 0);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Level.Should().Be(0);

        // Verify persistence
        var savedUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestCancellationToken);
        savedUser!.Level.Should().Be(0);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithMaxLevel_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEntity = CreateTestUserEntity(userId, initialLevel: 0);
        _dbContext!.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        var command = new SetUserLevelCommand(userId, 100);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Level.Should().Be(100);

        // Verify persistence
        var savedUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestCancellationToken);
        savedUser!.Level.Should().Be(100);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentUser_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var command = new SetUserLevelCommand(nonExistentUserId, 5);

        // Act & Assert
        var act = async () => await _handler!.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"User with identifier '{nonExistentUserId}' was not found");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _handler!.Handle(null!, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsCompleteUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEntity = CreateTestUserEntity(userId, initialLevel: 1, experiencePoints: 500);
        _dbContext!.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        var command = new SetUserLevelCommand(userId, 5);

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert - Verify all UserDto fields are populated
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be(userEntity.Email);
        result.DisplayName.Should().Be(userEntity.DisplayName);
        result.Role.Should().Be(userEntity.Role);
        result.Tier.Should().Be(userEntity.Tier);
        result.Level.Should().Be(5);
        result.ExperiencePoints.Should().Be(500);
        result.CreatedAt.Should().BeCloseTo(userEntity.CreatedAt, TimeSpan.FromSeconds(1));
    }

    private static UserEntity CreateTestUserEntity(
        Guid userId,
        int initialLevel = 0,
        int experiencePoints = 0)
    {
        return new UserEntity
        {
            Id = userId,
            Email = $"test-{userId}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed_password",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            Level = initialLevel,
            ExperiencePoints = experiencePoints,
            IsTwoFactorEnabled = false
        };
    }
}
