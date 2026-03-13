using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

[Trait("Category", TestCategories.Unit)]
public sealed class SeedProfileTests
{
    [Theory]
    [InlineData("dev", (int)SeedProfile.Dev)]
    [InlineData("DEV", (int)SeedProfile.Dev)]
    [InlineData("staging", (int)SeedProfile.Staging)]
    [InlineData("prod", (int)SeedProfile.Prod)]
    [InlineData("none", (int)SeedProfile.None)]
    public void Parse_ValidValues_ReturnsCorrectProfile(string input, int expectedInt)
    {
        var expected = (SeedProfile)expectedInt;
        Enum.TryParse<SeedProfile>(input, ignoreCase: true, out var result)
            .Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("development")]
    public void Parse_InvalidValues_ReturnsFalse(string input)
    {
        Enum.TryParse<SeedProfile>(input, ignoreCase: true, out _)
            .Should().BeFalse();
    }
}
