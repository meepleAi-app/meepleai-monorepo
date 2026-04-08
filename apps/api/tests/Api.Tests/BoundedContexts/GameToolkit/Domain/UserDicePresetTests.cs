using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class UserDicePresetTests
{
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateToolkit()
    {
        return new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), GameId, "Test Toolkit", UserId);
    }

    // ========================================================================
    // AddUserDicePreset
    // ========================================================================

    [Fact]
    public void AddUserDicePreset_WithValidParams_AddsPreset()
    {
        var toolkit = CreateToolkit();
        var presetUserId = Guid.NewGuid();

        toolkit.AddUserDicePreset(presetUserId, "Attack Roll", "2d6+3");

        toolkit.UserDicePresets.Should().HaveCount(1);
        var preset = toolkit.UserDicePresets[0];
        preset.UserId.Should().Be(presetUserId);
        preset.Name.Should().Be("Attack Roll");
        preset.Formula.Should().Be("2d6+3");
        preset.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddUserDicePreset_Over20Limit_ThrowsInvalidOperationException()
    {
        var toolkit = CreateToolkit();
        var presetUserId = Guid.NewGuid();

        for (int i = 0; i < 20; i++)
            toolkit.AddUserDicePreset(presetUserId, $"Preset {i}", $"{i + 1}d6");

        var act = () => toolkit.AddUserDicePreset(presetUserId, "Preset 20", "1d20");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*20*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUserDicePreset_WithEmptyName_ThrowsArgumentException(string name)
    {
        var toolkit = CreateToolkit();

        var act = () => toolkit.AddUserDicePreset(Guid.NewGuid(), name, "2d6");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUserDicePreset_WithEmptyFormula_ThrowsArgumentException(string formula)
    {
        var toolkit = CreateToolkit();

        var act = () => toolkit.AddUserDicePreset(Guid.NewGuid(), "Test", formula);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddUserDicePreset_WithEmptyUserId_ThrowsArgumentException()
    {
        var toolkit = CreateToolkit();

        var act = () => toolkit.AddUserDicePreset(Guid.Empty, "Test", "2d6");

        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // RemoveUserDicePreset
    // ========================================================================

    [Fact]
    public void RemoveUserDicePreset_ExistingPreset_ReturnsTrue()
    {
        var toolkit = CreateToolkit();
        var presetUserId = Guid.NewGuid();
        toolkit.AddUserDicePreset(presetUserId, "Attack Roll", "2d6+3");

        var result = toolkit.RemoveUserDicePreset(presetUserId, "Attack Roll");

        result.Should().BeTrue();
        toolkit.UserDicePresets.Should().BeEmpty();
    }

    [Fact]
    public void RemoveUserDicePreset_WrongUser_ReturnsFalse()
    {
        var toolkit = CreateToolkit();
        var presetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        toolkit.AddUserDicePreset(presetUserId, "Attack Roll", "2d6+3");

        var result = toolkit.RemoveUserDicePreset(otherUserId, "Attack Roll");

        result.Should().BeFalse();
        toolkit.UserDicePresets.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveUserDicePreset_NonExistentName_ReturnsFalse()
    {
        var toolkit = CreateToolkit();
        var presetUserId = Guid.NewGuid();
        toolkit.AddUserDicePreset(presetUserId, "Attack Roll", "2d6+3");

        var result = toolkit.RemoveUserDicePreset(presetUserId, "NonExistent");

        result.Should().BeFalse();
        toolkit.UserDicePresets.Should().HaveCount(1);
    }

    // ========================================================================
    // GetUserDicePresets
    // ========================================================================

    [Fact]
    public void GetUserDicePresets_ReturnsOnlyPresetsForSpecificUser()
    {
        var toolkit = CreateToolkit();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        toolkit.AddUserDicePreset(user1, "Attack Roll", "2d6+3");
        toolkit.AddUserDicePreset(user2, "Damage Roll", "1d8+5");
        toolkit.AddUserDicePreset(user1, "Save Roll", "1d20+2");

        var user1Presets = toolkit.GetUserDicePresets(user1);

        user1Presets.Should().HaveCount(2);
        user1Presets.Should().OnlyContain(p => p.UserId == user1);
    }

    // ========================================================================
    // UserDicePreset Value Object
    // ========================================================================

    [Fact]
    public void UserDicePreset_TrimsNameAndFormula()
    {
        var preset = new UserDicePreset(Guid.NewGuid(), "  Attack Roll  ", "  2d6+3  ", DateTime.UtcNow);

        preset.Name.Should().Be("Attack Roll");
        preset.Formula.Should().Be("2d6+3");
    }

    [Fact]
    public void UserDicePreset_NameExceeds50Chars_ThrowsArgumentException()
    {
        var act = () => new UserDicePreset(Guid.NewGuid(), new string('A', 51), "2d6", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*50*");
    }

    [Fact]
    public void UserDicePreset_FormulaExceeds100Chars_ThrowsArgumentException()
    {
        var act = () => new UserDicePreset(Guid.NewGuid(), "Test", new string('A', 101), DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*100*");
    }
}
