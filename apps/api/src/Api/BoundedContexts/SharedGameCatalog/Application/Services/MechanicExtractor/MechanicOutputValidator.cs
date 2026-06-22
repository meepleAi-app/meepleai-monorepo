using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.Observability;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// M1.3 (#525) guardrail chain orchestrator. Runs the registered <see cref="IMechanicGuardrail"/>
/// instances ordered by <see cref="IMechanicGuardrail.Order"/> (cheapest-first) and returns at the
/// first guardrail that produces violations (fail-fast). Emits per-guardrail metrics + structured
/// logs (AC-7).
/// </summary>
internal sealed class MechanicOutputValidator : IMechanicOutputValidator
{
    private readonly IReadOnlyList<IMechanicGuardrail> _guardrails;
    private readonly ILogger<MechanicOutputValidator> _logger;

    public MechanicOutputValidator(
        IEnumerable<IMechanicGuardrail> guardrails,
        ILogger<MechanicOutputValidator> logger)
    {
        _guardrails = guardrails.OrderBy(g => g.Order).ToList();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicValidationResult> ValidateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        foreach (var guardrail in _guardrails)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            var violations = await guardrail.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var outcome = violations.Count == 0 ? "pass" : "fail";
            MeepleAiMetrics.MechanicValidatorInvocations.Add(1, new System.Diagnostics.TagList
            {
                { "validator", guardrail.RuleFamily },
                { "outcome", outcome }
            });

            _logger.LogInformation(
                "Mechanic guardrail {Validator} {Outcome} for analysis {AnalysisId} section {Section} " +
                "(retry {RetryCount}) in {LatencyMs}ms{ViolationRule}",
                guardrail.RuleFamily, outcome, context.AnalysisId, context.Section, context.RetryCount,
                stopwatch.ElapsedMilliseconds,
                violations.Count == 0 ? string.Empty : $" — {violations[0].Rule}");

            if (violations.Count > 0)
            {
                foreach (var v in violations)
                {
                    MeepleAiMetrics.MechanicValidatorViolations.Add(1, new System.Diagnostics.TagList
                    {
                        { "rule", v.Rule }
                    });
                }
                return MechanicValidationResult.Invalid(violations); // fail-fast
            }
        }

        return MechanicValidationResult.Valid();
    }
}
