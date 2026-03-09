using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Scheduling;

/// <summary>
/// Unit tests for LlmRequestLogPseudonymizationJob — GDPR log pseudonymization.
/// Issue #5511: Verifies daily job pseudonymizes UserId after 7 days.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class LlmRequestLogPseudonymizationJobTests
{
    private readonly Mock<ILlmRequestLogRepository> _repositoryMock;
    private readonly Mock<IJobExecutionContext> _contextMock;
    private readonly LlmRequestLogPseudonymizationOptions _options;
    private readonly LlmRequestLogPseudonymizationJob _job;

    public LlmRequestLogPseudonymizationJobTests()
    {
        _repositoryMock = new Mock<ILlmRequestLogRepository>();
        _options = new LlmRequestLogPseudonymizationOptions { Salt = "test-salt" };
        _contextMock = new Mock<IJobExecutionContext>();
        _contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        _contextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);

        _job = new LlmRequestLogPseudonymizationJob(
            _repositoryMock.Object,
            Options.Create(_options),
            Mock.Of<ILogger<LlmRequestLogPseudonymizationJob>>());
    }

    [Fact]
    public async Task Execute_CallsRepositoryWithCorrectCutoff()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var before = DateTime.UtcNow.AddDays(-LlmRequestLogPseudonymizationJob.RetentionDays);

        await _job.Execute(_contextMock.Object);

        var after = DateTime.UtcNow.AddDays(-LlmRequestLogPseudonymizationJob.RetentionDays);

        _repositoryMock.Verify(r => r.PseudonymizeOldLogsAsync(
            It.Is<DateTime>(d => d >= before && d <= after),
            "test-salt",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_PassesSaltFromOptions()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _job.Execute(_contextMock.Object);

        _repositoryMock.Verify(r => r.PseudonymizeOldLogsAsync(
            It.IsAny<DateTime>(),
            "test-salt",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_SetsSuccessResult()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await _job.Execute(_contextMock.Object);

        _contextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.ToString()!.Contains("Success") && r.ToString()!.Contains("True")));
    }

    [Fact]
    public async Task Execute_DoesNotThrowOnException()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var act = () => _job.Execute(_contextMock.Object);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Execute_SetsFailureResultOnException()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _job.Execute(_contextMock.Object);

        _contextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.ToString()!.Contains("Success") && r.ToString()!.Contains("False")));
    }

    [Fact]
    public void RetentionDays_Is7()
    {
        LlmRequestLogPseudonymizationJob.RetentionDays.Should().Be(7);
    }

    [Fact]
    public void Constructor_ThrowsOnNullRepository()
    {
        var act = () => new LlmRequestLogPseudonymizationJob(
            null!,
            Options.Create(_options),
            Mock.Of<ILogger<LlmRequestLogPseudonymizationJob>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        var act = () => new LlmRequestLogPseudonymizationJob(
            _repositoryMock.Object,
            null!,
            Mock.Of<ILogger<LlmRequestLogPseudonymizationJob>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new LlmRequestLogPseudonymizationJob(
            _repositoryMock.Object,
            Options.Create(_options),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Execute_ZeroPseudonymized_StillSucceeds()
    {
        _repositoryMock
            .Setup(r => r.PseudonymizeOldLogsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var act = () => _job.Execute(_contextMock.Object);

        await act.Should().NotThrowAsync();
        _repositoryMock.Verify(r => r.PseudonymizeOldLogsAsync(
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
