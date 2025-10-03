using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class BackgroundTaskServiceTests
{
    private readonly Mock<ILogger<BackgroundTaskService>> _mockLogger = new();

    [Fact]
    public async Task Execute_WithSuccessfulTask_ExecutesTaskInBackground()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs = new TaskCompletionSource<bool>();
        var taskExecuted = false;

        // Act
        service.Execute(async () =>
        {
            taskExecuted = true;
            tcs.SetResult(true);
            await Task.CompletedTask;
        });

        // Wait for background task to complete
        await tcs.Task;

        // Assert
        Assert.True(taskExecuted);
    }

    [Fact]
    public async Task Execute_WithTaskThatThrowsException_LogsError()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs = new TaskCompletionSource<bool>();
        var expectedException = new InvalidOperationException("Test exception");

        // Act
        service.Execute(async () =>
        {
            try
            {
                throw expectedException;
            }
            finally
            {
                tcs.SetResult(true);
            }
            await Task.CompletedTask;
        });

        // Wait for background task to complete
        await tcs.Task;

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.Is<Exception>(ex => ex == expectedException),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void Execute_DoesNotBlock()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs = new TaskCompletionSource<bool>();

        // Act
        var startTime = DateTime.UtcNow;
        service.Execute(async () =>
        {
            await Task.Delay(100); // Simulate long-running task
            tcs.SetResult(true);
        });
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Execute should return immediately (within 50ms)
        Assert.True(elapsed.TotalMilliseconds < 50, $"Execute blocked for {elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Execute_WithMultipleTasks_ExecutesAllTasks()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        var tcs3 = new TaskCompletionSource<bool>();
        var task1Executed = false;
        var task2Executed = false;
        var task3Executed = false;

        // Act
        service.Execute(async () =>
        {
            task1Executed = true;
            tcs1.SetResult(true);
            await Task.CompletedTask;
        });

        service.Execute(async () =>
        {
            task2Executed = true;
            tcs2.SetResult(true);
            await Task.CompletedTask;
        });

        service.Execute(async () =>
        {
            task3Executed = true;
            tcs3.SetResult(true);
            await Task.CompletedTask;
        });

        // Wait for all background tasks to complete
        await Task.WhenAll(tcs1.Task, tcs2.Task, tcs3.Task);

        // Assert
        Assert.True(task1Executed);
        Assert.True(task2Executed);
        Assert.True(task3Executed);
    }

    [Fact]
    public async Task Execute_WithAsyncTask_CompletesSuccessfully()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs = new TaskCompletionSource<bool>();
        var taskCompleted = false;

        // Act
        service.Execute(async () =>
        {
            await Task.Delay(10);
            taskCompleted = true;
            tcs.SetResult(true);
        });

        // Wait for background task to complete
        await tcs.Task;

        // Assert
        Assert.True(taskCompleted);
    }

    [Fact]
    public async Task Execute_WithExceptionDuringAsyncOperation_LogsError()
    {
        // Arrange
        var service = new BackgroundTaskService(_mockLogger.Object);
        var tcs = new TaskCompletionSource<bool>();
        var expectedException = new ArgumentException("Async exception");

        // Act
        service.Execute(async () =>
        {
            try
            {
                await Task.Delay(10);
                throw expectedException;
            }
            finally
            {
                tcs.SetResult(true);
            }
        });

        // Wait for background task to complete
        await tcs.Task;

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.Is<Exception>(ex => ex == expectedException),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
