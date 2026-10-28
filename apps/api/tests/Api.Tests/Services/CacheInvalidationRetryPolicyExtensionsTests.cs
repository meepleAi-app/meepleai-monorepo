using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #4659 — <see cref="CacheInvalidationRetryPolicyExtensions.TryExecuteAsync"/>, la politica d'errore comune di
/// chi toglie voci dalla cache dopo una scrittura già salvata: un passo che si arrende si registra e non risale.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CacheInvalidationRetryPolicyExtensionsTests
{
    private readonly RecordingLogger _logger = new();

    [Fact]
    public async Task WhenTheStepSucceeds_ReturnsTrue_AndLogsNothing()
    {
        var ran = false;

        var ok = await new PassthroughRetryPolicy().TryExecuteAsync(
            _ =>
            {
                ran = true;
                return ValueTask.CompletedTask;
            },
            "shared-games.list", "search-games", _logger, CancellationToken.None);

        ok.Should().BeTrue();
        ran.Should().BeTrue();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheStepGivesUp_ReturnsFalse_DoesNotThrow_AndLogsAnErrorWithTheTagAndTheOperation()
    {
        var policy = GivingUpRetryPolicy.Always();

        var ok = await policy.TryExecuteAsync(
            _ => ValueTask.CompletedTask, "shared-games.detail", "shared-game:42", _logger, CancellationToken.None);

        ok.Should().BeFalse();
        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeOfType<InvalidOperationException>();
        entry.Message.Should().Contain("shared-game:42").And.Contain("shared-games.detail");
    }

    [Fact]
    public async Task ThePolicyReceivesTheOperationNameAndTheToken()
    {
        var policyMock = new Mock<ICacheInvalidationRetryPolicy>();
        using var source = new CancellationTokenSource();

        await policyMock.Object.TryExecuteAsync(
            _ => ValueTask.CompletedTask, "shared-games.rails", "catalog", _logger, source.Token);

        policyMock.Verify(
            p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), "shared-games.rails", source.Token),
            Times.Once);
    }

    /// <summary>
    /// L'annullamento chiesto dal chiamante non è un guasto della cache e non si inghiotte: un job fermato da
    /// Quartz deve risultare annullato, non «riuscito con un errore nel log».
    /// </summary>
    [Fact]
    public async Task WhenTheCallerCancelled_TheCancellationGoesUp_AndNothingIsLogged()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var policyMock = new Mock<ICacheInvalidationRetryPolicy>();
        policyMock
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromCanceled(source.Token));

        var act = () => policyMock.Object.TryExecuteAsync(
            _ => ValueTask.CompletedTask, "shared-games.list", "search-games", _logger, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().BeEmpty();
    }

    /// <summary>Un timeout interno (annullamento NON chiesto dal chiamante) è un guasto come un altro.</summary>
    [Fact]
    public async Task AnOperationCanceledNotAskedByTheCaller_IsAFailureLikeAnyOther()
    {
        var policyMock = new Mock<ICacheInvalidationRetryPolicy>();
        policyMock
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new OperationCanceledException("redis timeout")));

        var ok = await policyMock.Object.TryExecuteAsync(
            _ => ValueTask.CompletedTask, "shared-games.list", "search-games", _logger, CancellationToken.None);

        ok.Should().BeFalse();
        _logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Error);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
