using Api.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// #5093: work outside the database waits for the commit of the transaction it runs in. The transaction events are
/// called directly: the implementation keeps its state from them and reads none of their arguments. The real
/// transaction, with the [AtomicAudit] behavior, is in <c>DeleteKbDocumentCommandHandlerIntegrationTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedKernel")]
public sealed class AfterCommitWorkTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AfterCommitWork _work = new(NullLogger<AfterCommitWork>.Instance);
    private readonly List<string> _ran = [];

    private Func<CancellationToken, Task> Record(string name) => _ =>
    {
        _ran.Add(name);
        return Task.CompletedTask;
    };

    [Fact]
    public async Task WithoutATransaction_TheWorkRunsAtOnce()
    {
        await _work.RunAfterCommitAsync(Record("now"), Ct);

        _ran.Should().Equal("now");
    }

    [Fact]
    public async Task InsideATransaction_TheWorkWaitsForTheCommit_AndRunsOnce()
    {
        await _work.TransactionStartedAsync(null!, null!, null!, Ct);

        await _work.RunAfterCommitAsync(Record("after"), Ct);
        _ran.Should().BeEmpty();

        await _work.TransactionCommittedAsync(null!, null!, Ct);
        _ran.Should().Equal("after");

        // The transaction is over: nothing is left to run twice, and new work runs at once.
        await _work.TransactionCommittedAsync(null!, null!, Ct);
        await _work.RunAfterCommitAsync(Record("next"), Ct);
        _ran.Should().Equal("after", "next");
    }

    [Fact]
    public async Task ARollback_DropsTheWork()
    {
        await _work.TransactionStartedAsync(null!, null!, null!, Ct);
        await _work.RunAfterCommitAsync(Record("rolled back"), Ct);

        await _work.TransactionRolledBackAsync(null!, null!, Ct);
        await _work.TransactionCommittedAsync(null!, null!, Ct);

        _ran.Should().BeEmpty();
    }

    [Fact]
    public async Task AFailedTransaction_DropsTheWork()
    {
        await _work.TransactionStartedAsync(null!, null!, null!, Ct);
        await _work.RunAfterCommitAsync(Record("failed"), Ct);

        await _work.TransactionFailedAsync(null!, null!, Ct);
        await _work.TransactionCommittedAsync(null!, null!, Ct);

        _ran.Should().BeEmpty();
    }

    [Fact]
    public async Task ATransactionDisposedWithoutAnEnd_DoesNotPassItsWorkToTheNextOne()
    {
        // A transaction disposed without commit or rollback raises no event; the next one starts over.
        await _work.TransactionStartedAsync(null!, null!, null!, Ct);
        await _work.RunAfterCommitAsync(Record("abandoned"), Ct);

        await _work.TransactionStartedAsync(null!, null!, null!, Ct);
        await _work.RunAfterCommitAsync(Record("committed"), Ct);
        await _work.TransactionCommittedAsync(null!, null!, Ct);

        _ran.Should().Equal("committed");
    }

    // EF raises the sync events for a synchronous Commit or Rollback. Each case ends in a commit with no new start in
    // between: a new start drops queued work by itself, and would hide a rollback that kept it.
    [Fact]
    public async Task TheSyncRollback_DropsTheWork()
    {
        _work.TransactionStarted(null!, null!, null!);
        await _work.RunAfterCommitAsync(Record("rolled back"), Ct);

        _work.TransactionRolledBack(null!, null!);
        _work.TransactionCommitted(null!, null!);

        _ran.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSyncFailure_DropsTheWork()
    {
        _work.TransactionStarted(null!, null!, null!);
        await _work.RunAfterCommitAsync(Record("failed"), Ct);

        _work.TransactionFailed(null!, null!);
        _work.TransactionCommitted(null!, null!);

        _ran.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSyncCommit_RunsTheWork()
    {
        _work.TransactionStarted(null!, null!, null!);
        await _work.RunAfterCommitAsync(Record("committed"), Ct);
        _ran.Should().BeEmpty();

        _work.TransactionCommitted(null!, null!);

        _ran.Should().Equal("committed");
    }

    [Fact]
    public async Task AFailingItem_IsLogged_AndTheRestStillRuns_WithoutCancellation()
    {
        var logger = new Mock<ILogger<AfterCommitWork>>();
        var work = new AfterCommitWork(logger.Object);
        var tokens = new List<CancellationToken>();
        await work.TransactionStartedAsync(null!, null!, null!, Ct);
        await work.RunAfterCommitAsync(_ => throw new InvalidOperationException("storage down"), Ct);
        await work.RunAfterCommitAsync(token =>
        {
            tokens.Add(token);
            return Task.CompletedTask;
        }, Ct);

        var commit = () => work.TransactionCommittedAsync(null!, null!, Ct);

        await commit.Should().NotThrowAsync("the commit has happened and cannot be undone");
        // The commit is done: what belongs to it is not cancelled with the request.
        tokens.Should().Equal(CancellationToken.None);
        logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<InvalidOperationException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
