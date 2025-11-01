using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace MeepleAI.Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for CacheMetricsRecorder service.
/// Tests cache hit/miss metric recording, async fire-and-forget pattern, and error handling.
/// NOTE: Uses behavior-based testing (not mocking Counter<long> which is sealed).
/// Verifies logger calls, timing, and configuration handling rather than metric output.
/// </summary>
public class CacheMetricsRecorderTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<CacheMetricsRecorder>> _mockLogger;

    public CacheMetricsRecorderTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<CacheMetricsRecorder>>();
    }

    private CacheMetricsRecorder CreateRecorder(bool metricsEnabled = true)
    {
        var config = Options.Create(new CacheOptimizationConfiguration
        {
            MetricsEnabled = metricsEnabled
        });

        return new CacheMetricsRecorder(_mockLogger.Object, config);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange (Given): Invalid constructor parameters
        var config = Options.Create(new CacheOptimizationConfiguration());

        // Act (When): Constructing with null logger
        Action act = () => new CacheMetricsRecorder(null!, config);

        // Assert (Then): Throws ArgumentNullException
        act.Should().Throw<ArgumentNullException>();
        var exception = act.Should().Throw<ArgumentNullException>().Subject.Subject;
        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange (Given): Invalid constructor parameters
        // Act (When): Constructing with null config
        Action act = () => new CacheMetricsRecorder(_mockLogger.Object, null!);

        // Assert (Then): Throws ArgumentNullException
        act.Should().Throw<ArgumentNullException>();
        var exception = act.Should().Throw<ArgumentNullException>().Subject.Subject;
        exception.ParamName.Should().Be("config");
    }

    #endregion

    #region RecordCacheHitAsync Tests

    [Fact]
    public async Task RecordCacheHitAsync_MetricsEnabled_CompletesSuccessfully()
    {
        // Arrange (Given): Cache service ready with metrics enabled
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Cache hit occurs
        await recorder.RecordCacheHitAsync("get", "ai_response");

        // Small delay to allow fire-and-forget task to complete
        await Task.Delay(50);

        // Assert (Then): No errors logged (metrics recorded successfully)
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordCacheHitAsync_MetricsDisabled_SkipsRecording()
    {
        // Arrange (Given): Metrics disabled in configuration
        var recorder = CreateRecorder(metricsEnabled: false);

        // Act (When): Cache hit occurs
        await recorder.RecordCacheHitAsync("get", "ai_response");

        // Small delay to ensure fire-and-forget task completes
        await Task.Delay(50);

        // Assert (Then): No logs (metrics skipped silently)
        _mockLogger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecordCacheHitAsync_AsyncFireAndForget_CompletesQuickly()
    {
        // Arrange (Given): Metrics recording in fire-and-forget mode
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Cache operation completes
        var stopwatch = Stopwatch.StartNew();
        await recorder.RecordCacheHitAsync("get", "ai_response");
        stopwatch.Stop();

        // Assert (Then): Fire-and-forget completes in <5ms (doesn't block caller)
        (stopwatch.ElapsedMilliseconds < 5).Should().BeTrue($"Expected <5ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region RecordCacheMissAsync Tests

    [Fact]
    public async Task RecordCacheMissAsync_MetricsEnabled_CompletesSuccessfully()
    {
        // Arrange (Given): Cache service ready
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Cache miss occurs
        await recorder.RecordCacheMissAsync("get", "ai_response");

        // Small delay to allow fire-and-forget task to complete
        await Task.Delay(50);

        // Assert (Then): No errors logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordCacheMissAsync_MetricsDisabled_SkipsRecording()
    {
        // Arrange (Given): Metrics disabled
        var recorder = CreateRecorder(metricsEnabled: false);

        // Act (When): Cache miss occurs
        await recorder.RecordCacheMissAsync("get", "ai_response");

        await Task.Delay(50);

        // Assert (Then): No logs (metrics skipped)
        _mockLogger.VerifyNoOtherCalls();
    }

    #endregion

    #region RecordCacheEvictionAsync Tests

    [Fact]
    public async Task RecordCacheEvictionAsync_MetricsEnabled_CompletesSuccessfully()
    {
        // Arrange (Given): Cache service with eviction tracking
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Cache eviction occurs
        await recorder.RecordCacheEvictionAsync("ttl_expired");

        await Task.Delay(50);

        // Assert (Then): No errors logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordCacheEvictionAsync_MetricsDisabled_SkipsRecording()
    {
        // Arrange (Given): Metrics disabled
        var recorder = CreateRecorder(metricsEnabled: false);

        // Act (When): Cache eviction occurs
        await recorder.RecordCacheEvictionAsync("memory_pressure");

        await Task.Delay(50);

        // Assert (Then): No logs (metrics skipped)
        _mockLogger.VerifyNoOtherCalls();
    }

    #endregion

    #region Multiple Operations Tests

    [Fact]
    public async Task MultipleMetricOperations_AllComplete_NoErrors()
    {
        // Arrange (Given): Cache service handling mixed operations
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Multiple metric operations occur
        await recorder.RecordCacheHitAsync("get", "ai_response");
        await recorder.RecordCacheMissAsync("get", "vector_search");
        await recorder.RecordCacheEvictionAsync("ttl_expired");

        // Allow all fire-and-forget tasks to complete
        await Task.Delay(100);

        // Assert (Then): No errors logged for any operation
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task MultipleOperations_MetricsDisabled_AllSkipped()
    {
        // Arrange (Given): Metrics disabled globally
        var recorder = CreateRecorder(metricsEnabled: false);

        // Act (When): Multiple operations occur
        await recorder.RecordCacheHitAsync("get", "ai_response");
        await recorder.RecordCacheMissAsync("get", "vector_search");
        await recorder.RecordCacheEvictionAsync("ttl_expired");

        await Task.Delay(100);

        // Assert (Then): No logs at all
        _mockLogger.VerifyNoOtherCalls();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task RecordCacheHitAsync_EmptyOperation_CompletesSuccessfully()
    {
        // Arrange (Given): Edge case with empty operation string
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Recording with empty operation
        await recorder.RecordCacheHitAsync("", "ai_response");

        await Task.Delay(50);

        // Assert (Then): No errors (metrics handle empty strings gracefully)
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordCacheEvictionAsync_EmptyReason_CompletesSuccessfully()
    {
        // Arrange (Given): Edge case with empty reason
        var recorder = CreateRecorder(metricsEnabled: true);

        // Act (When): Recording eviction with empty reason
        await recorder.RecordCacheEvictionAsync("");

        await Task.Delay(50);

        // Assert (Then): No errors
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion
}