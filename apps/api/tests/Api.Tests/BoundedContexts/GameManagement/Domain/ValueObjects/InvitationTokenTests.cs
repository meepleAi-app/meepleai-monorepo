using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the InvitationToken value object.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public sealed class InvitationTokenTests
{
    #region Generate

    [Fact]
    public void Generate_Returns22CharacterToken()
    {
        var token = InvitationToken.Generate();

        token.Value.Should().HaveLength(InvitationToken.Length);
        token.Value.Should().HaveLength(22);
    }

    [Fact]
    public void Generate_ReturnsBase62Characters()
    {
        var token = InvitationToken.Generate();

        token.Value.Should().MatchRegex("^[0-9A-Za-z]{22}$");
    }

    [Fact]
    public void Generate_ProducesUniqueTokensAcrossManyCalls()
    {
        // 1000 tokens at ~131 bits entropy → collision probability vanishingly small.
        var tokens = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
        {
            tokens.Add(InvitationToken.Generate().Value);
        }

        tokens.Should().HaveCount(1000, "all generated tokens should be unique");
    }

    #endregion

    #region Create (reconstitution)

    [Fact]
    public void Create_WithValidToken_Succeeds()
    {
        var raw = InvitationToken.Generate().Value;

        var token = InvitationToken.Create(raw);

        token.Value.Should().Be(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithNullOrWhitespace_Throws(string? value)
    {
        var action = () => InvitationToken.Create(value!);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("0123456789012345678901234")] // 25 chars
    [InlineData("0123456789012345678901")]    // 22 chars but valid → handled by next test
    public void Create_WithWrongLength_Throws(string value)
    {
        if (value.Length == 22)
        {
            // Skip the boundary case here — covered by Create_WithValidToken_Succeeds.
            return;
        }

        var action = () => InvitationToken.Create(value);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*22 characters*");
    }

    [Theory]
    [InlineData("0123456789012345678901-")]   // 23 chars, has dash → length wins
    [InlineData("0123456789-12345678901")]    // 22 chars, has dash
    [InlineData("0123456789+12345678901")]    // 22 chars, has plus
    [InlineData("0123456789/12345678901")]    // 22 chars, has slash (invalid base62)
    [InlineData("01234567890123456789 1")]    // 22 chars, has space
    public void Create_WithNonBase62Characters_Throws(string value)
    {
        var action = () => InvitationToken.Create(value);

        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Implicit conversion

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var token = InvitationToken.Generate();

        string asString = token;

        asString.Should().Be(token.Value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var token = InvitationToken.Generate();

        token.ToString().Should().Be(token.Value);
    }

    #endregion
}
