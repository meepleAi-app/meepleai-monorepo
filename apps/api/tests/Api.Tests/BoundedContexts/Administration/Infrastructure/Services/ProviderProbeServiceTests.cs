using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Services;
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
        BuildSubject(IProviderProbeExecutor? executor = null, string providerName = "openrouter", string? envVar = "TEST_API_KEY")
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

        var svc = new ProviderProbeService(factory.Object, repo.Object);
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
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns("__ABSENT_TEST_VAR_972__");
        // Ensure env var is not set
        Environment.SetEnvironmentVariable("__ABSENT_TEST_VAR_972__", null);

        var (svc, repo, _) = BuildSubject(execMock.Object);
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
    public async Task ProbeAsync_OllamaNoAuthRequired_ProceedsWithoutKey()
    {
        var execMock = new Mock<IProviderProbeExecutor>();
        execMock.SetupGet(e => e.ProviderName).Returns("ollama-local");
        execMock.SetupGet(e => e.ApiKeyEnvVar).Returns((string?)null);
        execMock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeExecutionResult(ProbeOutcome.Success, null, null, 42, null));

        var (svc, repo, _) = BuildSubject(execMock.Object, providerName: "ollama-local", envVar: null);

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
