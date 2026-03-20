using Api.BoundedContexts.BusinessSimulations.Application.Jobs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Jobs;

/// <summary>
/// Unit tests for MonthlyLedgerReportJob (Issue #3724)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class MonthlyLedgerReportJobTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<MonthlyLedgerReportJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _contextMock;
    private readonly MonthlyLedgerReportJob _job;

    public MonthlyLedgerReportJobTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<MonthlyLedgerReportJob>>();
        _contextMock = new Mock<IJobExecutionContext>();
        _contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        _job = new MonthlyLedgerReportJob(_mediatorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_ShouldSendExportLedgerQuery()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ExportLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportLedgerResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf", "report.pdf"));

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ExportLedgerQuery>(q => q.Format == LedgerExportFormat.Pdf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldQueryPreviousMonthRange()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ExportLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportLedgerResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf", "report.pdf"));

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ExportLedgerQuery>(q =>
                q.DateFrom.HasValue &&
                q.DateTo.HasValue &&
                q.DateFrom.Value.Day == 1 &&
                q.DateTo.Value < DateTime.UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldSetContextResult()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ExportLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportLedgerResult(new byte[] { 0x25, 0x50 }, "application/pdf", "test.pdf"));

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert
        _contextMock.VerifySet(c => c.Result = It.IsAny<object>(), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenMediatorThrows_ShouldThrowJobExecutionException()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ExportLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository failure"));

        // Act & Assert
        var act = () => _job.Execute(_contextMock.Object);
        var exception = (await act.Should().ThrowAsync<JobExecutionException>()).Which;

        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Execute_WhenMediatorThrows_ShouldNotRefireImmediately()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ExportLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));

        // Act & Assert
        var act2 = () => _job.Execute(_contextMock.Object);
        var exception = (await act2.Should().ThrowAsync<JobExecutionException>()).Which;

        exception.RefireImmediately.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullMediator_ShouldThrow()
    {
        var act3 = () =>
            new MonthlyLedgerReportJob(null!, _loggerMock.Object);
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrow()
    {
        var act4 = () =>
            new MonthlyLedgerReportJob(_mediatorMock.Object, null!);
        act4.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Job_ShouldHaveDisallowConcurrentExecutionAttribute()
    {
        var attr = typeof(MonthlyLedgerReportJob)
            .GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), false);
        attr.Should().HaveCount(1);
    }
}
