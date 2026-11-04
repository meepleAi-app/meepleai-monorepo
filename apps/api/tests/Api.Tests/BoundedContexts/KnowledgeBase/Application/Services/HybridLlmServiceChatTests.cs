using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ADR-097: la chat con strumenti passa dal gateway con le garanzie della risposta (#4168): modello e
/// riserva dal chiamante, circuit breaker a ogni tentativo, tetto prima della chiamata, costo con la
/// versione del profilo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4239")]
public sealed class HybridLlmServiceChatTests
{
    private const string Primary = "deepseek-flash";
    private const string Reserve = "meta-llama/llama-3.3-70b-instruct";
    private static readonly Guid VersionId = Guid.Parse("6f2c0c5e-3c1a-4c3e-9a7e-1f6a2b9d4e10");
    private static readonly LlmToolDefinition Tool =
        new("stato_partita", "Legge lo stato della partita", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement);

    private readonly Mock<ILlmClient> _deepSeek = Client("DeepSeek", Primary);
    private readonly Mock<ILlmClient> _openRouter = Client("OpenRouter", Reserve);
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly List<LlmCompletionResult> _loggedCosts = [];

    public HybridLlmServiceChatTests()
    {
        Chats(_deepSeek, Primary, "DeepSeek");
        Chats(_openRouter, Reserve, "OpenRouter");
        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Admission(LlmBudgetRejection.None));
        _circuitBreakers.Setup(c => c.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        _costService
            .Setup(c => c.LogSuccessAsync(It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionResult, LlmUserContext, long, RequestSource, CancellationToken>((r, _, _, _, _) => _loggedCosts.Add(r))
            .Returns(Task.CompletedTask);
    }

    private HybridLlmService CreateSut() =>
        new([_deepSeek.Object, _openRouter.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object);

    private static LlmChatRequest Request(string? fallback = Reserve) =>
        new([new LlmChatMessage(LlmChatRoles.System, "Sei l'assistente."), new LlmChatMessage(LlmChatRoles.User, "a chi tocca?")],
            [Tool], Primary, fallback, Temperature: 0.2, MaxTokens: 600, ProfileVersionId: VersionId, RequestSource.SessionAssistant);

    [Fact]
    public async Task Chat_SendsMessagesToolsModelTemperatureAndLength_AndReturnsToolCalls()
    {
        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ToolCalls.Should().ContainSingle().Which.Name.Should().Be("stato_partita");
        result.ProfileVersionId.Should().Be(VersionId);
        _deepSeek.Verify(c => c.ChatAsync(Primary, It.Is<IReadOnlyList<LlmChatMessage>>(m => m.Count == 2),
            It.Is<IReadOnlyList<LlmToolDefinition>>(t => t.Single().Name == "stato_partita"), 0.2, 600, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Chat_AdmitsEachCallAsSessionAssistant_CountingTheToolSchemas()
    {
        await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        var messageCharacters = "Sei l'assistente.".Length + "a chi tocca?".Length;
        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.Source == RequestSource.SessionAssistant && r.ModelId == Primary
                && r.MaxOutputTokens == 600 && r.PromptCharacters > messageCharacters),
            It.IsAny<CancellationToken>()), Times.Once);
        _loggedCosts.Should().ContainSingle().Which.ProfileVersionId.Should().Be(VersionId);
    }

    [Fact]
    public async Task Chat_PrimaryFails_AnswersWithTheReserve_AndBothAttemptsReachTheBreaker()
    {
        _deepSeek.Setup(c => c.ChatAsync(Primary, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("DeepSeek 503"));

        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess("OpenRouter", Reserve, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
    }

    [Fact]
    public async Task Chat_OverTheCap_IsRefused_WithoutCallingAnyClient()
    {
        _gate.Setup(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.ModelId == Primary), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Admission(LlmBudgetRejection.DailyCapReached, "refused"));

        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _deepSeek.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _openRouter.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Chat_WithoutMessages_IsAProgrammingError()
    {
        var act = () => CreateSut().ChatAsync(Request() with { Messages = [] }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static Mock<ILlmClient> Client(string provider, string model)
    {
        var client = new Mock<ILlmClient>();
        client.Setup(c => c.ProviderName).Returns(provider);
        client.Setup(c => c.SupportsModel(model)).Returns(true);
        return client;
    }

    private static void Chats(Mock<ILlmClient> client, string model, string provider) =>
        client.Setup(c => c.ChatAsync(model, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                string.Empty,
                new LlmUsage(150, 12, 162),
                new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider }) with
            {
                ToolCalls = [new LlmToolCall("call_1", "stato_partita", "{}")],
            });

    private static LlmBudgetAdmission Admission(LlmBudgetRejection rejection, string? message = null) => new()
    {
        ReservationId = rejection == LlmBudgetRejection.None ? Guid.NewGuid() : Guid.Empty,
        Rejection = rejection,
        BudgetClass = LlmBudgetClass.Answer,
        EstimatedCostUsd = 0.004m,
        Message = message,
    };
}
