using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookProgressTests
{
    [Fact]
    public void Create_WithValidParagraph_Succeeds()
    {
        var progress = GamebookProgress.Create(currentParagraph: 47, history: new[] { 42, 45, 47 });
        progress.CurrentParagraph.Should().Be(47);
        progress.History.Should().BeEquivalentTo(new[] { 42, 45, 47 });
        progress.LastReadAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithCurrentParagraphNotInHistory_AppendsToHistory()
    {
        var progress = GamebookProgress.Create(currentParagraph: 50, history: new[] { 42, 45 });
        progress.History.Should().EndWith(50);
    }

    [Fact]
    public void Create_WithNegativeParagraph_Throws()
    {
        Action act = () => GamebookProgress.Create(currentParagraph: -1, history: Array.Empty<int>());
        act.Should().Throw<ArgumentException>().WithMessage("*paragraph*");
    }

    [Fact]
    public void Empty_ReturnsZeroState()
    {
        var progress = GamebookProgress.Empty();
        progress.CurrentParagraph.Should().Be(0);
        progress.History.Should().BeEmpty();
    }
}
