using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Api.Tests.Helpers;

/// <summary>
/// Helper class for coordinating BackgroundService execution with FakeTimeProvider.
///
/// Problem: TestTimeProvider.Advance() advances virtual time but doesn't wake tasks
/// awaiting Task.Delay(). Background services run asynchronously, causing race conditions
/// in tests.
///
/// Solution: Provides coordination methods that advance time AND wait for background
/// service to process, ensuring deterministic test execution.
/// </summary>
/// <typeparam name="T">Type of BackgroundService being tested</typeparam>
public class BackgroundServiceTestHelper<T> : IDisposable where T : BackgroundService
{
    private readonly T _service;
    private readonly FakeTimeProvider _timeProvider;
    private readonly CancellationTokenSource _cts;
    private readonly int _processingDelayMs;

    /// <summary>
    /// Creates a new BackgroundServiceTestHelper.
    /// </summary>
    /// <param name="service">Background service instance to coordinate</param>
    /// <param name="timeProvider">TestTimeProvider for virtual time control</param>
    /// <param name="timeout">Optional timeout for service operations (default: 10 seconds)</param>
    /// <param name="processingDelayMs">Delay to allow background processing (default: 50ms)</param>
    public BackgroundServiceTestHelper(
        T service,
        FakeTimeProvider timeProvider,
        TimeSpan? timeout = null,
        int processingDelayMs = 50)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _processingDelayMs = processingDelayMs;

        _cts = new CancellationTokenSource();

        if (timeout.HasValue)
        {
            _cts.CancelAfter(timeout.Value);
        }
        else
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(10)); // Default 10s timeout
        }
    }

    /// <summary>
    /// Starts the background service and waits for initialization.
    /// </summary>
    public async Task StartAsync()
    {
        await _service.StartAsync(_cts.Token);

        // Give service time to initialize and enter ExecuteAsync
        await Task.Delay(_processingDelayMs);
    }

    /// <summary>
    /// Advances virtual time and waits for background service to process.
    ///
    /// This is the key coordination method: it advances TestTimeProvider's virtual time
    /// AND gives the background service thread time to wake up and process.
    /// </summary>
    /// <param name="duration">Amount of virtual time to advance</param>
    public async Task AdvanceAndWaitAsync(TimeSpan duration)
    {
        // Advance virtual time
        _timeProvider.Advance(duration);

        // Give background service time to wake up and process
        // This small real delay allows the thread scheduler to run the background task
        await Task.Delay(_processingDelayMs);

        // Additional yield for safety
        await Task.Yield();
    }

    /// <summary>
    /// Advances virtual time by seconds and waits for processing.
    /// </summary>
    public async Task AdvanceSecondsAsync(int seconds)
    {
        await AdvanceAndWaitAsync(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Advances virtual time by minutes and waits for processing.
    /// </summary>
    public async Task AdvanceMinutesAsync(int minutes)
    {
        await AdvanceAndWaitAsync(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Advances virtual time by hours and waits for processing.
    /// </summary>
    public async Task AdvanceHoursAsync(int hours)
    {
        await AdvanceAndWaitAsync(TimeSpan.FromHours(hours));
    }

    /// <summary>
    /// Waits for background service to complete current processing cycle.
    /// Useful after time advancement when you need to ensure processing is complete.
    /// </summary>
    public async Task WaitForProcessingAsync()
    {
        await Task.Delay(_processingDelayMs);
        await Task.Yield();
    }

    /// <summary>
    /// Stops the background service gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        await _service.StopAsync(_cts.Token);
    }

    /// <summary>
    /// Disposes resources used by the helper.
    /// </summary>
    public void Dispose()
    {
        _cts?.Dispose();
    }
}

/// <summary>
/// Extension methods for BackgroundServiceTestHelper to provide fluent API.
/// </summary>
public static class BackgroundServiceTestHelperExtensions
{
    /// <summary>
    /// Creates a test helper for the given background service.
    /// </summary>
    public static BackgroundServiceTestHelper<T> CreateHelper<T>(
        this T service,
        FakeTimeProvider timeProvider,
        TimeSpan? timeout = null,
        int processingDelayMs = 50) where T : BackgroundService
    {
        return new BackgroundServiceTestHelper<T>(service, timeProvider, timeout, processingDelayMs);
    }
}