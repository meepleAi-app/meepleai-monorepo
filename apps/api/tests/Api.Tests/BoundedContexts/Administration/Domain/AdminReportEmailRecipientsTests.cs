using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

/// <summary>
/// Unit tests for AdminReport email recipients validation
/// ISSUE-918: Email delivery integration tests
/// </summary>
public sealed class AdminReportEmailRecipientsTests
{
    [Fact]
    public void Create_WithNoEmailRecipients_ShouldCreateWithEmptyList()
    {
        // Arrange & Act
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: null);

        // Assert
        Assert.NotNull(report.EmailRecipients);
        Assert.Empty(report.EmailRecipients);
    }

    [Fact]
    public void Create_WithValidEmailRecipients_ShouldCreateSuccessfully()
    {
        // Arrange
        var recipients = new List<string> { "user1@test.com", "user2@test.com" };

        // Act
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: recipients);

        // Assert
        Assert.Equal(2, report.EmailRecipients.Count);
        Assert.Contains("user1@test.com", report.EmailRecipients);
        Assert.Contains("user2@test.com", report.EmailRecipients);
    }

    [Fact]
    public void Create_WithInvalidEmailAddress_ShouldThrowArgumentException()
    {
        // Arrange
        var recipients = new List<string> { "invalid-email" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            AdminReport.Create(
                name: "Test Report",
                description: "Test Description",
                template: ReportTemplate.SystemHealth,
                format: ReportFormat.Pdf,
                parameters: null,
                scheduleExpression: "0 0 * * *",
                createdBy: "admin@test.com",
                emailRecipients: recipients));

        Assert.Contains("Invalid email address", ex.Message);
    }

    [Fact]
    public void Create_WithTooManyRecipients_ShouldThrowArgumentException()
    {
        // Arrange
        var recipients = Enumerable.Range(1, 11)
            .Select(i => $"user{i}@test.com")
            .ToList();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            AdminReport.Create(
                name: "Test Report",
                description: "Test Description",
                template: ReportTemplate.SystemHealth,
                format: ReportFormat.Pdf,
                parameters: null,
                scheduleExpression: "0 0 * * *",
                createdBy: "admin@test.com",
                emailRecipients: recipients));

        Assert.Contains("Maximum 10 email recipients allowed", ex.Message);
    }

    [Fact]
    public void Create_WithDuplicateEmails_ShouldDeduplicateAndNormalize()
    {
        // Arrange
        var recipients = new List<string>
        {
            "User1@Test.com",
            "user1@test.com",
            "USER1@TEST.COM",
            "user2@test.com"
        };

        // Act
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: recipients);

        // Assert
        Assert.Equal(2, report.EmailRecipients.Count);
        Assert.Contains("user1@test.com", report.EmailRecipients);
        Assert.Contains("user2@test.com", report.EmailRecipients);
    }

    [Fact]
    public void Create_WithWhitespaceInEmails_ShouldTrimAndNormalize()
    {
        // Arrange
        var recipients = new List<string>
        {
            "  user1@test.com  ",
            "\tuser2@test.com\n"
        };

        // Act
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: recipients);

        // Assert
        Assert.Equal(2, report.EmailRecipients.Count);
        Assert.All(report.EmailRecipients, email =>
        {
            Assert.DoesNotContain(" ", email);
            Assert.DoesNotContain("\t", email);
            Assert.DoesNotContain("\n", email);
        });
    }

    [Fact]
    public void WithEmailRecipients_ShouldUpdateRecipients()
    {
        // Arrange
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: new List<string> { "old@test.com" });

        var newRecipients = new List<string> { "new1@test.com", "new2@test.com" };

        // Act
        var updatedReport = report.WithEmailRecipients(newRecipients);

        // Assert
        Assert.Equal(2, updatedReport.EmailRecipients.Count);
        Assert.Contains("new1@test.com", updatedReport.EmailRecipients);
        Assert.Contains("new2@test.com", updatedReport.EmailRecipients);
        Assert.DoesNotContain("old@test.com", updatedReport.EmailRecipients);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user+tag@example.co.uk")]
    [InlineData("123@test.io")]
    public void Create_WithValidEmailFormats_ShouldSucceed(string email)
    {
        // Arrange & Act
        var report = AdminReport.Create(
            name: "Test Report",
            description: "Test Description",
            template: ReportTemplate.SystemHealth,
            format: ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: "0 0 * * *",
            createdBy: "admin@test.com",
            emailRecipients: new List<string> { email });

        // Assert
        Assert.Single(report.EmailRecipients);
        Assert.Contains(email.ToLowerInvariant(), report.EmailRecipients);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plaintext")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user @example.com")]
    public void Create_WithInvalidEmailFormats_ShouldThrowOrSkip(string email)
    {
        // Arrange
        var recipients = new List<string> { email };

        // Act & Assert
        if (string.IsNullOrWhiteSpace(email))
        {
            var report = AdminReport.Create(
                name: "Test Report",
                description: "Test Description",
                template: ReportTemplate.SystemHealth,
                format: ReportFormat.Pdf,
                parameters: null,
                scheduleExpression: "0 0 * * *",
                createdBy: "admin@test.com",
                emailRecipients: recipients);
            Assert.Empty(report.EmailRecipients);
        }
        else
        {
            Assert.Throws<ArgumentException>(() =>
                AdminReport.Create(
                    name: "Test Report",
                    description: "Test Description",
                    template: ReportTemplate.SystemHealth,
                    format: ReportFormat.Pdf,
                    parameters: null,
                    scheduleExpression: "0 0 * * *",
                    createdBy: "admin@test.com",
                    emailRecipients: recipients));
        }
    }
}
