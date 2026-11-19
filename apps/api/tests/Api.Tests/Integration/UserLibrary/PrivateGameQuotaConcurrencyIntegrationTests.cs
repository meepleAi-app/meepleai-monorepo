using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using System.Data.Common;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// #5020: the two paths that create a private game — manual creation and the BGG import — count the private games the
/// user has against the tier's limit, and the count and the insert are one atomic step. Before, the count and the
/// insert were two steps: concurrent requests of one user all read the same count and all passed. Runs against
/// PostgreSQL (Testcontainers), with requests released together by a barrier. The transaction runs under the retrying
/// execution strategy: the replay tests inject a transient failure at the save and at the commit, and each must still
/// end in exactly one game.
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
    private readonly TransientFaults _faults = new();
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
        // #5079: the deletion first checks that the game has no open proposal to the catalog.
        services.AddScoped<IShareRequestRepository, ShareRequestRepository>();

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
        services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(_faults.Save, _faults.Commit));

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

    [Fact]
    public async Task ATransientFailureAtTheSave_IsReplayedIntoOneGame()
    {
        _faults.Save.Armed = true;

        await SendAsync(new AddPrivateGameCommand(TestUserId, "Manual", null, "Replayed at the save", 2, 4));

        _faults.Save.Tripped.Should().BeTrue("the first save failed and the strategy ran the transaction again");
        await AssertOneGameAsync("Replayed at the save");
    }

    [Fact]
    public async Task ATransientFailureAtTheCommit_IsReplayedIntoOneGame()
    {
        // From the review of #5067: the save had already accepted the rows (Unchanged) when the commit failed. Staging
        // them only once, the replay saved nothing and the handler answered success for a game that did not exist.
        _faults.Commit.Armed = true;

        await SendAsync(new AddPrivateGameCommand(TestUserId, "Manual", null, "Replayed at the commit", 2, 4));

        _faults.Commit.Tripped.Should().BeTrue("the first commit failed and the strategy ran the transaction again");
        await AssertOneGameAsync("Replayed at the commit");
    }

    [Fact]
    public async Task ADeletedPrivateGame_GivesItsPlaceBack()
    {
        for (var i = 1; i <= MaxPrivateGames; i++)
        {
            await SendAsync(new AddPrivateGameCommand(TestUserId, "Manual", null, $"Game {i}", 2, 4));
        }

        var deletedId = await _dbContext!.PrivateGames.AsNoTracking()
            .Where(g => g.OwnerId == TestUserId).Select(g => g.Id).FirstAsync(TestCancellationToken);
        await SendAsync(new DeletePrivateGameCommand(deletedId, TestUserId));

        var create = () => SendAsync(new AddPrivateGameCommand(TestUserId, "Manual", null, "After a deletion", 2, 4));

        await create.Should().NotThrowAsync("a deleted private game does not count against the limit");
    }

    private async Task SendAsync(IBaseRequest command)
    {
        using var scope = _serviceProvider!.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send((object)command, TestCancellationToken);
    }

    private async Task AssertOneGameAsync(string title)
    {
        var games = await _dbContext!.PrivateGames.AsNoTracking()
            .Where(g => g.OwnerId == TestUserId)
            .Select(g => new { g.Id, g.Title })
            .ToListAsync(TestCancellationToken);
        games.Should().ContainSingle("one creation, replayed, is one game").Which.Title.Should().Be(title);

        var entries = await _dbContext.UserLibraryEntries.AsNoTracking()
            .CountAsync(e => e.UserId == TestUserId && e.PrivateGameId == games[0].Id, TestCancellationToken);
        entries.Should().Be(1, "and one library entry");
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
            // Not released together means not concurrent: a run that timed out on the barrier proves nothing.
            barrier.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue("all the requests are released together");
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

    /// <summary>
    /// One transient Npgsql failure, the kind the retrying execution strategy replays, at the save of a private game or
    /// just before the commit of its transaction. Armed per test, tripped once.
    /// </summary>
    private sealed class TransientFaults
    {
        public SaveFault Save { get; } = new();
        public CommitFault Commit { get; } = new();

        private static NpgsqlException Transient(string where) =>
            new($"transient failure at the {where}, injected by the test", new TimeoutException());

        public sealed class SaveFault : SaveChangesInterceptor
        {
            public volatile bool Armed;
            public volatile bool Tripped;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (Armed && eventData.Context!.ChangeTracker.Entries<PrivateGameEntity>().Any(e => e.State == EntityState.Added))
                {
                    Armed = false;
                    Tripped = true;
                    throw Transient("save");
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        public sealed class CommitFault : DbTransactionInterceptor
        {
            public volatile bool Armed;
            public volatile bool Tripped;

            public override ValueTask<InterceptionResult> TransactionCommittingAsync(
                DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
                CancellationToken cancellationToken = default)
            {
                if (Armed && eventData.Context!.ChangeTracker.Entries<PrivateGameEntity>().Any())
                {
                    Armed = false;
                    Tripped = true;
                    throw Transient("commit");
                }

                return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
            }
        }
    }
}
