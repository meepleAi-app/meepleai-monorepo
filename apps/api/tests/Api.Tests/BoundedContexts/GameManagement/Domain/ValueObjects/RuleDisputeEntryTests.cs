using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RuleDisputeEntryTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateEntry()
    {
        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "Can I play 2 cards per turn?",
            verdict: "No, only one card per turn is allowed.",
            ruleReferences: new List<string> { "Page 12, Section 3.2" },
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow);

        entry.RaisedByPlayerName.Should().Be("Marco");
        entry.RuleReferences.Should().ContainSingle();
        entry.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithEmptyDescription_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "",
            verdict: "verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "   ",
            verdict: "verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithEmptyVerdict_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "Some dispute",
            verdict: "",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithEmptyPlayerName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "Some dispute",
            verdict: "Some verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "",
            timestamp: DateTime.UtcNow));
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldGenerateNewId()
    {
        var entry = new RuleDisputeEntry(
            id: Guid.Empty,
            description: "dispute",
            verdict: "verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow);

        entry.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithNullRuleReferences_ShouldDefaultToEmptyList()
    {
        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "dispute",
            verdict: "verdict",
            ruleReferences: null!,
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow);

        Assert.NotNull(entry.RuleReferences);
        Assert.Empty(entry.RuleReferences);
    }

    [Fact]
    public void Create_WithMultipleRuleReferences_ShouldStoreAll()
    {
        var references = new List<string> { "Page 5", "Section 2.1", "Appendix A" };

        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "Complex dispute",
            verdict: "Ruling stands as written",
            ruleReferences: references,
            raisedByPlayerName: "Anna",
            timestamp: DateTime.UtcNow);

        entry.RuleReferences.Count.Should().Be(3);
        entry.RuleReferences.Should().Contain("Page 5");
        entry.RuleReferences.Should().Contain("Section 2.1");
    }

    [Fact]
    public void Create_StoresTimestampCorrectly()
    {
        var timestamp = new DateTime(2026, 3, 15, 14, 30, 0, DateTimeKind.Utc);

        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "dispute",
            verdict: "verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Luca",
            timestamp: timestamp);

        entry.Timestamp.Should().Be(timestamp);
    }
}
