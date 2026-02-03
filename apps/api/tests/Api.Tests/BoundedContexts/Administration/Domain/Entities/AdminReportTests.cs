using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Tests for the AdminReport entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 7
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdminReportTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsAdminReport()
    {
        // Arrange
        var name = "System Health Report";
        var description = "Daily system health metrics";
        var template = ReportTemplate.SystemHealth;
        var format = ReportFormat.Json;
        var createdBy = "admin@test.com";

        // Act
        var report = AdminReport.Create(
            name,
            description,
            template,
            format,
            parameters: null,
            scheduleExpression: null,
            createdBy);

        // Assert
        report.Should().NotBeNull();
        report.Id.Should().NotBe(Guid.Empty);
        report.Name.Should().Be(name);
        report.Description.Should().Be(description);
        report.Template.Should().Be(template);
        report.Format.Should().Be(format);
        report.CreatedBy.Should().Be(createdBy);
        report.IsActive.Should().BeTrue();
        report.LastExecutedAt.Should().BeNull();
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var report = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.UserActivity,
            ReportFormat.Csv,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");
        var after = DateTime.UtcNow;

        // Assert
        report.CreatedAt.Should().BeOnOrAfter(before);
        report.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_WithParameters_IncludesParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "startDate", "2024-01-01" },
            { "endDate", "2024-12-31" },
            { "includeCharts", true }
        };

        // Act
        var report = AdminReport.Create(
            "Parameterized Report",
            "Report with parameters",
            ReportTemplate.AIUsage,
            ReportFormat.Pdf,
            parameters,
            scheduleExpression: null,
            "admin@test.com");

        // Assert
        report.Parameters.Should().HaveCount(3);
        report.Parameters["startDate"].Should().Be("2024-01-01");
        report.Parameters["includeCharts"].Should().Be(true);
    }

    [Fact]
    public void Create_WithNullParameters_UsesEmptyDictionary()
    {
        // Act
        var report = AdminReport.Create(
            "No Params Report",
            "Report without parameters",
            ReportTemplate.ContentMetrics,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Assert
        report.Parameters.Should().NotBeNull();
        report.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithScheduleExpression_SetsSchedule()
    {
        // Arrange
        var cronExpression = "0 0 * * *"; // Daily at midnight

        // Act
        var report = AdminReport.Create(
            "Scheduled Report",
            "Daily scheduled report",
            ReportTemplate.SystemHealth,
            ReportFormat.Csv,
            parameters: null,
            scheduleExpression: cronExpression,
            "admin@test.com");

        // Assert
        report.ScheduleExpression.Should().Be(cronExpression);
    }

    [Fact]
    public void Create_WithValidEmailRecipients_IncludesRecipients()
    {
        // Arrange
        var recipients = new List<string> { "user1@test.com", "user2@test.com" };

        // Act
        var report = AdminReport.Create(
            "Email Report",
            "Report with email delivery",
            ReportTemplate.UserActivity,
            ReportFormat.Pdf,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com",
            recipients);

        // Assert
        report.EmailRecipients.Should().HaveCount(2);
        report.EmailRecipients.Should().Contain("user1@test.com");
        report.EmailRecipients.Should().Contain("user2@test.com");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        // Act
        var action = () => AdminReport.Create(
            name!,
            "Valid description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        // Act
        var action = () => AdminReport.Create(
            "Valid Name",
            description!,
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidCreatedBy_ThrowsArgumentException(string? createdBy)
    {
        // Act
        var action = () => AdminReport.Create(
            "Valid Name",
            "Valid Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            createdBy!);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTooManyEmailRecipients_ThrowsArgumentException()
    {
        // Arrange - more than 10 recipients
        var recipients = Enumerable.Range(1, 11)
            .Select(i => $"user{i}@test.com")
            .ToList();

        // Act
        var action = () => AdminReport.Create(
            "Valid Name",
            "Valid Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com",
            recipients);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Maximum 10 email recipients*");
    }

    [Fact]
    public void Create_WithInvalidEmailFormat_ThrowsArgumentException()
    {
        // Arrange
        var recipients = new List<string> { "invalid-email", "user@test.com" };

        // Act
        var action = () => AdminReport.Create(
            "Valid Name",
            "Valid Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com",
            recipients);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid email address*");
    }

    [Fact]
    public void Create_WithDuplicateEmails_RemovesDuplicates()
    {
        // Arrange
        var recipients = new List<string>
        {
            "user@test.com",
            "USER@TEST.COM", // Duplicate (case-insensitive)
            "other@test.com"
        };

        // Act
        var report = AdminReport.Create(
            "Valid Name",
            "Valid Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com",
            recipients);

        // Assert
        report.EmailRecipients.Should().HaveCount(2);
    }

    #endregion

    #region State Change Tests

    [Fact]
    public void WithLastExecutedAt_ReturnsNewInstanceWithUpdatedTimestamp()
    {
        // Arrange
        var report = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");
        var executedAt = DateTime.UtcNow;

        // Act
        var updatedReport = report.WithLastExecutedAt(executedAt);

        // Assert
        updatedReport.LastExecutedAt.Should().Be(executedAt);
        updatedReport.Should().NotBeSameAs(report);
        report.LastExecutedAt.Should().BeNull(); // Original unchanged
    }

    [Fact]
    public void Deactivate_ReturnsNewInstanceWithIsActiveFalse()
    {
        // Arrange
        var report = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Act
        var deactivatedReport = report.Deactivate();

        // Assert
        deactivatedReport.IsActive.Should().BeFalse();
        deactivatedReport.Should().NotBeSameAs(report);
        report.IsActive.Should().BeTrue(); // Original unchanged
    }

    [Fact]
    public void Activate_ReturnsNewInstanceWithIsActiveTrue()
    {
        // Arrange
        var report = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");
        var deactivatedReport = report.Deactivate();

        // Act
        var activatedReport = deactivatedReport.Activate();

        // Assert
        activatedReport.IsActive.Should().BeTrue();
    }

    [Fact]
    public void WithEmailRecipients_UpdatesRecipientsList()
    {
        // Arrange
        var report = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");
        var newRecipients = new List<string> { "new@test.com", "another@test.com" };

        // Act
        var updatedReport = report.WithEmailRecipients(newRecipients);

        // Assert
        updatedReport.EmailRecipients.Should().HaveCount(2);
        updatedReport.EmailRecipients.Should().Contain("new@test.com");
        updatedReport.Should().NotBeSameAs(report);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoReports_WithSameId_AreConsideredEqual()
    {
        // Arrange
        var report1 = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Act - Create a "copy" with same values
        var report2 = report1 with { }; // Same values

        // Assert
        report1.Should().Be(report2);
    }

    [Fact]
    public void TwoReports_WithDifferentIds_AreNotEqual()
    {
        // Arrange & Act
        var report1 = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        var report2 = AdminReport.Create(
            "Test Report",
            "Test Description",
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters: null,
            scheduleExpression: null,
            "admin@test.com");

        // Assert
        report1.Should().NotBe(report2); // Different IDs
    }

    #endregion
}
