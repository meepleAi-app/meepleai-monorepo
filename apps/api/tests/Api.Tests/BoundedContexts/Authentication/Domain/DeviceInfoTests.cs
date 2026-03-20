using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Unit tests for DeviceInfo value object (User-Agent parsing).
/// Issue #3340: Login device tracking and management.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class DeviceInfoTests
{
    #region Parse Tests - Desktop Browsers

    [Fact]
    public void Parse_ChromeOnWindows_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Chrome");
        deviceInfo.BrowserVersion.Should().StartWith("120");
        deviceInfo.OperatingSystem.Should().Be("Windows");
        deviceInfo.OsVersion.Should().Be("10/11");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Parse_FirefoxOnMac_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.2; rv:121.0) Gecko/20100101 Firefox/121.0";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Firefox");
        deviceInfo.BrowserVersion.Should().StartWith("121");
        deviceInfo.OperatingSystem.Should().Be("macOS");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Parse_SafariOnMac_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Safari");
        deviceInfo.BrowserVersion.Should().StartWith("17");
        deviceInfo.OperatingSystem.Should().Be("macOS");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Parse_EdgeOnWindows_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.2210.91";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Edge");
        deviceInfo.BrowserVersion.Should().StartWith("120");
        deviceInfo.OperatingSystem.Should().Be("Windows");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Parse_ChromeOnLinux_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Chrome");
        deviceInfo.OperatingSystem.Should().Be("Linux");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    #endregion

    #region Parse Tests - Mobile Devices

    [Fact]
    public void Parse_SafariOnIPhone_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Phone");
        deviceInfo.Browser.Should().Be("Safari");
        deviceInfo.OperatingSystem.Should().Be("iOS");
        deviceInfo.IsMobile.Should().BeTrue();
    }

    [Fact]
    public void Parse_ChromeOnAndroidPhone_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.230 Mobile Safari/537.36";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Phone");
        deviceInfo.Browser.Should().Be("Chrome");
        deviceInfo.OperatingSystem.Should().Be("Android");
        deviceInfo.OsVersion.Should().StartWith("14");
        deviceInfo.IsMobile.Should().BeTrue();
    }

    [Fact]
    public void Parse_SafariOnIPad_ParsesCorrectly()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Tablet");
        deviceInfo.Browser.Should().Be("Safari");
        deviceInfo.OperatingSystem.Should().Be("iOS");
        deviceInfo.IsMobile.Should().BeTrue();
    }

    [Fact]
    public void Parse_ChromeOnAndroidTablet_ParsesCorrectly()
    {
        // Arrange - Android tablet user agents typically don't include "Mobile"
        var userAgent = "Mozilla/5.0 (Linux; Android 13; SM-X810) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.230 Safari/537.36";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Tablet");
        deviceInfo.Browser.Should().Be("Chrome");
        deviceInfo.OperatingSystem.Should().Be("Android");
        deviceInfo.IsMobile.Should().BeFalse(); // Android tablets without "Mobile" are not marked as mobile
    }

    #endregion

    #region Parse Tests - Edge Cases

    [Fact]
    public void Parse_NullUserAgent_ReturnsUnknown()
    {
        // Act
        var deviceInfo = DeviceInfo.Parse(null);

        // Assert
        deviceInfo.DeviceType.Should().Be("Unknown");
        deviceInfo.Browser.Should().Be("Unknown");
        deviceInfo.OperatingSystem.Should().Be("Unknown");
        deviceInfo.IsMobile.Should().BeFalse();
        deviceInfo.RawUserAgent.Should().Be("");
    }

    [Fact]
    public void Parse_EmptyUserAgent_ReturnsUnknown()
    {
        // Act
        var deviceInfo = DeviceInfo.Parse("");

        // Assert
        deviceInfo.DeviceType.Should().Be("Unknown");
        deviceInfo.Browser.Should().Be("Unknown");
        deviceInfo.OperatingSystem.Should().Be("Unknown");
        deviceInfo.IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhitespaceUserAgent_ReturnsUnknown()
    {
        // Act
        var deviceInfo = DeviceInfo.Parse("   ");

        // Assert
        deviceInfo.DeviceType.Should().Be("Unknown");
        deviceInfo.Browser.Should().Be("Unknown");
    }

    [Fact]
    public void Parse_UnrecognizedUserAgent_ReturnsUnknown()
    {
        // Arrange
        var userAgent = "CustomBot/1.0";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.DeviceType.Should().Be("Desktop");
        deviceInfo.Browser.Should().Be("Unknown");
        deviceInfo.OperatingSystem.Should().Be("Unknown");
        deviceInfo.RawUserAgent.Should().Be(userAgent);
    }

    #endregion

    #region GetDisplayName Tests

    [Fact]
    public void GetDisplayName_WithVersion_ReturnsFormattedString()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36";
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Act
        var displayName = deviceInfo.GetDisplayName();

        // Assert
        displayName.Should().Contain("Chrome");
        displayName.Should().Contain("Windows");
        displayName.Should().Contain("on");
    }

    [Fact]
    public void GetDisplayName_UnknownDevice_ReturnsUnknownOnUnknown()
    {
        // Arrange
        var deviceInfo = DeviceInfo.Parse(null);

        // Act
        var displayName = deviceInfo.GetDisplayName();

        // Assert
        displayName.Should().Be("Unknown on Unknown");
    }

    #endregion

    #region Windows Version Tests

    [Theory]
    [InlineData("Windows NT 6.1", "7")]
    [InlineData("Windows NT 6.2", "8")]
    [InlineData("Windows NT 6.3", "8.1")]
    [InlineData("Windows NT 10.0", "10/11")]
    public void Parse_WindowsVersions_ParsesCorrectly(string ntVersion, string expectedVersion)
    {
        // Arrange
        var userAgent = $"Mozilla/5.0 ({ntVersion}; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0";

        // Act
        var deviceInfo = DeviceInfo.Parse(userAgent);

        // Assert
        deviceInfo.OperatingSystem.Should().Be("Windows");
        deviceInfo.OsVersion.Should().Be(expectedVersion);
    }

    #endregion
}