using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;
using ReportFormat = Api.BoundedContexts.Administration.Domain.ValueObjects.ReportFormat;
using ReportTemplate = Api.BoundedContexts.Administration.Domain.ValueObjects.ReportTemplate;
using ReportExecutionStatus = Api.BoundedContexts.Administration.Domain.ValueObjects.ReportExecutionStatus;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for report generation with email delivery
/// ISSUE-918: Email delivery integration tests
/// </summary>
public sealed class ReportEmailIntegrationTests
{
    private readonly Mock<IReportGeneratorService> _mockReportGenerator;
    private readonly Mock<IAdminReportRepository> _mockReportRepository;
    private readonly Mock<IReportExecutionRepository> _mockExecutionRepository;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<GenerateReportJob>> _mockLogger;
    private readonly Mock<IJobExecutionContext> _mockContext;

    public ReportEmailIntegrationTests()
    {
        _mockReportGenerator = new Mock<IReportGeneratorService>();
        _mockReportRepository = new Mock<IAdminReportRepository>();
        _mockExecutionRepository = new Mock<IReportExecutionRepository>();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<GenerateReportJob>>();
        _mockContext = new Mock<IJobExecutionContext>();

        var jobDataMap = new JobDataMap { { "ReportId", Guid.NewGuid() } };
        _mockContext.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);
        _mockContext.Setup(x => x.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
    }

    [Fact]
    public async Task Execute_WithEmailRecipients_ShouldSendEmail()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var recipients = new List<string> { "admin@test.com", "user@test.com" }.AsReadOnly();
        
        var report = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test Description",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 * * *",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = recipients
        };

        var reportData = new ReportData(
            Content: new byte[] { 1, 2, 3, 4, 5 },
            FileName: "test-report.pdf",
            FileSizeBytes: 5,
            Metadata: new Dictionary<string, object>());

        var jobDataMap = new JobDataMap { { "ReportId", reportId } };
        _mockContext.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _mockReportGenerator
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

        _mockReportRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEmailService
            .Setup(x => x.SendReportEmailAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new GenerateReportJob(
            _mockReportGenerator.Object,
            _mockReportRepository.Object,
            _mockExecutionRepository.Object,
            _mockEmailService.Object,
            _mockLogger.Object);

        // Act
        await job.Execute(_mockContext.Object);

        // Assert
        _mockEmailService.Verify(
            x => x.SendReportEmailAsync(
                It.Is<IReadOnlyList<string>>(r => r.Count == 2),
                "Test Report",
                "Test Description",
                It.IsAny<byte[]>(),
                "test-report.pdf",
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithNoEmailRecipients_ShouldNotSendEmail()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        
        var report = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test Description",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 * * *",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = Array.Empty<string>()
        };

        var reportData = new ReportData(
            Content: new byte[] { 1, 2, 3 },
            FileName: "test-report.pdf",
            FileSizeBytes: 3,
            Metadata: new Dictionary<string, object>());

        var jobDataMap = new JobDataMap { { "ReportId", reportId } };
        _mockContext.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _mockReportGenerator
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

        _mockReportRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new GenerateReportJob(
            _mockReportGenerator.Object,
            _mockReportRepository.Object,
            _mockExecutionRepository.Object,
            _mockEmailService.Object,
            _mockLogger.Object);

        // Act
        await job.Execute(_mockContext.Object);

        // Assert
        _mockEmailService.Verify(
            x => x.SendReportEmailAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_WhenReportFails_ShouldSendFailureEmail()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var recipients = new List<string> { "admin@test.com" }.AsReadOnly();
        
        var report = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test Description",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 * * *",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = recipients
        };

        var jobDataMap = new JobDataMap { { "ReportId", reportId } };
        _mockContext.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _mockReportGenerator
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

        _mockEmailService
            .Setup(x => x.SendReportFailureEmailAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new GenerateReportJob(
            _mockReportGenerator.Object,
            _mockReportRepository.Object,
            _mockExecutionRepository.Object,
            _mockEmailService.Object,
            _mockLogger.Object);

        // Act
        await job.Execute(_mockContext.Object);

        // Assert
        _mockEmailService.Verify(
            x => x.SendReportFailureEmailAsync(
                It.Is<IReadOnlyList<string>>(r => r.Count == 1),
                "Test Report",
                "Test error",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WhenEmailFails_ShouldLogErrorButCompleteReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var recipients = new List<string> { "admin@test.com" }.AsReadOnly();
        
        var report = new AdminReport
        {
            Id = reportId,
            Name = "Test Report",
            Description = "Test Description",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 * * *",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = "admin@test.com",
            EmailRecipients = recipients
        };

        var reportData = new ReportData(
            Content: new byte[] { 1, 2, 3 },
            FileName: "test-report.pdf",
            FileSizeBytes: 3,
            Metadata: new Dictionary<string, object>());

        var jobDataMap = new JobDataMap { { "ReportId", reportId } };
        _mockContext.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);

        _mockReportRepository
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _mockReportGenerator
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

        _mockReportRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AdminReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEmailService
            .Setup(x => x.SendReportEmailAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP error"));

        var job = new GenerateReportJob(
            _mockReportGenerator.Object,
            _mockReportRepository.Object,
            _mockExecutionRepository.Object,
            _mockEmailService.Object,
            _mockLogger.Object);

        // Act
        await job.Execute(_mockContext.Object);

        // Assert - Report should still be marked as completed
        _mockExecutionRepository.Verify(
            x => x.UpdateAsync(
                It.Is<ReportExecution>(e => e.Status == ReportExecutionStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Error should be logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send report email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
