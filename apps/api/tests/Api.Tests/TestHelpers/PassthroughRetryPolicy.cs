using Api.Services;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="ICacheInvalidationRetryPolicy"/> that invokes
/// the operation exactly once with no retry semantics. Used in handler unit
/// tests where the goal is to verify the wrapped invalidation logic, not
/// the retry policy itself (covered by <c>CacheInvalidationRetryPolicyTests</c>).
///
/// Issue #613.
/// </summary>
internal sealed class PassthroughRetryPolicy : ICacheInvalidationRetryPolicy
{
    public Task ExecuteAsync(
        Func<CancellationToken, ValueTask> operation,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrEmpty(operationName);
        return operation(ct).AsTask();
    }
}
