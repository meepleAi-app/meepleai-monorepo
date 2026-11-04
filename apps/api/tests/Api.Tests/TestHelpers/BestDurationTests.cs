using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.TestHelpers;

/// <summary>
/// #4274 — the contract of <see cref="BestDuration"/>, on scripted durations: what one
/// sample measures besides the code (the cold start, a stalled runner) must not decide the
/// outcome, and code that is slow every time must still be reported.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class BestDurationTests
{
    private static Func<TimeSpan> Scripted(params double[] milliseconds)
    {
        var calls = 0;
        return () => TimeSpan.FromMilliseconds(milliseconds[calls++]);
    }

    [Fact]
    public void Of_ColdFirstCall_IsNotMeasured()
    {
        var measure = Scripted(500, 3, 4, 3);

        BestDuration.Of(measure, samples: 3).Should().Be(TimeSpan.FromMilliseconds(3));
    }

    [Fact]
    public void Of_OneStalledSample_DoesNotDecide()
    {
        var measure = Scripted(20, 4, 90, 3, 250);

        BestDuration.Of(measure, samples: 4).Should().Be(TimeSpan.FromMilliseconds(3));
    }

    [Fact]
    public void Of_CodeThatIsSlowEveryTime_IsStillReported()
    {
        var measure = Scripted(60, 61, 60, 63);

        BestDuration.Of(measure, samples: 3).Should().Be(TimeSpan.FromMilliseconds(60));
    }

    [Fact]
    public void Of_RunsTheWarmUpAndThenExactlyTheSamplesAsked()
    {
        var calls = 0;

        BestDuration.Of(
            () =>
            {
                calls++;
                return TimeSpan.FromMilliseconds(1);
            },
            samples: 5);

        calls.Should().Be(6);
    }

    [Fact]
    public void Of_NoSamples_IsRefused()
    {
        var act = () => BestDuration.Of(() => TimeSpan.Zero, samples: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
