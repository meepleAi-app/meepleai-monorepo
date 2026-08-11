using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Per-claim guardrail badge outcome. Rule ∈ {T1,T2,T3a,T3b,T4}; Outcome ∈ {pass,fail,notRun}.
/// Score is populated only for T3b (grounding cosine); null otherwise (#2782 FU-1).
/// </summary>
/// <remarks>
/// FU-1 (#2782): values are the REAL per-claim outcomes captured at pipeline time and persisted
/// on the claim (<see cref="MechanicClaim.Validations"/> / <c>mechanic_claims.validations</c> jsonb).
/// They are surfaced via <see cref="MechanicClaimValidations.FromDomain"/> (command handlers,
/// projecting the in-memory aggregate) and <see cref="MechanicClaimValidations.FromEntity"/>
/// (the query handler, reading the entity). Pre-FU-1 claims with no persisted validations fall
/// back to the legacy all-pass shape via <see cref="MechanicClaimValidations.DeriveLegacyAllPassFallback"/>.
/// #527 snapshots this array into <c>mechanic_cards.content</c>.
/// </remarks>
public sealed record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null, double? Score = null);

/// <summary>Projection of the FU-1 per-claim guardrail badges to the review DTOs.</summary>
public static class MechanicClaimValidations
{
    /// <summary>The 5 canonical badge rules, ordered T1→T4 with the T3 citation-presence/grounding split.</summary>
    public static readonly IReadOnlyList<string> Families = new[] { "T1", "T2", "T3a", "T3b", "T4" };

    /// <summary>
    /// Map a DOMAIN claim's REAL persisted validations to DTOs (used by the 4 command handlers that
    /// project from the in-memory mutated aggregate). Falls back to the legacy all-pass shape ONLY for
    /// pre-FU-1 claims that carry no validations.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> FromDomain(MechanicClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return Map(claim.Validations);
    }

    /// <summary>
    /// Map an ENTITY claim's REAL persisted validations to DTOs (used by
    /// <c>GetMechanicAnalysisClaimsQueryHandler</c>, which queries <c>MechanicClaimEntity</c> directly).
    /// Reads the <c>validations</c> jsonb column; same legacy-null fallback as <see cref="FromDomain"/>.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> FromEntity(MechanicClaimEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Map(entity.Validations);
    }

    private static IReadOnlyList<MechanicClaimValidationDto> Map(
        IReadOnlyList<MechanicClaimValidation>? validations)
    {
        if (validations is null || validations.Count == 0)
        {
            return DeriveLegacyAllPassFallback();
        }

        return validations
            .Select(v => new MechanicClaimValidationDto(v.Rule, v.Outcome, v.Message, v.Score))
            .ToList();
    }

    /// <summary>
    /// LEGACY-ONLY fallback for pre-FU-1 (#2782) claims with no persisted validations
    /// (<c>validations IS NULL</c>). Returns all-pass across the 5 rules. This is NOT the default path
    /// anymore — real outcomes come from <see cref="FromDomain"/> / <see cref="FromEntity"/>.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> DeriveLegacyAllPassFallback() =>
        Families.Select(f => new MechanicClaimValidationDto(f, "pass")).ToList();
}
