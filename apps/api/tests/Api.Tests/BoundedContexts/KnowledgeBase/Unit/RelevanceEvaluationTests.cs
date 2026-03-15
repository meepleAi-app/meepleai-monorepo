using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RelevanceEvaluationTests
{
    [Fact]
    public void Correct_UseRetrievedDocuments_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.95f, "High scores");

        Assert.True(sut.UseRetrievedDocuments);
    }

    [Fact]
    public void Correct_ShouldRequery_ShouldBeFalse()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.95f, "High scores");

        Assert.False(sut.ShouldRequery);
    }

    [Fact]
    public void Ambiguous_UseRetrievedDocuments_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.7f, "Partially relevant");

        Assert.True(sut.UseRetrievedDocuments);
    }

    [Fact]
    public void Ambiguous_ShouldRequery_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.7f, "Partially relevant");

        Assert.True(sut.ShouldRequery);
    }

    [Fact]
    public void Incorrect_UseRetrievedDocuments_ShouldBeFalse()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "Not relevant");

        Assert.False(sut.UseRetrievedDocuments);
    }

    [Fact]
    public void Incorrect_ShouldRequery_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "Not relevant");

        Assert.True(sut.ShouldRequery);
    }

    [Fact]
    public void Record_ShouldPreserveAllProperties()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.72f, "borderline");

        Assert.Equal(RelevanceVerdict.Ambiguous, sut.Verdict);
        Assert.Equal(0.72f, sut.Confidence);
        Assert.Equal("borderline", sut.Reason);
    }

    [Fact]
    public void Record_Equality_SameValues_ShouldBeEqual()
    {
        var a = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");
        var b = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_Equality_DifferentVerdict_ShouldNotBeEqual()
    {
        var a = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");
        var b = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "test");

        Assert.NotEqual(a, b);
    }
}
