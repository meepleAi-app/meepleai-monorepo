using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.Infrastructure.BackgroundTasks;

/// <summary>
/// Redis-backed implementation of background task orchestration with distributed coordination.
/// Uses Redis for distributed locking and task status tracking.
/// </summary>
public class RedisBackgroundTaskOrchestrator : IBackgroundTaskOrchestrator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisBackgroundTaskOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledTasks;
    private const string TaskStatusKeyPrefix = "meepleai:tasks:status:";
    private const string TaskLockKeyPrefix = "meepleai:tasks:lock:";

    public RedisBackgroundTaskOrchestrator(
        IConnectionMultiplexer redis,
        ILogger<RedisBackgroundTaskOrchestrator> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runningTasks = new ConcurrentDictionary<string, CancellationTokenSource>();
        _scheduledTasks = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    public async Task ScheduleAsync(
        string taskId,
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));

        _logger.LogInformation("Scheduling task {TaskId} ({TaskName})", taskId, taskName);

        await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Scheduled);

        // Create a linked cancellation token source for this scheduled task
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scheduledTasks.TryAdd(taskId, cts);

        _ = Task.Run(async () => await ExecuteTaskAsync(taskId, taskName, taskFactory, cts.Token), cts.Token);
    }

    public async Task ScheduleDelayedAsync(
        string taskId,
        string taskName,
        TimeSpan delay,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));
        if (delay < TimeSpan.Zero)
            throw new ArgumentException("Delay cannot be negative", nameof(delay));

        _logger.LogInformation("Scheduling delayed task {TaskId} ({TaskName}) with delay {Delay}",
            taskId, taskName, delay);

        await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Scheduled);

        // Create a linked cancellation token source for this scheduled task
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scheduledTasks.TryAdd(taskId, cts);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await ExecuteTaskAsync(taskId, taskName, taskFactory, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled during delay - cleanup handled by CancelAsync
                _logger.LogInformation("Delayed task {TaskId} ({TaskName}) was cancelled before execution", taskId, taskName);
            }
        }, cts.Token);
    }

    public async Task ScheduleRecurringAsync(
        string taskId,
        string taskName,
        TimeSpan interval,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be positive", nameof(interval));

        _logger.LogInformation("Scheduling recurring task {TaskId} ({TaskName}) with interval {Interval}",
            taskId, taskName, interval);

        await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Scheduled);

        // Create a linked cancellation token source for this scheduled task
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scheduledTasks.TryAdd(taskId, cts);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await ExecuteTaskAsync(taskId, taskName, taskFactory, cts.Token);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(interval, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled during execution or delay - cleanup handled by CancelAsync
                _logger.LogInformation("Recurring task {TaskId} ({TaskName}) was cancelled", taskId, taskName);
            }
        }, cts.Token);
    }

    public async Task<bool> CancelAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        // First, try to cancel a scheduled task (not yet executing)
        if (_scheduledTasks.TryRemove(taskId, out var scheduledCts))
        {
            _logger.LogInformation("Cancelling scheduled task {TaskId}", taskId);
            scheduledCts.Cancel();
            scheduledCts.Dispose();
            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Cancelled);
            return true;
        }

        // If not scheduled, try to cancel a running task
        if (_runningTasks.TryRemove(taskId, out var runningCts))
        {
            _logger.LogInformation("Cancelling running task {TaskId}", taskId);
            runningCts.Cancel();
            runningCts.Dispose();
            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Cancelled);
            return true;
        }

        _logger.LogWarning("Task {TaskId} not found for cancellation", taskId);
        return false;
    }

    public async Task<BackgroundTaskStatus?> GetStatusAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

        try
        {
            var db = _redis.GetDatabase();
            var statusValue = await db.StringGetAsync(TaskStatusKeyPrefix + taskId);

            if (!statusValue.HasValue)
                return null;

            if (Enum.TryParse<BackgroundTaskStatus>(statusValue!, out var status))
                return status;

            _logger.LogWarning("Invalid task status value for {TaskId}: {StatusValue}", taskId, statusValue);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for task {TaskId}", taskId);
            return null;
        }
    }

    public async Task<bool> ExecuteWithDistributedLockAsync(
        string lockKey,
        Func<CancellationToken, Task> taskFactory,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));
        if (lockTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Lock timeout must be positive", nameof(lockTimeout));

        var fullLockKey = TaskLockKeyPrefix + lockKey;
        var lockValue = Guid.NewGuid().ToString(); // Unique value to identify this lock holder

        try
        {
            var db = _redis.GetDatabase();

            // Try to acquire distributed lock
            var lockAcquired = await db.StringSetAsync(
                fullLockKey,
                lockValue,
                lockTimeout,
                When.NotExists);

            if (!lockAcquired)
            {
                _logger.LogDebug("Failed to acquire distributed lock for {LockKey}", lockKey);
                return false;
            }

            _logger.LogInformation("Acquired distributed lock for {LockKey}", lockKey);

            try
            {
                // Execute task while holding the lock
                await taskFactory(cancellationToken);
                return true;
            }
            finally
            {
                // Release lock only if we still hold it (verify by value)
                await ReleaseLockAsync(db, fullLockKey, lockValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task with distributed lock {LockKey}", lockKey);
            throw;
        }
    }

    private async Task ExecuteTaskAsync(
        string taskId,
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken)
    {
        // Remove from scheduled tasks as we're now executing
        _scheduledTasks.TryRemove(taskId, out _);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningTasks.TryAdd(taskId, cts);

        try
        {
            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Running);
            _logger.LogInformation("Executing task {TaskId} ({TaskName})", taskId, taskName);

            await taskFactory(cts.Token);

            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Completed);
            _logger.LogInformation("Task {TaskId} ({TaskName}) completed successfully", taskId, taskName);
        }
        catch (OperationCanceledException)
        {
            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Cancelled);
            _logger.LogInformation("Task {TaskId} ({TaskName}) was cancelled", taskId, taskName);
        }
        catch (Exception ex)
        {
            await SetTaskStatusAsync(taskId, BackgroundTaskStatus.Failed);
            _logger.LogError(ex, "Task {TaskId} ({TaskName}) failed with error", taskId, taskName);
        }
        finally
        {
            _runningTasks.TryRemove(taskId, out _);
            cts.Dispose();
        }
    }

    private async Task SetTaskStatusAsync(string taskId, BackgroundTaskStatus status)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(
                TaskStatusKeyPrefix + taskId,
                status.ToString(),
                TimeSpan.FromHours(24)); // Status TTL: 24 hours
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting status for task {TaskId} to {Status}", taskId, status);
        }
    }

    private async Task ReleaseLockAsync(IDatabase db, string lockKey, string lockValue)
    {
        try
        {
            // Lua script to atomically check and delete lock only if we still hold it
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await db.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
            _logger.LogDebug("Released distributed lock {LockKey}", lockKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing distributed lock {LockKey}", lockKey);
        }
    }
}
