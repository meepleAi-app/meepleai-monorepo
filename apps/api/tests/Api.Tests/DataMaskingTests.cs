using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Xunit;

namespace Api.Tests;

public class DataMaskingTests
{
    [Theory]
    [InlineData("test@example.com", "t***t@example.com")]
    [InlineData("a@b.com", "***@b.com")]
    [InlineData("john.doe@company.org", "j***e@company.org")]
    [InlineData(null, "***@***.***")]
    [InlineData("", "***@***.***")]
    [InlineData("invalid", "***@***.***")]
    public void MaskEmail_MasksCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.MaskEmail(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("abcdefghij", "abcd...ghij")]
    [InlineData("short", "***")]
    [InlineData(null, "***")]
    [InlineData("", "***")]
    public void MaskString_MasksCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.MaskString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U", "eyJhbGciOiJIUzI1NiIs...***")] // 20 chars prefix
    [InlineData("short", "***")]
    [InlineData(null, "***")]
    public void MaskJwt_MasksCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.MaskJwt(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1234567890123456", "****-****-****-3456")]
    [InlineData("1234-5678-9012-3456", "****-****-****-3456")]
    [InlineData("123", "****-****-****-****")]
    [InlineData(null, "****-****-****-****")]
    [InlineData("", "****-****-****-****")]
    public void MaskCreditCard_MasksCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.MaskCreditCard(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Server=localhost;Database=test;User=admin;Password=secret123", "Server=localhost;Database=test;User=admin;Password=***REDACTED***")] // Preserves original case
    [InlineData("Host=db;Port=5432;Database=mydb;Username=user;Password=pass123", "Host=db;Port=5432;Database=mydb;Username=user;Password=***REDACTED***")] // Preserves original case
    [InlineData(null, "")]
    [InlineData("", "")]
    public void RedactConnectionString_RedactsPasswordCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.RedactConnectionString(input);

        // Assert
        Assert.Equal(expected, result);

        // Only check redaction for non-empty inputs
        if (!string.IsNullOrEmpty(input))
        {
            Assert.Contains("***REDACTED***", result);
            Assert.DoesNotContain("secret123", result);
            Assert.DoesNotContain("pass123", result);
        }
    }

    [Fact]
    public void SanitizeUser_RemovesSensitiveFields()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = UserRole.User,
            PasswordHash = "sensitive-hash",
            TotpSecretEncrypted = "sensitive-totp",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var sanitized = DataMasking.SanitizeUser(user);
        var sanitizedString = sanitized.ToString();

        // Assert
        Assert.NotNull(sanitized);
        Assert.DoesNotContain("test@example.com", sanitizedString); // Email should be masked
        Assert.DoesNotContain("sensitive-hash", sanitizedString);
        Assert.DoesNotContain("sensitive-totp", sanitizedString);
        Assert.Contains("user-123", sanitizedString);
    }

    [Theory]
    [InlineData("192.168.1.100", "192.168.1.***")]
    [InlineData("10.0.0.5", "10.0.0.***")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", "2001:0db8:85a3:0000:0000:8a2e:0370:***")]
    [InlineData("unknown", "***")]
    [InlineData(null, "***")]
    [InlineData("", "***")]
    public void MaskIpAddress_MasksCorrectly(string? input, string expected)
    {
        // Act
        var result = DataMasking.MaskIpAddress(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MaskResponseBody_RedactsSensitivePatterns()
    {
        // Arrange
        var responseBody = @"{
            ""email"": ""user@example.com"",
            ""api_key"": ""sk-1234567890"",
            ""password"": ""MyPassword123"",
            ""secret"": ""secret-value"",
            ""token"": ""Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9""
        }";

        // Act
        var result = DataMasking.MaskResponseBody(responseBody);

        // Assert
        Assert.DoesNotContain("user@example.com", result);
        Assert.DoesNotContain("sk-1234567890", result);
        Assert.DoesNotContain("MyPassword123", result);
        Assert.DoesNotContain("secret-value", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void MaskResponseBody_TruncatesLongResponses()
    {
        // Arrange
        var longBody = new string('x', 1000);

        // Act
        var result = DataMasking.MaskResponseBody(longBody, maxLength: 500);

        // Assert
        Assert.Contains("[truncated", result);
        Assert.True(result.Length < longBody.Length);
    }

    [Fact]
    public void MaskResponseBody_HandlesNull()
    {
        // Act
        var result = DataMasking.MaskResponseBody(null);

        // Assert
        Assert.Equal("[empty]", result);
    }

    [Fact]
    public void MaskEmail_PreventsPiiLeakage()
    {
        // Arrange
        var realEmail = "john.smith@confidential.com";

        // Act
        var masked = DataMasking.MaskEmail(realEmail);

        // Assert
        Assert.DoesNotContain("john.smith", masked);
        Assert.Contains("@confidential.com", masked); // Domain preserved for debugging
        Assert.Contains("***", masked);
    }

    [Fact]
    public void MaskString_HandlesEdgeCases()
    {
        // Act & Assert
        Assert.Equal("***", DataMasking.MaskString("a"));
        Assert.Equal("***", DataMasking.MaskString("ab"));
        Assert.Equal("***", DataMasking.MaskString("abc"));
        Assert.Equal("***", DataMasking.MaskString("abcd"));
        Assert.Equal("***", DataMasking.MaskString("abcde"));
        Assert.Equal("***", DataMasking.MaskString("abcdef"));
        Assert.Equal("***", DataMasking.MaskString("abcdefg"));
        Assert.Equal("abcd...wxyz", DataMasking.MaskString("abcdefghijklmnopqrstuvwxyz"));
    }
}
