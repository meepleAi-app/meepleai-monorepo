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
}
