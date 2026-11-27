using Api.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.Persistence;

/// <summary>#5156 — the cut that makes a text fit a varchar column.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "5156")]
public sealed class ColumnTextTests
{
    [Fact]
    public void Null_StaysNull() => ColumnText.Fit(null, 10).Should().BeNull();

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("exactly10!")]
    public void AValueThatFits_IsLeftAsItIs(string value) => ColumnText.Fit(value, 10).Should().BeSameAs(value);

    [Fact]
    public void ALongerValue_KeepsItsStart_AndEndsWithAVisibleCut()
    {
        ColumnText.Fit("exactly11!!", 10).Should().Be("exactly...");
        ColumnText.Fit(new string('x', 5000), 256).Should().HaveLength(256).And.EndWith("...");
    }

    /// <summary>A character outside the BMP is two chars: the cut drops both halves rather than leaving one.</summary>
    [Fact]
    public void TheCut_DoesNotSplitACharacterInTwo()
    {
        // "abcdef" + U+1F3B2 (a die, two chars) + "ghij", 12 chars: a cut at 10 keeps 7, the 7th being the first half.
        var value = "abcdef\U0001F3B2ghij";

        var fitted = ColumnText.Fit(value, 10);

        fitted.Should().Be("abcdef...");
        fitted!.Any(char.IsSurrogate).Should().BeFalse();
    }
}
