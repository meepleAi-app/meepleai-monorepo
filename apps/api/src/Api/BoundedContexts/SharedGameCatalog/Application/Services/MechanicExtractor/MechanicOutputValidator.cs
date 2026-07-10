using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
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
        var outcomes = new List<MechanicRuleOutcome>(_guardrails.Count);

        for (var i = 0; i < _guardrails.Count; i++)
        {
            var guardrail = _guardrails[i];
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            var detailed = await guardrail.EvaluateDetailedAsync(context, cancellationToken).ConfigureAwait(false);
            var violations = detailed.Violations;
            stopwatch.Stop();

            var outcomeLabel = violations.Count == 0 ? "pass" : "fail";
            MeepleAiMetrics.MechanicValidatorInvocations.Add(1, new System.Diagnostics.TagList
            {
                { "validator", guardrail.RuleFamily },
                { "outcome", outcomeLabel }
            });

            _logger.LogInformation(
                "Mechanic guardrail {Validator} {Outcome} for analysis {AnalysisId} section {Section} " +
                "(retry {RetryCount}) in {LatencyMs}ms{ViolationRule}",
                guardrail.RuleFamily, outcomeLabel, context.AnalysisId, context.Section, context.RetryCount,
                stopwatch.ElapsedMilliseconds,
                violations.Count == 0 ? string.Empty : $" — {violations[0].Rule}");

            var first = violations.Count > 0 ? violations[0] : null;
            outcomes.Add(new MechanicRuleOutcome(
                Rule: guardrail.RuleFamily,
                Outcome: violations.Count == 0 ? MechanicClaimValidationOutcomes.Pass : MechanicClaimValidationOutcomes.Fail,
                Message: first?.Message,
                Path: first?.Path,
                Score: detailed.Score,
                Violations: violations));

            if (violations.Count > 0)
            {
                foreach (var v in violations)
                {
                    MeepleAiMetrics.MechanicValidatorViolations.Add(1, new System.Diagnostics.TagList
                    {
                        { "rule", v.Rule }
                    });
                }

                // Fail-fast: every guardrail AFTER this one is notRun.
                for (var j = i + 1; j < _guardrails.Count; j++)
                {
                    outcomes.Add(new MechanicRuleOutcome(
                        Rule: _guardrails[j].RuleFamily,
                        Outcome: MechanicClaimValidationOutcomes.NotRun,
                        Message: null, Path: null, Score: null,
                        Violations: Array.Empty<MechanicValidationViolation>()));
                }

                return MechanicValidationResult.Invalid(violations, outcomes); // fail-fast
            }
        }

        return MechanicValidationResult.Valid(outcomes);
    }
}
