using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Unit coverage for #1729 AI metrics trend handler.
///
/// Validates:
///   - range → window/bucket mapping (Live, 1h, 24h, 7d)
///   - invalid range → ArgumentException (endpoint maps it to 400)
///   - continuous bucket series with zeros for empty intervals
///   - percentile + avg + errorRate calculations on populated buckets
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAiMetricsTrendQueryHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);

    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AiMetricsTrendTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static GetAiMetricsTrendQueryHandler CreateHandler(MeepleAiDbContext db)
    {
        var clock = new FakeTimeProvider(FixedNow);
        return new GetAiMetricsTrendQueryHandler(db, clock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2h")]
    [InlineData("90d")]
    public async Task Handle_WithInvalidRange_ThrowsArgumentException(string range)
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WithInvalidRange_ThrowsArgumentException));
        var handler = CreateHandler(db);

        await FluentActions
            .Invoking(() => handler.Handle(new GetAiMetricsTrendQuery(range), CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithNullRequest_ThrowsArgumentNullException()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WithNullRequest_ThrowsArgumentNullException));
        var handler = CreateHandler(db);

        await FluentActions
            .Invoking(() => handler.Handle(null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    // Series spans [windowStart, windowEnd) where windowEnd = align(now) + bucket,
    // so the current bucket is always included → count = window/bucket + 1.
    [Theory]
    [InlineData("Live", 61, "1m")] // 60 + current 1m bucket
    [InlineData("1h", 61, "1m")]
    [InlineData("24h", 97, "15m")] // 96 + current
    [InlineData("7d", 169, "1h")]  // 168 + current
    public async Task Handle_ReturnsContinuousBucketSeriesWithCorrectSize(string range, int expectedDatapoints, string expectedLabel)
    {
        await using var db = CreateInMemoryDb(
            $"{nameof(Handle_ReturnsContinuousBucketSeriesWithCorrectSize)}_{range}");
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiMetricsTrendQuery(range), CancellationToken.None);

        result.Should().NotBeNull();
        result.Range.Should().Be(range);
        result.BucketSize.Should().Be(expectedLabel);
        // No data in DB → all buckets surface as zeros (continuous series)
        result.Datapoints.Should().HaveCount(expectedDatapoints);
        result.Datapoints.Should().OnlyContain(d =>
            d.RequestCount == 0 && d.AvgLatencyMs == 0 && d.P50LatencyMs == 0 && d.P95LatencyMs == 0 && d.ErrorRate == 0);
    }

    [Fact]
    public async Task Handle_AggregatesPercentilesAndErrorRateForPopulatedBucket()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_AggregatesPercentilesAndErrorRateForPopulatedBucket));

        // Seed 20 rows into the SAME 1m bucket at 11:30:00 UTC (within the 1h "Live" window).
        var bucketStart = new DateTime(2026, 5, 31, 11, 30, 0, DateTimeKind.Utc);
        var latencies = new[] { 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250, 260, 270, 280, 290 };
        for (var i = 0; i < latencies.Length; i++)
        {
            // 18 success + 2 error → errorRate = 0.10
            db.AiRequestLogs.Add(new AiRequestLogEntity
            {
                Id = Guid.NewGuid(),
                Endpoint = "qa-stream",
                LatencyMs = latencies[i],
                Status = i < 2 ? "Error" : "Success",
                CreatedAt = bucketStart.AddSeconds(i * 2),
                TokenCount = 100,
                PromptTokens = 80,
                CompletionTokens = 20,
            });
        }
        db.SaveChanges();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new GetAiMetricsTrendQuery("Live"), CancellationToken.None);

        var bucket = result.Datapoints.Should()
            .Contain(d => d.Timestamp == bucketStart)
            .Which;

        bucket.RequestCount.Should().Be(20);
        bucket.AvgLatencyMs.Should().Be((int)Math.Round(latencies.Average())); // 195
        // Nearest-rank percentile: q=0.50 → rank=10 → sorted[9] = 190
        bucket.P50LatencyMs.Should().Be(190);
        // q=0.95 → rank=19 → sorted[18] = 280
        bucket.P95LatencyMs.Should().Be(280);
        // 2 errors / 20 = 0.10
        bucket.ErrorRate.Should().BeApproximately(0.10, 0.0001);
    }

    [Fact]
    public async Task Handle_IgnoresRowsOutsideTheTimeWindow()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_IgnoresRowsOutsideTheTimeWindow));

        // Row 2 hours ago → outside the "Live"/"1h" window (which is the last 1h)
        db.AiRequestLogs.Add(new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            Endpoint = "qa",
            LatencyMs = 999,
            Status = "Success",
            CreatedAt = FixedNow.UtcDateTime.AddHours(-2),
        });
        db.SaveChanges();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new GetAiMetricsTrendQuery("1h"), CancellationToken.None);

        result.Datapoints.Should().OnlyContain(d => d.RequestCount == 0);
    }
}
