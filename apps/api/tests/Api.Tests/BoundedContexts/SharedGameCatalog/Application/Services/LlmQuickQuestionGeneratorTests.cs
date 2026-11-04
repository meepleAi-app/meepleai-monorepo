using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4596 — il generatore dichiara quando il suo risultato è il ripiego.
/// </summary>
/// <remarks>
/// Se l'LLM fallisce (402, 429, rete), risponde con JSON illeggibile o non restituisce domande, il
/// generatore dà cinque domande generiche. Finché nessuno salvava il risultato era innocuo; ora la
/// rigenerazione scrive su <c>quick_questions</c>, quindi chi la chiama deve poter distinguere il
/// ripiego da un risultato vero. Nessuna chiamata a un LLM: <see cref="ILlmService"/> è un doppio.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4596")]
public sealed class LlmQuickQuestionGeneratorTests
{
    private static LlmQuickQuestionGenerator CreateGenerator(Mock<ILlmService> llm) =>
        new(llm.Object, NullLogger<LlmQuickQuestionGenerator>.Instance);

    /// <summary>
    /// <c>GenerateJsonAsync&lt;T&gt;</c> con <c>T</c> privato del generatore: il doppio deserializza il JSON nel tipo
    /// che il generatore chiede, <c>null</c> compreso.
    /// </summary>
    private static Mock<ILlmService> LlmAnswering(string json)
    {
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateJsonAsync<It.IsAnyType>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var type = invocation.Method.GetGenericArguments()[0];
                var parsed = JsonSerializer.Deserialize(json, type, JsonSerializerOptions.Web);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, [parsed])!;
            }));
        return llm;
    }

    private static Mock<ILlmService> LlmFailingWith(Exception exception)
    {
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateJsonAsync<It.IsAnyType>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(_ => throw exception));
        return llm;
    }

    [Fact]
    public async Task GenerateQuestionsAsync_WithAnAnswerFromTheLlm_IsNotAFallback()
    {
        var llm = LlmAnswering("""
            {"questions":[{"text":"Come si prepara la partita?","category":"Setup","confidence":0.9},
                          {"text":"Come si vince?","category":"Winning","confidence":0.8}],
             "overallConfidence":0.85}
            """);

        var result = await CreateGenerator(llm).GenerateQuestionsAsync("Catan", "regole", TestContext.Current.CancellationToken);

        result.IsFallback.Should().BeFalse();
        result.Questions.Select(q => (q.Text, q.Category))
            .Should().Equal(("Come si prepara la partita?", QuestionCategory.Setup), ("Come si vince?", QuestionCategory.Winning));
        result.ConfidenceScore.Should().Be(0.85m);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_WhenTheLlmFails_IsAFallback()
    {
        // 402/429/rete: il servizio LLM lancia.
        var llm = LlmFailingWith(new HttpRequestException("429 Too Many Requests"));

        var result = await CreateGenerator(llm).GenerateQuestionsAsync("Catan", "regole", TestContext.Current.CancellationToken);

        result.IsFallback.Should().BeTrue();
        result.Questions.Should().NotBeEmpty("the fixed generic questions are still returned, marked as a fallback");
    }

    [Fact]
    public async Task GenerateQuestionsAsync_WhenTheAnswerIsUnreadable_IsAFallback()
    {
        // GenerateJsonAsync restituisce null quando il JSON non si legge o la chiamata non ha avuto esito.
        var llm = LlmAnswering("null");

        var result = await CreateGenerator(llm).GenerateQuestionsAsync("Catan", "regole", TestContext.Current.CancellationToken);

        result.IsFallback.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateQuestionsAsync_WhenTheLlmReturnsNoQuestions_IsAFallback()
    {
        var llm = LlmAnswering("""{"questions":[],"overallConfidence":0.1}""");

        var result = await CreateGenerator(llm).GenerateQuestionsAsync("Catan", string.Empty, TestContext.Current.CancellationToken);

        result.IsFallback.Should().BeTrue();
    }
}
