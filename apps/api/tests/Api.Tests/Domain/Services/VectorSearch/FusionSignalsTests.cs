using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.Services.VectorSearch;

public class FusionSignalsTests
{
    [Fact]
    public void ComputeRoleMatchBoost_WhenRolesIntersect_ReturnsBoost()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Setup | GameBookRole.RulesReference)
            .Should().Be(0.15f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenHintIsNone_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.None, GameBookRole.Setup).Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenNoIntersection_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Narrative).Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithFewerThanTwoPointers_ReturnsZero()
    {
        FusionSignals.ComputeLegendPenaltyFactor("See page 12 for details.").Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithDenseCrossReferences_ReturnsPenaltyInRange()
    {
        var legendy = "See p. 3. See p. 5. See p. 7. See p. 9.";
        var factor = FusionSignals.ComputeLegendPenaltyFactor(legendy);
        factor.Should().BeInRange(0f, 0.5f);
        factor.Should().BeGreaterThan(0f);
    }
}
