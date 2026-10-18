using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>Transport VO for the four v3 fields (parser output, review commands).</summary>
public sealed record MechanicClaimStructure(
    MechanicClaimKind Kind,
    MechanicRulePriority Priority,
    IReadOnlyList<Guid> Overrides,
    MechanicTrigger? Trigger)
{
    public static MechanicClaimStructure Default { get; } =
        new(MechanicClaimKind.Rule, MechanicRulePriority.Base, Array.Empty<Guid>(), null);
}
