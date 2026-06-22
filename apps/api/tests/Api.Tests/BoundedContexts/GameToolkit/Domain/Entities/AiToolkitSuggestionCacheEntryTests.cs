using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public sealed class AiToolkitSuggestionCacheEntryTests
{
    [Fact]
    public void Create_ValidArgs_SetsPropertiesAndStampsGeneratedAt()
    {
        var before = DateTimeOffset.UtcNow;
        var gameId = Guid.NewGuid();
        var entry = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"foo\":1}", kbVersion: 3);
        var after = DateTimeOffset.UtcNow;

        entry.Id.Should().NotBe(Guid.Empty);
        entry.GameId.Should().Be(gameId);
        entry.SuggestionJson.Should().Be("{\"foo\":1}");
        entry.KbVersion.Should().Be(3);
        entry.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_EmptyGameId_Throws()
    {
        var act = () => AiToolkitSuggestionCacheEntry.Create(Guid.Empty, "{}", null);
        act.Should().Throw<ArgumentException>().WithMessage("*GameId*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrWhitespaceJson_Throws(string? json)
    {
        var act = () => AiToolkitSuggestionCacheEntry.Create(Guid.NewGuid(), json!, null);
        act.Should().Throw<ArgumentException>().WithMessage("*suggestion*");
    }

    [Fact]
    public void Refresh_UpdatesJsonAndKbVersionAndBumpsGeneratedAt()
    {
        var entry = AiToolkitSuggestionCacheEntry.Create(Guid.NewGuid(), "{\"v\":1}", kbVersion: 1);
        var originalGeneratedAt = entry.GeneratedAt;
        Thread.Sleep(5);  // ensure observable delta

        entry.Refresh("{\"v\":2}", kbVersion: 2);

        entry.SuggestionJson.Should().Be("{\"v\":2}");
        entry.KbVersion.Should().Be(2);
        entry.GeneratedAt.Should().BeAfter(originalGeneratedAt);
    }
}
