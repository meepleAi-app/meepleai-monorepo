using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Misurato con M.E.AI 10.6.0: un parametro obbligatorio mancante fa fallire il binding con
/// ArgumentException prima dello strumento. Senza FunctionInvoker il modello riceve «Error: Function
/// failed.», e dopo tre errori consecutivi il ciclo rilancia l'eccezione.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4239")]
public sealed class AssistantToolInvocationTests
{
    [Fact]
    public async Task Turn_InvalidToolArguments_BecomeATypedErrorForTheModel()
    {
        var requests = new List<LlmChatRequest>();
        var responses = new Queue<LlmCompletionResult>(
        [
            LlmCompletionResult.CreateSuccess(string.Empty) with { ToolCalls = [new LlmToolCall("call_1", "proponi_punteggio", "{}")] },
            LlmCompletionResult.CreateSuccess("Per quale giocatore?"),
        ]);
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => requests.Add(r))
            .ReturnsAsync(() => responses.Dequeue());
        var meter = new ChatTurnMeter();
        var tool = AIFunctionFactory.Create((string giocatore, int punti) => "ok", "proponi_punteggio", "Propone");
        var factory = new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());

        var response = await factory.Create(new AssistantModelSettings("deepseek-flash", null, 0.2, 600, 4, null, RequestSource.SessionAssistant), meter)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "segna")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Per quale giocatore?");
        meter.InvalidArguments.Should().Be(1);
        meter.ToolCalls.Should().Equal("proponi_punteggio");
        requests[1].Messages[^1].Role.Should().Be(LlmChatRoles.Tool);
        requests[1].Messages[^1].Content.Should().Contain("argomenti_non_validi");
    }

    /// <summary>
    /// Un errore DENTRO lo strumento non è un errore di argomenti: il modello non deve ritentare gli
    /// stessi argomenti, e la metrica «argomenti non validi» dello spike resta pulita (spec §6.3).
    /// </summary>
    [Fact]
    public async Task Turn_ToolBodyException_IsNotCountedAsInvalidArguments()
    {
        var responses = new Queue<LlmCompletionResult>(
        [
            LlmCompletionResult.CreateSuccess(string.Empty) with
            {
                ToolCalls = [new LlmToolCall("call_1", "proponi_punteggio", """{"giocatore":"Marco","punti":5}""")],
            },
            LlmCompletionResult.CreateSuccess("Non riesco a segnarlo."),
        ]);
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => responses.Dequeue());
        var meter = new ChatTurnMeter();
        var tool = AIFunctionFactory.Create(
            new Func<string, int, string>((giocatore, punti) => throw new ArgumentOutOfRangeException(nameof(punti))),
            "proponi_punteggio", "Propone");
        var factory = new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());

        var response = await factory.Create(new AssistantModelSettings("deepseek-flash", null, 0.2, 600, 4, null, RequestSource.SessionAssistant), meter)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "Marco fa 5")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Non riesco a segnarlo.");
        meter.InvalidArguments.Should().Be(0);
        meter.ToolCalls.Should().Equal("proponi_punteggio");
    }

    /// <summary>
    /// Fissa la semantica di MaxToolIterations (misurata con M.E.AI 10.6.0): N giri con strumenti, poi
    /// UNA chiamata finale senza strumenti che obbliga il modello a rispondere. Un turno costa quindi al
    /// più N+1 chiamate. Se un aggiornamento della libreria la cambia, questo test diventa rosso.
    /// </summary>
    [Fact]
    public async Task Turn_ModelThatNeverStops_MakesNToolRoundsPlusOneFinalCallWithoutTools()
    {
        var requests = new List<LlmChatRequest>();
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => requests.Add(r))
            .ReturnsAsync(() => LlmCompletionResult.CreateSuccess(string.Empty) with
            {
                ToolCalls = [new LlmToolCall($"call_{requests.Count}", "stato_partita", "{}")],
            });
        var tool = AIFunctionFactory.Create(() => "{}", "stato_partita", "Legge lo stato");
        var factory = new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());

        await factory.Create(new AssistantModelSettings("deepseek-flash", null, 0.2, 600, MaxToolIterations: 2, null, RequestSource.SessionAssistant), new ChatTurnMeter())
            .GetResponseAsync([new ChatMessage(ChatRole.User, "a chi tocca?")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        requests.Should().HaveCount(3);
        requests.Take(2).Should().OnlyContain(r => r.Tools.Count == 1);
        requests[2].Tools.Should().BeEmpty("l'ultima chiamata obbliga il modello a rispondere");
    }
}
