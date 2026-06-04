using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates.AlertChannel;

/// <summary>
/// Unit tests for the <see cref="Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel"/>
/// aggregate (Issue #1840 SP5 F4-C7). Covers factory invariants, JSON guard,
/// upsert lifecycle, and test-result recording semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public sealed class AlertChannelTests
{
    [Fact]
    public void Create_WithValidConfig_InitializesAggregate()
    {
        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/services/T/B/x","channel":"#alerts"}""",
            isEnabled: true,
            updatedBy: "admin@meepleai.dev");

        channel.Type.Should().Be(AlertChannelType.Slack);
        channel.IsEnabled.Should().BeTrue();
        channel.UpdatedBy.Should().Be("admin@meepleai.dev");
        channel.LastTestedAt.Should().BeNull();
        channel.LastTestStatus.Should().BeNull();
        channel.LastTestMessage.Should().BeNull();
        channel.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyConfigJson_Throws(string configJson)
    {
        Action act = () => Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack, configJson, isEnabled: true, updatedBy: "admin");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[\"array\"]")]
    [InlineData("42")]
    public void Create_WithNonObjectJson_Throws(string configJson)
    {
        Action act = () => Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Email, configJson, isEnabled: true, updatedBy: "admin");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*JSON object*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyUpdatedBy_Throws(string updatedBy)
    {
        Action act = () => Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Email, "{}", isEnabled: true, updatedBy: updatedBy);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateConfig_PreservesCreatedAtBumpsUpdatedAt()
    {
        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack, """{"webhookUrl":"u","channel":"#a"}""", true, "admin");
        var origCreated = channel.CreatedAt;

        // Force a measurable delta by sleeping briefly (UTC clock might tick during test run).
        System.Threading.Thread.Sleep(2);

        channel.UpdateConfig("""{"webhookUrl":"u2","channel":"#b"}""", isEnabled: false, "admin2");

        channel.ConfigJson.Should().Contain("u2");
        channel.IsEnabled.Should().BeFalse();
        channel.UpdatedBy.Should().Be("admin2");
        channel.CreatedAt.Should().Be(origCreated);
        channel.UpdatedAt.Should().BeOnOrAfter(origCreated);
    }

    [Fact]
    public void RecordTestResult_OnSuccess_SetsOkStatusAndMessage()
    {
        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack, "{}", true, "admin");
        var probedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

        channel.RecordTestResult(success: true, message: "200 OK", probedAt);

        channel.LastTestStatus.Should().Be("ok");
        channel.LastTestMessage.Should().Be("200 OK");
        channel.LastTestedAt.Should().Be(probedAt);
    }

    [Fact]
    public void RecordTestResult_OnFailure_SetsErrorStatusAndMessage()
    {
        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack, "{}", true, "admin");
        var probedAt = DateTime.UtcNow;

        channel.RecordTestResult(success: false, message: "HTTP 401 invalid_token", probedAt);

        channel.LastTestStatus.Should().Be("error");
        channel.LastTestMessage.Should().Be("HTTP 401 invalid_token");
    }

    [Fact]
    public void RecordTestResult_WithEmptyMessage_SubstitutesDefault()
    {
        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Create(
            AlertChannelType.Slack, "{}", true, "admin");

        channel.RecordTestResult(success: true, message: "", DateTime.UtcNow);

        channel.LastTestMessage.Should().Be("Connection OK");
    }

    [Fact]
    public void Reconstitute_PreservesRowVersionAndAuditFields()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4 };
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var lastTestedAt = new DateTime(2026, 6, 4, 11, 0, 0, DateTimeKind.Utc);

        var channel = Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels.AlertChannel.Reconstitute(
            AlertChannelType.Email,
            """{"smtpHost":"smtp.example.com"}""",
            isEnabled: true,
            lastTestedAt: lastTestedAt,
            lastTestStatus: "ok",
            lastTestMessage: "All good",
            createdAt: createdAt,
            updatedAt: updatedAt,
            createdBy: "founder@meepleai.dev",
            updatedBy: "ops@meepleai.dev",
            rowVersion: rowVersion);

        channel.RowVersion.Should().BeEquivalentTo(rowVersion);
        channel.CreatedAt.Should().Be(createdAt);
        channel.UpdatedAt.Should().Be(updatedAt);
        channel.LastTestedAt.Should().Be(lastTestedAt);
        channel.LastTestStatus.Should().Be("ok");
        channel.LastTestMessage.Should().Be("All good");
        channel.CreatedBy.Should().Be("founder@meepleai.dev");
        channel.UpdatedBy.Should().Be("ops@meepleai.dev");
    }

    [Theory]
    [InlineData("email", 0)]
    [InlineData("slack", 1)]
    [InlineData("EMAIL", 0)]
    [InlineData("Slack", 1)]
    public void AlertChannelType_FromWireValue_ParsesCaseInsensitive(string wire, int expectedEnumValue)
    {
        // Use int to keep the enum type out of the public test signature
        // (AlertChannelType is internal — see Api.csproj InternalsVisibleTo).
        var expected = (AlertChannelType)expectedEnumValue;
        AlertChannelTypeExtensions.FromWireValue(wire).Should().Be(expected);
    }

    [Theory]
    [InlineData("pagerduty")]
    [InlineData("teams")]
    [InlineData("")]
    public void AlertChannelType_FromWireValue_RejectsUnknown(string wire)
    {
        Action act = () => AlertChannelTypeExtensions.FromWireValue(wire);
        act.Should().Throw<ArgumentException>();
    }
}
