using System.Diagnostics.Metrics;
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

    private static List<(long Value, IReadOnlyDictionary<string, object?> Tags)> Capture(
        string instrumentName, Action act)
    {
        var captured = new List<(long, IReadOnlyDictionary<string, object?>)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var t in tags)
            {
                dict[t.Key] = t.Value;
            }
            captured.Add((value, dict));
        });
        listener.Start();

        act();

        return captured;
    }

    [Fact]
    public void ResolveAndValidate_PrivateIp_IncrementsBlocked_BeforeThrow_BoundedTags_NoHostOrIp()
    {
        var dns = new FakeDnsResolver("10.0.0.5");

        var captured = Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
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

        captured.Should().HaveCount(1);
        var (value, tags) = captured[0];
        value.Should().Be(1);
        // Bounded cardinality: exactly {sink, reason}, both the intended constants.
        tags.Keys.Should().BeEquivalentTo("sink", "reason");
        tags["sink"].Should().Be("manual");
        tags["reason"].Should().Be("private_ip");
        // The host/IP must NEVER leak into a metric tag (unbounded cardinality + PII/SSRF-target leak).
        tags.Values.Should().NotContain("evil.example.com").And.NotContain("10.0.0.5");
    }

    [Fact]
    public void ResolveAndValidate_PublicIp_IncrementsAllowed_BySink()
    {
        var dns = new FakeDnsResolver("8.8.8.8");

        var captured = Capture(MeepleAiMetrics.EgressAllowed.Name, () =>
            SsrfPinnedConnect.ResolveAndValidateAsync(
                    dns, "cdn.example.com", MeepleAiMetrics.EgressSinks.Manual, CancellationToken.None)
                .GetAwaiter().GetResult());

        captured.Should().HaveCount(1);
        captured[0].Value.Should().Be(1);
        captured[0].Tags.Keys.Should().BeEquivalentTo("sink");
        captured[0].Tags["sink"].Should().Be("manual");
    }

    [Fact]
    public void Counters_HaveStableBoundedNames()
    {
        MeepleAiMetrics.EgressBlocked.Name.Should().Be("meepleai.egress.blocked.total");
        MeepleAiMetrics.EgressAllowed.Name.Should().Be("meepleai.egress.allowed.total");
    }
}
