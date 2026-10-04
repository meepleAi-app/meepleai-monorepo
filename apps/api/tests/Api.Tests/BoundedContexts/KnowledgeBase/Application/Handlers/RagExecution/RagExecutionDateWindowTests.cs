using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagExecution;

/// <summary>
/// The <c>dateFrom</c>/<c>dateTo</c> window of the two <c>/api/v1/admin/rag-executions</c>
/// endpoints, which arrives as bare <c>YYYY-MM-DD</c> query-string values.
/// </summary>
/// <remarks>
/// <para>
/// Both assertions below reproduce a defect measured on 2026-10-04 against the local stack, as
/// superadmin. The request the inspector sends on open —
/// <c>?skip=0&amp;take=20&amp;dateFrom=2026-10-04&amp;dateTo=2026-10-04</c> — answered 400
/// "Invalid request parameters", thrown inside Npgsql
/// (<c>DateTimeConverterResolver`1.Get</c>) because minimal-API binding hands the handler a
/// <see cref="DateTimeKind.Unspecified"/> value that cannot be written to a <c>timestamptz</c>
/// column. <c>?skip=0&amp;take=20</c> answered 200, and <c>dateFrom=2026-10-03&amp;dateTo=2026-10-04</c>
/// also answered 400, so equal dates were never the trigger.
/// </para>
/// <para>
/// <see cref="DateTimeKind"/> is asserted rather than the Npgsql rejection itself: these are unit
/// tests over a mocked repository, so the kind is the observable that the database constraint reads.
/// The end-of-day assertion covers the second half of the defect, which the kind fix alone leaves in
/// place — an inclusive bound at midnight excludes the day it names.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagExecutionHistory")]
public sealed class RagExecutionDateWindowTests
{
    private static readonly DateTime BareDate = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IRagExecutionRepository> _repository = new();

    public RagExecutionDateWindowTests()
    {
        _repository
            .Setup(r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.RagExecution>(), 0));

        _repository
            .Setup(r => r.GetStatsAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagExecutionStats(0, 0, 0, 0, 0, 0));
    }

    // ---------------------------------------------------------------- list endpoint

    [Fact]
    public async Task ListHandler_SameDayWindow_PassesUtcBoundsCoveringThatWholeDay()
    {
        var handler = new GetRagExecutionsQueryHandler(_repository.Object);
        var query = new GetRagExecutionsQuery(From: BareDate, To: BareDate);

        await handler.Handle(query, TestContext.Current.CancellationToken);

        var (from, to) = CapturedPagedWindow();

        from!.Value.Kind.Should().Be(DateTimeKind.Utc);
        to!.Value.Kind.Should().Be(DateTimeKind.Utc);
        from.Value.Should().Be(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc));
        to.Value.Should().Be(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1));
        (to.Value - from.Value).Should().BeGreaterThan(TimeSpan.FromHours(23));
    }

    [Fact]
    public async Task ListHandler_MultiDayWindow_PassesUtcBounds()
    {
        var handler = new GetRagExecutionsQueryHandler(_repository.Object);
        var query = new GetRagExecutionsQuery(
            From: new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Unspecified),
            To: BareDate);

        await handler.Handle(query, TestContext.Current.CancellationToken);

        var (from, to) = CapturedPagedWindow();

        from!.Value.Kind.Should().Be(DateTimeKind.Utc);
        to!.Value.Kind.Should().Be(DateTimeKind.Utc);
        from.Value.Should().Be(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ListHandler_NoWindow_PassesNoBounds()
    {
        var handler = new GetRagExecutionsQueryHandler(_repository.Object);

        await handler.Handle(new GetRagExecutionsQuery(), TestContext.Current.CancellationToken);

        var (from, to) = CapturedPagedWindow();

        from.Should().BeNull();
        to.Should().BeNull();
    }

    [Fact]
    public async Task ListHandler_ExplicitTimeOnUpperBound_IsNotWidened()
    {
        var handler = new GetRagExecutionsQueryHandler(_repository.Object);
        var upper = new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Unspecified);

        await handler.Handle(
            new GetRagExecutionsQuery(From: BareDate, To: upper),
            TestContext.Current.CancellationToken);

        var (_, to) = CapturedPagedWindow();

        to!.Value.Should().Be(new DateTime(2026, 10, 4, 9, 30, 0, DateTimeKind.Utc));
        to.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ---------------------------------------------------------------- stats endpoint

    [Fact]
    public async Task StatsHandler_SameDayWindow_PassesUtcBoundsCoveringThatWholeDay()
    {
        var handler = new GetRagExecutionStatsQueryHandler(_repository.Object);

        await handler.Handle(
            new GetRagExecutionStatsQuery(From: BareDate, To: BareDate),
            TestContext.Current.CancellationToken);

        var (from, to) = CapturedStatsWindow();

        from!.Value.Kind.Should().Be(DateTimeKind.Utc);
        to!.Value.Kind.Should().Be(DateTimeKind.Utc);
        from.Value.Should().Be(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc));
        to.Value.Should().Be(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1));
    }

    [Fact]
    public async Task StatsHandler_NoWindow_PassesNoBounds()
    {
        var handler = new GetRagExecutionStatsQueryHandler(_repository.Object);

        await handler.Handle(new GetRagExecutionStatsQuery(), TestContext.Current.CancellationToken);

        var (from, to) = CapturedStatsWindow();

        from.Should().BeNull();
        to.Should().BeNull();
    }

    // ---------------------------------------------------------------- window semantics

    [Fact]
    public void EndInclusive_AlreadyUtc_KeepsTheInstantAndTheKind()
    {
        var utcNoon = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        RagExecutionDateWindow.EndInclusive(utcNoon).Should().Be(utcNoon);
    }

    [Fact]
    public void EndInclusive_LastRepresentableDay_ClampsInsteadOfThrowing()
    {
        var lastDay = new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);

        var end = RagExecutionDateWindow.EndInclusive(lastDay);

        end!.Value.Kind.Should().Be(DateTimeKind.Utc);
        end.Value.Should().Be(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-10-04", "2026-10-04", true)]  // the inspector's default: one day, valid
    [InlineData("2026-10-03", "2026-10-04", true)]
    [InlineData("2026-10-05", "2026-10-04", false)] // strictly inverted
    [InlineData("2026-10-04", null, true)]
    [InlineData(null, "2026-10-04", true)]
    [InlineData(null, null, true)]
    public void IsOrdered_AcceptsEqualDatesAndRejectsOnlyInvertedWindows(
        string? from, string? to, bool expected)
    {
        RagExecutionDateWindow.IsOrdered(Parse(from), Parse(to)).Should().Be(expected);
    }

    // ---------------------------------------------------------------- helpers

    private static DateTime? Parse(string? value) =>
        value is null
            ? null
            : DateTime.SpecifyKind(DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Unspecified);

    private (DateTime? From, DateTime? To) CapturedPagedWindow()
    {
        var call = _repository.Invocations.Single(i => i.Method.Name == nameof(IRagExecutionRepository.GetPagedAsync));
        return ((DateTime?)call.Arguments[7], (DateTime?)call.Arguments[8]);
    }

    private (DateTime? From, DateTime? To) CapturedStatsWindow()
    {
        var call = _repository.Invocations.Single(i => i.Method.Name == nameof(IRagExecutionRepository.GetStatsAsync));
        return ((DateTime?)call.Arguments[0], (DateTime?)call.Arguments[1]);
    }
}
