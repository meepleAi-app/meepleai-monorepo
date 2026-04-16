using System;
using System.Collections.Generic;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// F3 follow-up: verifies the AreAllProvidersUnavailable() short-circuit
/// helper in ChatWithSessionAgentCommandHandler. The helper is used to surface
/// a friendly "agent temporarily unavailable" error code when every LLM provider
/// has its circuit breaker in Open state.
/// </summary>
public class ChatWithSessionAgentFallbackTests
{
    [Fact]
    public void AreAllProvidersUnavailable_NoProvidersTracked_ReturnsFalse()
    {
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>());

        var handler = CreateHandler(registry.Object);

        handler.AreAllProvidersUnavailable().Should().BeFalse();
    }

    [Fact]
    public void AreAllProvidersUnavailable_AllClosed_ReturnsFalse()
    {
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>
            {
                ["openrouter"] = ("Closed", "p95=150ms"),
                ["ollama"] = ("Closed", "p95=80ms"),
            });

        var handler = CreateHandler(registry.Object);

        handler.AreAllProvidersUnavailable().Should().BeFalse();
    }

    [Fact]
    public void AreAllProvidersUnavailable_MixedStates_ReturnsFalse()
    {
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>
            {
                ["openrouter"] = ("Open (3 failures)", "p95=N/A"),
                ["ollama"] = ("Closed", "p95=80ms"),
            });

        var handler = CreateHandler(registry.Object);

        handler.AreAllProvidersUnavailable().Should().BeFalse();
    }

    [Fact]
    public void AreAllProvidersUnavailable_AllOpen_ReturnsTrue()
    {
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>
            {
                ["openrouter"] = ("Open (3 failures, retry in 45s)", "p95=N/A"),
                ["ollama"] = ("Open (5 failures, retry in 30s)", "p95=N/A"),
            });

        var handler = CreateHandler(registry.Object);

        handler.AreAllProvidersUnavailable().Should().BeTrue();
    }

    [Fact]
    public void AreAllProvidersUnavailable_HalfOpenNotTreatedAsUnavailable_ReturnsFalse()
    {
        // HalfOpen means the breaker is probing — the provider may succeed.
        // The helper must only return true when ALL states start with "Open".
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>
            {
                ["openrouter"] = ("HalfOpen (probing)", "p95=N/A"),
                ["ollama"] = ("Open (5 failures, retry in 30s)", "p95=N/A"),
            });

        var handler = CreateHandler(registry.Object);

        handler.AreAllProvidersUnavailable().Should().BeFalse();
    }

    [Fact]
    public void ErrorCodeAndMessageConstants_AreStableAndItalian()
    {
        // Guard against accidental rename of the error code (frontend depends on it)
        // and against the localized message being accidentally translated/removed.
        ChatWithSessionAgentCommandHandler.AgentUnavailableErrorCode
            .Should().Be("AGENT_TEMPORARILY_UNAVAILABLE");
        ChatWithSessionAgentCommandHandler.AgentUnavailableItalianMessage
            .Should().Contain("temporaneamente non disponibile");
        ChatWithSessionAgentCommandHandler.AgentUnavailableItalianMessage
            .Should().Contain("strumenti manuali");
    }

    // ------------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------------

    private static ChatWithSessionAgentCommandHandler CreateHandler(ICircuitBreakerRegistry registry)
    {
        return new ChatWithSessionAgentCommandHandler(
            sessionRepository: Mock.Of<IAgentSessionRepository>(),
            definitionRepository: Mock.Of<IAgentDefinitionRepository>(),
            chatThreadRepository: Mock.Of<IChatThreadRepository>(),
            gameRepository: Mock.Of<IGameRepository>(),
            liveSessionRepository: Mock.Of<ILiveSessionRepository>(),
            unitOfWork: Mock.Of<IUnitOfWork>(),
            ragPromptService: Mock.Of<IRagPromptAssemblyService>(),
            copyrightTierResolver: Mock.Of<ICopyrightTierResolver>(),
            agentMemoryContextBuilder: Mock.Of<IAgentMemoryContextBuilder>(),
            llmService: Mock.Of<ILlmService>(),
            userBudgetService: Mock.Of<IUserBudgetService>(),
            circuitBreakerRegistry: registry,
            scopeFactory: Mock.Of<IServiceScopeFactory>(),
            logger: NullLogger<ChatWithSessionAgentCommandHandler>.Instance,
            copyrightLeakGuard: Mock.Of<ICopyrightLeakGuard>(),
            fallbackMessageProvider: Mock.Of<ICopyrightFallbackMessageProvider>(),
            copyrightOptions: Options.Create(new CopyrightLeakGuardOptions()));
    }
}
