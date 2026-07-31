using System;
using System.Collections.Generic;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class MechanicSectionRouterTests
{
    public static IEnumerable<object[]> MappingCases() => new[]
    {
        // single-flag mappings (spec §8)
        new object[] { GameBookRole.Setup, new[] { MechanicSection.Setup, MechanicSection.Components } },
        new object[] { GameBookRole.Tutorial, new[] { MechanicSection.Summary, MechanicSection.Setup } },
        new object[] { GameBookRole.RulesReference, new[] { MechanicSection.Mechanics, MechanicSection.Phases, MechanicSection.Resources } },
        new object[] { GameBookRole.Encounter, new[] { MechanicSection.Phases, MechanicSection.Mechanics } },
        // roles with no mechanic-section coverage → empty (RAG-only)
        new object[] { GameBookRole.Narrative, Array.Empty<MechanicSection>() },
        new object[] { GameBookRole.Lore, Array.Empty<MechanicSection>() },
        new object[] { GameBookRole.None, Array.Empty<MechanicSection>() },
    };

    [Theory]
    [MemberData(nameof(MappingCases))]
    public void Route_MapsRoleToExpectedSections(GameBookRole role, MechanicSection[] expected)
    {
        MechanicSectionRouter.Route(role).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Route_UnionsMultipleFlags_AndDeduplicates()
    {
        // Tutorial (Summary, Setup) | Setup (Setup, Components) → union deduped
        var result = MechanicSectionRouter.Route(GameBookRole.Tutorial | GameBookRole.Setup);

        result.Should().BeEquivalentTo(new[]
        {
            MechanicSection.Summary,
            MechanicSection.Setup,
            MechanicSection.Components,
        });
        result.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Route_ReturnsSectionsInCanonicalCardDisplayOrder()
    {
        // Canonical order (GetPublishedMechanicCardByGameQueryHandler display order):
        // Summary, Setup, Components, Mechanics, Resources, Phases, Victory, EndgameScoring, Faq
        var result = MechanicSectionRouter.Route(
            GameBookRole.Tutorial | GameBookRole.Setup | GameBookRole.RulesReference);

        result.Should().ContainInOrder(
            MechanicSection.Summary,
            MechanicSection.Setup,
            MechanicSection.Components,
            MechanicSection.Mechanics,
            MechanicSection.Resources,
            MechanicSection.Phases);
    }
}
