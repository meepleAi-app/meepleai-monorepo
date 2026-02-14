using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
public sealed class MetricsMaintenanceJobTests
{
    private readonly Mock<IProcessingMetricsService> _metricsServiceMock;
    private readonly Mock<IJobExecutionContext> _contextMock;
    private readonly MetricsMaintenanceJob _job;

    public MetricsMaintenanceJobTests()
    {
        _metricsServiceMock = new Mock<IProcessingMetricsService>();
        _contextMock = new Mock<IJobExecutionContext>();
        _contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _job = new MetricsMaintenanceJob(
            _metricsServiceMock.Object,
            NullLogger<MetricsMaintenanceJob>.Instance);
    }

    [Fact]
    public async Task Execute_Success_DeletesOldMetrics()
    {
        // Arrange
        _metricsServiceMock
            .Setup(s => s.CleanupOldMetricsAsync(1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(250); // 250 records deleted

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert
        _metricsServiceMock.Verify(
            s => s.CleanupOldMetricsAsync(1000, It.IsAny<CancellationToken>()),
            Times.Once);

        _contextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            (int)r.GetType().GetProperty("MetricsDeleted")!.GetValue(r)! == 250));
    }

    [Fact]
    public async Task Execute_ServiceThrows_CapturesException()
    {
        // Arrange
        _metricsServiceMock
            .Setup(s => s.CleanupOldMetricsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert - Job should not throw, but set Result with error
        _contextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false)));
    }

    [Fact]
    public async Task Execute_CancellationRequested_HandlesGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _contextMock.Setup(c => c.CancellationToken).Returns(cts.Token);

        _metricsServiceMock
            .Setup(s => s.CleanupOldMetricsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _job.Execute(_contextMock.Object);

        // Assert - Should handle cancellation gracefully
        _contextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false)));
    }
}
