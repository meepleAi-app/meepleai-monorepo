using System.Globalization;

namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class EvalRateLimitedException : Exception
{
    public TimeSpan RetryAfter { get; }

    public EvalRateLimitedException(TimeSpan retryAfter)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "Eval rate limited; retry after {0:F0}s",
            retryAfter.TotalSeconds))
    {
        RetryAfter = retryAfter;
    }
}
