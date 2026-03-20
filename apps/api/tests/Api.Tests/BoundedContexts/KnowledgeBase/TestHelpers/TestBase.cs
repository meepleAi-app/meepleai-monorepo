using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

/// <summary>
/// Base class for all KnowledgeBase bounded context tests.
/// Provides common setup, teardown, and assertion helpers.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected ITestOutputHelper Output { get; }
    protected ILoggerFactory LoggerFactory { get; }
    protected TimeProvider TimeProvider { get; }

    private readonly List<IDisposable> _disposables = new();
    private bool _disposed;

    protected TestBase(ITestOutputHelper? output = null)
    {
        Output = output ?? new NullTestOutputHelper();
        LoggerFactory = CreateLoggerFactory();
        TimeProvider = TimeProvider.System;
    }

    /// <summary>
    /// Creates a logger factory with output redirection for debugging.
    /// </summary>
    private ILoggerFactory CreateLoggerFactory()
    {
        if (Output is NullTestOutputHelper)
            return NullLoggerFactory.Instance;

        // For integration tests with ITestOutputHelper
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(Output));
        });
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    protected ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates a mock logger for the specified type.
    /// </summary>
    protected Mock<ILogger<T>> CreateMockLogger<T>() => new Mock<ILogger<T>>();

    /// <summary>
    /// Registers a disposable resource to be cleaned up.
    /// </summary>
    protected T RegisterDisposable<T>(T disposable) where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Asserts that the action throws a domain exception with the expected message.
    /// </summary>
    protected static void AssertDomainException(Action action, string expectedMessagePart)
    {
        Action act = action;
        var ex = act.Should().Throw<Api.SharedKernel.Domain.Exceptions.DomainException>().Which;
        ex.Message.Should().ContainEquivalentOf(expectedMessagePart);
    }

    /// <summary>
    /// Asserts that the action throws a validation exception with the expected message.
    /// </summary>
    protected static void AssertValidationException(Action action, string expectedMessagePart)
    {
        Action act = action;
        var ex = act.Should().Throw<Api.SharedKernel.Domain.Exceptions.ValidationException>().Which;
        ex.Message.Should().ContainEquivalentOf(expectedMessagePart);
    }

    /// <summary>
    /// Asserts that two DateTimes are approximately equal (within tolerance).
    /// </summary>
    protected static void AssertDateTimeApproximate(DateTime expected, DateTime actual, TimeSpan? tolerance = null)
    {
        var allowedDifference = tolerance ?? TestConstants.Timing.VeryShortTimeout;
        var difference = Math.Abs((expected - actual).TotalSeconds);
        (difference <= allowedDifference.TotalSeconds).Should().BeTrue($"Expected {expected:O}, but got {actual:O} (difference: {difference}s)");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
            _disposables.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Null implementation of ITestOutputHelper for tests that don't need output.
    /// </summary>
    private class NullTestOutputHelper : ITestOutputHelper
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }

    /// <summary>
    /// Xunit logger provider for redirecting logs to test output.
    /// </summary>
    private sealed class XunitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new XunitLogger(output, categoryName);
        public void Dispose() { }
    }

    /// <summary>
    /// Xunit logger implementation.
    /// </summary>
    private class XunitLogger(ITestOutputHelper output, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            output.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
            if (exception != null)
                output.WriteLine(exception.ToString());
        }
    }
}

