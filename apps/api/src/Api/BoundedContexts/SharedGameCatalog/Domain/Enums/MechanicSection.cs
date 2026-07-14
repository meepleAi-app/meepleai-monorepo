namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Logical section of the rulebook a MechanicClaim belongs to.
/// Used both for UI grouping (reference cards) and prompt routing during extraction.
/// </summary>
public enum MechanicSection
{
    Summary = 0,
    Mechanics = 1,
    Victory = 2,
    Resources = 3,
    Phases = 4,
    Faq = 5,
    // v1.1.0 (#539 follow-up): values are persisted as int — APPEND only, never renumber.
    // Display order is controlled separately (Setup/Components render high in the card, not at
    // the tail); see the FE section-order map + the card grouping.
    Setup = 6,
    Components = 7,
    EndgameScoring = 8
}
