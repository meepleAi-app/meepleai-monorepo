using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityClassifierTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly QueryComplexityClassifier _sut;

    public QueryComplexityClassifierTests()
    {
        _sut = new QueryComplexityClassifier(
            _llmServiceMock.Object,
            NullLogger<QueryComplexityClassifier>.Instance);
    }

    // --- Heuristic: Simple queries ---

    [Theory]
    [InlineData("what is Catan?")]
    [InlineData("define worker placement")]
    [InlineData("who is the designer?")]
    [InlineData("how many players?")]
    [InlineData("when was it released?")]
    [InlineData("where is it published?")]
    public async Task ClassifyAsync_ShortSimpleQuery_ShouldReturnSimple(string query)
    {
        var result = await _sut.ClassifyAsync(query);

        result.Level.Should().Be(QueryComplexityLevel.Simple);
        (result.Confidence >= 0.9f).Should().BeTrue();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Heuristic: Complex queries ---

    [Theory]
    [InlineData("compare Catan vs Carcassonne and recommend the best strategy")]
    [InlineData("if I buy this expansion, can I use it with the base game after the first round?")]
    [InlineData("should i get the expansion versus the standalone, pros and cons")]
    public async Task ClassifyAsync_MultipleComplexIndicators_ShouldReturnComplex(string query)
    {
        var result = await _sut.ClassifyAsync(query);

        result.Level.Should().Be(QueryComplexityLevel.Complex);
        (result.Confidence >= 0.8f).Should().BeTrue();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- LLM fallback ---

    [Fact]
    public async Task ClassifyAsync_AmbiguousQuery_ShouldCallLlm()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Moderate", 0.75f, "Needs rules context"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Moderate);
        result.Confidence.Should().Be(0.75f);
        result.Reason.Should().Be("Needs rules context");
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsSimple_ShouldMapCorrectly()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Simple", 0.92f, "Direct fact"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Simple);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsComplex_ShouldMapCorrectly()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Complex", 0.88f, "Multi-step reasoning"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Complex);
    }

    // --- Error fallback ---

    [Fact]
    public async Task ClassifyAsync_LlmThrows_ShouldReturnModerateFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Moderate);
        result.Confidence.Should().Be(0.5f);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsNull_ShouldReturnModerateFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComplexityClassification?)null);

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Moderate);
    }

    // --- Edge cases ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ClassifyAsync_EmptyOrNullQuery_ShouldReturnSimple(string? query)
    {
        var result = await _sut.ClassifyAsync(query!);

        result.Level.Should().Be(QueryComplexityLevel.Simple);
        result.Confidence.Should().Be(1.0f);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsUnknownLevel_ShouldDefaultToModerate()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Unknown", 0.6f, "Unrecognized"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Level.Should().Be(QueryComplexityLevel.Moderate);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsOutOfRangeConfidence_ShouldClamp()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Simple", 1.5f, "Overconfident"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        result.Confidence.Should().Be(1.0f);
    }

    // --- Heuristic unit tests (static method) ---

    [Fact]
    public void ClassifyByHeuristic_LongSimplePrefix_ShouldReturnNull()
    {
        // "what is" prefix but too many words (>6)
        var result = QueryComplexityClassifier.ClassifyByHeuristic(
            "what is the maximum number of players allowed in this game?");

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyByHeuristic_SingleComplexIndicator_ShouldReturnNull()
    {
        // Only 1 complex indicator, needs 2+
        var result = QueryComplexityClassifier.ClassifyByHeuristic("compare these two games");

        result.Should().BeNull();
    }

    // --- Italian heuristics: Simple ---

    [Theory]
    [InlineData("cos'è Catan?")]
    [InlineData("quanti giocatori?")]
    [InlineData("quando esce?")]
    [InlineData("dove si compra?")]
    [InlineData("chi è l'autore?")]
    public async Task ClassifyAsync_ItalianShortSimpleQuery_ShouldReturnSimple(string query)
    {
        var result = await _sut.ClassifyAsync(query);
        result.Level.Should().Be(QueryComplexityLevel.Simple);
        (result.Confidence >= 0.9f).Should().BeTrue();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Italian heuristics: Complex (interaction patterns) ---

    [Theory]
    [InlineData("se gioco una carta e si attiva un effetto durante il mio turno, come si risolve l'interazione?")]
    [InlineData("confronta le strategie e dimmi quale conviene se ho poche risorse")]
    [InlineData("quando pesco una carta che innesca un effetto combinato con il turno extra, in che ordine si risolvono?")]
    public async Task ClassifyAsync_ItalianComplexInteraction_ShouldReturnComplex(string query)
    {
        var result = await _sut.ClassifyAsync(query);
        result.Level.Should().Be(QueryComplexityLevel.Complex);
        (result.Confidence >= 0.8f).Should().BeTrue();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- English interaction patterns (new) ---

    [Theory]
    [InlineData("if I play a card that triggers an effect during combat, how does the interaction resolve with sustain damage?")]
    [InlineData("when I activate this ability and it triggers another effect simultaneously, what is the resolution order?")]
    public async Task ClassifyAsync_EnglishInteractionPatterns_ShouldReturnComplex(string query)
    {
        var result = await _sut.ClassifyAsync(query);
        result.Level.Should().Be(QueryComplexityLevel.Complex);
        (result.Confidence >= 0.8f).Should().BeTrue();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Structural complexity (language-agnostic) ---

    [Theory]
    [InlineData("Se uso la carta azione, pesco 2 carte, una innesca un effetto immediato che modifica i punti, e nel frattempo il turno extra si attiva, come funziona tutto?")]
    [InlineData("If I use the action card, draw 2 cards, one triggers an immediate effect that modifies points, and meanwhile the extra turn activates, how does everything work?")]
    public async Task ClassifyAsync_LongMultiClauseQuery_ShouldReturnComplex(string query)
    {
        var result = await _sut.ClassifyAsync(query);
        result.Level.Should().Be(QueryComplexityLevel.Complex);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Multilingual LLM prompt verification ---

    [Fact]
    public async Task ClassifyAsync_ItalianAmbiguousQuery_ShouldCallLlmWithMultilingualPrompt()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.Is<string>(s => s.Contains("any language", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Complex", 0.85f, "Multi-step rule interaction"));

        var result = await _sut.ClassifyAsync("Come funziona il punteggio con l'espansione attiva?");

        result.Level.Should().Be(QueryComplexityLevel.Complex);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<ComplexityClassification>(
                It.Is<string>(s => s.Contains("any language", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
