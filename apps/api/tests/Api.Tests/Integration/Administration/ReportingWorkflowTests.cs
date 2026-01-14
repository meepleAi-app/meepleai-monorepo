using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Quartz.Impl;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for reporting workflow
/// ISSUE-916: End-to-end reporting tests
/// </summary>
public sealed class ReportingWorkflowTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAdminReportRepository _reportRepository;
    private readonly IReportExecutionRepository _executionRepository;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly ISchedulerFactory _schedulerFactory;

    public ReportingWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ReportingWorkflowTests_{Guid.NewGuid()}")
            .Options;

        var mediatorMock = new Mock<MediatR.IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(e => e.GetAndClearEvents()).Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        _reportRepository = new AdminReportRepository(_dbContext);
        _executionRepository = new ReportExecutionRepository(_dbContext);

        var loggerMock = new Mock<ILogger<ReportGeneratorService>>();
        _reportGenerator = new ReportGeneratorService(_dbContext, loggerMock.Object);

        _schedulerFactory = new StdSchedulerFactory();
    }

    [Fact]
    public async Task GenerateReportCommand_SystemHealth_ShouldCreateExecutionAndReturnData()
    {
        // Arrange
        var handler = new GenerateReportCommandHandler(
            _reportGenerator,
            _executionRepository,
            new Mock<ILogger<GenerateReportCommandHandler>>().Object);

        var command = new GenerateReportCommand
        {
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Json,
            Parameters = new Dictionary<string, object>()
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.ExecutionId);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.FileName);
        Assert.True(result.FileSizeBytes > 0);

        // Verify execution was saved
        var execution = await _executionRepository.GetByIdAsync(result.ExecutionId);
        Assert.NotNull(execution);
        Assert.Equal(ReportExecutionStatus.Completed, execution.Status);
    }

    [Fact]
    public async Task ScheduleReportCommand_WithValidCronExpression_ShouldCreateReportAndScheduleJob()
    {
        // Arrange
        var schedulerService = new QuartzReportSchedulerService(
            _schedulerFactory,
            new Mock<ILogger<QuartzReportSchedulerService>>().Object);

        var handler = new ScheduleReportCommandHandler(
            _reportRepository,
            schedulerService,
            new Mock<ILogger<ScheduleReportCommandHandler>>().Object);

        var command = new ScheduleReportCommand
        {
            Name = "Daily System Health",
            Description = "Automated daily health report",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Pdf,
            Parameters = new Dictionary<string, object> { ["hours"] = 24 },
            ScheduleExpression = "0 0 6 * * ?", // Daily at 6 AM
            CreatedBy = "admin@test.com"
        };

        // Act
        var reportId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, reportId);

        // Verify report was saved
        var report = await _reportRepository.GetByIdAsync(reportId);
        Assert.NotNull(report);
        Assert.Equal("Daily System Health", report.Name);
        Assert.True(report.IsActive);
        Assert.Equal("0 0 6 * * ?", report.ScheduleExpression);
    }

    [Fact]
    public async Task GetScheduledReportsQuery_ShouldReturnAllScheduledReports()
    {
        // Arrange
        var schedulerService = new QuartzReportSchedulerService(
            _schedulerFactory,
            new Mock<ILogger<QuartzReportSchedulerService>>().Object);

        // Create test reports
        var createHandler = new ScheduleReportCommandHandler(
            _reportRepository,
            schedulerService,
            new Mock<ILogger<ScheduleReportCommandHandler>>().Object);

        await createHandler.Handle(new ScheduleReportCommand
        {
            Name = "Report 1",
            Description = "Test 1",
            Template = ReportTemplate.SystemHealth,
            Format = ReportFormat.Csv,
            Parameters = new Dictionary<string, object>(),
            ScheduleExpression = "0 0 * * * ?",
            CreatedBy = "admin"
        }, CancellationToken.None);

        await createHandler.Handle(new ScheduleReportCommand
        {
            Name = "Report 2",
            Description = "Test 2",
            Template = ReportTemplate.AIUsage,
            Format = ReportFormat.Json,
            Parameters = new Dictionary<string, object>
            {
                ["startDate"] = DateTime.UtcNow.AddDays(-7),
                ["endDate"] = DateTime.UtcNow
            },
            ScheduleExpression = "0 0 0 * * ?",
            CreatedBy = "admin"
        }, CancellationToken.None);

        var queryHandler = new GetScheduledReportsQueryHandler(
            _reportRepository,
            new Mock<ILogger<GetScheduledReportsQueryHandler>>().Object);

        // Act
        var reports = await queryHandler.Handle(new GetScheduledReportsQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(reports);
        Assert.Equal(2, reports.Count);
        Assert.Contains(reports, r => r.Name == "Report 1");
        Assert.Contains(reports, r => r.Name == "Report 2");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
