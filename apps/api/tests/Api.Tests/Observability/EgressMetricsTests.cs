using System.Net;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// #3495 M2 — the egress SSRF observability counters, driven through the shared connect-pin
/// chokepoint (<see cref="SsrfPinnedConnect.ResolveAndValidateAsync"/>). Asserts the block counter
/// increments BEFORE the throw with ONLY bounded {sink,reason} tags (never host/IP), and that the
/// allow counter is the block-ratio denominator.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Observability")]
public sealed class EgressMetricsTests
{
    private sealed class FakeDnsResolver : IDnsResolver
    {
        private readonly IReadOnlyList<IPAddress> _addresses;
        public FakeDnsResolver(params string[] ips) => _addresses = ips.Select(IPAddress.Parse).ToList();
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
            Task.FromResult(_addresses);
    }

    private sealed class ThrowingDnsResolver : IDnsResolver
    {
        private readonly Exception _toThrow;
        public ThrowingDnsResolver(Exception toThrow) => _toThrow = toThrow;
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
            Task.FromException<IReadOnlyList<IPAddress>>(_toThrow);
    }

    [Fact]
    public void ResolveAndValidate_PrivateIp_IncrementsBlocked_BeforeThrow_BoundedTags_NoHostOrIp()
    {
        var dns = new FakeDnsResolver("10.0.0.5");

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
        {
            // The guard records the block BEFORE it throws; swallow the expected throw so we can
            // assert the increment already happened.
            try
            {
                SsrfPinnedConnect.ResolveAndValidateAsync(
                        dns, "evil.example.com", MeepleAiMetrics.EgressSinks.Manual, CancellationToken.None)
                    .GetAwaiter().GetResult();
                throw new Xunit.Sdk.XunitException("expected the SSRF guard to fail closed");
            }
            catch (InvalidOperationException)
            {
                // expected — the block metric fired first
            }
        });

        // The MeterListener is process-wide and xUnit runs test classes in parallel, so the capture
        // window can also see increments from other egress tests. Select this scenario's measurement
        // by its tags instead of assuming the window is exclusively ours (#3495 Slice D: the new
        // gate emits blocks from several sinks, which made the old HaveCount(1) flaky).
        var mine = captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")
                && Equals(c.Tags.GetValueOrDefault("reason"), "private_ip"))
            .ToList();

        mine.Should().NotBeEmpty("the guard must record the block before throwing");
        var (value, tags) = mine[0];
        value.Should().Be(1);
        // Bounded cardinality: exactly {sink, reason}, both the intended constants.
        tags.Keys.Should().BeEquivalentTo("sink", "reason");
        // The host/IP must NEVER leak into a metric tag (unbounded cardinality + PII/SSRF-target leak).
        tags.Values.Should().NotContain("evil.example.com").And.NotContain("10.0.0.5");
        // Every measurement in the window — whoever emitted it — must keep the tag set bounded.
        captured.Should().AllSatisfy(c => c.Tags.Keys.Should().BeEquivalentTo("sink", "reason"));
    }

    [Fact]
    public void ResolveAndValidate_PublicIp_IncrementsAllowed_BySink()
    {
        var dns = new FakeDnsResolver("8.8.8.8");

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressAllowed.Name, () =>
            SsrfPinnedConnect.ResolveAndValidateAsync(
                    dns, "cdn.example.com", MeepleAiMetrics.EgressSinks.Manual, CancellationToken.None)
                .GetAwaiter().GetResult());

        // Same parallel-capture caveat as the blocked counter above: select by tag, don't assume the
        // window is exclusively ours.
        var mine = captured.Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")).ToList();

        mine.Should().NotBeEmpty("a validated public address must count towards the allowed denominator");
        mine[0].Value.Should().Be(1);
        captured.Should().AllSatisfy(c => c.Tags.Keys.Should().BeEquivalentTo("sink"));
    }

    [Fact]
    public void ResolveAndValidate_DnsThrows_IncrementsBlocked_WithDnsFailure_AndRethrows()
    {
        // Un NXDOMAIN / timeout del resolver: la connessione non avviene (fail-closed corretto), ma
        // senza questo counter il sink degradato è indistinguibile da un sink inattivo (#3583).
        var dns = new ThrowingDnsResolver(new System.Net.Sockets.SocketException(11001));

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
        {
            try
            {
                SsrfPinnedConnect.ResolveAndValidateAsync(
                        dns, "nxdomain.example.com", MeepleAiMetrics.EgressSinks.Wikidata, CancellationToken.None)
                    .GetAwaiter().GetResult();
                throw new Xunit.Sdk.XunitException("expected the DNS failure to propagate");
            }
            catch (System.Net.Sockets.SocketException)
            {
                // atteso — il guard registra e RILANCIA senza alterare l'esito
            }
        });

        var mine = captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikidata")
                && Equals(c.Tags.GetValueOrDefault("reason"), "dns_failure"))
            .ToList();

        mine.Should().NotBeEmpty("un fallimento DNS deve essere visibile sul counter di blocco");
        mine[0].Value.Should().Be(1);
        captured.Should().AllSatisfy(c => c.Tags.Keys.Should().BeEquivalentTo("sink", "reason"));
    }

    [Fact]
    public void ResolveAndValidate_CallerCancellation_DoesNotIncrementBlocked()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var dns = new ThrowingDnsResolver(new OperationCanceledException(cts.Token));

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
        {
            try
            {
                SsrfPinnedConnect.ResolveAndValidateAsync(
                        dns, "cancelled.example.com", MeepleAiMetrics.EgressSinks.Wikimedia, cts.Token)
                    .GetAwaiter().GetResult();
                throw new Xunit.Sdk.XunitException("expected the cancellation to propagate");
            }
            catch (OperationCanceledException)
            {
                // atteso
            }
        });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikimedia")
                && Equals(c.Tags.GetValueOrDefault("reason"), "dns_failure"))
            .Should().BeEmpty("un abort del chiamante non è un fallimento DNS e non va contato");
    }

    [Fact]
    public void Counters_HaveStableBoundedNames()
    {
        MeepleAiMetrics.EgressBlocked.Name.Should().Be("meepleai.egress.blocked.total");
        MeepleAiMetrics.EgressAllowed.Name.Should().Be("meepleai.egress.allowed.total");

        MeepleAiMetrics.EgressBlockReasons.DnsFailure.Should().Be("dns_failure");
        // Pinnati perché sono il selettore delle regole in infra/prometheus/alerts/egress-guard.yml:
        // rinominarli spegnerebbe gli alert senza rompere nulla in compilazione (#3583).
        MeepleAiMetrics.EgressBlockReasons.PrivateIp.Should().Be("private_ip");
        MeepleAiMetrics.EgressBlockReasons.DenylistHit.Should().Be("denylist_hit");
        MeepleAiMetrics.EgressBlockReasons.DecodeFail.Should().Be("decode_fail");
    }
}
