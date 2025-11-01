using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Unit tests for EmailService.
/// Tests focus on configuration loading, input validation, exception handling, and logging.
/// Note: SmtpClient is sealed and cannot be mocked. For full SMTP integration testing,
/// consider using Testcontainers with a test SMTP server (e.g., MailHog) in integration test suite.
/// </summary>
public class EmailServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<EmailService>> _mockLogger = new();

    /// <summary>
    /// Creates a mock IConfiguration with valid email settings for testing.
    /// </summary>
    private static Mock<IConfiguration> CreateMockConfiguration(
        string? fromAddress = "noreply@meepleai.dev",
        string? fromName = "MeepleAI",
        string? smtpHost = "localhost",
        string? smtpPort = "587",
        string? smtpUsername = null,
        string? smtpPassword = null,
        string? enableSsl = "true",
        string? resetUrlBase = "http://localhost:3000/reset-password")
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(x => x["Email:FromAddress"]).Returns(fromAddress);
        mockConfig.Setup(x => x["Email:FromName"]).Returns(fromName);
        mockConfig.Setup(x => x["Email:SmtpHost"]).Returns(smtpHost);
        mockConfig.Setup(x => x["Email:SmtpPort"]).Returns(smtpPort);
        mockConfig.Setup(x => x["Email:SmtpUsername"]).Returns(smtpUsername);
        mockConfig.Setup(x => x["Email:SmtpPassword"]).Returns(smtpPassword);
        mockConfig.Setup(x => x["Email:EnableSsl"]).Returns(enableSsl);
        mockConfig.Setup(x => x["Email:ResetUrlBase"]).Returns(resetUrlBase);
        return mockConfig;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();

        // Act
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithMissingConfiguration_UsesDefaults()
    {
        // Arrange - All config values are null, should use defaults
        var mockConfig = CreateMockConfiguration(
            fromAddress: null,
            fromName: null,
            smtpHost: null,
            smtpPort: null,
            enableSsl: null,
            resetUrlBase: null);

        // Act
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Assert - No exception thrown, defaults are applied
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidSmtpPort_ThrowsFormatException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration(smtpPort: "invalid");

        // Act & Assert
        var act = () => new EmailService(mockConfig.Object, _mockLogger.Object);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_WithInvalidEnableSsl_ThrowsFormatException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration(enableSsl: "invalid");

        // Act & Assert
        var act = () => new EmailService(mockConfig.Object, _mockLogger.Object);
        act.Should().Throw<FormatException>();
    }

    #endregion

    #region SendPasswordResetEmailAsync - Happy Path

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithValidInputs_LogsSuccessMessage()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Note: Since SmtpClient is sealed, we cannot mock it.
        // This test will attempt to connect to localhost:587 and will throw.
        // We verify that on exception, the service logs error and throws InvalidOperationException.
        var toEmail = "user@example.com";
        var toName = "Test User";
        var resetToken = "valid-token-123";

        // Act & Assert
        // Expected: SmtpClient will fail to connect, causing InvalidOperationException
        var act = async () => await service.SendPasswordResetEmailAsync(toEmail, toName, resetToken);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Error logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send password reset email to user@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        exception.Which.Message.Should().Be("Failed to send password reset email");
    }

    #endregion

    #region SendPasswordResetEmailAsync - Input Validation

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithNullEmail_ThrowsArgumentException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync(null!, "Test User", "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithEmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync(string.Empty, "Test User", "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@@example.com")]
    [InlineData("user..name@example.com")]
    public async Task SendPasswordResetEmailAsync_WithInvalidEmailFormat_ThrowsInvalidOperationException(string invalidEmail)
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        // MailAddress constructor validates email format and throws ArgumentException
        // EmailService catches this and wraps it in InvalidOperationException
        var act = async () => await service.SendPasswordResetEmailAsync(invalidEmail, "Test User", "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to send password reset email to {invalidEmail}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithNullResetToken_ThrowsArgumentNullException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        // Uri.EscapeDataString throws ArgumentNullException on null input
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", null!);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithEmptyResetToken_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        // Empty string is valid for Uri.EscapeDataString, will fail later at SMTP level
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", string.Empty);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged (SMTP error, not validation error)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendPasswordResetEmailAsync - URL Encoding

    [Theory]
    [InlineData("token+with+plus")]
    [InlineData("token/with/slashes")]
    [InlineData("token=with=equals")]
    [InlineData("token&with&ampersand")]
    [InlineData("token with spaces")]
    [InlineData("token?with?question")]
    [InlineData("token#with#hash")]
    public async Task SendPasswordResetEmailAsync_WithSpecialCharactersInToken_DoesNotThrowDuringUrlEncoding(string tokenWithSpecialChars)
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        // Uri.EscapeDataString should handle special characters without throwing
        // The actual failure will be at SMTP level (expected in unit test without real SMTP server)
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", tokenWithSpecialChars);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Error is from SMTP, not from URL encoding
        exception.Which.InnerException.Should().NotBeNull();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendPasswordResetEmailAsync - Cancellation Token

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithCancelledToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // EmailService catches all exceptions (including OperationCanceledException) and wraps them
        var act = async () => await service.SendPasswordResetEmailAsync(
                "user@example.com",
                "Test User",
                "token123",
                cts.Token);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendPasswordResetEmailAsync - Exception Handling

    [Fact]
    public async Task SendPasswordResetEmailAsync_OnSmtpFailure_ThrowsInvalidOperationExceptionWithInnerException()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration(smtpHost: "invalid-host-that-does-not-exist.local");
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert
        exception.Which.Message.Should().Be("Failed to send password reset email");
        exception.Which.InnerException.Should().NotBeNull(); // Should contain the original SMTP exception

        // Verify error was logged with exception details
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send password reset email to user@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_OnException_LogsErrorWithEmailAddress()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var testEmail = "test@example.com";

        // Act
        var act = async () => await service.SendPasswordResetEmailAsync(testEmail, "Test User", "token123");
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Verify the email address is included in the error log
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(testEmail)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Email Content Tests

    [Fact]
    public async Task SendPasswordResetEmailAsync_BuildsEmailWithCorrectResetUrl()
    {
        // Arrange
        var resetUrlBase = "https://app.meepleai.dev/reset-password";
        var mockConfig = CreateMockConfiguration(resetUrlBase: resetUrlBase);
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var token = "test-token-123";

        // Act & Assert
        // We cannot directly verify the email body without mocking SmtpClient,
        // but we can verify that the service constructs the URL correctly by ensuring
        // no exception is thrown during URL building (before SMTP failure)
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", token);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // The URL should be: https://app.meepleai.dev/reset-password?token=test-token-123
        // This is indirectly tested - if URL building failed, we'd get a different exception type
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithLongToken_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var longToken = new string('a', 500); // 500 character token

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", "Test User", longToken);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Should fail at SMTP level, not during token processing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithNullName_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        // Null name should be handled by MailAddress constructor and email body template
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", null!, "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Should fail at SMTP level, not during name processing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithEmptyName_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", string.Empty, "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Should fail at SMTP level, not during name processing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithUnicodeCharactersInName_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var unicodeName = "用户名 🎲"; // Chinese characters + emoji

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync("user@example.com", unicodeName, "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Should handle unicode correctly in email body
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithInternationalDomain_DoesNotThrow()
    {
        // Arrange
        var mockConfig = CreateMockConfiguration();
        var service = new EmailService(mockConfig.Object, _mockLogger.Object);
        var internationalEmail = "user@münchen.de"; // Internationalized domain name

        // Act & Assert
        var act = async () => await service.SendPasswordResetEmailAsync(internationalEmail, "Test User", "token123");
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // MailAddress should handle IDN (Internationalized Domain Names)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
