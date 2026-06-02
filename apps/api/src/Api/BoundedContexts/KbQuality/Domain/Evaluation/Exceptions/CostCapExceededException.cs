using System.Globalization;

namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class CostCapExceededException : Exception
{
    public decimal EstimatedCostUsd { get; }
    public decimal RemainingBudgetUsd { get; }

    public CostCapExceededException(decimal estimated, decimal remaining)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "Eval cost {0:F2} USD exceeds remaining budget {1:F2} USD",
            estimated, remaining))
    {
        EstimatedCostUsd = estimated;
        RemainingBudgetUsd = remaining;
    }
}
