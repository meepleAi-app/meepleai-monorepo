using Api.SharedKernel.Infrastructure.Persistence;
using Moq;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Issue #3636 — helpers for mocking <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>.
///
/// <para>
/// Handlers no longer drive the transaction with <c>Begin</c>/<c>Commit</c>/<c>Rollback</c>: they
/// hand the work to the UoW, which owns the transaction and — crucially — wraps it in the
/// execution strategy required by <c>EnableRetryOnFailure</c>. A bare <c>Mock&lt;IUnitOfWork&gt;</c>
/// returns <c>default</c> for that method, so the delegate never runs and the handler observes
/// nulls; these extensions make the mock actually execute it.
/// </para>
/// <para>
/// Note for whoever writes the assertions: verifying «Begin was called once, Commit once» no longer
/// means anything — that sequencing is the UoW's business and is covered by its own tests. Assert
/// on what the handler produced instead (rows written, commands dispatched, exception mapped).
/// </para>
/// </summary>
internal static class UnitOfWorkMockExtensions
{
    /// <summary>
    /// Makes <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> run the supplied delegate and
    /// return its result — the mock equivalent of a transaction that commits.
    /// </summary>
    public static Mock<IUnitOfWork> SetupExecuteInTransaction<T>(this Mock<IUnitOfWork> mock)
    {
        mock.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<T>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct));

        return mock;
    }

    /// <summary>
    /// Void overload of <see cref="SetupExecuteInTransaction{T}"/>.
    /// </summary>
    public static Mock<IUnitOfWork> SetupExecuteInTransaction(this Mock<IUnitOfWork> mock)
    {
        mock.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> work, CancellationToken ct) => work(ct));

        return mock;
    }
}
