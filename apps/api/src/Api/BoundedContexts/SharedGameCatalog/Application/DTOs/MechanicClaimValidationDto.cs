namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Per-claim guardrail badge outcome (#526 AC-1). Rule ∈ {T1,T2,T3,T4}; Outcome ∈ {pass,fail,notRun}.
/// </summary>
/// <remarks>
/// CORE ITERATION: values are DERIVED, not persisted. Every claim that reaches the review queue
/// passed its section's guardrails by the pipeline pass-invariant (rejection sampling retries a
/// section until its output satisfies T1–T4, else aborts to PartiallyExtracted/Rejected), so all
/// persisted claims are surfaced as <c>pass</c>. FU-1 (#526 follow-up) replaces this with real
/// per-claim outcomes + scores captured at pipeline time; the <c>fail</c>/<c>notRun</c> states and
/// <see cref="Message"/> light up then. #527 snapshots this array into <c>mechanic_cards.content</c>.
/// </remarks>
public sealed record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null);

/// <summary>Derivation of the AC-1 badge families for the core iteration.</summary>
public static class MechanicClaimValidations
{
    /// <summary>Badge families, ordered T1→T4 (T3 = grounding + citation-present).</summary>
    public static readonly IReadOnlyList<string> Families = new[] { "T1", "T2", "T3", "T4" };

    /// <summary>Derived all-pass outcomes (see <see cref="MechanicClaimValidationDto"/> remarks).</summary>
    public static IReadOnlyList<MechanicClaimValidationDto> DerivePass() =>
        Families.Select(f => new MechanicClaimValidationDto(f, "pass")).ToList();
}
