using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// #5020: the two paths that create a private game — manual creation and the BGG import — count the private games the
/// user has against the tier's limit, and the count and the insert are one atomic step. Before, the count and the
/// insert were two steps: concurrent requests of one user all read the same count and all passed. Runs against
/// PostgreSQL (Testcontainers), with requests released together by a barrier.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "5020")]
public sealed class PrivateGameQuotaConcurrencyIntegrationTests : IAsyncLifetime
{
    private const int Concurrent = 6;

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000005020");

    /// <summary>The free tier's limit, the one under test.</summary>
    private static readonly int MaxPrivateGames = TierLimits.FreeTier.MaxPrivateGames;

    public PrivateGameQuotaConcurrencyIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pgquota_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IPrivateGameRepository, PrivateGameRepository>();
        services.AddScoped<IUserLibraryRepository, UserLibraryRepository>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();

        // The free tier, so the limit is the real one (3).
        var tier = new Mock<ITierEnforcementService>();
        tier.Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier);
        tier.Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddScoped<ITierEnforcementService>(_ => tier.Object);

        // BGG answers every id with a game of its own.
        var bgg = new Mock<IBggApiClient>();
        bgg.Setup(b => b.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new BggGameDetails
            {
                BggId = id,
                Title = $"BGG game {id}",
                MinPlayers = 2,
                MaxPlayers = 4,
            });
        services.AddScoped<IBggApiClient>(_ => bgg.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, TestCancellationToken);

        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "pgquota-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Private game quota test",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable d)
            await d.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // best-effort cleanup — test isolation already achieved
            }
        }
    }

    [Fact]
    public async Task ManualCreation_ConcurrentRequests_NeverExceedTheTierLimit()
    {
        var results = await RaceAsync(Enumerable.Range(1, Concurrent).Select(i =>
            (IBaseRequest)new AddPrivateGameCommand(TestUserId, "Manual", null, $"Manual game {i}", 2, 4)));

        await AssertTheLimitHeldAsync(results);
    }

    [Fact]
    public async Task BggImport_ConcurrentRequests_NeverExceedTheTierLimit()
    {
        var results = await RaceAsync(Enumerable.Range(1, Concurrent).Select(i =>
            (IBaseRequest)new ImportBggGameCommand(TestUserId, 900_000 + i)));

        await AssertTheLimitHeldAsync(results);
    }

    [Fact]
    public async Task ManualAndBggImport_Concurrently_ShareOneLimit()
    {
        var commands = Enumerable.Range(1, Concurrent).Select(i => i % 2 == 0
            ? (IBaseRequest)new AddPrivateGameCommand(TestUserId, "Manual", null, $"Manual game {i}", 2, 4)
            : new ImportBggGameCommand(TestUserId, 900_000 + i));

        var results = await RaceAsync(commands);

        await AssertTheLimitHeldAsync(results);
    }

    /// <summary>Sends every command from its own scope, released together, and returns what each one threw.</summary>
    private async Task<IReadOnlyList<Exception?>> RaceAsync(IEnumerable<IBaseRequest> commands)
    {
        var list = commands.ToList();
        using var barrier = new Barrier(list.Count);

        var tasks = list.Select(command => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            try
            {
                await mediator.Send((object)command, TestCancellationToken);
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }));

        return await Task.WhenAll(tasks);
    }

    private async Task AssertTheLimitHeldAsync(IReadOnlyList<Exception?> results)
    {
        results.Should().OnlyContain(
            e => e == null || e is PrivateGameQuotaExceededException,
            "a request either creates its game or is refused with the 402 of the quota");

        var owned = await _dbContext!.PrivateGames.AsNoTracking()
            .CountAsync(g => g.OwnerId == TestUserId, TestCancellationToken);
        owned.Should().Be(MaxPrivateGames, "the limit holds under concurrency, and is reached");
        results.Count(e => e == null).Should().Be(MaxPrivateGames);
        results.Count(e => e is PrivateGameQuotaExceededException).Should().Be(Concurrent - MaxPrivateGames);

        var libraryEntries = await _dbContext.UserLibraryEntries.AsNoTracking()
            .CountAsync(e => e.UserId == TestUserId && e.PrivateGameId != null, TestCancellationToken);
        libraryEntries.Should().Be(MaxPrivateGames, "every created game has its library entry, and no refused one does");
    }
}
