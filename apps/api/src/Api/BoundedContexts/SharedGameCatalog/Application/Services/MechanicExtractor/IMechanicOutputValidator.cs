using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Validates LLM output against ADR-051 guardrails. ISSUE-525 / M1.3 evaluates a chain of
/// injectable <see cref="IMechanicGuardrail"/> (T1 quote cap, T2 long-verbatim, T3 citation
/// present/grounded, T4 page+substring) cheapest-first / fail-fast.
/// </summary>
public interface IMechanicOutputValidator
{
    /// <summary>
    /// Validate a parsed section output against the guardrail chain.
    /// </summary>
    /// <param name="context">Section output + source chunk pool + page count + options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated validation result — <see cref="MechanicValidationResult.IsValid"/>
    /// is <c>false</c> if any guardrail produces violations (fail-fast at the first one).</returns>
    Task<MechanicValidationResult> ValidateAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of validation — either valid or a list of violations, PLUS the per-guardrail
/// rule outcomes accumulated during the fail-fast pass (#2782 D1).</summary>
public sealed record MechanicValidationResult(
    bool IsValid,
    IReadOnlyList<MechanicValidationViolation> Violations,
    IReadOnlyList<MechanicRuleOutcome> RuleOutcomes)
{
    public static MechanicValidationResult Valid() =>
        new(true, Array.Empty<MechanicValidationViolation>(), Array.Empty<MechanicRuleOutcome>());

    public static MechanicValidationResult Valid(IReadOnlyList<MechanicRuleOutcome> ruleOutcomes) =>
        new(true, Array.Empty<MechanicValidationViolation>(), ruleOutcomes);

    public static MechanicValidationResult Invalid(IReadOnlyList<MechanicValidationViolation> violations) =>
        new(false, violations, Array.Empty<MechanicRuleOutcome>());

    public static MechanicValidationResult Invalid(
        IReadOnlyList<MechanicValidationViolation> violations,
        IReadOnlyList<MechanicRuleOutcome> ruleOutcomes) =>
        new(false, violations, ruleOutcomes);
}

/// <summary>One guardrail's outcome captured during the fail-fast pass (#2782 D1). Rule is the
/// guardrail RuleFamily (T1/T2/T3a/T3b/T4). Outcome ∈ {pass,fail,notRun} — notRun = the guardrail
/// was downstream of the first failing guardrail and never ran.</summary>
public sealed record MechanicRuleOutcome(
    string Rule,
    string Outcome,
    string? Message,
    string? Path,
    double? Score,
    IReadOnlyList<MechanicValidationViolation> Violations,
    // #2811: per-claim grounding cosine keyed by the claim object's JSONPath (T3b only; null
    // otherwise). CorrelateValidations attaches each claim's own score from here instead of the
    // section-wide min carried by Score.
    IReadOnlyDictionary<string, double>? ClaimScores = null);

public sealed record MechanicValidationViolation(
    string Rule,
    string Message,
    string? Path = null);
