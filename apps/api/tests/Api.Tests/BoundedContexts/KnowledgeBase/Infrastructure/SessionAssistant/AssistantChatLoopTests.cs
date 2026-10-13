using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097, spec §7.2: ogni giro del ciclo con strumenti passa dal tetto e dal registro costi, e un
/// giro rifiutato non arriva al client.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4239")]
public sealed class AssistantChatLoopTests
{
    private const string Model = "deepseek-flash";
    private readonly Mock<ILlmClient> _client = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ICircuitBreakerRegistry> _breakers = new();

    public AssistantChatLoopTests()
    {
        _client.Setup(c => c.ProviderName).Returns("DeepSeek");
        _client.Setup(c => c.SupportsModel(Model)).Returns(true);
        _breakers.Setup(b => b.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        _client.SetupSequence(c => c.ChatAsync(Model, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCall("call_1"))
            .ReturnsAsync(ToolCall("call_2"))
            .ReturnsAsync(Text("Tocca a Luca."));
    }

    [Fact]
    public async Task Turn_WithTwoToolRounds_AdmitsAndLogsEveryRoundTrip()
    {
        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Allowed);
        var invocations = 0;
        var tool = AIFunctionFactory.Create(() => { invocations++; return "{\"turno\":\"Luca\"}"; }, "stato_partita", "Legge lo stato");
        var meter = new ChatTurnMeter();

        var response = await Factory().Create(Settings, meter)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "a chi tocca?")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Tocca a Luca.");
        invocations.Should().Be(2);
        meter.RoundTrips.Should().Be(3);
        _gate.Verify(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.Source == RequestSource.SessionAssistant), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _costService.Verify(c => c.LogSuccessAsync(It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(),
            RequestSource.SessionAssistant, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Turn_CapReachedOnSecondRoundTrip_StopsWithoutCallingTheClientAgain()
    {
        _gate.SetupSequence(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Allowed)
            .ReturnsAsync(new LlmBudgetAdmission
            {
                // ErrorCode è calcolato da Rejection: DailyCapReached → DailyBudgetErrorCode.
                ReservationId = Guid.Empty, Rejection = LlmBudgetRejection.DailyCapReached, BudgetClass = LlmBudgetClass.Answer,
                EstimatedCostUsd = 0.004m, Message = "cap",
            });
        var tool = AIFunctionFactory.Create(() => "{}", "stato_partita", "Legge lo stato");

        var act = () => Factory().Create(Settings, new ChatTurnMeter())
            .GetResponseAsync([new ChatMessage(ChatRole.User, "a chi tocca?")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AssistantGatewayException>()).Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _client.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Chain_HasFunctionInvocationWithoutDetailedErrors_AndNoResponseCache()
    {
        var chat = Factory().Create(Settings, new ChatTurnMeter());

        var invoking = chat.GetService<FunctionInvokingChatClient>();
        invoking.Should().NotBeNull();
        invoking!.MaximumIterationsPerRequest.Should().Be(Settings.MaxToolIterations);
        invoking.IncludeDetailedErrors.Should().BeFalse();
        chat.GetService<DistributedCachingChatClient>().Should().BeNull("una risposta in cache salterebbe gli strumenti (spec §4.1)");
        chat.GetService<GatewayChatClient>().Should().NotBeNull();
    }

    private static AssistantModelSettings Settings => new(Model, null, 0.2, 600, 4, null, RequestSource.SessionAssistant);

    private static LlmBudgetAdmission Allowed => new()
    {
        ReservationId = Guid.NewGuid(), Rejection = LlmBudgetRejection.None, BudgetClass = LlmBudgetClass.Answer, EstimatedCostUsd = 0.001m,
    };

    private AssistantChatClientFactory Factory()
    {
        var gateway = new HybridLlmService([_client.Object], Mock.Of<ILlmProviderSelector>(), _breakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object);
        return new AssistantChatClientFactory(gateway, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
    }

    private static LlmCompletionResult ToolCall(string id) =>
        LlmCompletionResult.CreateSuccess(string.Empty, new LlmUsage(100, 5, 105),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0001m, ModelId = Model, Provider = "DeepSeek" }) with
        {
            ToolCalls = [new LlmToolCall(id, "stato_partita", "{}")],
        };

    private static LlmCompletionResult Text(string text) =>
        LlmCompletionResult.CreateSuccess(text, new LlmUsage(120, 6, 126),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0001m, ModelId = Model, Provider = "DeepSeek" });
}
