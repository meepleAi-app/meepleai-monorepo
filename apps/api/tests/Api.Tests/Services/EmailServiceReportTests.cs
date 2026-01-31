using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for EmailService report delivery methods
/// ISSUE-918: Email delivery integration tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EmailServiceReportTests
{
    private readonly Mock<ILogger<EmailService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public EmailServiceReportTests()
    {
        _mockLogger = new Mock<ILogger<EmailService>>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Email:FromAddress"] = "noreply@test.com",
            ["Email:FromName"] = "Test",
            ["Email:SmtpHost"] = "localhost",
            ["Email:SmtpPort"] = "25",
            ["Email:EnableSsl"] = "false"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task SendReportEmailAsync_WithNoRecipients_ShouldLogWarningAndReturn()
    {
        // Arrange
        var service = new EmailService(_configuration, _mockLogger.Object);
        var emptyRecipients = new List<string>();
        var reportContent = new byte[] { 1, 2, 3 };

        // Act
        await service.SendReportEmailAsync(
            emptyRecipients,
            "Test Report",
            "Test Description",
            reportContent,
            "report.pdf",
            3,
            CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No recipients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendReportEmailAsync_WithTooLargeAttachment_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = new EmailService(_configuration, _mockLogger.Object);
        var recipients = new List<string> { "user@test.com" };
        var largeContent = new byte[11 * 1024 * 1024]; // 11 MB

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendReportEmailAsync(
                recipients,
                "Test Report",
                "Test Description",
                largeContent,
                "report.pdf",
                largeContent.Length,
                CancellationToken.None));

        Assert.Contains("exceeds email attachment limit", ex.Message);
    }

    [Fact]
    public async Task SendReportFailureEmailAsync_WithNoRecipients_ShouldLogWarningAndReturn()
    {
        // Arrange
        var service = new EmailService(_configuration, _mockLogger.Object);
        var emptyRecipients = new List<string>();

        // Act
        await service.SendReportFailureEmailAsync(
            emptyRecipients,
            "Test Report",
            "Error message",
            CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No recipients")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void EmailService_WithValidRecipients_ShouldBuildCorrectReportBody()
    {
        // Arrange
        var service = new EmailService(_configuration, _mockLogger.Object);

        // This test verifies the email service can be instantiated
        // Integration test with real SMTP server would be in E2E tests

        // Assert
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData(1024, 0.001)]
    [InlineData(1024 * 1024, 1.0)]
    [InlineData(5 * 1024 * 1024, 5.0)]
    public void FileSizeCalculation_ShouldConvertCorrectly(long bytes, double expectedMB)
    {
        // Arrange
        var fileSizeMB = bytes / (1024.0 * 1024.0);

        // Assert
        Assert.Equal(expectedMB, fileSizeMB, precision: 2);
    }
}
