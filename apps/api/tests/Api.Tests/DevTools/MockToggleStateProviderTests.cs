using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api.DevTools;
using Xunit;

namespace Api.Tests.DevTools;

public class MockToggleStateProviderTests
{
    [Fact]
    public void InitializesFromEnvironmentVariables()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["MOCK_LLM"] = "true",
            ["MOCK_EMBEDDING"] = "false",
            ["MOCK_S3"] = "true"
        };
        var provider = new MockToggleStateProvider(env, new[] { "llm", "embedding", "s3", "bgg" });

        Assert.True(provider.IsMocked("llm"));
        Assert.False(provider.IsMocked("embedding"));
        Assert.True(provider.IsMocked("s3"));
        Assert.False(provider.IsMocked("bgg"));
    }

    [Fact]
    public void DefaultsMissingKeysToFalse()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "llm" });
        Assert.False(provider.IsMocked("llm"));
    }

    [Fact]
    public void IsMockedThrowsOnUnknownService()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "llm" });
        Assert.Throws<InvalidOperationException>(() => { provider.IsMocked("unknown"); });
    }

    [Fact]
    public void SetUpdatesStateAndEmitsEvent()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["MOCK_LLM"] = "false" },
            new[] { "llm" });

        MockToggleChangedEventArgs? received = null;
        provider.ToggleChanged += (_, args) => received = args;

        provider.Set("llm", true);

        Assert.True(provider.IsMocked("llm"));
        Assert.NotNull(received);
        Assert.Equal("llm", received!.ServiceName);
        Assert.True(received.Mocked);
    }

    [Fact]
    public void SetThrowsOnUnknownService()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "llm" });
        Assert.Throws<InvalidOperationException>(() => { provider.Set("unknown", true); });
    }

    [Fact]
    public async Task ConcurrentReadsAndWritesAreSafe()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "llm", "embedding" });

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                provider.Set("llm", idx % 2 == 0);
                provider.IsMocked("llm");
                provider.GetAll();
            }));
        }
        await Task.WhenAll(tasks).ConfigureAwait(true);
        Assert.True(true);
    }

    [Fact]
    public void GetAllReturnsReadOnlySnapshot()
    {
        var provider = new MockToggleStateProvider(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["MOCK_LLM"] = "true" },
            new[] { "llm" });
        var snapshot = provider.GetAll();
        Assert.True(snapshot["llm"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, bool>>(snapshot);
    }
}
