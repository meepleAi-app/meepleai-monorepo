using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for reporting handlers (Commands/Queries)
/// ISSUE-919: Handler logic testing (90%+ coverage)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ReportingHandlersTests
{
    private readonly Mock<IReportGeneratorService> _mockGeneratorService;
    private readonly Mock<IAdminReportRepository> _mockReportRepository;
    private readonly Mock<IReportExecutionRepository> _mockExecutionRepository;
    private readonly Mock<IReportSchedulerService> _mockSchedulerService;

    public ReportingHandlersTests()
    {
        _mockGeneratorService = new Mock<IReportGeneratorService>();
        _mockReportRepository = new Mock<IAdminReportRepository>();
        _mockExecutionRepository = new Mock<IReportExecutionRepository>();
        _mockSchedulerService = new Mock<IReportSchedulerService>();
    }

    #region GenerateReportCommandHandler Tests

    [Fact]
    public async Task GenerateReportCommandHandler_WithValidCommand_ShouldGenerateReport()
    {
        // Arrange
        var handler = new GenerateReportCommandHandler(
            _mockGeneratorService.Object,
            _mockExecutionRepository.Object,
            new Mock<ILogger<GenerateReportCommandHandler>>().Object);

        var command = new GenerateReportCommand
        {
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Json,
            Parameters = new Dictionary<string, object> { ["hours"] = 24 }
        };

        var reportData = new ReportData(
            Content: new byte[] { 1, 2, 3 },
            FileName: "test.json",
            FileSizeBytes: 3,
            Metadata: new Dictionary<string, object>());

        _mockGeneratorService
            .Setup(x => x.GenerateAsync(
                It.IsAny<ReportTemplate>(),
                It.IsAny<ReportFormat>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportData);

        _mockExecutionRepository
            .Setup(x => x.AddAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockExecutionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Content.Length.Should().Be(3);
        result.FileName.Should().Be("test.json");
        result.FileSizeBytes.Should().Be(3);
        result.ExecutionId.Should().NotBe(Guid.Empty);

        _mockGeneratorService.Verify(
            x => x.GenerateAsync(
                ReportTemplate.SystemHealth,
                ReportFormat.Json,
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockExecutionRepository.Verify(
            x => x.AddAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockExecutionRepository.Verify(
            x => x.UpdateAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportCommandHandler_WhenGenerationFails_ShouldRecordFailure()
    {
        // Arrange
        var handler = new GenerateReportCommandHandler(
            _mockGeneratorService.Object,
            _mockExecutionRepository.Object,
            new Mock<ILogger<GenerateReportCommandHandler>>().Object);

        var command = new GenerateReportCommand
        {
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>()
        };

        _mockGeneratorService
            .Setup(x => x.GenerateAsync(
                It.IsAny<ReportTemplate>(),
                It.IsAny<ReportFormat>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _mockExecutionRepository
            .Setup(x => x.AddAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockExecutionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var act = () =>
            handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify execution was created and updated with failure
        _mockExecutionRepository.Verify(
            x => x.AddAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockExecutionRepository.Verify(
            x => x.UpdateAsync(
                It.Is<ReportExecution>(e => e.Status == ReportExecutionStatus.Failed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportCommandHandler_WithAllTemplates_ShouldWork()
    {
        // Arrange
        var handler = new GenerateReportCommandHandler(
            _mockGeneratorService.Object,
            _mockExecutionRepository.Object,
            new Mock<ILogger<GenerateReportCommandHandler>>().Object);

        var reportData = new ReportData(
            Content: new byte[] { 1 },
            FileName: "test.json",
            FileSizeBytes: 1,
            Metadata: new Dictionary<string, object>());

        _mockGeneratorService
            .Setup(x => x.GenerateAsync(
                It.IsAny<ReportTemplate>(),
                It.IsAny<ReportFormat>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportData);

        _mockExecutionRepository
            .Setup(x => x.AddAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockExecutionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ReportExecution>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Test all templates
        var templates = new[]
        {
            ReportTemplate.SystemHealth,
            ReportTemplate.UserActivity,
            ReportTemplate.AIUsage,
            ReportTemplate.ContentMetrics
        };

        foreach (var template in templates)
        {
            var command = new GenerateReportCommand
            {
                Template = template,
                Format = ReportFormat.Json,
                Parameters = template == ReportTemplate.SystemHealth
                    ? new Dictionary<string, object> { ["hours"] = 24 }
                    : new Dictionary<string, object>
                    {
                        ["startDate"] = DateTime.UtcNow.AddDays(-7),
                        ["endDate"] = DateTime.UtcNow
                    }
            };

            var result = await handler.Handle(command, CancellationToken.None);
            result.Should().NotBeNull();
        }
    }

    #endregion

    #region ScheduleReportCommandHandler Tests

    [Fact]
    public async Task ScheduleReportCommandHandler_WithValidCommand_ShouldScheduleReport()
    {
        // Arrange
        var handler = new ScheduleReportCommandHandler(
            _mockReportRepository.Object,
            _mockSchedulerService.Object,
            new Mock<ILogger<ScheduleReportCommandHandler>>().Object);

        var command = new ScheduleReportCommand
        {
            Name = "Daily Report",
            Description = "Test",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Csv,
            Parameters = new Dictionary<string, object> { ["hours"] = 24 },
            ScheduleExpression = "0 0 9 * * ?",
            CreatedBy = "admin@test.com",
            EmailRecipients = new List<string> { "recipient@test.com" }
        };

        _mockReportRepository
            .Setup(x => x.AddAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSchedulerService
            .Setup(x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var reportId = await handler.Handle(command, CancellationToken.None);

        // Assert
        reportId.Should().NotBe(Guid.Empty);

        _mockReportRepository.Verify(
            x => x.AddAsync(
                It.Is<AdminReport>(r =>
                    r.Name == "Daily Report" &&
                    r.Template == ReportTemplate.SystemHealth &&
                    r.ScheduleExpression == "0 0 9 * * ?"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSchedulerService.Verify(
            x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleReportCommandHandler_WithEmailRecipients_ShouldSaveRecipients()
    {
        // Arrange
        var handler = new ScheduleReportCommandHandler(
            _mockReportRepository.Object,
            _mockSchedulerService.Object,
            new Mock<ILogger<ScheduleReportCommandHandler>>().Object);

        var recipients = new List<string> { "user1@test.com", "user2@test.com" };
        var command = new ScheduleReportCommand
        {
            Name = "Weekly Report",
            Description = "Test",
            Template = ReportTemplate.UserActivity,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>
            {
                ["startDate"] = DateTime.UtcNow.AddDays(-7),
                ["endDate"] = DateTime.UtcNow
            },
            ScheduleExpression = "0 0 9 ? * MON",
            CreatedBy = "admin@test.com",
            EmailRecipients = recipients
        };

        _mockReportRepository
            .Setup(x => x.AddAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSchedulerService
            .Setup(x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var reportId = await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockReportRepository.Verify(
            x => x.AddAsync(
                It.Is<AdminReport>(r => r.EmailRecipients.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpdateReportScheduleCommandHandler Tests

    [Fact]
    public async Task UpdateReportScheduleCommandHandler_WithExistingReport_ShouldUpdateSchedule()
    {
        // Arrange
        var handler = new UpdateReportScheduleCommandHandler(
            _mockReportRepository.Object,
            _mockSchedulerService.Object,
            new Mock<ILogger<UpdateReportScheduleCommandHandler>>().Object);

        var reportId = Guid.NewGuid();
        var existingReport = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Json,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 9 * * ?",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = Array.Empty<string>()
        };

        var command = new UpdateReportScheduleCommand
        {
            ReportId = reportId,
            ScheduleExpression = "0 0 12 * * ?",
            IsActive = true
        };

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReport);

        _mockReportRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSchedulerService
            .Setup(x => x.UnscheduleReportAsync(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSchedulerService
            .Setup(x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var success = await handler.Handle(command, CancellationToken.None);

        // Assert
        success.Should().BeTrue();

        _mockSchedulerService.Verify(
            x => x.UnscheduleReportAsync(reportId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSchedulerService.Verify(
            x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateReportScheduleCommandHandler_WithNonExistingReport_ShouldReturnFalse()
    {
        // Arrange
        var handler = new UpdateReportScheduleCommandHandler(
            _mockReportRepository.Object,
            _mockSchedulerService.Object,
            new Mock<ILogger<UpdateReportScheduleCommandHandler>>().Object);

        var command = new UpdateReportScheduleCommand
        {
            ReportId = Guid.NewGuid(),
            ScheduleExpression = "0 0 12 * * ?",
            IsActive = true
        };

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminReport?)null);

        // Act
        var success = await handler.Handle(command, CancellationToken.None);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateReportScheduleCommandHandler_WhenDeactivating_ShouldUnscheduleOnly()
    {
        // Arrange
        var handler = new UpdateReportScheduleCommandHandler(
            _mockReportRepository.Object,
            _mockSchedulerService.Object,
            new Mock<ILogger<UpdateReportScheduleCommandHandler>>().Object);

        var reportId = Guid.NewGuid();
        var existingReport = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Json,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 9 * * ?",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = Array.Empty<string>()
        };

        var command = new UpdateReportScheduleCommand
        {
            ReportId = reportId,
            ScheduleExpression = null,
            IsActive = false
        };

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReport);

        _mockReportRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSchedulerService
            .Setup(x => x.UnscheduleReportAsync(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var success = await handler.Handle(command, CancellationToken.None);

        // Assert
        success.Should().BeTrue();

        _mockSchedulerService.Verify(
            x => x.UnscheduleReportAsync(reportId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSchedulerService.Verify(
            x => x.ScheduleReportAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetScheduledReportsQueryHandler Tests

    [Fact]
    public async Task GetScheduledReportsQueryHandler_ShouldReturnAllReports()
    {
        // Arrange
        var handler = new GetScheduledReportsQueryHandler(
            _mockReportRepository.Object,
            new Mock<ILogger<GetScheduledReportsQueryHandler>>().Object);

        var reports = new List<AdminReport>
        {
            new AdminReport
            {
                Id = Guid.NewGuid(),
                Name = "Report 1",
                Description = "Desc 1",
                Template = ReportTemplate.SystemHealth,
                Format = ReportFormat.Csv,
                Parameters = new Dictionary<string, object>(),
                ScheduleExpression = "0 0 9 * * ?",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastExecutedAt = null,
                CreatedBy = "admin",
                EmailRecipients = Array.Empty<string>()
            },
            new AdminReport
            {
                Id = Guid.NewGuid(),
                Name = "Report 2",
                Description = "Desc 2",
                Template = ReportTemplate.AIUsage,
                Format = ReportFormat.Pdf,
                Parameters = new Dictionary<string, object>(),
                ScheduleExpression = "0 0 12 * * ?",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastExecutedAt = null,
                CreatedBy = "admin",
                EmailRecipients = Array.Empty<string>()
            }
        };

        _mockReportRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        // Act
        var result = await handler.Handle(new GetScheduledReportsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().Contain(r => r.Name == "Report 1");
        result.Should().Contain(r => r.Name == "Report 2");
    }

    [Fact]
    public async Task GetScheduledReportsQueryHandler_WithNoReports_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetScheduledReportsQueryHandler(
            _mockReportRepository.Object,
            new Mock<ILogger<GetScheduledReportsQueryHandler>>().Object);

        _mockReportRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdminReport>());

        // Act
        var result = await handler.Handle(new GetScheduledReportsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetReportExecutionsQueryHandler Tests

    [Fact]
    public async Task GetReportExecutionsQueryHandler_WithReportId_ShouldFilterByReportId()
    {
        // Arrange
        var handler = new GetReportExecutionsQueryHandler(
            _mockExecutionRepository.Object,
            new Mock<ILogger<GetReportExecutionsQueryHandler>>().Object);

        var reportId = Guid.NewGuid();
        var query = new GetReportExecutionsQuery
        {
            ReportId = reportId,
            Limit = 10
        };

        var executions = new List<ReportExecution>
        {
            new ReportExecution
            {
                Id = Guid.NewGuid(),
                ReportId = reportId,
                Status = ReportExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                FileSizeBytes = 1000,
                ErrorMessage = null,
                OutputPath = "/reports/test.pdf",
                Duration = TimeSpan.FromHours(1),
                ExecutionMetadata = new Dictionary<string, object>()
            }
        };

        _mockExecutionRepository
            .Setup(x => x.GetByReportIdAsync(reportId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(executions);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].ReportId.Should().Be(reportId);
    }

    [Fact]
    public async Task GetReportExecutionsQueryHandler_WithoutReportId_ShouldReturnAllExecutions()
    {
        // Arrange
        var handler = new GetReportExecutionsQueryHandler(
            _mockExecutionRepository.Object,
            new Mock<ILogger<GetReportExecutionsQueryHandler>>().Object);

        var query = new GetReportExecutionsQuery
        {
            ReportId = null,
            Limit = 50
        };

        var executions = new List<ReportExecution>
        {
            new ReportExecution
            {
                Id = Guid.NewGuid(),
                ReportId = Guid.NewGuid(),
                Status = ReportExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow.AddHours(-3),
                CompletedAt = DateTime.UtcNow.AddHours(-2),
                FileSizeBytes = 1000,
                ErrorMessage = null,
                OutputPath = "/reports/test1.pdf",
                Duration = TimeSpan.FromHours(1),
                ExecutionMetadata = new Dictionary<string, object>()
            },
            new ReportExecution
            {
                Id = Guid.NewGuid(),
                ReportId = Guid.NewGuid(),
                Status = ReportExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                CompletedAt = DateTime.UtcNow,
                FileSizeBytes = 2000,
                ErrorMessage = null,
                OutputPath = "/reports/test2.pdf",
                Duration = TimeSpan.FromHours(1),
                ExecutionMetadata = new Dictionary<string, object>()
            }
        };

        _mockExecutionRepository
            .Setup(x => x.GetRecentExecutionsAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(executions);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
    }

    #endregion
}
