using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for FileSize value object.
/// Issue #2381: Comprehensive validation logic testing for file size handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class FileSizeTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidBytes_CreatesInstance()
    {
        // Act
        var fileSize = new FileSize(1024);

        // Assert
        Assert.Equal(1024, fileSize.Bytes);
    }

    [Fact]
    public void Constructor_MinimumSize_CreatesInstance()
    {
        // Act - 1 byte is minimum valid size
        var fileSize = new FileSize(1);

        // Assert
        Assert.Equal(1, fileSize.Bytes);
    }

    [Fact]
    public void Constructor_ZeroBytes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new FileSize(0));
        Assert.Contains("bytes", exception.Message);
    }

    [Fact]
    public void Constructor_NegativeBytes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new FileSize(-1));
        Assert.Contains("bytes", exception.Message);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void FromMegabytes_ValidValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromMegabytes(5);

        // Assert
        Assert.Equal(5 * 1024 * 1024, fileSize.Bytes);
    }

    [Fact]
    public void FromMegabytes_FractionalValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromMegabytes(1.5);

        // Assert
        Assert.Equal((long)(1.5 * 1024 * 1024), fileSize.Bytes);
    }

    [Fact]
    public void FromMegabytes_ZeroValue_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => FileSize.FromMegabytes(0));
    }

    [Fact]
    public void FromMegabytes_NegativeValue_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => FileSize.FromMegabytes(-1));
    }

    [Fact]
    public void FromKilobytes_ValidValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromKilobytes(500);

        // Assert
        Assert.Equal(500 * 1024, fileSize.Bytes);
    }

    [Fact]
    public void FromKilobytes_ZeroValue_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => FileSize.FromKilobytes(0));
    }

    [Fact]
    public void FromKilobytes_NegativeValue_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => FileSize.FromKilobytes(-1));
    }

    #endregion

    #region Conversion Properties Tests

    [Fact]
    public void Kilobytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2048); // 2 KB

        // Assert
        Assert.Equal(2.0, fileSize.Kilobytes);
    }

    [Fact]
    public void Megabytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2 * 1024 * 1024); // 2 MB

        // Assert
        Assert.Equal(2.0, fileSize.Megabytes);
    }

    [Fact]
    public void Gigabytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2L * 1024 * 1024 * 1024); // 2 GB

        // Assert
        Assert.Equal(2.0, fileSize.Gigabytes);
    }

    #endregion

    #region Size Category Tests

    [Fact]
    public void IsVerySmall_BelowOneKilobyte_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(512); // 512 bytes

        // Assert
        Assert.True(fileSize.IsVerySmall);
    }

    [Fact]
    public void IsVerySmall_ExactlyOneKilobyte_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(1024); // 1 KB

        // Assert
        Assert.False(fileSize.IsVerySmall);
    }

    [Fact]
    public void IsSmall_BelowOneMegabyte_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(500 * 1024); // 500 KB

        // Assert
        Assert.True(fileSize.IsSmall);
    }

    [Fact]
    public void IsSmall_ExactlyOneMegabyte_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(1024 * 1024); // 1 MB

        // Assert
        Assert.False(fileSize.IsSmall);
    }

    [Fact]
    public void IsMedium_BetweenOneMBAndTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5); // 5 MB

        // Assert
        Assert.True(fileSize.IsMedium);
    }

    [Fact]
    public void IsMedium_ExactlyOneMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(1); // 1 MB

        // Assert
        Assert.True(fileSize.IsMedium);
    }

    [Fact]
    public void IsMedium_ExactlyTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(10); // 10 MB

        // Assert
        Assert.True(fileSize.IsMedium);
    }

    [Fact]
    public void IsLarge_AboveTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(15); // 15 MB

        // Assert
        Assert.True(fileSize.IsLarge);
    }

    [Fact]
    public void IsLarge_ExactlyTenMB_ReturnsFalse()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(10); // 10 MB

        // Assert
        Assert.False(fileSize.IsLarge);
    }

    #endregion

    #region IsWithinLimit Tests

    [Fact]
    public void IsWithinLimit_BelowLimit_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Assert
        Assert.True(fileSize.IsWithinLimit(2000));
    }

    [Fact]
    public void IsWithinLimit_ExactlyAtLimit_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Assert
        Assert.True(fileSize.IsWithinLimit(1000));
    }

    [Fact]
    public void IsWithinLimit_AboveLimit_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(2000);

        // Assert
        Assert.False(fileSize.IsWithinLimit(1000));
    }

    [Fact]
    public void IsWithinLimit_ZeroMaxBytes_ThrowsValidationException()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Act & Assert
        Assert.Throws<ValidationException>(() => fileSize.IsWithinLimit(0));
    }

    [Fact]
    public void IsWithinLimit_NegativeMaxBytes_ThrowsValidationException()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Act & Assert
        Assert.Throws<ValidationException>(() => fileSize.IsWithinLimit(-1));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_BytesUnderOneKB_ShowsBytes()
    {
        // Arrange
        var fileSize = new FileSize(512);

        // Act & Assert
        Assert.Equal("512 bytes", fileSize.ToString());
    }

    [Fact]
    public void ToString_KilobytesUnderOneMB_ShowsKB()
    {
        // Arrange
        var fileSize = new FileSize(5 * 1024);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for KB suffix and value
        Assert.Contains("KB", result);
        Assert.StartsWith("5", result);
    }

    [Fact]
    public void ToString_MegabytesUnderOneGB_ShowsMB()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for MB suffix and value
        Assert.Contains("MB", result);
        Assert.StartsWith("5", result);
    }

    [Fact]
    public void ToString_Gigabytes_ShowsGB()
    {
        // Arrange
        var fileSize = new FileSize(2L * 1024 * 1024 * 1024);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for GB suffix and value
        Assert.Contains("GB", result);
        Assert.StartsWith("2", result);
    }

    [Fact]
    public void ToMegabytesString_FormatsCorrectly()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5.3);
        var result = fileSize.ToMegabytesString();

        // Assert - Culture-independent: check for MB suffix and value
        Assert.Contains("MB", result);
        Assert.StartsWith("5", result);
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void OneByte_IsOneByte()
    {
        // Assert
        Assert.Equal(1, FileSize.OneByte.Bytes);
    }

    [Fact]
    public void OneKilobyte_Is1024Bytes()
    {
        // Assert
        Assert.Equal(1024, FileSize.OneKilobyte.Bytes);
    }

    [Fact]
    public void OneMegabyte_Is1048576Bytes()
    {
        // Assert
        Assert.Equal(1024 * 1024, FileSize.OneMegabyte.Bytes);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToLong_ReturnsBytes()
    {
        // Arrange
        var fileSize = new FileSize(2048);

        // Act
        long value = fileSize;

        // Assert
        Assert.Equal(2048, value);
    }

    [Fact]
    public void ImplicitConversionToLong_NullFileSize_ThrowsArgumentNullException()
    {
        // Arrange
        FileSize? fileSize = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            long _ = fileSize!;
        });
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameBytes_AreEqual()
    {
        // Arrange
        var fileSize1 = new FileSize(1024);
        var fileSize2 = new FileSize(1024);

        // Assert
        Assert.Equal(fileSize1, fileSize2);
        Assert.True(fileSize1 == fileSize2);
        Assert.False(fileSize1 != fileSize2);
    }

    [Fact]
    public void Equality_DifferentBytes_AreNotEqual()
    {
        // Arrange
        var fileSize1 = new FileSize(1024);
        var fileSize2 = new FileSize(2048);

        // Assert
        Assert.NotEqual(fileSize1, fileSize2);
        Assert.False(fileSize1 == fileSize2);
        Assert.True(fileSize1 != fileSize2);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(1)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1048575)]
    [InlineData(1048576)]
    [InlineData(10485760)]
    [InlineData(10485761)]
    public void Constructor_BoundaryValues_CreatesInstance(long bytes)
    {
        // Act
        var fileSize = new FileSize(bytes);

        // Assert
        Assert.Equal(bytes, fileSize.Bytes);
    }

    #endregion
}
