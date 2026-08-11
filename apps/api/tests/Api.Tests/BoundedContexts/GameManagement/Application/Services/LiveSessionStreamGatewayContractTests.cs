using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Contract test: verifies that <see cref="ILiveSessionStreamGateway"/> and
/// <see cref="LiveSessionStreamEvent"/> expose exactly the expected signatures.
/// The compile-time fake implementation is the assertion — if the interface or
/// record changes shape, this file fails to build (RED) before any runtime test runs.
/// Issue #2561 SP2 T1 ACL contract.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2561")]
public class LiveSessionStreamGatewayContractTests
{
    private sealed class FakeGateway : ILiveSessionStreamGateway
    {
        public async IAsyncEnumerable<LiveSessionStreamEvent> SubscribeAsync(
            Guid id,
            Guid u,
            string? l,
            CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task BroadcastAsync(Guid id, LiveSessionStreamEvent e, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void CanonicalEvent_carries_type_and_data()
    {
        var e = new LiveSessionStreamEvent("session:score", new { playerId = Guid.NewGuid(), value = 3 });
        Assert.Equal("session:score", e.Type);
        Assert.NotNull(e.Data);
    }

    [Fact]
    public void FakeGateway_implements_ILiveSessionStreamGateway()
    {
        // Compile-time contract: if ILiveSessionStreamGateway changes signatures,
        // the FakeGateway above will fail to compile — that is the expected RED.
        ILiveSessionStreamGateway gateway = new FakeGateway();
        Assert.NotNull(gateway);
    }

    [Fact]
    public async Task SubscribeAsync_returns_empty_stream_when_no_events()
    {
        ILiveSessionStreamGateway gateway = new FakeGateway();
        var events = new List<LiveSessionStreamEvent>();

        await foreach (var evt in gateway.SubscribeAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Empty(events);
    }

    [Fact]
    public async Task BroadcastAsync_completes_without_throwing()
    {
        ILiveSessionStreamGateway gateway = new FakeGateway();
        var evt = new LiveSessionStreamEvent("session:turn", new { turn = 1 });

        // Should complete successfully with no exception
        await gateway.BroadcastAsync(Guid.NewGuid(), evt);
    }
}
