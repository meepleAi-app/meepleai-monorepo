using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates;

/// <summary>
/// Tests for the ConfigCategory enum and extensions.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfigCategoryTests
{
    #region Enum Value Tests

    [Fact]
    public void ConfigCategory_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<ConfigCategory>().Should().HaveCount(4);
        Enum.IsDefined(ConfigCategory.Global).Should().BeTrue();
        Enum.IsDefined(ConfigCategory.Email).Should().BeTrue();
        Enum.IsDefined(ConfigCategory.Slack).Should().BeTrue();
        Enum.IsDefined(ConfigCategory.PagerDuty).Should().BeTrue();
    }

    #endregion

    #region ToDisplayString Tests

    [Fact]
    public void ToDisplayString_WithGlobal_ReturnsGlobal()
    {
        // Act
        var result = ConfigCategory.Global.ToDisplayString();

        // Assert
        result.Should().Be("Global");
    }

    [Fact]
    public void ToDisplayString_WithEmail_ReturnsEmail()
    {
        // Act
        var result = ConfigCategory.Email.ToDisplayString();

        // Assert
        result.Should().Be("Email");
    }

    [Fact]
    public void ToDisplayString_WithSlack_ReturnsSlack()
    {
        // Act
        var result = ConfigCategory.Slack.ToDisplayString();

        // Assert
        result.Should().Be("Slack");
    }

    [Fact]
    public void ToDisplayString_WithPagerDuty_ReturnsPagerDuty()
    {
        // Act
        var result = ConfigCategory.PagerDuty.ToDisplayString();

        // Assert
        result.Should().Be("PagerDuty");
    }

    [Fact]
    public void ToDisplayString_WithInvalidCategory_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidCategory = (ConfigCategory)999;

        // Act
        var action = () => invalidCategory.ToDisplayString();

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FromString Tests

    [Fact]
    public void FromString_WithLowercaseGlobal_ReturnsGlobal()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("global");

        // Assert
        result.Should().Be(ConfigCategory.Global);
    }

    [Fact]
    public void FromString_WithLowercaseEmail_ReturnsEmail()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("email");

        // Assert
        result.Should().Be(ConfigCategory.Email);
    }

    [Fact]
    public void FromString_WithLowercaseSlack_ReturnsSlack()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("slack");

        // Assert
        result.Should().Be(ConfigCategory.Slack);
    }

    [Fact]
    public void FromString_WithLowercasePagerDuty_ReturnsPagerDuty()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("pagerduty");

        // Assert
        result.Should().Be(ConfigCategory.PagerDuty);
    }

    [Fact]
    public void FromString_WithUppercaseGLOBAL_ReturnsGlobal()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("GLOBAL");

        // Assert
        result.Should().Be(ConfigCategory.Global);
    }

    [Fact]
    public void FromString_WithUppercaseEMAIL_ReturnsEmail()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("EMAIL");

        // Assert
        result.Should().Be(ConfigCategory.Email);
    }

    [Fact]
    public void FromString_WithUppercaseSLACK_ReturnsSlack()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("SLACK");

        // Assert
        result.Should().Be(ConfigCategory.Slack);
    }

    [Fact]
    public void FromString_WithUppercasePAGERDUTY_ReturnsPagerDuty()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("PAGERDUTY");

        // Assert
        result.Should().Be(ConfigCategory.PagerDuty);
    }

    [Fact]
    public void FromString_WithMixedCaseGlobal_ReturnsCategory()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("Global");

        // Assert
        result.Should().Be(ConfigCategory.Global);
    }

    [Fact]
    public void FromString_WithMixedCaseEmail_ReturnsCategory()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("Email");

        // Assert
        result.Should().Be(ConfigCategory.Email);
    }

    [Fact]
    public void FromString_WithMixedCaseSlack_ReturnsCategory()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("Slack");

        // Assert
        result.Should().Be(ConfigCategory.Slack);
    }

    [Fact]
    public void FromString_WithMixedCasePagerDuty_ReturnsCategory()
    {
        // Act
        var result = ConfigCategoryExtensions.FromString("PagerDuty");

        // Assert
        result.Should().Be(ConfigCategory.PagerDuty);
    }

    [Fact]
    public void FromString_WithInvalidCategory_ThrowsArgumentException()
    {
        // Act
        var action = () => ConfigCategoryExtensions.FromString("invalid");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*Invalid category: invalid*");
    }

    [Fact]
    public void FromString_WithEmptyString_ThrowsArgumentException()
    {
        // Act
        var action = () => ConfigCategoryExtensions.FromString("");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void Roundtrip_Global_PreservesValue()
    {
        // Act
        var displayString = ConfigCategory.Global.ToDisplayString();
        var restored = ConfigCategoryExtensions.FromString(displayString);

        // Assert
        restored.Should().Be(ConfigCategory.Global);
    }

    [Fact]
    public void Roundtrip_Email_PreservesValue()
    {
        // Act
        var displayString = ConfigCategory.Email.ToDisplayString();
        var restored = ConfigCategoryExtensions.FromString(displayString);

        // Assert
        restored.Should().Be(ConfigCategory.Email);
    }

    [Fact]
    public void Roundtrip_Slack_PreservesValue()
    {
        // Act
        var displayString = ConfigCategory.Slack.ToDisplayString();
        var restored = ConfigCategoryExtensions.FromString(displayString);

        // Assert
        restored.Should().Be(ConfigCategory.Slack);
    }

    [Fact]
    public void Roundtrip_PagerDuty_PreservesValue()
    {
        // Act
        var displayString = ConfigCategory.PagerDuty.ToDisplayString();
        var restored = ConfigCategoryExtensions.FromString(displayString);

        // Assert
        restored.Should().Be(ConfigCategory.PagerDuty);
    }

    #endregion
}
