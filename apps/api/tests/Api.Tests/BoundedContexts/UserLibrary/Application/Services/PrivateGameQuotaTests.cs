using Api.BoundedContexts.UserLibrary.Application.Services;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Services;

/// <summary>
/// #5020: <see cref="PrivateGameQuota"/> — the count under the owner's lock decides, and a replayed transaction forgets
/// what the previous attempt staged and stages anew. The concurrency, and the replay against a real change tracker,
/// are proven against PostgreSQL in PrivateGameQuotaConcurrencyIntegrationTests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class PrivateGameQuotaTests
{
    private const int Max = 3;
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly Mock<IPrivateGameRepository> _privateGames = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private int _staged;

    private Task CreateAsync() => PrivateGameQuota.CreateWithinQuotaAsync(
        Owner,
        Max,
        _ =>
        {
            _staged++;
            return Task.CompletedTask;
        },
        _privateGames.Object,
        _unitOfWork.Object,
        NullLogger.Instance,
        CancellationToken.None);

    /// <summary>The unit of work runs the work <paramref name="attempts"/> times, as the retrying strategy can.</summary>
    private void TransactionRuns(int attempts) =>
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task> work, CancellationToken ct) =>
            {
                for (var i = 0; i < attempts; i++)
                {
                    await work(ct);
                }
            });

    [Fact]
    public async Task UnderTheLimit_TakesTheLockThenCountsThenStages()
    {
        var order = new List<string>();
        TransactionRuns(1);
        _privateGames.Setup(r => r.LockOwnerQuotaAsync(Owner, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock")).Returns(Task.CompletedTask);
        _privateGames.Setup(r => r.CountByOwnerIdAsync(Owner, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("count")).ReturnsAsync(Max - 1);

        await CreateAsync();

        order.Should().Equal("lock", "count");
        _staged.Should().Be(1);
        _privateGames.Verify(r => r.ForgetStagedCreations(), Times.Never, "a first attempt has nothing to forget");
    }

    [Fact]
    public async Task AtTheLimit_Throws402AndStagesNothing()
    {
        TransactionRuns(1);
        _privateGames.Setup(r => r.CountByOwnerIdAsync(Owner, It.IsAny<CancellationToken>())).ReturnsAsync(Max);

        var act = () => CreateAsync();

        (await act.Should().ThrowAsync<PrivateGameQuotaExceededException>()).Which.MaxAllowed.Should().Be(Max);
        _staged.Should().Be(0);
    }

    [Fact]
    public async Task AReplayedAttempt_ForgetsThePreviousOneThenCountsAndStagesAgain()
    {
        // A replay can follow a SaveChanges that failed (rows still Added: staging again would insert them twice) or a
        // SaveChanges that succeeded and a commit that failed (rows already accepted: not staging again would insert
        // nothing). Forgetting and staging anew covers both, and the count is taken again under the lock.
        var order = new List<string>();
        TransactionRuns(2);
        _privateGames.Setup(r => r.ForgetStagedCreations()).Callback(() => order.Add("forget"));
        _privateGames.Setup(r => r.LockOwnerQuotaAsync(Owner, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock")).Returns(Task.CompletedTask);
        _privateGames.Setup(r => r.CountByOwnerIdAsync(Owner, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("count")).ReturnsAsync(0);

        await CreateAsync();

        order.Should().Equal("lock", "count", "forget", "lock", "count");
        _staged.Should().Be(2);
    }
}
