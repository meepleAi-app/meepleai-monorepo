using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Validates LLM output against ADR-051 guardrails T1-T4 (quote cap, no long verbatim,
/// citation required, grounding). ISSUE-524 / M1.2 ships a stub that only enforces the
/// cheap checks (quote word count, presence of citations) — the deeper guardrails
/// (semantic grounding via embedding similarity, long-sequence detection against source)
/// land in M1.3.
/// </summary>
public interface IMechanicOutputValidator
{
    /// <summary>
    /// Validate a raw JSON section output.
    /// </summary>
    /// <param name="section">Target section of this output.</param>
    /// <param name="rawJson">Raw JSON text as returned by the LLM.</param>
    /// <returns>Aggregated validation result — <see cref="MechanicValidationResult.IsValid"/>
    /// is <c>false</c> if any violation is detected.</returns>
    MechanicValidationResult Validate(MechanicSection section, string rawJson);
}

/// <summary>Outcome of validation — either valid or a list of violations.</summary>
public sealed record MechanicValidationResult(bool IsValid, IReadOnlyList<MechanicValidationViolation> Violations)
{
    public static MechanicValidationResult Valid() => new(true, Array.Empty<MechanicValidationViolation>());

    public static MechanicValidationResult Invalid(IReadOnlyList<MechanicValidationViolation> violations) =>
        new(false, violations);
}

public sealed record MechanicValidationViolation(
    string Rule,
    string Message,
    string? Path = null);
