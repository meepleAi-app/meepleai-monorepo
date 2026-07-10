namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// A real per-claim guardrail outcome captured at pipeline time (#2782 FU-1).
/// Rule ∈ {T1,T2,T3a,T3b,T4}; Outcome ∈ {pass,fail,notRun}. Score is populated only for T3b
/// (grounding cosine); null for all other rules.
/// </summary>
public sealed record MechanicClaimValidation(
    string Rule,
    string Outcome,
    string? Message = null,
    double? Score = null);

/// <summary>Canonical outcome strings for <see cref="MechanicClaimValidation.Outcome"/>.</summary>
public static class MechanicClaimValidationOutcomes
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string NotRun = "notRun";
}
