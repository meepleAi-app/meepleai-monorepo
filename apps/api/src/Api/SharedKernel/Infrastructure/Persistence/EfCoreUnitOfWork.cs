using Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Unit of Work pattern.
/// Manages transactions and change tracking for the DbContext.
/// </summary>
internal class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly MeepleAiDbContext _dbContext;
    private IDbContextTransaction? _currentTransaction;

    public EfCoreUnitOfWork(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        // #3636: CreateExecutionStrategy() is what makes the transaction legal under
        // NpgsqlRetryingExecutionStrategy. Opening one directly throws — the defect this replaces.
        // The strategy owns the retry loop, so the delegate below (transaction included) may run
        // more than once.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            // Tracked so that a caller's RollbackTransactionAsync (e.g. in a catch block that
            // predates this method) still finds a transaction to roll back rather than throwing
            // "No transaction in progress" and masking the original error.
            _currentTransaction = transaction;

            try
            {
                var result = await work(cancellationToken).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch
            {
                // Best-effort: the transaction may already be gone if the caller rolled it back.
                if (_currentTransaction is not null)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception rollbackEx) when (rollbackEx is not OperationCanceledException)
                    {
                        // Swallowing here is deliberate: rethrowing would replace the ORIGINAL
                        // failure with a rollback error and lose the cause.
                    }
                }

                throw;
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
                _currentTransaction = null;
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await ExecuteInTransactionAsync<object?>(async ct =>
        {
            await work(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync().ConfigureAwait(false);
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync().ConfigureAwait(false);
                _currentTransaction = null;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentTransaction?.Dispose();
        }
    }
}
