using System;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveHealthLoggerServiceTests
{
    [Fact]
    public void EvaluateAndLog_WhenAgeIsNull_DoesNotLog()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns((long?)null);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        VerifyNoWarning(logger);
    }

    [Fact]
    public void EvaluateAndLog_WhenAgeBelowThreshold_DoesNotLog()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns(60L);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        VerifyNoWarning(logger);
    }

    [Fact]
    public void EvaluateAndLog_WhenAgeAboveThreshold_LogsWarning()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns(150L);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("AutoSave job stale")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static void VerifyNoWarning(Mock<ILogger<AutoSaveHealthLoggerService>> logger)
    {
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
