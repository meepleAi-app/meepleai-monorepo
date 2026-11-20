using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

/// <summary>
/// #5093: work outside the database waits for the commit of the transaction it runs in. Real Postgres transactions:
/// whether one is open is read from the DbContext, and the commit, the rollback and the failure come from EF itself.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "5093")]
public sealed class AfterCommitWorkIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private ServiceProvider? _provider;
    private readonly List<string> _ran = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AfterCommitWorkIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_aftercommit_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        // Explicit transactions need no retrying execution strategy (#3633).
        _provider = IntegrationServiceCollectionBuilder.CreateBase(_connectionString, useRetryOnFailure: false)
            .BuildServiceProvider();
        using var scope = _provider.CreateScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null) await _provider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* cleanup only */ }
    }

    private Func<IServiceProvider, CancellationToken, Task> Record(string name) => (_, _) =>
    {
        _ran.Add(name);
        return Task.CompletedTask;
    };

    [Fact]
    public async Task WithoutATransaction_TheWorkRunsAtOnce_WithTheCallersServices()
    {
        await using var scope = _provider!.CreateAsyncScope();
        IServiceProvider? given = null;

        await scope.ServiceProvider.GetRequiredService<IAfterCommitWork>().RunAfterCommitAsync((services, _) =>
        {
            given = services;
            return Task.CompletedTask;
        }, Ct);

        given.Should().BeSameAs(scope.ServiceProvider);
    }

    [Fact]
    public async Task InATransaction_TheWorkWaitsForTheCommit_AndRunsOnceInANewScope()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IAfterCommitWork>();
        MeepleAiDbContext? contextGiven = null;

        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        await work.RunAfterCommitAsync((services, _) =>
        {
            contextGiven = services.GetRequiredService<MeepleAiDbContext>();
            _ran.Add("after");
            return Task.CompletedTask;
        }, Ct);
        _ran.Should().BeEmpty("the transaction has not committed yet");

        await tx.CommitAsync(Ct);

        _ran.Should().Equal("after");
        contextGiven.Should().NotBeSameAs(db, "the caller's context still holds the transaction being closed");
        // Over: new work runs at once, and nothing runs twice.
        await work.RunAfterCommitAsync(Record("next"), Ct);
        _ran.Should().Equal("after", "next");
    }

    [Fact]
    public async Task AfterTheCommit_TheWorkCanWriteToTheDatabase()
    {
        // Publishing a domain event writes its audit row: the work must be able to use a DbContext.
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = Guid.NewGuid();

        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            await scope.ServiceProvider.GetRequiredService<IAfterCommitWork>().RunAfterCommitAsync(async (services, ct) =>
            {
                var own = services.GetRequiredService<MeepleAiDbContext>();
                own.Users.Add(new UserEntity
                {
                    Id = userId,
                    Email = $"after-{userId:N}@test.local",
                    DisplayName = "After commit",
                    Role = "user",
                    Tier = "free",
                    CreatedAt = DateTime.UtcNow,
                });
                await own.SaveChangesAsync(ct);
            }, Ct);
            await tx.CommitAsync(Ct);
        }

        await using var check = _provider.CreateAsyncScope();
        (await check.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ARollback_DropsTheWork()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IAfterCommitWork>();

        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            await work.RunAfterCommitAsync(Record("rolled back"), Ct);
            await tx.RollbackAsync(Ct);
        }

        await using (var next = await db.Database.BeginTransactionAsync(Ct))
        {
            await next.CommitAsync(Ct);
        }

        _ran.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSyncCommitAndRollback_BehaveLikeTheAsyncOnes()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IAfterCommitWork>();

        using (var rolledBack = db.Database.BeginTransaction())
        {
            await work.RunAfterCommitAsync(Record("rolled back"), Ct);
            rolledBack.Rollback();
        }

        using (var committed = db.Database.BeginTransaction())
        {
            await work.RunAfterCommitAsync(Record("committed"), Ct);
            _ran.Should().BeEmpty();
            committed.Commit();
        }

        _ran.Should().Equal("committed");
    }

    [Fact]
    public async Task ATransactionDisposedWithoutCommit_DropsItsWork_AndDoesNotHoldBackLaterWork()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IAfterCommitWork>();

        await using (await db.Database.BeginTransactionAsync(Ct))
        {
            await work.RunAfterCommitAsync(Record("abandoned"), Ct);
        }

        await work.RunAfterCommitAsync(Record("no transaction"), Ct);
        await using (var next = await db.Database.BeginTransactionAsync(Ct))
        {
            await work.RunAfterCommitAsync(Record("committed"), Ct);
            await next.CommitAsync(Ct);
        }

        _ran.Should().Equal("no transaction", "committed");
    }

    [Fact]
    public async Task AFailingItem_DoesNotUndoTheCommit_AndTheRestStillRuns_WithoutCancellation()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IAfterCommitWork>();
        var tokens = new List<CancellationToken>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        await work.RunAfterCommitAsync((_, _) => throw new InvalidOperationException("storage down"), Ct);
        await work.RunAfterCommitAsync((_, token) =>
        {
            tokens.Add(token);
            return Task.CompletedTask;
        }, Ct);

        var commit = () => tx.CommitAsync(Ct);

        await commit.Should().NotThrowAsync("the commit has happened and cannot be undone");
        // The commit is done: what belongs to it is not cancelled with the request.
        tokens.Should().Equal(CancellationToken.None);
    }

    [Fact]
    public async Task ADbContextWithoutTheInterceptor_RefusesTheWork_InsteadOfRunningItBeforeTheCommit()
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString, useRetryOnFailure: false);
        // AddDbContext also leaves its configuration (with the interceptor) as IDbContextOptionsConfiguration: EF
        // composes them, so it goes too.
        services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<MeepleAiDbContext>>();
        services.RemoveAll<MeepleAiDbContext>();
        services.AddDbContext<MeepleAiDbContext>(o => o.UseNpgsql(_connectionString, x => x.UseVector()));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);

        var act = () => scope.ServiceProvider.GetRequiredService<IAfterCommitWork>()
            .RunAfterCommitAsync(Record("before the commit"), Ct);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not among the context's interceptors*");
        _ran.Should().BeEmpty();
    }
}
