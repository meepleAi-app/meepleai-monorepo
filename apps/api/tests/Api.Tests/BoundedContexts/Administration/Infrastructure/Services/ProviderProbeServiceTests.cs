using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services.Providers.Probe;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "972")]
public sealed class ProviderProbeServiceTests
{
    private static (ProviderProbeService svc, Mock<IProviderProbeAuditRepository> repo, Mock<IProviderProbeExecutorFactory> factory)
        BuildSubject(
            IProviderProbeExecutor? executor = null,
            string providerName = "openrouter",
            string? envVar = "TEST_API_KEY",
            Mock<IProviderCredentialResolver>? resolver = null)
    {
        var repo = new Mock<IProviderProbeAuditRepository>();
        var factory = new Mock<IProviderProbeExecutorFactory>();

        if (executor is null)
        {
            var execMock = new Mock<IProviderProbeExecutor>();
            execMock.SetupGet(e => e.ProviderName).Returns(providerName);
            execMock.SetupGet(e => e.ApiKeyEnvVar).Returns(envVar);
            executor = execMock.Object;
        }

        factory.Setup(f => f.GetExecutor(providerName)).Returns(executor);
        factory.Setup(f => f.GetExecutor(It.Is<string>(n => n != providerName))).Returns((IProviderProbeExecutor?)null);

        // Default: a benign resolver returning a key. Callers that need a throw pass their own.
        if (resolver is null)
        {
            resolver = new Mock<IProviderCredentialResolver>();
            resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("resolved-key");
        }

        var svc = new ProviderProbeService(factory.Object, repo.Object, resolver.Object);
        return (svc, repo, factory);
    }

    [Fact]
    public async Task ProbeAsync_UnknownProvider_ThrowsUnknownProviderException()
    {
        var (svc, _, _) = BuildSubject();

        var act = async () => await svc.ProbeAsync("cohere", Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<UnknownProviderException>();
    }

    [Fact]
    public async Task ProbeAsync_NotConfigured_WritesAuditAndReturnsNotConfigured()
    {
        var execMock = new Mock<IProviderProbeExecutor>();
        execMock.SetupGet(e => e.ProviderName).Returns("openrouter");
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns("OPENROUTER_API_KEY"); // requiresAuth=true

        // #3044: neither DB credential nor env var → resolver throws → graceful not_configured + audit.
        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCredentialNotConfiguredException("openrouter"));

        var (svc, repo, _) = BuildSubject(execMock.Object, resolver: resolver);
        var actorId = Guid.NewGuid();

        var result = await svc.ProbeAsync("openrouter", actorId, null, CancellationToken.None);

        result.TokenConfigured.Should().BeFalse();
        result.TokenAuthenticated.Should().BeFalse();
        result.ErrorCode.Should().Be("not_configured");
        result.ModelAvailable.Should().BeNull();

        repo.Verify(r => r.AddAsync(
                It.Is<ProviderProbeAuditEntry>(e =>
                    e.ProviderName == "openrouter" &&
                    e.Outcome == ProbeOutcome.NotConfigured &&
                    e.ActorId == actorId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        execMock.Verify(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Issue", "3044")]
    public async Task ProbeAsync_AuthRequired_PassesResolvedKeyToExecutor()
    {
        // Coerenza post-rotazione: la key risolta (DB active-row) arriva all'executor, non l'env stale.
        var execMock = new Mock<IProviderProbeExecutor>();
        execMock.SetupGet(e => e.ProviderName).Returns("openrouter");
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns("OPENROUTER_API_KEY"); // requiresAuth=true
        execMock.Setup(e => e.ExecuteAsync("secret-key", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeExecutionResult(ProbeOutcome.Success, null, null, 42, null));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>())).ReturnsAsync("secret-key");

        var (svc, _, _) = BuildSubject(execMock.Object, resolver: resolver);

        var result = await svc.ProbeAsync("openrouter", Guid.NewGuid(), null, CancellationToken.None);

        result.TokenConfigured.Should().BeTrue();
        result.TokenAuthenticated.Should().BeTrue();
        result.TokenFingerprint.Should().NotBeNull();
        execMock.Verify(e => e.ExecuteAsync("secret-key", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProbeAsync_OllamaNoAuthRequired_ProceedsWithoutKey()
    {
        var execMock = new Mock<IProviderProbeExecutor>();
        execMock.SetupGet(e => e.ProviderName).Returns("ollama-local");
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns((string?)null);
        execMock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeExecutionResult(ProbeOutcome.Success, null, null, 42, null));

        // No-auth provider: the resolver must NOT be called (it would throw for an unmapped provider).
        var resolver = new Mock<IProviderCredentialResolver>(MockBehavior.Strict);

        var (svc, repo, _) = BuildSubject(execMock.Object, providerName: "ollama-local", envVar: null, resolver: resolver);

        var result = await svc.ProbeAsync("ollama-local", Guid.NewGuid(), null, CancellationToken.None);

        result.TokenConfigured.Should().BeTrue();
        result.TokenAuthenticated.Should().BeTrue();
        result.LatencyMs.Should().Be(42);
        result.TokenFingerprint.Should().BeNull();

        execMock.Verify(e => e.ExecuteAsync(string.Empty, null, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(
                It.Is<ProviderProbeAuditEntry>(e => e.Outcome == ProbeOutcome.Success),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProbeAsync_PropagatesExpectedModel_ToExecutor()
    {
        var execMock = new Mock<IProviderProbeExecutor>();
        execMock.SetupGet(e => e.ProviderName).Returns("ollama-local");
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns((string?)null);
        execMock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeExecutionResult(ProbeOutcome.Success, null, null, 100, false));

        var (svc, _, _) = BuildSubject(execMock.Object, providerName: "ollama-local", envVar: null);

        var result = await svc.ProbeAsync("ollama-local", Guid.NewGuid(), "fake-model", CancellationToken.None);

        result.ExpectedModel.Should().Be("fake-model");
        result.ModelAvailable.Should().BeFalse();
        execMock.Verify(e => e.ExecuteAsync(string.Empty, "fake-model", It.IsAny<CancellationToken>()), Times.Once);
    }
}
