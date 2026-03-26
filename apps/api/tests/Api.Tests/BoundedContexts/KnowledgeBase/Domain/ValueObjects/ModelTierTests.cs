using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for ModelTier enum and extensions
/// Issue #3377: Models Tier Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelTierTests
{
    #region Enum Values Tests

    [Fact]
    public void ModelTier_HasCorrectValues()
    {
        ((int)ModelTier.Free).Should().Be(0);
        ((int)ModelTier.Normal).Should().Be(1);
        ((int)ModelTier.Premium).Should().Be(2);
        ((int)ModelTier.Custom).Should().Be(3);
    }

    [Fact]
    public void ModelTier_Hierarchy_FreeIsLowest()
    {
        (ModelTier.Free < ModelTier.Normal).Should().BeTrue();
        (ModelTier.Normal < ModelTier.Premium).Should().BeTrue();
        (ModelTier.Premium < ModelTier.Custom).Should().BeTrue();
    }

    #endregion

    #region CanAccess Tests

    [Theory]
    [InlineData(ModelTier.Free, ModelTier.Free, true)]
    [InlineData(ModelTier.Free, ModelTier.Normal, false)]
    [InlineData(ModelTier.Free, ModelTier.Premium, false)]
    [InlineData(ModelTier.Free, ModelTier.Custom, false)]
    [InlineData(ModelTier.Normal, ModelTier.Free, true)]
    [InlineData(ModelTier.Normal, ModelTier.Normal, true)]
    [InlineData(ModelTier.Normal, ModelTier.Premium, false)]
    [InlineData(ModelTier.Normal, ModelTier.Custom, false)]
    [InlineData(ModelTier.Premium, ModelTier.Free, true)]
    [InlineData(ModelTier.Premium, ModelTier.Normal, true)]
    [InlineData(ModelTier.Premium, ModelTier.Premium, true)]
    [InlineData(ModelTier.Premium, ModelTier.Custom, false)]
    [InlineData(ModelTier.Custom, ModelTier.Free, true)]
    [InlineData(ModelTier.Custom, ModelTier.Normal, true)]
    [InlineData(ModelTier.Custom, ModelTier.Premium, true)]
    [InlineData(ModelTier.Custom, ModelTier.Custom, true)]
    public void CanAccess_VariousCombinations_ReturnsExpectedResult(
        ModelTier userTier, ModelTier modelTier, bool expectedCanAccess)
    {
        // Act
        var canAccess = userTier.CanAccess(modelTier);

        // Assert
        canAccess.Should().Be(expectedCanAccess);
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("free", ModelTier.Free)]
    [InlineData("Free", ModelTier.Free)]
    [InlineData("FREE", ModelTier.Free)]
    [InlineData("normal", ModelTier.Normal)]
    [InlineData("Normal", ModelTier.Normal)]
    [InlineData("NORMAL", ModelTier.Normal)]
    [InlineData("premium", ModelTier.Premium)]
    [InlineData("Premium", ModelTier.Premium)]
    [InlineData("PREMIUM", ModelTier.Premium)]
    [InlineData("custom", ModelTier.Custom)]
    [InlineData("Custom", ModelTier.Custom)]
    [InlineData("CUSTOM", ModelTier.Custom)]
    public void Parse_ValidTier_ReturnsTier(string value, ModelTier expectedTier)
    {
        // Act
        var tier = ModelTierExtensions.Parse(value);

        // Assert
        tier.Should().Be(expectedTier);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("enterprise")]
    [InlineData("basic")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_InvalidTier_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Action act = () => ModelTierExtensions.Parse(value);
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("Invalid model tier");
    }

    #endregion

    #region TryParse Tests

    [Theory]
    [InlineData("free", true, ModelTier.Free)]
    [InlineData("normal", true, ModelTier.Normal)]
    [InlineData("premium", true, ModelTier.Premium)]
    [InlineData("custom", true, ModelTier.Custom)]
    [InlineData("invalid", false, ModelTier.Free)]
    [InlineData("", false, ModelTier.Free)]
    [InlineData(null, false, ModelTier.Free)]
    [InlineData("   ", false, ModelTier.Free)]
    public void TryParse_VariousInputs_ReturnsExpectedResult(
        string? value, bool expectedSuccess, ModelTier expectedTier)
    {
        // Act
        var success = ModelTierExtensions.TryParse(value, out var tier);

        // Assert
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            tier.Should().Be(expectedTier);
        }
    }

    #endregion

    #region GetDisplayName Tests

    [Theory]
    [InlineData(ModelTier.Free, "Free")]
    [InlineData(ModelTier.Normal, "Normal")]
    [InlineData(ModelTier.Premium, "Premium")]
    [InlineData(ModelTier.Custom, "Custom")]
    public void GetDisplayName_ReturnsExpectedValue(ModelTier tier, string expectedDisplayName)
    {
        // Act
        var displayName = tier.GetDisplayName();

        // Assert
        displayName.Should().Be(expectedDisplayName);
    }

    #endregion
}
