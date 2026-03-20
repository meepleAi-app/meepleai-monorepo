using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Domain;

/// <summary>
/// Unit tests for AdminReport email recipients validation
/// ISSUE-918: Email delivery integration tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        report.EmailRecipients.Should().NotBeNull();
        report.EmailRecipients.Should().BeEmpty();
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
        report.EmailRecipients.Count.Should().Be(2);
        report.EmailRecipients.Should().Contain("user1@test.com");
        report.EmailRecipients.Should().Contain("user2@test.com");
    }

    [Fact]
    public void Create_WithInvalidEmailAddress_ShouldThrowArgumentException()
    {
        // Arrange
        var recipients = new List<string> { "invalid-email" };

        // Act & Assert
        var act = () =>
            AdminReport.Create(
                name: "Test Report",
                description: "Test Description",
                template: ReportTemplate.SystemHealth,
                format: ReportFormat.Pdf,
                parameters: null,
                scheduleExpression: "0 0 * * *",
                createdBy: "admin@test.com",
                emailRecipients: recipients);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("Invalid email address");
    }

    [Fact]
    public void Create_WithTooManyRecipients_ShouldThrowArgumentException()
    {
        // Arrange
        var recipients = Enumerable.Range(1, 11)
            .Select(i => $"user{i}@test.com")
            .ToList();

        // Act & Assert
        var act = () =>
            AdminReport.Create(
                name: "Test Report",
                description: "Test Description",
                template: ReportTemplate.SystemHealth,
                format: ReportFormat.Pdf,
                parameters: null,
                scheduleExpression: "0 0 * * *",
                createdBy: "admin@test.com",
                emailRecipients: recipients);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("Maximum 10 email recipients allowed");
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
        report.EmailRecipients.Count.Should().Be(2);
        report.EmailRecipients.Should().Contain("user1@test.com");
        report.EmailRecipients.Should().Contain("user2@test.com");
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
        report.EmailRecipients.Count.Should().Be(2);
        Assert.All(report.EmailRecipients, email =>
        {
            email.Should().NotContain(" ");
            email.Should().NotContain("\t");
            email.Should().NotContain("\n");
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
        updatedReport.EmailRecipients.Count.Should().Be(2);
        updatedReport.EmailRecipients.Should().Contain("new1@test.com");
        updatedReport.EmailRecipients.Should().Contain("new2@test.com");
        updatedReport.EmailRecipients.Should().NotContain("old@test.com");
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
        report.EmailRecipients.Should().ContainSingle();
        report.EmailRecipients.Should().Contain(email.ToLowerInvariant());
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
            report.EmailRecipients.Should().BeEmpty();
        }
        else
        {
            var act = () =>
                AdminReport.Create(
                    name: "Test Report",
                    description: "Test Description",
                    template: ReportTemplate.SystemHealth,
                    format: ReportFormat.Pdf,
                    parameters: null,
                    scheduleExpression: "0 0 * * *",
                    createdBy: "admin@test.com",
                    emailRecipients: recipients);
act.Should().Throw<ArgumentException>();
        }
    }
}