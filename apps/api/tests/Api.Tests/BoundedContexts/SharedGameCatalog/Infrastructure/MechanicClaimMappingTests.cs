using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicClaimMappingTests
{
    [Fact]
    public void Entity_RoundTrip_PreservesStructure()
    {
        var target = Guid.NewGuid();
        var cit = MechanicCitation.Create(Guid.NewGuid(), 2, "q", null, 0);
        var claim = MechanicClaim.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Faq, "t", 1, MechanicClaimStatus.Pending,
            null, null, null, new[] { cit },
            kind: MechanicClaimKind.Exception, priority: MechanicRulePriority.Card,
            overrides: new[] { target }, trigger: new MechanicTrigger(null, "muovere", null));

        var entity = MechanicAnalysisRepository.MapClaimToEntity(claim);
        entity.Kind.Should().Be(1);
        entity.Priority.Should().Be(2);
        entity.Overrides.Should().Equal(target);
        entity.Trigger!.Action.Should().Be("muovere");

        var back = MechanicAnalysisRepository.MapClaimToDomain(entity);
        back.Kind.Should().Be(MechanicClaimKind.Exception);
        back.Priority.Should().Be(MechanicRulePriority.Card);
        back.Overrides.Should().Equal(target);
        back.Trigger.Should().Be(claim.Trigger);
    }

    [Fact]
    public void Entity_WithNullJsonColumns_MapsToDefaults()
    {
        var entity = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity
        {
            Id = Guid.NewGuid(), AnalysisId = Guid.NewGuid(), Section = 0, Text = "t", Status = 0,
            Kind = 0, Priority = 0, Overrides = null, Trigger = null,
            Citations = new List<Api.Infrastructure.Entities.SharedGameCatalog.MechanicCitationEntity>
            {
                new() { Id = Guid.NewGuid(), PdfPage = 1, Quote = "q", DisplayOrder = 0 }
            }
        };
        var back = MechanicAnalysisRepository.MapClaimToDomain(entity);
        back.Overrides.Should().BeEmpty();
        back.Trigger.Should().BeNull();
    }
}
