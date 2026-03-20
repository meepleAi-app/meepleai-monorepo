using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityTests
{
    [Fact]
    public void Simple_RequiresRetrieval_ShouldBeFalse()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        sut.RequiresRetrieval.Should().BeFalse();
    }

    [Fact]
    public void Simple_CanDowngradeToFast_ShouldBeTrue()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        sut.CanDowngradeToFast.Should().BeTrue();
    }

    [Fact]
    public void Simple_RequiresMultiStep_ShouldBeFalse()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        sut.RequiresMultiStep.Should().BeFalse();
    }

    [Fact]
    public void Moderate_RequiresRetrieval_ShouldBeTrue()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        sut.RequiresRetrieval.Should().BeTrue();
    }

    [Fact]
    public void Moderate_CanDowngradeToFast_ShouldBeFalse()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        sut.CanDowngradeToFast.Should().BeFalse();
    }

    [Fact]
    public void Moderate_RequiresMultiStep_ShouldBeFalse()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        sut.RequiresMultiStep.Should().BeFalse();
    }

    [Fact]
    public void Complex_RequiresRetrieval_ShouldBeTrue()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        sut.RequiresRetrieval.Should().BeTrue();
    }

    [Fact]
    public void Complex_RequiresMultiStep_ShouldBeTrue()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        sut.RequiresMultiStep.Should().BeTrue();
    }

    [Fact]
    public void Complex_CanDowngradeToFast_ShouldBeFalse()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        sut.CanDowngradeToFast.Should().BeFalse();
    }

    [Fact]
    public void FactoryMethods_ShouldSetCorrectLevel()
    {
        QueryComplexity.Simple("r", 0.5f).Level.Should().Be(QueryComplexityLevel.Simple);
        QueryComplexity.Moderate("r", 0.5f).Level.Should().Be(QueryComplexityLevel.Moderate);
        QueryComplexity.Complex("r", 0.5f).Level.Should().Be(QueryComplexityLevel.Complex);
    }

    [Fact]
    public void FactoryMethods_ShouldPreserveConfidenceAndReason()
    {
        var sut = QueryComplexity.Moderate("scoring rules needed", 0.87f);

        sut.Confidence.Should().Be(0.87f);
        sut.Reason.Should().Be("scoring rules needed");
    }

    [Fact]
    public void Record_Equality_SameValues_ShouldBeEqual()
    {
        var a = QueryComplexity.Simple("test", 0.9f);
        var b = QueryComplexity.Simple("test", 0.9f);

        b.Should().Be(a);
    }

    [Fact]
    public void Record_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = QueryComplexity.Simple("test", 0.9f);
        var b = QueryComplexity.Moderate("test", 0.9f);

        b.Should().NotBe(a);
    }
}
