using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Optional rule trigger (when a rule applies) exposed on the wire.
/// </summary>
/// <param name="Phase">Game phase in which the rule applies.</param>
/// <param name="Action">Player action that activates the rule.</param>
/// <param name="Component">Game component involved.</param>
public sealed record MechanicTriggerDto(string? Phase, string? Action, string? Component)
{
    /// <summary>Maps to the domain value object (normalised; <c>null</c> when all parts are empty).</summary>
    public MechanicTrigger? ToDomain() => MechanicTrigger.Create(Phase, Action, Component);

    /// <summary>Maps a domain trigger to its DTO.</summary>
    public static MechanicTriggerDto? FromDomain(MechanicTrigger? t) =>
        t is null ? null : new MechanicTriggerDto(t.Phase, t.Action, t.Component);
}

/// <summary>
/// Reviewer-editable structure of a claim: kind, priority, overridden claims and trigger.
/// </summary>
/// <param name="Kind">Kind of claim.</param>
/// <param name="Priority">Rule priority tier.</param>
/// <param name="Overrides">Ids of the claims this one overrides (same analysis).</param>
/// <param name="Trigger">Optional trigger.</param>
public sealed record MechanicClaimStructureDto(
    MechanicClaimKind Kind,
    MechanicRulePriority Priority,
    IReadOnlyList<Guid> Overrides,
    MechanicTriggerDto? Trigger)
{
    /// <summary>Maps to the domain structure value object.</summary>
    public MechanicClaimStructure ToDomain() =>
        new(Kind, Priority, Overrides ?? Array.Empty<Guid>(), Trigger?.ToDomain());
}
