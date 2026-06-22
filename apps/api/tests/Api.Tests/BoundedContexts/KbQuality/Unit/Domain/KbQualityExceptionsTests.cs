using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class KbQualityExceptionsTests
{
    [Fact]
    public void CostCapExceeded_CarriesEstimatedAndRemaining()
    {
        var ex = new CostCapExceededException(estimated: 0.60m, remaining: 0.50m);

        ex.EstimatedCostUsd.Should().Be(0.60m);
        ex.RemainingBudgetUsd.Should().Be(0.50m);
        ex.Message.Should().Contain("0.60").And.Contain("0.50");
    }

    [Fact]
    public void EvalRateLimited_CarriesRetryAfter()
    {
        var retryAfter = TimeSpan.FromMinutes(7);
        var ex = new EvalRateLimitedException(retryAfter);

        ex.RetryAfter.Should().Be(retryAfter);
    }

    [Fact]
    public void InvalidGoldsetVersion_CarriesRequestedAndAvailable()
    {
        var ex = new InvalidGoldsetVersionException("manual-v1", ["auto-v1"]);

        ex.RequestedVersion.Should().Be("manual-v1");
        ex.AvailableVersions.Should().BeEquivalentTo(["auto-v1"]);
    }
}
