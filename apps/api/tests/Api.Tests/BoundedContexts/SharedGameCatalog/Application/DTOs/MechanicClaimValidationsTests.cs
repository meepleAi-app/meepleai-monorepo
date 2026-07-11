using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.DTOs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationsTests
{
    [Fact]
    public void DeriveLegacyAllPassFallback_ReturnsFivePassBadges_T1ToT4WithT3Split()
    {
        var validations = MechanicClaimValidations.DeriveLegacyAllPassFallback();

        validations.Select(v => v.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
        validations.Should().OnlyContain(v => v.Outcome == "pass" && v.Message == null);
    }
}
