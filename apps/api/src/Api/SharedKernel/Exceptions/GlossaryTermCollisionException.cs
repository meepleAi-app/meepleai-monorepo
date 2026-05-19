using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Api.SharedKernel.Exceptions;

/// <summary>
/// Thrown by <c>UpsertGlossaryEntryCommandHandler</c> when the target Italian
/// translation is already in use by another entry on the same campaign.
///
/// Issue #1312 — surfaces the colliding entry's identifier and English source
/// term so the frontend can render the collision banner (state-04) with the
/// `[Sovrascrivi]` / `[Cambia traduzione]` recovery actions.
/// </summary>
public sealed class GlossaryTermCollisionException : HttpException
{
    /// <summary>Id of the entry that already holds the target IT term.</summary>
    public Guid CollidingEntryId { get; }

    /// <summary>English source term of the colliding entry (for UI display).</summary>
    public string CollidingTermEn { get; }

    [SetsRequiredMembers]
    public GlossaryTermCollisionException(Guid collidingEntryId, string collidingTermEn)
        : base(
            StatusCodes.Status409Conflict,
            "glossary_term_collision",
            $"Another glossary entry on this campaign already uses this Italian translation (entry {collidingEntryId}).")
    {
        CollidingEntryId = collidingEntryId;
        CollidingTermEn = collidingTermEn;
    }
}
