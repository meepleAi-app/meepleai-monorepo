using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>ADR-097: GatewayChatClient traduce i tipi M.E.AI da e verso la chat con strumenti del gateway.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4239")]
public sealed class GatewayChatClientTests
{
    private static readonly AssistantModelSettings Settings =
        new("deepseek-flash", null, 0.2, 600, 4, Guid.Parse("6f2c0c5e-3c1a-4c3e-9a7e-1f6a2b9d4e10"), RequestSource.SessionAssistant);

    [Fact]
    public async Task GetResponse_TranslatesHistoryToolsAndToolResults()
    {
        LlmChatRequest? sent = null;
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Tocca a Luca.", new LlmUsage(10, 3, 13)));
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());
        var tool = AIFunctionFactory.Create(() => "{}", "stato_partita", "Legge lo stato");

        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "Sei l'assistente."),
            new ChatMessage(ChatRole.User, "a chi tocca?"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_1", "stato_partita", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", JsonDocument.Parse("""{"turno":"Luca"}""").RootElement)]),
        ], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Tocca a Luca.");
        response.Usage!.InputTokenCount.Should().Be(10);
        sent!.Tools.Should().ContainSingle().Which.Name.Should().Be("stato_partita");
        sent.Source.Should().Be(RequestSource.SessionAssistant);
        sent.Messages.Select(m => m.Role).Should().Equal("system", "user", "assistant", "tool");
        sent.Messages[2].ToolCalls!.Single().Id.Should().Be("call_1");
        sent.Messages[3].ToolCallId.Should().Be("call_1");
        sent.Messages[3].ToolName.Should().Be("stato_partita");
        sent.Messages[3].Content.Should().Be("""{"turno":"Luca"}""");
    }

    /// <summary>
    /// Ollama non dà id alle chiamate: ogni giro ricomincia da call_0. Il nome dello strumento sul
    /// risultato deve essere quello della chiamata del suo giro, non del primo.
    /// </summary>
    [Fact]
    public async Task GetResponse_RepeatedCallIdsAcrossRounds_KeepEachResultsToolName()
    {
        LlmChatRequest? sent = null;
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Fatto."));
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "passa il turno"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_0", "stato_partita", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_0", "{}")]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_0", "proponi_turno", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_0", "{}")]),
        ], null, TestContext.Current.CancellationToken);

        sent!.Messages[2].ToolName.Should().Be("stato_partita");
        sent.Messages[4].ToolName.Should().Be("proponi_turno");
    }

    [Fact]
    public async Task GetResponse_ToolCalls_BecomeFunctionCallContents()
    {
        var gateway = Gateway(LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall("call_7", "proponi_punteggio", """{"giocatore":"Marco","punti":5}""")],
        });
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Marco fa 5")], null, TestContext.Current.CancellationToken);

        var call = response.Messages.Single().Contents.OfType<FunctionCallContent>().Single();
        call.CallId.Should().Be("call_7");
        call.Arguments!["giocatore"]!.ToString().Should().Be("Marco");
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
    }

    [Fact]
    public async Task GetResponse_InvalidArgumentsJson_DoesNotThrow()
    {
        var gateway = Gateway(LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall("call_1", "proponi_punteggio", "{giocatore: Marco")],
        });
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], null, TestContext.Current.CancellationToken);

        response.Messages.Single().Contents.OfType<FunctionCallContent>().Single().Arguments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_GatewayFailure_ThrowsWithTheErrorCode_AndIsMetered()
    {
        var refused = LlmCompletionResult.CreateFailure("cap") with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = LlmBudgetAdmission.DailyBudgetErrorCode },
        };
        var meter = new ChatTurnMeter();
        var client = new GatewayChatClient(Gateway(refused).Object, Settings, meter);

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], null, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AssistantGatewayException>()).Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        meter.RoundTrips.Should().Be(1);
    }

    private static Mock<ILlmService> Gateway(LlmCompletionResult result)
    {
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return gateway;
    }
}
