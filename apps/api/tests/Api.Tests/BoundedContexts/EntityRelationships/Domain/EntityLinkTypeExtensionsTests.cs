using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.EntityRelationships.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class EntityLinkTypeExtensionsTests
{
    [Theory]
    [InlineData(EntityLinkType.CompanionTo, true)]
    [InlineData(EntityLinkType.RelatedTo, true)]
    [InlineData(EntityLinkType.CollaboratesWith, true)]
    [InlineData(EntityLinkType.ExpansionOf, false)]
    [InlineData(EntityLinkType.SequelOf, false)]
    [InlineData(EntityLinkType.Reimplements, false)]
    [InlineData(EntityLinkType.PartOf, false)]
    [InlineData(EntityLinkType.SpecializedBy, false)]
    public void IsBidirectional_ReturnsCorrectValue(EntityLinkType linkType, bool expected)
    {
        linkType.IsBidirectional().Should().Be(expected);
    }

    [Theory]
    [InlineData(EntityLinkType.ExpansionOf, "expansion_of")]
    [InlineData(EntityLinkType.SequelOf, "sequel_of")]
    [InlineData(EntityLinkType.Reimplements, "reimplements")]
    [InlineData(EntityLinkType.CompanionTo, "companion_to")]
    [InlineData(EntityLinkType.RelatedTo, "related_to")]
    [InlineData(EntityLinkType.PartOf, "part_of")]
    [InlineData(EntityLinkType.CollaboratesWith, "collaborates_with")]
    [InlineData(EntityLinkType.SpecializedBy, "specialized_by")]
    public void ToSnakeCase_ReturnsCorrectString(EntityLinkType linkType, string expected)
    {
        linkType.ToSnakeCase().Should().Be(expected);
    }

    [Fact]
    public void ToSnakeCase_UnknownValue_ThrowsArgumentOutOfRangeException()
    {
        var unknown = (EntityLinkType)999;
        ((Action)(() => unknown.ToSnakeCase())).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsBidirectional_ExactlyThreeBilateralTypes()
    {
        var allTypes = Enum.GetValues<EntityLinkType>();
        var bilateralCount = allTypes.Count(t => t.IsBidirectional());
        bilateralCount.Should().Be(3);
    }
}
