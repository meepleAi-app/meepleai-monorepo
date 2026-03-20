using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RelevanceEvaluationTests
{
    [Fact]
    public void Correct_UseRetrievedDocuments_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.95f, "High scores");

        sut.UseRetrievedDocuments.Should().BeTrue();
    }

    [Fact]
    public void Correct_ShouldRequery_ShouldBeFalse()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.95f, "High scores");

        sut.ShouldRequery.Should().BeFalse();
    }

    [Fact]
    public void Ambiguous_UseRetrievedDocuments_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.7f, "Partially relevant");

        sut.UseRetrievedDocuments.Should().BeTrue();
    }

    [Fact]
    public void Ambiguous_ShouldRequery_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.7f, "Partially relevant");

        sut.ShouldRequery.Should().BeTrue();
    }

    [Fact]
    public void Incorrect_UseRetrievedDocuments_ShouldBeFalse()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "Not relevant");

        sut.UseRetrievedDocuments.Should().BeFalse();
    }

    [Fact]
    public void Incorrect_ShouldRequery_ShouldBeTrue()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "Not relevant");

        sut.ShouldRequery.Should().BeTrue();
    }

    [Fact]
    public void Record_ShouldPreserveAllProperties()
    {
        var sut = new RelevanceEvaluation(RelevanceVerdict.Ambiguous, 0.72f, "borderline");

        sut.Verdict.Should().Be(RelevanceVerdict.Ambiguous);
        sut.Confidence.Should().Be(0.72f);
        sut.Reason.Should().Be("borderline");
    }

    [Fact]
    public void Record_Equality_SameValues_ShouldBeEqual()
    {
        var a = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");
        var b = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");

        b.Should().Be(a);
    }

    [Fact]
    public void Record_Equality_DifferentVerdict_ShouldNotBeEqual()
    {
        var a = new RelevanceEvaluation(RelevanceVerdict.Correct, 0.9f, "test");
        var b = new RelevanceEvaluation(RelevanceVerdict.Incorrect, 0.9f, "test");

        b.Should().NotBe(a);
    }
}
