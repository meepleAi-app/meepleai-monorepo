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
    Faq = 5
}
