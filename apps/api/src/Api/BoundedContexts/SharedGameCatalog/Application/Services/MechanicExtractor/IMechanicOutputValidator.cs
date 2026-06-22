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
