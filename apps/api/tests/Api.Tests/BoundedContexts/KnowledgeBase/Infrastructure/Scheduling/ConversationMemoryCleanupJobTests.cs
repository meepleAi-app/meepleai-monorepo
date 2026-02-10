using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Tests for ConversationMemoryCleanupJob.
/// Issue #3498: Conversation Memory - Temporal RAG Implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ConversationMemoryCleanupJobTests
{
    private readonly Mock<IConversationMemoryRepository> _repositoryMock;
    private readonly Mock<IOptions<ConversationMemoryCleanupOptions>> _optionsMock;
    private readonly Mock<ILogger<ConversationMemoryCleanupJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _jobContextMock;

    public ConversationMemoryCleanupJobTests()
    {
        _repositoryMock = new Mock<IConversationMemoryRepository>();
        _optionsMock = new Mock<IOptions<ConversationMemoryCleanupOptions>>();
        _loggerMock = new Mock<ILogger<ConversationMemoryCleanupJob>>();
        _jobContextMock = new Mock<IJobExecutionContext>();

        _jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        _jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
    }

    private ConversationMemoryCleanupJob CreateJob(ConversationMemoryCleanupOptions? options = null)
    {
        var opts = options ?? new ConversationMemoryCleanupOptions
        {
            Enabled = true,
            RetentionPeriod = TimeSpan.FromDays(90),
            CleanupInterval = TimeSpan.FromHours(24),
            BatchSize = 1000
        };
        _optionsMock.Setup(o => o.Value).Returns(opts);

        return new ConversationMemoryCleanupJob(
            _repositoryMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_WhenDisabled_DoesNotDeleteAnything()
    {
        // Arrange
        var job = CreateJob(new ConversationMemoryCleanupOptions { Enabled = false });

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _repositoryMock.Verify(
            r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_WhenEnabled_DeletesOldMemories()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _repositoryMock.Verify(
            r => r.DeleteOlderThanAsync(
                It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-89)), // 90 days minus some tolerance
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ReturnsSuccessResult_WhenDeletionSucceeds()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("DeletedCount")!.GetValue(r)!.Equals(42)));
    }

    [Fact]
    public async Task Execute_ReturnsFailureResult_WhenExceptionOccurs()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert - Job should catch exception and not rethrow
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false)));
    }

    [Fact]
    public async Task Execute_DoesNotThrow_WhenRepositoryFails()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act & Assert - Should not throw (Quartz background job pattern)
        var exception = await Record.ExceptionAsync(() => job.Execute(_jobContextMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Execute_UsesConfiguredRetentionPeriod()
    {
        // Arrange
        var customOptions = new ConversationMemoryCleanupOptions
        {
            Enabled = true,
            RetentionPeriod = TimeSpan.FromDays(30), // 30 days instead of default 90
            CleanupInterval = TimeSpan.FromHours(24),
            BatchSize = 1000
        };
        var job = CreateJob(customOptions);
        DateTime capturedCutoff = default;

        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync(10);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert - Cutoff should be ~30 days ago
        var expectedCutoff = DateTime.UtcNow.AddDays(-30);
        var tolerance = TimeSpan.FromMinutes(1);
        Assert.InRange(capturedCutoff, expectedCutoff.Add(-tolerance), expectedCutoff.Add(tolerance));
    }

    [Fact]
    public async Task Execute_LogsInformation_OnSuccessfulExecution()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert - Verify logging was called (at least start and completion)
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting conversation memory cleanup")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_PassesCancellationToken_ToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _jobContextMock.Setup(c => c.CancellationToken).Returns(cts.Token);

        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), cts.Token))
            .ReturnsAsync(0);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _repositoryMock.Verify(
            r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), cts.Token),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public async Task Execute_WorksWithVariousRetentionPeriods(int days)
    {
        // Arrange
        var options = new ConversationMemoryCleanupOptions
        {
            Enabled = true,
            RetentionPeriod = TimeSpan.FromDays(days),
            CleanupInterval = TimeSpan.FromHours(24),
            BatchSize = 1000
        };
        var job = CreateJob(options);
        DateTime capturedCutoff = default;

        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync(0);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        var expectedCutoff = DateTime.UtcNow.AddDays(-days);
        var tolerance = TimeSpan.FromMinutes(1);
        Assert.InRange(capturedCutoff, expectedCutoff.Add(-tolerance), expectedCutoff.Add(tolerance));
    }

    [Fact]
    public async Task Execute_DeletesZeroRecords_StillReportsSuccess()
    {
        // Arrange
        var job = CreateJob();
        _repositoryMock
            .Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await job.Execute(_jobContextMock.Object);

        // Assert
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("DeletedCount")!.GetValue(r)!.Equals(0)));
    }
}
