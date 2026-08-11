using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationTests
{
    [Fact]
    public void Record_CarriesRuleOutcomeMessageScore()
    {
        var v = new MechanicClaimValidation("T3b", MechanicClaimValidationOutcomes.Pass, Message: null, Score: 0.87);

        v.Rule.Should().Be("T3b");
        v.Outcome.Should().Be("pass");
        v.Score.Should().Be(0.87);
    }

    [Fact]
    public void Outcomes_ExposeCanonicalStrings()
    {
        MechanicClaimValidationOutcomes.Pass.Should().Be("pass");
        MechanicClaimValidationOutcomes.Fail.Should().Be("fail");
        MechanicClaimValidationOutcomes.NotRun.Should().Be("notRun");
    }
}
