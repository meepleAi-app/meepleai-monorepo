using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class RelationshipSeederTests
{
    [Theory]
    [InlineData("Worker Placement", "worker-placement")]
    [InlineData("Dice Rolling", "dice-rolling")]
    [InlineData("Area Control / Area Influence", "area-control-area-influence")]
    [InlineData("  Trick-taking  ", "trick-taking")]
    [InlineData("Rock\u2013Paper\u2013Scissors", "rock-paper-scissors")]
    public void GenerateSlug_ConvertsNameToSlug(string name, string expected)
    {
        var result = RelationshipSeeder.GenerateSlug(name);

        result.Should().Be(expected);
    }
}
