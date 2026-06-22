using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="WikidataCoverDeadLetterRetentionJob"/>. Issue #1823
/// Wave 3 retention sweep (ADR DEC-3j).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverDeadLetterRetentionJobTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 11, 3, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(FixedNow, TimeSpan.Zero));

    private WikidataCoverDeadLetterRetentionJob Sut() => new(
        Mock.Of<IServiceProvider>(),
        _time,
        NullLogger<WikidataCoverDeadLetterRetentionJob>.Instance);

    [Fact]
    public async Task RunOnceAsync_PassesCutoff7DaysBeforeNow()
    {
        DateTime? captured = null;
        _attempts.Setup(r => r.DeleteDeadLetteredOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync(0);

        await Sut().RunOnceAsync(_attempts.Object, default);

        captured.Should().Be(FixedNow.AddDays(-7),
            "DEC-3j retention window is exactly 7 days — the cutoff must be now-7d UTC");
    }

    [Fact]
    public async Task RunOnceAsync_ReturnsDeletedCountFromRepo()
    {
        _attempts.Setup(r => r.DeleteDeadLetteredOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var deleted = await Sut().RunOnceAsync(_attempts.Object, default);

        deleted.Should().Be(42);
    }

    [Fact]
    public async Task RunOnceAsync_NoRowsDeleted_StillCompletesSuccessfully()
    {
        _attempts.Setup(r => r.DeleteDeadLetteredOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var deleted = await Sut().RunOnceAsync(_attempts.Object, default);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task RunOnceAsync_HonoursCancellation()
    {
        _attempts.Setup(r => r.DeleteDeadLetteredOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await Sut().RunOnceAsync(_attempts.Object, new CancellationToken(true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void RetentionWindow_IsExactly7Days()
    {
        WikidataCoverDeadLetterRetentionJob.RetentionWindow
            .Should().Be(TimeSpan.FromDays(7),
                "DEC-3j locks the dead-letter retention window at 7 days — any change requires a deliberate ADR update");
    }
}
