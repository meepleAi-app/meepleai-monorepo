using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
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
        Assert.Equal(50, pageCount.Value);
    }

    [Fact]
    public void Constructor_MinimumValue_CreatesInstance()
    {
        // Act - 1 page is minimum valid count
        var pageCount = new PageCount(1);

        // Assert
        Assert.Equal(1, pageCount.Value);
    }

    [Fact]
    public void Constructor_ZeroPages_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PageCount(0));
        Assert.Contains("value", exception.Message);
    }

    [Fact]
    public void Constructor_NegativePages_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PageCount(-1));
        Assert.Contains("value", exception.Message);
    }

    #endregion

    #region Size Category Tests

    [Fact]
    public void IsSinglePage_OnePage_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Assert
        Assert.True(pageCount.IsSinglePage);
    }

    [Fact]
    public void IsSinglePage_TwoPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(2);

        // Assert
        Assert.False(pageCount.IsSinglePage);
    }

    [Fact]
    public void IsSmallPdf_TenPagesOrLess_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(10);

        // Assert
        Assert.True(pageCount.IsSmallPdf);
    }

    [Fact]
    public void IsSmallPdf_OnePage_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Assert
        Assert.True(pageCount.IsSmallPdf);
    }

    [Fact]
    public void IsSmallPdf_ElevenPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(11);

        // Assert
        Assert.False(pageCount.IsSmallPdf);
    }

    [Fact]
    public void IsMediumPdf_BetweenElevenAndHundred_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Assert
        Assert.True(pageCount.IsMediumPdf);
    }

    [Fact]
    public void IsMediumPdf_ExactlyElevenPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(11);

        // Assert
        Assert.True(pageCount.IsMediumPdf);
    }

    [Fact]
    public void IsMediumPdf_ExactlyHundredPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        Assert.True(pageCount.IsMediumPdf);
    }

    [Fact]
    public void IsMediumPdf_TenPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(10);

        // Assert
        Assert.False(pageCount.IsMediumPdf);
    }

    [Fact]
    public void IsLargePdf_AboveHundredPages_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(150);

        // Assert
        Assert.True(pageCount.IsLargePdf);
    }

    [Fact]
    public void IsLargePdf_ExactlyHundredPages_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        Assert.False(pageCount.IsLargePdf);
    }

    #endregion

    #region IsWithinLimit Tests

    [Fact]
    public void IsWithinLimit_BelowLimit_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Assert
        Assert.True(pageCount.IsWithinLimit(100));
    }

    [Fact]
    public void IsWithinLimit_ExactlyAtLimit_ReturnsTrue()
    {
        // Arrange
        var pageCount = new PageCount(100);

        // Assert
        Assert.True(pageCount.IsWithinLimit(100));
    }

    [Fact]
    public void IsWithinLimit_AboveLimit_ReturnsFalse()
    {
        // Arrange
        var pageCount = new PageCount(150);

        // Assert
        Assert.False(pageCount.IsWithinLimit(100));
    }

    [Fact]
    public void IsWithinLimit_ZeroMaxPages_ThrowsValidationException()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        Assert.Throws<ValidationException>(() => pageCount.IsWithinLimit(0));
    }

    [Fact]
    public void IsWithinLimit_NegativeMaxPages_ThrowsValidationException()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        Assert.Throws<ValidationException>(() => pageCount.IsWithinLimit(-1));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_SinglePage_FormatsCorrectly()
    {
        // Arrange
        var pageCount = new PageCount(1);

        // Act & Assert
        Assert.Equal("1 page(s)", pageCount.ToString());
    }

    [Fact]
    public void ToString_MultiplePages_FormatsCorrectly()
    {
        // Arrange
        var pageCount = new PageCount(50);

        // Act & Assert
        Assert.Equal("50 page(s)", pageCount.ToString());
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void SinglePage_IsOnePage()
    {
        // Assert
        Assert.Equal(1, PageCount.SinglePage.Value);
    }

    [Fact]
    public void TwoPages_IsTwoPages()
    {
        // Assert
        Assert.Equal(2, PageCount.TwoPages.Value);
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
        Assert.Equal(50, value);
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
        Assert.Equal(pageCount1, pageCount2);
        Assert.True(pageCount1 == pageCount2);
        Assert.False(pageCount1 != pageCount2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var pageCount1 = new PageCount(50);
        var pageCount2 = new PageCount(100);

        // Assert
        Assert.NotEqual(pageCount1, pageCount2);
        Assert.False(pageCount1 == pageCount2);
        Assert.True(pageCount1 != pageCount2);
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
        Assert.Equal(pages, pageCount.Value);
    }

    #endregion
}
