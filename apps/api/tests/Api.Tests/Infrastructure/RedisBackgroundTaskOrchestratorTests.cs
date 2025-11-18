using System;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure.BackgroundTasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for RedisBackgroundTaskOrchestrator.
/// Tests background task scheduling, execution, cancellation, and distributed locking.
/// </summary>
public class RedisBackgroundTaskOrchestratorTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisBackgroundTaskOrchestrator>> _mockLogger;
    private readonly RedisBackgroundTaskOrchestrator _orchestrator;

    public RedisBackgroundTaskOrchestratorTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisBackgroundTaskOrchestrator>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _orchestrator = new RedisBackgroundTaskOrchestrator(_mockRedis.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ScheduleAsync_ValidTask_SchedulesSuccessfully()
    {
        // Arrange
        var taskId = "test-task-1";
        var taskName = "Test Task";
        var taskExecuted = false;
        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            await Task.Delay(10, ct);
            taskExecuted = true;
        };

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _orchestrator.ScheduleAsync(taskId, taskName, taskFactory);

        // Give task time to execute
        await Task.Delay(100);

        // Assert
        Assert.True(taskExecuted);
        _mockDatabase.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("tasks:status")),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ScheduleAsync_NullTaskId_ThrowsArgumentException()
    {
        // Arrange
        Func<CancellationToken, Task> taskFactory = ct => Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.ScheduleAsync(null!, "Test", taskFactory));
    }

    [Fact]
    public async Task ScheduleAsync_NullTaskFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _orchestrator.ScheduleAsync("test-id", "Test", null!));
    }

    [Fact]
    public async Task ScheduleDelayedAsync_ValidTask_DelaysExecution()
    {
        // Arrange
        var taskId = "delayed-task-1";
        var taskName = "Delayed Task";
        var delay = TimeSpan.FromMilliseconds(100);
        var taskExecuted = false;
        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            await Task.Delay(10, ct);
            taskExecuted = true;
        };

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _orchestrator.ScheduleDelayedAsync(taskId, taskName, delay, taskFactory);

        // Assert - task should not be executed immediately
        Assert.False(taskExecuted);

        // Wait for delay + execution time
        await Task.Delay(200);

        // Assert - task should be executed after delay
        Assert.True(taskExecuted);
    }

    [Fact]
    public async Task ScheduleDelayedAsync_NegativeDelay_ThrowsArgumentException()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(-100);
        Func<CancellationToken, Task> taskFactory = ct => Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.ScheduleDelayedAsync("test-id", "Test", delay, taskFactory));
    }

    [Fact]
    public async Task CancelAsync_RunningTask_CancelsSuccessfully()
    {
        // Arrange
        var taskId = "cancellable-task";
        var taskName = "Cancellable Task";
        var taskCancelled = false;
        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            try
            {
                await Task.Delay(5000, ct); // Long-running task
            }
            catch (OperationCanceledException)
            {
                taskCancelled = true;
                throw;
            }
        };

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _orchestrator.ScheduleAsync(taskId, taskName, taskFactory);
        await Task.Delay(50); // Give task time to start

        var cancelResult = await _orchestrator.CancelAsync(taskId);

        // Wait for cancellation to complete
        await Task.Delay(100);

        // Assert
        Assert.True(cancelResult);
        Assert.True(taskCancelled);
    }

    [Fact]
    public async Task CancelAsync_NonExistentTask_ReturnsFalse()
    {
        // Act
        var result = await _orchestrator.CancelAsync("nonexistent-task");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetStatusAsync_ExistingTask_ReturnsStatus()
    {
        // Arrange
        var taskId = "status-task";
        var statusValue = BackgroundTaskStatus.Running.ToString();

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(taskId)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(statusValue));

        // Act
        var status = await _orchestrator.GetStatusAsync(taskId);

        // Assert
        Assert.NotNull(status);
        Assert.Equal(BackgroundTaskStatus.Running, status.Value);
    }

    [Fact]
    public async Task GetStatusAsync_NonExistentTask_ReturnsNull()
    {
        // Arrange
        var taskId = "nonexistent-task";

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var status = await _orchestrator.GetStatusAsync(taskId);

        // Assert
        Assert.Null(status);
    }

    [Fact]
    public async Task ExecuteWithDistributedLockAsync_LockAcquired_ExecutesTask()
    {
        // Arrange
        var lockKey = "test-lock";
        var taskExecuted = false;
        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            await Task.Delay(10, ct);
            taskExecuted = true;
        };
        var lockTimeout = TimeSpan.FromMinutes(1);

        // Mock successful lock acquisition
        _mockDatabase.Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(lockKey)),
                It.IsAny<RedisValue>(),
                lockTimeout,
                When.NotExists,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Mock lock release
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        var result = await _orchestrator.ExecuteWithDistributedLockAsync(
            lockKey, taskFactory, lockTimeout);

        // Assert
        Assert.True(result);
        Assert.True(taskExecuted);
    }

    [Fact]
    public async Task ExecuteWithDistributedLockAsync_LockNotAcquired_DoesNotExecuteTask()
    {
        // Arrange
        var lockKey = "test-lock";
        var taskExecuted = false;
        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            await Task.Delay(10, ct);
            taskExecuted = true;
        };
        var lockTimeout = TimeSpan.FromMinutes(1);

        // Mock failed lock acquisition (already held)
        _mockDatabase.Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(lockKey)),
                It.IsAny<RedisValue>(),
                lockTimeout,
                When.NotExists,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _orchestrator.ExecuteWithDistributedLockAsync(
            lockKey, taskFactory, lockTimeout);

        // Assert
        Assert.False(result);
        Assert.False(taskExecuted);
    }

    [Fact]
    public async Task ExecuteWithDistributedLockAsync_NullLockKey_ThrowsArgumentException()
    {
        // Arrange
        Func<CancellationToken, Task> taskFactory = ct => Task.CompletedTask;
        var lockTimeout = TimeSpan.FromMinutes(1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.ExecuteWithDistributedLockAsync(null!, taskFactory, lockTimeout));
    }

    [Fact]
    public async Task ExecuteWithDistributedLockAsync_NegativeLockTimeout_ThrowsArgumentException()
    {
        // Arrange
        Func<CancellationToken, Task> taskFactory = ct => Task.CompletedTask;
        var lockTimeout = TimeSpan.FromMinutes(-1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.ExecuteWithDistributedLockAsync("test-lock", taskFactory, lockTimeout));
    }

    [Fact]
    public async Task ScheduleRecurringAsync_ValidTask_SchedulesRecurring()
    {
        // Arrange
        var taskId = "recurring-task";
        var taskName = "Recurring Task";
        var interval = TimeSpan.FromMilliseconds(50);
        var executionCount = 0;
        var cts = new CancellationTokenSource();

        Func<CancellationToken, Task> taskFactory = async ct =>
        {
            await Task.Delay(10, ct);
            executionCount++;
            if (executionCount >= 2)
            {
                cts.Cancel(); // Stop after 2 executions
            }
        };

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _orchestrator.ScheduleRecurringAsync(taskId, taskName, interval, taskFactory, cts.Token);

        // Wait for at least 2 executions
        await Task.Delay(300);

        // Assert
        Assert.True(executionCount >= 2);
    }

    [Fact]
    public async Task ScheduleRecurringAsync_ZeroInterval_ThrowsArgumentException()
    {
        // Arrange
        var interval = TimeSpan.Zero;
        Func<CancellationToken, Task> taskFactory = ct => Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orchestrator.ScheduleRecurringAsync("test-id", "Test", interval, taskFactory));
    }
}
