using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
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
        fileSize.Bytes.Should().Be(1024);
    }

    [Fact]
    public void Constructor_MinimumSize_CreatesInstance()
    {
        // Act - 1 byte is minimum valid size
        var fileSize = new FileSize(1);

        // Assert
        fileSize.Bytes.Should().Be(1);
    }

    [Fact]
    public void Constructor_ZeroBytes_ThrowsValidationException()
    {
        // Act & Assert
        var act = () => new FileSize(0);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("bytes");
    }

    [Fact]
    public void Constructor_NegativeBytes_ThrowsValidationException()
    {
        // Act & Assert
        var act2 = () => new FileSize(-1);
        var exception = act2.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("bytes");
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void FromMegabytes_ValidValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromMegabytes(5);

        // Assert
        fileSize.Bytes.Should().Be(5 * 1024 * 1024);
    }

    [Fact]
    public void FromMegabytes_FractionalValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromMegabytes(1.5);

        // Assert
        fileSize.Bytes.Should().Be((long)(1.5 * 1024 * 1024));
    }

    [Fact]
    public void FromMegabytes_ZeroValue_ThrowsValidationException()
    {
        // Act & Assert
        var act3 = () => FileSize.FromMegabytes(0);
        act3.Should().Throw<ValidationException>();
    }

    [Fact]
    public void FromMegabytes_NegativeValue_ThrowsValidationException()
    {
        // Act & Assert
        var act4 = () => FileSize.FromMegabytes(-1);
        act4.Should().Throw<ValidationException>();
    }

    [Fact]
    public void FromKilobytes_ValidValue_CreatesInstance()
    {
        // Act
        var fileSize = FileSize.FromKilobytes(500);

        // Assert
        fileSize.Bytes.Should().Be(500 * 1024);
    }

    [Fact]
    public void FromKilobytes_ZeroValue_ThrowsValidationException()
    {
        // Act & Assert
        var act5 = () => FileSize.FromKilobytes(0);
        act5.Should().Throw<ValidationException>();
    }

    [Fact]
    public void FromKilobytes_NegativeValue_ThrowsValidationException()
    {
        // Act & Assert
        var act6 = () => FileSize.FromKilobytes(-1);
        act6.Should().Throw<ValidationException>();
    }

    #endregion

    #region Conversion Properties Tests

    [Fact]
    public void Kilobytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2048); // 2 KB

        // Assert
        fileSize.Kilobytes.Should().Be(2.0);
    }

    [Fact]
    public void Megabytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2 * 1024 * 1024); // 2 MB

        // Assert
        fileSize.Megabytes.Should().Be(2.0);
    }

    [Fact]
    public void Gigabytes_CalculatesCorrectly()
    {
        // Arrange
        var fileSize = new FileSize(2L * 1024 * 1024 * 1024); // 2 GB

        // Assert
        fileSize.Gigabytes.Should().Be(2.0);
    }

    #endregion

    #region Size Category Tests

    [Fact]
    public void IsVerySmall_BelowOneKilobyte_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(512); // 512 bytes

        // Assert
        fileSize.IsVerySmall.Should().BeTrue();
    }

    [Fact]
    public void IsVerySmall_ExactlyOneKilobyte_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(1024); // 1 KB

        // Assert
        fileSize.IsVerySmall.Should().BeFalse();
    }

    [Fact]
    public void IsSmall_BelowOneMegabyte_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(500 * 1024); // 500 KB

        // Assert
        fileSize.IsSmall.Should().BeTrue();
    }

    [Fact]
    public void IsSmall_ExactlyOneMegabyte_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(1024 * 1024); // 1 MB

        // Assert
        fileSize.IsSmall.Should().BeFalse();
    }

    [Fact]
    public void IsMedium_BetweenOneMBAndTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5); // 5 MB

        // Assert
        fileSize.IsMedium.Should().BeTrue();
    }

    [Fact]
    public void IsMedium_ExactlyOneMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(1); // 1 MB

        // Assert
        fileSize.IsMedium.Should().BeTrue();
    }

    [Fact]
    public void IsMedium_ExactlyTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(10); // 10 MB

        // Assert
        fileSize.IsMedium.Should().BeTrue();
    }

    [Fact]
    public void IsLarge_AboveTenMB_ReturnsTrue()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(15); // 15 MB

        // Assert
        fileSize.IsLarge.Should().BeTrue();
    }

    [Fact]
    public void IsLarge_ExactlyTenMB_ReturnsFalse()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(10); // 10 MB

        // Assert
        fileSize.IsLarge.Should().BeFalse();
    }

    #endregion

    #region IsWithinLimit Tests

    [Fact]
    public void IsWithinLimit_BelowLimit_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Assert
        fileSize.IsWithinLimit(2000).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_ExactlyAtLimit_ReturnsTrue()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Assert
        fileSize.IsWithinLimit(1000).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_AboveLimit_ReturnsFalse()
    {
        // Arrange
        var fileSize = new FileSize(2000);

        // Assert
        fileSize.IsWithinLimit(1000).Should().BeFalse();
    }

    [Fact]
    public void IsWithinLimit_ZeroMaxBytes_ThrowsValidationException()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Act & Assert
        var act7 = () => fileSize.IsWithinLimit(0);
        act7.Should().Throw<ValidationException>();
    }

    [Fact]
    public void IsWithinLimit_NegativeMaxBytes_ThrowsValidationException()
    {
        // Arrange
        var fileSize = new FileSize(1000);

        // Act & Assert
        var act8 = () => fileSize.IsWithinLimit(-1);
        act8.Should().Throw<ValidationException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_BytesUnderOneKB_ShowsBytes()
    {
        // Arrange
        var fileSize = new FileSize(512);

        // Act & Assert
        fileSize.ToString().Should().Be("512 bytes");
    }

    [Fact]
    public void ToString_KilobytesUnderOneMB_ShowsKB()
    {
        // Arrange
        var fileSize = new FileSize(5 * 1024);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for KB suffix and value
        result.Should().Contain("KB");
        result.Should().StartWith("5");
    }

    [Fact]
    public void ToString_MegabytesUnderOneGB_ShowsMB()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for MB suffix and value
        result.Should().Contain("MB");
        result.Should().StartWith("5");
    }

    [Fact]
    public void ToString_Gigabytes_ShowsGB()
    {
        // Arrange
        var fileSize = new FileSize(2L * 1024 * 1024 * 1024);
        var result = fileSize.ToString();

        // Assert - Culture-independent: check for GB suffix and value
        result.Should().Contain("GB");
        result.Should().StartWith("2");
    }

    [Fact]
    public void ToMegabytesString_FormatsCorrectly()
    {
        // Arrange
        var fileSize = FileSize.FromMegabytes(5.3);
        var result = fileSize.ToMegabytesString();

        // Assert - Culture-independent: check for MB suffix and value
        result.Should().Contain("MB");
        result.Should().StartWith("5");
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void OneByte_IsOneByte()
    {
        // Assert
        FileSize.OneByte.Bytes.Should().Be(1);
    }

    [Fact]
    public void OneKilobyte_Is1024Bytes()
    {
        // Assert
        FileSize.OneKilobyte.Bytes.Should().Be(1024);
    }

    [Fact]
    public void OneMegabyte_Is1048576Bytes()
    {
        // Assert
        FileSize.OneMegabyte.Bytes.Should().Be(1024 * 1024);
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
        value.Should().Be(2048);
    }

    [Fact]
    public void ImplicitConversionToLong_NullFileSize_ThrowsArgumentNullException()
    {
        // Arrange
        FileSize? fileSize = null;

        // Act & Assert
        var act9 = () =>
        {
            long _ = fileSize!;
        };
        act9.Should().Throw<ArgumentNullException>();
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
        fileSize2.Should().Be(fileSize1);
        (fileSize1 == fileSize2).Should().BeTrue();
        (fileSize1 != fileSize2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentBytes_AreNotEqual()
    {
        // Arrange
        var fileSize1 = new FileSize(1024);
        var fileSize2 = new FileSize(2048);

        // Assert
        fileSize2.Should().NotBe(fileSize1);
        (fileSize1 == fileSize2).Should().BeFalse();
        (fileSize1 != fileSize2).Should().BeTrue();
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
        fileSize.Bytes.Should().Be(bytes);
    }

    #endregion
}
