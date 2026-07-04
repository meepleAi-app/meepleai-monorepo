using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// ME-M1.3 (#525) guardrail observability (AC-7). Counters for guardrail evaluations and
/// violations, tagged by validator family / rule.
/// </summary>
internal static partial class MeepleAiMetrics
{
    /// <summary>Guardrail evaluations, tagged {validator, outcome=pass|fail}.</summary>
    public static readonly Counter<long> MechanicValidatorInvocations =
        Meter.CreateCounter<long>(
            "mechanic_validator_invocations_total",
            description: "Mechanic guardrail evaluations by validator family and outcome.");

    /// <summary>Guardrail violations, tagged {rule}.</summary>
    public static readonly Counter<long> MechanicValidatorViolations =
        Meter.CreateCounter<long>(
            "mechanic_validator_violations_total",
            description: "Mechanic guardrail violations by rule.");
}
