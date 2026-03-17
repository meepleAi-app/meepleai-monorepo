using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityTests
{
    [Fact]
    public void Simple_RequiresRetrieval_ShouldBeFalse()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        Assert.False(sut.RequiresRetrieval);
    }

    [Fact]
    public void Simple_CanDowngradeToFast_ShouldBeTrue()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        Assert.True(sut.CanDowngradeToFast);
    }

    [Fact]
    public void Simple_RequiresMultiStep_ShouldBeFalse()
    {
        var sut = QueryComplexity.Simple("test", 0.9f);

        Assert.False(sut.RequiresMultiStep);
    }

    [Fact]
    public void Moderate_RequiresRetrieval_ShouldBeTrue()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        Assert.True(sut.RequiresRetrieval);
    }

    [Fact]
    public void Moderate_CanDowngradeToFast_ShouldBeFalse()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        Assert.False(sut.CanDowngradeToFast);
    }

    [Fact]
    public void Moderate_RequiresMultiStep_ShouldBeFalse()
    {
        var sut = QueryComplexity.Moderate("test", 0.8f);

        Assert.False(sut.RequiresMultiStep);
    }

    [Fact]
    public void Complex_RequiresRetrieval_ShouldBeTrue()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        Assert.True(sut.RequiresRetrieval);
    }

    [Fact]
    public void Complex_RequiresMultiStep_ShouldBeTrue()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        Assert.True(sut.RequiresMultiStep);
    }

    [Fact]
    public void Complex_CanDowngradeToFast_ShouldBeFalse()
    {
        var sut = QueryComplexity.Complex("test", 0.7f);

        Assert.False(sut.CanDowngradeToFast);
    }

    [Fact]
    public void FactoryMethods_ShouldSetCorrectLevel()
    {
        Assert.Equal(QueryComplexityLevel.Simple, QueryComplexity.Simple("r", 0.5f).Level);
        Assert.Equal(QueryComplexityLevel.Moderate, QueryComplexity.Moderate("r", 0.5f).Level);
        Assert.Equal(QueryComplexityLevel.Complex, QueryComplexity.Complex("r", 0.5f).Level);
    }

    [Fact]
    public void FactoryMethods_ShouldPreserveConfidenceAndReason()
    {
        var sut = QueryComplexity.Moderate("scoring rules needed", 0.87f);

        Assert.Equal(0.87f, sut.Confidence);
        Assert.Equal("scoring rules needed", sut.Reason);
    }

    [Fact]
    public void Record_Equality_SameValues_ShouldBeEqual()
    {
        var a = QueryComplexity.Simple("test", 0.9f);
        var b = QueryComplexity.Simple("test", 0.9f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = QueryComplexity.Simple("test", 0.9f);
        var b = QueryComplexity.Moderate("test", 0.9f);

        Assert.NotEqual(a, b);
    }
}
