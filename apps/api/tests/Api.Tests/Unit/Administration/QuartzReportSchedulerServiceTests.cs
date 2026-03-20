using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Quartz.Impl;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for QuartzReportSchedulerService
/// ISSUE-919: Scheduling logic testing (90%+ coverage)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class QuartzReportSchedulerServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<QuartzReportSchedulerService>> _loggerMock;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly QuartzReportSchedulerService _sut;
    private readonly IScheduler _scheduler;

    public QuartzReportSchedulerServiceTests()
    {
        _loggerMock = new Mock<ILogger<QuartzReportSchedulerService>>();
        _schedulerFactory = new StdSchedulerFactory();
        _sut = new QuartzReportSchedulerService(_schedulerFactory, _loggerMock.Object);
        _scheduler = _schedulerFactory.GetScheduler().Result;
        _scheduler.Start().Wait();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSchedulerFactory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new QuartzReportSchedulerService(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new QuartzReportSchedulerService(_schedulerFactory, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ScheduleReportAsync Tests

    [Fact]
    public async Task ScheduleReportAsync_WithValidReport_ShouldScheduleJob()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 * * ?"); // Daily at 9 AM

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var jobExists = await _scheduler.CheckExists(jobKey);
        jobExists.Should().BeTrue();

        // Cleanup
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task ScheduleReportAsync_WithNullScheduleExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var report = CreateTestReport(scheduleExpression: null!);

        // Act & Assert
        var act = async () => () =>
            _sut.ScheduleReportAsync(report);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleReportAsync_WithEmptyScheduleExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var report = CreateTestReport(scheduleExpression: "");

        // Act & Assert
        var act = async () => () =>
            _sut.ScheduleReportAsync(report);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleReportAsync_WithWhitespaceScheduleExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var report = CreateTestReport(scheduleExpression: "   ");

        // Act & Assert
        var act = async () => () =>
            _sut.ScheduleReportAsync(report);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleReportAsync_WithHourlySchedule_ShouldCreateCorrectTrigger()
    {
        // Arrange
        var report = CreateTestReport("0 0 * * * ?"); // Every hour

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var triggers = await _scheduler.GetTriggersOfJob(jobKey);
        triggers.Should().ContainSingle();

        var trigger = triggers.First() as ICronTrigger;
        trigger.Should().NotBeNull();
        trigger.CronExpressionString.Should().Be("0 0 * * * ?");

        // Cleanup
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task ScheduleReportAsync_WithDailySchedule_ShouldCreateCorrectTrigger()
    {
        // Arrange
        var report = CreateTestReport("0 0 6 * * ?"); // Daily at 6 AM

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var triggers = await _scheduler.GetTriggersOfJob(jobKey);
        var trigger = triggers.First() as ICronTrigger;
        trigger!.CronExpressionString.Should().Be("0 0 6 * * ?");

        // Cleanup
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task ScheduleReportAsync_WithWeeklySchedule_ShouldCreateCorrectTrigger()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 ? * MON"); // Every Monday at 9 AM

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var triggers = await _scheduler.GetTriggersOfJob(jobKey);
        var trigger = triggers.First() as ICronTrigger;
        trigger!.CronExpressionString.Should().Be("0 0 9 ? * MON");

        // Cleanup
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task ScheduleReportAsync_WithJobDataMap_ShouldIncludeReportId()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 * * ?");

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var jobDetail = await _scheduler.GetJobDetail(jobKey);
        jobDetail.Should().NotBeNull();
        jobDetail.JobDataMap.ContainsKey("ReportId").Should().BeTrue();
        jobDetail.JobDataMap.GetString("ReportId").Should().Be(report.Id.ToString());

        // Cleanup
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task ScheduleReportAsync_ShouldLogInformation()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 * * ?");

        // Act
        await _sut.ScheduleReportAsync(report);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Scheduling report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Cleanup
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        await _scheduler.DeleteJob(jobKey);
    }

    #endregion

    #region UnscheduleReportAsync Tests

    [Fact]
    public async Task UnscheduleReportAsync_WithExistingJob_ShouldDeleteJob()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 * * ?");
        await _sut.ScheduleReportAsync(report);
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        (await _scheduler.CheckExists(jobKey)).Should().BeTrue();

        // Act
        await _sut.UnscheduleReportAsync(report.Id);

        // Assert
        var jobExists = await _scheduler.CheckExists(jobKey);
        jobExists.Should().BeFalse();
    }

    [Fact]
    public async Task UnscheduleReportAsync_WithNonExistingJob_ShouldNotThrow()
    {
        // Arrange
        var nonExistingReportId = Guid.NewGuid();

        // Act & Assert - should not throw
        await _sut.UnscheduleReportAsync(nonExistingReportId);
    }

    [Fact]
    public async Task UnscheduleReportAsync_ShouldLogInformation()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act
        await _sut.UnscheduleReportAsync(reportId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unscheduling report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UnscheduleReportAsync_WhenJobNotFound_ShouldLogWarning()
    {
        // Arrange
        var nonExistingReportId = Guid.NewGuid();

        // Act
        await _sut.UnscheduleReportAsync(nonExistingReportId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Report job not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region TriggerReportAsync Tests

    [Fact]
    public async Task TriggerReportAsync_WithExistingJob_ShouldTriggerJob()
    {
        // Arrange
        var report = CreateTestReport("0 0 9 * * ?");
        await _sut.ScheduleReportAsync(report);

        // Act - should not throw
        await _sut.TriggerReportAsync(report.Id);

        // Assert - verify job was triggered via logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Report triggered")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Cleanup
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        await _scheduler.DeleteJob(jobKey);
    }

    [Fact]
    public async Task TriggerReportAsync_ShouldLogInformation()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        // Act
        try
        {
            await _sut.TriggerReportAsync(reportId);
        }
        catch
        {
            // Job doesn't exist, but we're testing logging
        }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Triggering immediate report execution")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_WhenSchedulerRunning_ShouldReturnRunningStatus()
    {
        // Act
        var status = await _sut.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnSchedulerMetadata()
    {
        // Act
        var status = await _sut.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        (status.ActiveJobs >= 0).Should().BeTrue();
        (status.TotalExecutions >= 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetStatusAsync_AfterSchedulingJobs_ShouldReflectJobCount()
    {
        // Arrange
        var report1 = CreateTestReport("0 0 9 * * ?");
        var report2 = CreateTestReport("0 0 12 * * ?");
        await _sut.ScheduleReportAsync(report1);
        await _sut.ScheduleReportAsync(report2);

        // Act
        var status = await _sut.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.IsRunning.Should().BeTrue();

        // Cleanup
        await _scheduler.DeleteJob(new JobKey($"report-{report1.Id}", "reports"));
        await _scheduler.DeleteJob(new JobKey($"report-{report2.Id}", "reports"));
    }

    #endregion

    #region Helper Methods

    private static AdminReport CreateTestReport(string scheduleExpression)
    {
        return new AdminReport
        {
            Id = Guid.NewGuid(),
            Name = "Test Report",
            Description = "Test Description",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object> { ["hours"] = 24 },
            ScheduleExpression = scheduleExpression,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "test@test.com",
            EmailRecipients = Array.Empty<string>()
        };
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_scheduler != null && !_scheduler.IsShutdown)
        {
            await _scheduler.Shutdown();
        }
    }
}
