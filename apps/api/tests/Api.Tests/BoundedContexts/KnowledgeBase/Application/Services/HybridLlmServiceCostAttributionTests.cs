using System.Runtime.CompilerServices;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4164 (ADR-095 fetta 1b): il costo di una chiamata va all'utente della richiesta anche quando il
/// chiamante non passa un <c>User</c>, e l'instradamento resta quello di prima.
/// </summary>
/// <remarks>
/// Il difetto, visto nel registro locale il 2026-10-10: una domanda fatta da un superadmin
/// autenticato (<c>POST /knowledge-base/ask</c>) è stata registrata con <c>user_id = null</c> e
/// <c>user_role = internal</c>, perché la pipeline RAG chiama <c>ILlmService</c> senza utente. Questi
/// test non compilano contro il codice di prima, che non ha <see cref="ILlmCallerAccessor"/>.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridLlmServiceCostAttributionTests
{
    private static readonly Guid RequestUserId = Guid.Parse("7d1f3c2a-5b6e-4f8a-9c0d-1e2f3a4b5c6d");
    private static readonly LlmUserContext RequestUser = new(RequestUserId, "user", LlmUserTier.User);

    private readonly Mock<ILlmClient> _client = new();
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ILlmCallerAccessor> _caller = new();

    public HybridLlmServiceCostAttributionTests()
    {
        _client.Setup(c => c.ProviderName).Returns("DeepSeek");
        _client.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);
        _client
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "answer",
                new LlmUsage(100, 20, 120),
                new LlmCost { InputCost = 0.00003m, OutputCost = 0.000024m, ModelId = "deepseek-chat", Provider = "DeepSeek" }));
        _client
            .Setup(c => c.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => FinalChunkOnly());
        _selector
            .Setup(s => s.SelectProviderAsync(
                It.IsAny<LlmUserContext>(), It.IsAny<RagStrategy>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(_client.Object, new LlmRoutingDecision("DeepSeek", "deepseek-chat", "test")));
    }

    private HybridLlmService CreateSut() =>
        new([_client.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, piiDetector: null, callerAccessor: _caller.Object);

    private static async IAsyncEnumerable<StreamChunk> FinalChunkOnly([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return new StreamChunk("answer");
        yield return new StreamChunk(
            null,
            new LlmUsage(100, 20, 120),
            new LlmCost { InputCost = 0.00003m, OutputCost = 0.000024m, ModelId = "deepseek-chat", Provider = "DeepSeek" },
            IsFinal: true);
    }

    private void VerifyCostAttributedTo(Guid? userId, string? role) =>
        _costService.Verify(c => c.LogSuccessAsync(
            It.IsAny<LlmCompletionResult>(),
            It.Is<LlmUserContext>(u => u.UserId == userId && u.RoleName == role),
            It.IsAny<long>(),
            It.IsAny<RequestSource>(),
            It.IsAny<CancellationToken>()), Times.Once);

    [Fact]
    public async Task CallWithoutAUser_IsAttributedToTheRequestUser_ButRoutedAsBefore()
    {
        _caller.Setup(c => c.Current).Returns(RequestUser);

        await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.RagPipeline);

        VerifyCostAttributedTo(RequestUserId, "user");
        _selector.Verify(s => s.SelectProviderAsync(
            LlmUserContext.Internal, It.IsAny<RagStrategy>(), RequestSource.RagPipeline, It.IsAny<CancellationToken>()), Times.Once,
            "il tier dell'utente cambierebbe il modello: l'instradamento deve restare quello di una chiamata interna");
    }

    [Fact]
    public async Task CallWithoutAUser_OutsideARequest_KeepsTheInternalContext()
    {
        _caller.Setup(c => c.Current).Returns((LlmUserContext?)null);

        await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.RagClassification);

        VerifyCostAttributedTo(null, "internal");
    }

    [Fact]
    public async Task ExplicitModelCall_IsAttributedToTheRequestUser()
    {
        _caller.Setup(c => c.Current).Returns(RequestUser);

        await CreateSut().GenerateCompletionWithModelAsync("deepseek-chat", "system", "question", RequestSource.AdminOperation);

        VerifyCostAttributedTo(RequestUserId, "user");
    }

    [Fact]
    public async Task ExplicitModelFallbackCall_IsAttributedToTheRequestUser()
    {
        _caller.Setup(c => c.Current).Returns(RequestUser);

        await CreateSut().GenerateCompletionWithModelFallbackAsync("deepseek-chat", "system", "question", RequestSource.AdminOperation);

        VerifyCostAttributedTo(RequestUserId, "user");
    }

    [Fact]
    public async Task ExplicitModelFailure_IsLogged_AndAttributed()
    {
        // Prima #4164 i percorsi a modello esplicito registravano solo i successi.
        _caller.Setup(c => c.Current).Returns(RequestUser);
        _client
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("402 insufficient balance"));

        await CreateSut().GenerateCompletionWithModelAsync("deepseek-chat", "system", "question", RequestSource.AdminOperation);

        _costService.Verify(c => c.LogFailureAsync(
            "402 insufficient balance",
            It.Is<LlmUserContext>(u => u.UserId == RequestUserId),
            It.IsAny<long>(),
            RequestSource.AdminOperation,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplicitModelFallback_LogsOneFailurePerFailedAttempt()
    {
        // Primo tentativo: DeepSeek lancia. Secondo: OpenRouter, dal fallback, risponde con un
        // fallimento. Poi nessun altro provider.
        _client
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var openRouter = new Mock<ILlmClient>();
        openRouter.Setup(c => c.ProviderName).Returns("OpenRouter");
        openRouter
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("404 model not found"));
        _selector
            .SetupSequence(s => s.GetNextFallbackAsync(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(openRouter.Object, new LlmRoutingDecision("OpenRouter", "meta-llama/llama-3.3-70b-instruct", "fallback")))
            .ReturnsAsync(new ProviderSelectionResult(null, new LlmRoutingDecision("none", "none", "no fallback")));

        await CreateSut().GenerateCompletionWithModelFallbackAsync("deepseek-chat", "system", "question", RequestSource.AdminOperation);

        _costService.Verify(c => c.LogFailureAsync(
            "connection refused", It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.AdminOperation, It.IsAny<CancellationToken>()), Times.Once);
        _costService.Verify(c => c.LogFailureAsync(
            "404 model not found", It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.AdminOperation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RoutedCall_LogsWithoutTheRequestToken_SoADisconnectDoesNotLoseTheRow()
    {
        using var cts = new CancellationTokenSource();

        await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.RagPipeline, cts.Token);

        _costService.Verify(c => c.LogSuccessAsync(
            It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), It.IsAny<RequestSource>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Stream_IsAttributedToTheRequestUser()
    {
        _caller.Setup(c => c.Current).Returns(RequestUser);

        await foreach (var _ in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline))
        {
        }

        VerifyCostAttributedTo(RequestUserId, "user");
    }
}
