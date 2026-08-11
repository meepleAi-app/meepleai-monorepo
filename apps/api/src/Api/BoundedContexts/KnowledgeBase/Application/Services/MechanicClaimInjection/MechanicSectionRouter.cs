using System;
using System.Collections.Generic;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

/// <summary>
/// Maps a query's <see cref="GameBookRole"/> intent hint to the set of
/// <see cref="MechanicSection"/> whose approved claims should be injected into the
/// RAG prompt. Pure, stateless. See spec §8 (2026-07-30-mechanic-card-rag-integration).
/// </summary>
internal static class MechanicSectionRouter
{
    // Canonical read/display order — mirrors GetPublishedMechanicCardByGameQueryHandler
    // so injected claim sections read in a stable, card-consistent sequence.
    private static readonly MechanicSection[] CanonicalOrder =
    {
        MechanicSection.Summary,
        MechanicSection.Setup,
        MechanicSection.Components,
        MechanicSection.Mechanics,
        MechanicSection.Resources,
        MechanicSection.Phases,
        MechanicSection.Victory,
        MechanicSection.EndgameScoring,
        MechanicSection.Faq,
    };

    public static IReadOnlyList<MechanicSection> Route(GameBookRole role)
    {
        var set = new HashSet<MechanicSection>();

        if (role.HasFlag(GameBookRole.Setup))
        {
            set.Add(MechanicSection.Setup);
            set.Add(MechanicSection.Components);
        }

        if (role.HasFlag(GameBookRole.Tutorial))
        {
            set.Add(MechanicSection.Summary);
            set.Add(MechanicSection.Setup);
        }

        if (role.HasFlag(GameBookRole.RulesReference))
        {
            set.Add(MechanicSection.Mechanics);
            set.Add(MechanicSection.Phases);
            set.Add(MechanicSection.Resources);
        }

        if (role.HasFlag(GameBookRole.Encounter))
        {
            set.Add(MechanicSection.Phases);
            set.Add(MechanicSection.Mechanics);
        }

        // Narrative / Lore / None → no mechanic-section coverage (RAG-only).

        if (set.Count == 0)
        {
            return Array.Empty<MechanicSection>();
        }

        var result = new List<MechanicSection>(set.Count);
        foreach (var section in CanonicalOrder)
        {
            if (set.Contains(section))
            {
                result.Add(section);
            }
        }

        return result;
    }
}
