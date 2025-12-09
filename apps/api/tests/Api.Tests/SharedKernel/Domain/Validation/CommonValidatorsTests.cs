using Api.SharedKernel.Domain.Validation;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Domain.Validation;

[Trait("Category", TestCategories.Unit)]

public class CommonValidatorsTests
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@example.co.uk")]
    [InlineData("test123@test.com")]
    public void IsValidEmail_WithValidEmail_ReturnsSuccess(string email)
    {
        // Act
        var result = email.IsValidEmail();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(email, result.Value);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test @example.com")]
    public void IsValidEmail_WithInvalidEmail_ReturnsFailure(string email)
    {
        // Act
        var result = email.IsValidEmail();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com:8080/path?query=value")]
    public void IsValidUrl_WithValidUrl_ReturnsSuccess(string url)
    {
        // Act
        var result = url.IsValidUrl();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(url, result.Value);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("example.com")]
    public void IsValidUrl_WithInvalidUrl_ReturnsFailure(string url)
    {
        // Act
        var result = url.IsValidUrl();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("mpl_dev_YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo=")]
    [InlineData("mpl_staging_YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo=")]
    [InlineData("mpl_prod_YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo=")]
    public void IsValidApiKey_WithValidKey_ReturnsSuccess(string apiKey)
    {
        // Act
        var result = apiKey.IsValidApiKey();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(apiKey, result.Value);
    }

    [Theory]
    [InlineData("invalid-key")]
    [InlineData("mpl_invalid_key")]
    [InlineData("mpl_dev_")]
    [InlineData("api_dev_YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo=")]
    public void IsValidApiKey_WithInvalidKey_ReturnsFailure(string apiKey)
    {
        // Act
        var result = apiKey.IsValidApiKey();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("Test123!")]
    [InlineData("SecureP@ssw0rd")]
    [InlineData("Abcd1234!")]
    public void IsValidPassword_WithValidPassword_ReturnsSuccess(string password)
    {
        // Act
        var result = password.IsValidPassword();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(password, result.Value);
    }

    [Theory]
    [InlineData("short1!")] // Too short
    [InlineData("NoDigits!")] // No digit
    [InlineData("nouppercas3!")] // No uppercase
    [InlineData("NOLOWERCASE1!")] // No lowercase
    [InlineData("NoSpecial1")] // No special character
    public void IsValidPassword_WithInvalidPassword_ReturnsFailure(string password)
    {
        // Act
        var result = password.IsValidPassword();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("document.pdf")]
    [InlineData("my-file-123.txt")]
    [InlineData("test.pdf")]
    public void IsValidFileName_WithValidFileName_ReturnsSuccess(string fileName)
    {
        // Act
        var result = fileName.IsValidFileName();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(fileName, result.Value);
    }

    [Theory]
    [InlineData("file<name.txt")] // Invalid character
    [InlineData("file|name.txt")] // Invalid character
    [InlineData("file:name.txt")] // Invalid character (on Windows)
    public void IsValidFileName_WithInvalidFileName_ReturnsFailure(string fileName)
    {
        // Act
        var result = fileName.IsValidFileName();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void HasAllowedExtension_WithAllowedExtension_ReturnsSuccess()
    {
        // Arrange
        var fileName = "document.pdf";
        var allowedExtensions = new[] { ".pdf", ".txt" };

        // Act
        var result = fileName.HasAllowedExtension(allowedExtensions);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void HasAllowedExtension_WithDisallowedExtension_ReturnsFailure()
    {
        // Arrange
        var fileName = "document.exe";
        var allowedExtensions = new[] { ".pdf", ".txt" };

        // Act
        var result = fileName.HasAllowedExtension(allowedExtensions);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("{\"key\": \"value\"}")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"nested\": {\"key\": \"value\"}}")]
    public void IsValidJson_WithValidJson_ReturnsSuccess(string json)
    {
        // Act
        var result = json.IsValidJson();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(json, result.Value);
    }

    [Theory]
    [InlineData("{invalid json}")]
    [InlineData("[1, 2, 3")]
    [InlineData("not json at all")]
    public void IsValidJson_WithInvalidJson_ReturnsFailure(string json)
    {
        // Act
        var result = json.IsValidJson();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0")]
    [InlineData("2.5.3")]
    public void IsValidVersion_WithValidVersion_ReturnsSuccess(string version)
    {
        // Act
        var result = version.IsValidVersion();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(version, result.Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("v1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.a")]
    public void IsValidVersion_WithInvalidVersion_ReturnsFailure(string version)
    {
        // Act
        var result = version.IsValidVersion();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Theory]
    [InlineData("RateLimit")]
    [InlineData("RateLimit.Admin.MaxTokens")]
    [InlineData("Section_1.Key_2")]
    public void IsValidConfigKey_WithValidKey_ReturnsSuccess(string key)
    {
        // Act
        var result = key.IsValidConfigKey();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(key, result.Value);
    }

    [Theory]
    [InlineData("1_StartWithNumber")]
    [InlineData(".StartWithDot")]
    [InlineData("Invalid Key")] // Space not allowed
    [InlineData("Invalid@Key")] // @ not allowed
    public void IsValidConfigKey_WithInvalidKey_ReturnsFailure(string key)
    {
        // Act
        var result = key.IsValidConfigKey();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    [Fact]
    public void NotInFuture_WithPastDate_ReturnsSuccess()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = date.NotInFuture();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(date, result.Value);
    }

    [Fact]
    public void NotInFuture_WithFutureDate_ReturnsFailure()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(1);

        // Act
        var result = date.NotInFuture();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void NotInPast_WithFutureDate_ReturnsSuccess()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(1);

        // Act
        var result = date.NotInPast();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(date, result.Value);
    }

    [Fact]
    public void NotInPast_WithPastDate_ReturnsFailure()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = date.NotInPast();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void InDateRange_WithDateInRange_ReturnsSuccess()
    {
        // Arrange
        var date = new DateTime(2024, 6, 15);
        var minDate = new DateTime(2024, 1, 1);
        var maxDate = new DateTime(2024, 12, 31);

        // Act
        var result = date.InDateRange(minDate, maxDate);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void InDateRange_WithDateOutOfRange_ReturnsFailure()
    {
        // Arrange
        var date = new DateTime(2025, 1, 1);
        var minDate = new DateTime(2024, 1, 1);
        var maxDate = new DateTime(2024, 12, 31);

        // Act
        var result = date.InDateRange(minDate, maxDate);

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
    private enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    [Theory]
    [InlineData("Value1")]
    [InlineData("value2")] // Case insensitive
    [InlineData("VALUE3")] // Case insensitive
    public void IsValidEnum_WithValidValue_ReturnsSuccess(string value)
    {
        // Act
        var result = value.IsValidEnum<TestEnum>();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("InvalidValue")]
    [InlineData("99")]
    [InlineData("")]
    public void IsValidEnum_WithInvalidValue_ReturnsFailure(string value)
    {
        // Act
        var result = value.IsValidEnum<TestEnum>();

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
}

