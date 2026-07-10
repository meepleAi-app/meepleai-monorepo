using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.DTOs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationsMappingTests
{
    // MechanicClaim.CreateWithId enforces ADR-051 T3 (>=1 citation), so build a valid one.
    private static MechanicClaim CreateClaimWithCitation(Guid claimId)
    {
        var citation = MechanicCitation.Create(claimId, pdfPage: 1, quote: "cited quote", chunkId: null, displayOrder: 0);
        return MechanicClaim.CreateWithId(claimId, Guid.NewGuid(), MechanicSection.Summary,
            "text", 0, new[] { citation }, sourceAnchor: "$.summary");
    }

    [Fact]
    public void FromDomain_MapsRealValidations_IncludingScore()
    {
        var claim = CreateClaimWithCitation(Guid.NewGuid());
        claim.AttachValidations(new[]
        {
            new MechanicClaimValidation("T2", "fail", "long verbatim", null),
            new MechanicClaimValidation("T3b", "pass", null, 0.83),
        });

        var dtos = MechanicClaimValidations.FromDomain(claim);

        dtos.Single(d => d.Rule == "T2").Outcome.Should().Be("fail");
        dtos.Single(d => d.Rule == "T3b").Score.Should().Be(0.83);
    }

    [Fact]
    public void FromDomain_FallsBackToLegacyAllPass_WhenClaimHasNoValidations()
    {
        var claim = CreateClaimWithCitation(Guid.NewGuid());

        var dtos = MechanicClaimValidations.FromDomain(claim);

        dtos.Select(d => d.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
        dtos.Should().OnlyContain(d => d.Outcome == "pass");
    }

    [Fact]
    public void FromEntity_MapsJsonbColumn_AndFallsBackWhenNull()
    {
        var withData = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity
        {
            Validations = new List<MechanicClaimValidation>
            {
                new("T3b", "pass", null, 0.71),
            }
        };
        MechanicClaimValidations.FromEntity(withData).Single(d => d.Rule == "T3b").Score.Should().Be(0.71);

        var legacy = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity { Validations = null };
        MechanicClaimValidations.FromEntity(legacy).Select(d => d.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
    }
}
