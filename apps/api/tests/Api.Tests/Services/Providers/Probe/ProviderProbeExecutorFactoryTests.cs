using Api.Services.Providers.Probe;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Providers.Probe;

[Trait("Category", "Unit")]
public sealed class ProviderProbeExecutorFactoryTests
{
    private static ProviderProbeExecutorFactory Build(params IProviderProbeExecutor[] executors)
        => new(executors);

    private sealed class FakeExec : IProviderProbeExecutor
    {
        public FakeExec(string name) => ProviderName = name;
        public string ProviderName { get; }
        public Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Fact]
    public void GetExecutor_KnownProvider_ReturnsMatchingExecutor()
    {
        var factory = Build(new FakeExec("openrouter"), new FakeExec("openai"));

        factory.GetExecutor("openrouter").Should().NotBeNull();
        factory.GetExecutor("openrouter")!.ProviderName.Should().Be("openrouter");
    }

    [Fact]
    public void GetExecutor_UnknownProvider_ReturnsNull()
    {
        var factory = Build(new FakeExec("openrouter"));

        factory.GetExecutor("cohere").Should().BeNull();
    }

    [Fact]
    public void GetExecutor_CaseInsensitive()
    {
        var factory = Build(new FakeExec("openrouter"));

        factory.GetExecutor("OpenRouter").Should().NotBeNull();
    }

    [Fact]
    public void KnownProviderNames_ReturnsAllRegistered()
    {
        var factory = Build(new FakeExec("openrouter"), new FakeExec("openai"));

        factory.KnownProviderNames.Should().BeEquivalentTo(new[] { "openrouter", "openai" });
    }
}
