using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for creating games with BGG integration.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
public sealed class CreateGameWithBggIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private UserEntity? _testUser;
    private IServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    public CreateGameWithBggIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_create_game_bgg_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Issue #2541: Register IMediator and IDomainEventCollector required by MeepleAiDbContext
        services.AddSingleton<IMediator>(TestDbContextFactory.CreateMockMediator().Object);
        services.AddSingleton<IDomainEventCollector>(TestDbContextFactory.CreateMockEventCollector().Object);

        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector())); // Issue #3547

        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<CreateGameCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync();

        // Seed user (though CreateGameCommandHandler doesn't strictly check permissions, endpoints do)
        _testUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "admin@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(_testUser);
        await _dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (_serviceProvider is IDisposable disposable) disposable.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task CreateGame_WithBggId_PersistsToDatabase()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<CreateGameCommandHandler>();
        var command = new CreateGameCommand(
            Title: "Terraforming Mars",
            BggId: 167791,
            YearPublished: 2016,
            Publisher: "FryxGames",
            MinPlayers: 1,
            MaxPlayers: 5
        );

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Terraforming Mars", result.Title);
        Assert.Equal(167791, result.BggId);

        // Verify Database Persistence
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var persistedGame = await db.Games.FirstOrDefaultAsync(g => g.Id == result.Id);

        Assert.NotNull(persistedGame);
        Assert.Equal("Terraforming Mars", persistedGame.Name); // GameEntity maps Title to Name
        Assert.Equal(167791, persistedGame.BggId);
    }
}
