using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Api.Services;
using Xunit;

namespace Api.Tests.DevTools;

public class MockLlmServiceTests
{
    private static MockLlmService MakeService() => new(new ScenarioLoader());

    [Fact]
    public async Task GenerateCompletion_ReturnsDeterministicResponse()
    {
        var svc = MakeService();
        var r1 = await svc.GenerateCompletionAsync("sys", "hello world", RequestSource.Manual, CancellationToken.None);
        var r2 = await svc.GenerateCompletionAsync("sys", "hello world", RequestSource.Manual, CancellationToken.None);
        Assert.True(r1.Success);
        Assert.Equal(r1.Response, r2.Response);
        Assert.Contains("mock", r1.Response, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateCompletion_ZeroCost()
    {
        var svc = MakeService();
        var r = await svc.GenerateCompletionAsync("sys", "hi", RequestSource.Manual, CancellationToken.None);
        Assert.Equal(0m, r.Cost.TotalCost);
    }

    [Fact]
    public async Task GenerateCompletionStream_EmitsMultipleChunks()
    {
        var svc = MakeService();
        int count = 0;
        bool sawFinal = false;
        await foreach (var chunk in svc.GenerateCompletionStreamAsync("sys", "tell me a story", RequestSource.Manual, CancellationToken.None))
        {
            count++;
            if (chunk.IsFinal)
            {
                sawFinal = true;
            }
        }
        Assert.True(count >= 2, "Expected at least 2 chunks");
        Assert.True(sawFinal, "Expected a final chunk with IsFinal=true");
    }

    [Fact]
    public async Task GenerateJson_ReturnsTypeOrNullWithoutThrowing()
    {
        var svc = MakeService();
        var result = await svc.GenerateJsonAsync<TestRecord>("sys", "hello", RequestSource.Manual, CancellationToken.None);
        // Activator.CreateInstance succeeds for TestRecord (parameterless ctor) — result must be non-null.
        Assert.NotNull(result);
    }

#pragma warning disable S2094 // Classes should not be empty
    private sealed class TestRecord { }
#pragma warning restore S2094
}
