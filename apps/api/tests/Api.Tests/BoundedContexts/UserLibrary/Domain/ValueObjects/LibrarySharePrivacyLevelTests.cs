using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the LibrarySharePrivacyLevel enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 8
/// </summary>
[Trait("Category", "Unit")]
public sealed class LibrarySharePrivacyLevelTests
{
    #region Enum Value Tests

    [Fact]
    public void LibrarySharePrivacyLevel_Public_HasCorrectValue()
    {
        ((int)LibrarySharePrivacyLevel.Public).Should().Be(0);
    }

    [Fact]
    public void LibrarySharePrivacyLevel_Unlisted_HasCorrectValue()
    {
        ((int)LibrarySharePrivacyLevel.Unlisted).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void LibrarySharePrivacyLevel_HasTwoValues()
    {
        var values = Enum.GetValues<LibrarySharePrivacyLevel>();
        values.Should().HaveCount(2);
    }

    [Fact]
    public void LibrarySharePrivacyLevel_AllValuesCanBeParsed()
    {
        var names = new[] { "Public", "Unlisted" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<LibrarySharePrivacyLevel>(name);
            parsed.Should().BeOneOf(Enum.GetValues<LibrarySharePrivacyLevel>());
        }
    }

    [Fact]
    public void LibrarySharePrivacyLevel_ToString_ReturnsExpectedNames()
    {
        LibrarySharePrivacyLevel.Public.ToString().Should().Be("Public");
        LibrarySharePrivacyLevel.Unlisted.ToString().Should().Be("Unlisted");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void LibrarySharePrivacyLevel_Public_IsDiscoverable()
    {
        // Public = 0 indicates it's the default/more open option
        var publicLevel = LibrarySharePrivacyLevel.Public;
        ((int)publicLevel).Should().Be(0);
    }

    [Fact]
    public void LibrarySharePrivacyLevel_Unlisted_RequiresDirectLink()
    {
        // Unlisted = 1 indicates restricted access
        var unlistedLevel = LibrarySharePrivacyLevel.Unlisted;
        ((int)unlistedLevel).Should().BeGreaterThan((int)LibrarySharePrivacyLevel.Public);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void LibrarySharePrivacyLevel_CastFromInt_ReturnsCorrectLevels()
    {
        ((LibrarySharePrivacyLevel)0).Should().Be(LibrarySharePrivacyLevel.Public);
        ((LibrarySharePrivacyLevel)1).Should().Be(LibrarySharePrivacyLevel.Unlisted);
    }

    [Fact]
    public void LibrarySharePrivacyLevel_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(LibrarySharePrivacyLevel), 0).Should().BeTrue();
        Enum.IsDefined(typeof(LibrarySharePrivacyLevel), 1).Should().BeTrue();
    }

    [Fact]
    public void LibrarySharePrivacyLevel_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(LibrarySharePrivacyLevel), 2).Should().BeFalse();
        Enum.IsDefined(typeof(LibrarySharePrivacyLevel), -1).Should().BeFalse();
    }

    #endregion
}
