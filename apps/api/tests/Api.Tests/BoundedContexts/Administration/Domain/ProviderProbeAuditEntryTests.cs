using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

public class ProviderProbeAuditEntryTests
{
    [Fact]
    public void Create_ValidArgs_SetsAllFields()
    {
        var actorId = Guid.NewGuid();
        var entry = ProviderProbeAuditEntry.Create(
            providerName: "openrouter",
            actorId: actorId,
            tokenFingerprint: "abcd1234",
            outcome: ProbeOutcome.Success,
            errorCode: null,
            latencyMs: 250);

        entry.Id.Should().NotBeEmpty();
        entry.ProviderName.Should().Be("openrouter");
        entry.ActorId.Should().Be(actorId);
        entry.TokenFingerprint.Should().Be("abcd1234");
        entry.Outcome.Should().Be(ProbeOutcome.Success);
        entry.ErrorCode.Should().BeNull();
        entry.LatencyMs.Should().Be(250);
        entry.ProbedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_InvalidProviderName_Throws(string? name)
    {
        var act = () => ProviderProbeAuditEntry.Create(
            providerName: name!,
            actorId: Guid.NewGuid(),
            tokenFingerprint: "abcd1234",
            outcome: ProbeOutcome.Success,
            errorCode: null,
            latencyMs: 100);

        act.Should().Throw<ArgumentException>().WithParameterName("providerName");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcd12345")]
    [InlineData("ABCD1234")]
    [InlineData("xyz!1234")]
    public void Create_InvalidFingerprint_Throws(string fp)
    {
        var act = () => ProviderProbeAuditEntry.Create(
            "openrouter", Guid.NewGuid(), fp, ProbeOutcome.Success, null, 100);

        act.Should().Throw<ArgumentException>().WithParameterName("tokenFingerprint");
    }

    [Fact]
    public void Create_NegativeLatency_Throws()
    {
        var act = () => ProviderProbeAuditEntry.Create(
            "openrouter", Guid.NewGuid(), "abcd1234", ProbeOutcome.Success, null, -1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("latencyMs");
    }

    [Fact]
    public void Create_NotConfigured_AllowsNullFingerprint()
    {
        var entry = ProviderProbeAuditEntry.Create(
            "anthropic", Guid.NewGuid(), tokenFingerprint: null,
            ProbeOutcome.NotConfigured, errorCode: "not_configured", latencyMs: 0);

        entry.TokenFingerprint.Should().BeNull();
        entry.Outcome.Should().Be(ProbeOutcome.NotConfigured);
    }
}
