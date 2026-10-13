namespace Api.Tests.TestHelpers;

/// <summary>
/// The duration to assert on when a test guards how fast a piece of code is (#4274).
/// <para>
/// One sample does not measure the code alone: the first call of the process also pays the
/// cold start (JIT, static initialisation), and any call can be stretched by whatever else
/// the runner is doing — on the fast gate, the rest of the suite. A threshold on a single
/// sample therefore races the suite, and a wider margin only postpones the next red.
/// </para>
/// <para>
/// The fastest of several samples, after a call that is not measured, is a property of the
/// code: no scheduling stall can make it smaller, and code that became slow is slow on
/// every sample, so a real regression still fails.
/// </para>
/// </summary>
internal static class BestDuration
{
    /// <param name="measure">Runs the code once and returns how long it took.</param>
    /// <param name="samples">How many measured runs follow the warm-up.</param>
    public static TimeSpan Of(Func<TimeSpan> measure, int samples = 10)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);

        measure();

        var best = TimeSpan.MaxValue;
        for (var i = 0; i < samples; i++)
        {
            var duration = measure();
            if (duration < best)
            {
                best = duration;
            }
        }

        return best;
    }
}
