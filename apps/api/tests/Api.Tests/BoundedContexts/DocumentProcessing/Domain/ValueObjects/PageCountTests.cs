using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for PageCount value object.
/// Issue #2381: Comprehensive validation logic testing for page count handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PageCountTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidValue_CreatesInstance()
    {
        // Act
        var pageCount = new PageCount(50);

        // Assert
        pageCount.Value.Should().Be(50);
    }

    [Fact]
    public void Constructor_MinimumValue_CreatesInstance()
    {
        // Act - 1 page is minimum valid count
        var pageCount = new PageCount(1);

        // Assert
        pageCount.Value.Should().Be(1);
    }

    [Fact]
    public void Constructor_ZeroPages_ThrowsValidationException()
    {
        // Act & Assert
        var act = () => new PageCount(0);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("value");
    }

    [Fact]
    public void Constructor_NegativePages_ThrowsValidationException()
    {
        // Act & Assert
        var act2 = () => new PageCount(-1);
        var exception = act2.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("value");
    }

    #endregion

    #region Size Category Tests

    [Fact]
    public void IsSinglePage_OnePage_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Assert
        pageCount.IsSinglePage.Should().BeTrue();
    }

    [Fact]
    public void IsSinglePage_TwoPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(2);

        // Assert
        pageCount.IsSinglePage.Should().BeFalse();
    }

    [Fact]
    public void IsSmallPdf_TenPagesOrLess_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(10);

        // Assert
        pageCount.IsSmallPdf.Should().BeTrue();
    }

    [Fact]
    public void IsSmallPdf_OnePage_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Assert
        pageCount.IsSmallPdf.Should().BeTrue();
    }

    [Fact]
    public void IsSmallPdf_ElevenPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(11);

        // Assert
        pageCount.IsSmallPdf.Should().BeFalse();
    }

    [Fact]
    public void IsMediumPdf_BetweenElevenAndHundred_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Assert
        pageCount.IsMediumPdf.Should().BeTrue();
    }

    [Fact]
    public void IsMediumPdf_ExactlyElevenPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(11);

        // Assert
        pageCount.IsMediumPdf.Should().BeTrue();
    }

    [Fact]
    public void IsMediumPdf_ExactlyHundredPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        pageCount.IsMediumPdf.Should().BeTrue();
    }

    [Fact]
    public void IsMediumPdf_TenPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(10);

        // Assert
        pageCount.IsMediumPdf.Should().BeFalse();
    }

    [Fact]
    public void IsLargePdf_AboveHundredPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(150);

        // Assert
        pageCount.IsLargePdf.Should().BeTrue();
    }

    [Fact]
    public void IsLargePdf_ExactlyHundredPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        pageCount.IsLargePdf.Should().BeFalse();
    }

    #endregion

    #region IsWithinLimit Tests

    [Fact]
    public void IsWithinLimit_BelowLimit_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Assert
        pageCount.IsWithinLimit(100).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_ExactlyAtLimit_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        pageCount.IsWithinLimit(100).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_AboveLimit_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(150);

        // Assert
        pageCount.IsWithinLimit(100).Should().BeFalse();
    }

    [Fact]
    public void IsWithinLimit_ZeroMaxPages_ThrowsValidationException()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        var act3 = () => pageCount.IsWithinLimit(0);
        act3.Should().Throw<ValidationException>();
    }

    [Fact]
    public void IsWithinLimit_NegativeMaxPages_ThrowsValidationException()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        var act4 = () => pageCount.IsWithinLimit(-1);
        act4.Should().Throw<ValidationException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_SinglePage_FormatsCorrectly()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Act & Assert
        pageCount.ToString().Should().Be("1 page(s)");
    }

    [Fact]
    public void ToString_MultiplePages_FormatsCorrectly()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        pageCount.ToString().Should().Be("50 page(s)");
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void SinglePage_IsOnePage()
    {
        // Assert
        PageCount.SinglePage.Value.Should().Be(1);
    }

    [Fact]
    public void TwoPages_IsTwoPages()
    {
        // Assert
        PageCount.TwoPages.Value.Should().Be(2);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToInt_ReturnsValue()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act
        int value = pageCount;

        // Assert
        value.Should().Be(50);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var pageCount1 = new PageCount(50);
        var pageCount2 = new PageCount(50);

        // Assert
        pageCount2.Should().Be(pageCount1);
        (pageCount1 == pageCount2).Should().BeTrue();
        (pageCount1 != pageCount2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var pageCount1 = new PageCount(50);
        var pageCount2 = new PageCount(100);

        // Assert
        pageCount2.Should().NotBe(pageCount1);
        (pageCount1 == pageCount2).Should().BeFalse();
        (pageCount1 != pageCount2).Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(500)]
    public void Constructor_BoundaryValues_CreatesInstance(int pages)
    {
        // Act
        var pageCount = new PageCount(pages);

        // Assert
        pageCount.Value.Should().Be(pages);
    }

    #endregion
}
