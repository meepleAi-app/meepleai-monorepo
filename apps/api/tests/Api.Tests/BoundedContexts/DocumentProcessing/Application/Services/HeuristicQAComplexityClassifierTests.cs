using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for HeuristicQAComplexityClassifier (Phase 2 Task 2.4).
/// Validates keyword-heuristic Q&amp;A complexity classification for the libro game assistant context.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class HeuristicQAComplexityClassifierTests
{
    private readonly HeuristicQAComplexityClassifier _sut = new();

    [Fact]
    public void Classify_SynthesisKeyword_ReturnsSynthesis()
    {
        var result = _sut.Classify("Qual è la differenza tra esplorazione e combattimento?");
        result.Level.Should().Be(QAComplexityLevel.Synthesis);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.80f);
    }

    [Fact]
    public void Classify_MultiStepKeyword_ReturnsMultiStep()
    {
        var result = _sut.Classify("Se uso il modulo Stealth e poi esploro, cosa succede?");
        result.Level.Should().Be(QAComplexityLevel.MultiStep);
    }

    [Fact]
    public void Classify_SimpleFactualPattern_ReturnsSimple()
    {
        var result = _sut.Classify("Quanti dadi tira il mago?");
        result.Level.Should().Be(QAComplexityLevel.Simple);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.85f);
    }

    [Fact]
    public void Classify_EmptyInput_ReturnsSimpleWithoutThrowing()
    {
        var result = _sut.Classify("");
        result.Level.Should().Be(QAComplexityLevel.Simple);
        result.Confidence.Should().Be(1.0f);
        result.Reason.Should().Be("empty_input");
    }

    [Fact]
    public void Classify_NullInput_ReturnsSimpleWithoutThrowing()
    {
        var result = _sut.Classify(null!);
        result.Level.Should().Be(QAComplexityLevel.Simple);
    }

    [Fact]
    public void Classify_SameInput_ReturnsSameResult()
    {
        var question = "Come funziona il combat?";
        var r1 = _sut.Classify(question);
        var r2 = _sut.Classify(question);
        r1.Should().BeEquivalentTo(r2);
    }

    [Fact]
    public void Classify_MixedLanguage_DoesNotThrow()
    {
        var act = () => _sut.Classify("Come si usa il Stealth module durante l'exploration?");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("compare X with Y")]
    [InlineData("differenza tra A e B")]
    [InlineData("X versus Y")]
    public void Classify_ComparisonQuestions_ReturnSynthesis(string question)
    {
        var result = _sut.Classify(question);
        result.Level.Should().Be(QAComplexityLevel.Synthesis);
    }
}
