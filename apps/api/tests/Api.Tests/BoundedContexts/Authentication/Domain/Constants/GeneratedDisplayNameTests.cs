using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// #4775. An account registered without a name is given an opaque one (<c>Player-</c> and seven hex digits;
/// never the local part of the address, which R3 took out for privacy). The email that greets the person must
/// not say «Hello Player-1a2b3c4»: «generated» is a property of the name, decided where the name is made, not
/// guessed by the email layer from a regex of its own.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4775")]
public sealed class GeneratedDisplayNameTests
{
    [Fact]
    public void Create_ProducesAName_ThatIsRecognizedAsGenerated()
    {
        var name = GeneratedDisplayName.Create();

        GeneratedDisplayName.IsGenerated(name).Should().BeTrue();
        name.Should().StartWith("Player-");
        name.Length.Should().Be(14, "the length the two generators always produced, and the column and the profile expect");
    }

    [Fact]
    public void Create_ProducesADifferentNameEachTime()
    {
        var names = Enumerable.Range(0, 50).Select(_ => GeneratedDisplayName.Create()).ToList();

        names.Distinct(StringComparer.Ordinal).Count().Should().BeGreaterThan(40);
    }

    [Theory]
    [InlineData("Player-1a2b3c4")]
    [InlineData("Player-0000000")]
    [InlineData("Player-fffffff")]
    public void IsGenerated_IsTrueForTheShapeTheGeneratorsProduce(string name)
    {
        GeneratedDisplayName.IsGenerated(name).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Marco")]
    [InlineData("Player-")]
    [InlineData("Player-1a2b3c")]       // one digit short
    [InlineData("Player-1a2b3c45")]     // one digit too many
    [InlineData("Player-1a2b3cg")]      // not hex
    [InlineData("Player-1A2B3C4")]      // the generators write lowercase
    [InlineData("player-1a2b3c4")]      // the prefix is exactly «Player-»
    [InlineData("xPlayer-1a2b3c")]
    [InlineData(" Player-1a2b3c4")]     // not trimmed: it is the stored value that is judged
    public void IsGenerated_IsFalseForAnythingElse(string? name)
    {
        GeneratedDisplayName.IsGenerated(name).Should().BeFalse();
    }
}
