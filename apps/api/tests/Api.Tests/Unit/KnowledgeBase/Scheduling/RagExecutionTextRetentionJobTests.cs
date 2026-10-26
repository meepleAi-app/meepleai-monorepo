using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
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
/// #4634: <c>knowledge_base.rag_executions</c> keeps the question and previews of the retrieved passages of every
/// playground run with no expiry. The job blanks the texts of the rows older than a configurable number of days and
/// keeps the rows. These tests pin what the job asks of the repository; what the repository does to the table is in
/// <c>RagExecutionTextRetentionJobIntegrationTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4634")]
public sealed class RagExecutionTextRetentionJobTests
{
    private readonly Mock<IRagExecutionRepository> _repositoryMock = new();
    private readonly Mock<IJobExecutionContext> _contextMock = new();

    public RagExecutionTextRetentionJobTests()
    {
        _contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        _contextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
    }

    private RagExecutionTextRetentionJob CreateJob(RagExecutionTextRetentionOptions options) =>
        new(_repositoryMock.Object, Options.Create(options), Mock.Of<ILogger<RagExecutionTextRetentionJob>>());

    [Fact]
    public async Task Execute_BlanksTheRowsOlderThanTheConfiguredDays_InBatchesOfTheConfiguredSize()
    {
        _repositoryMock
            .Setup(r => r.BlankTextsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var job = CreateJob(new RagExecutionTextRetentionOptions { RetentionDays = 30, BatchSize = 200 });

        var before = DateTime.UtcNow.AddDays(-30);
        await job.Execute(_contextMock.Object);
        var after = DateTime.UtcNow.AddDays(-30);

        _repositoryMock.Verify(r => r.BlankTextsOlderThanAsync(
            It.Is<DateTime>(d => d >= before && d <= after),
            200,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A retention of zero days (or less) is a wrong setting, not "blank everything now": the only safe reaction is to
    /// touch nothing.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    public async Task Execute_WithARetentionThatIsNotPositive_TouchesNothing(int days)
    {
        var job = CreateJob(new RagExecutionTextRetentionOptions { RetentionDays = days });

        await job.Execute(_contextMock.Object);

        _repositoryMock.Verify(r => r.BlankTextsOlderThanAsync(
            It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Execute_WithABatchSizeThatIsNotPositive_UsesTheDefaultBatch(int batchSize)
    {
        var job = CreateJob(new RagExecutionTextRetentionOptions { RetentionDays = 90, BatchSize = batchSize });

        await job.Execute(_contextMock.Object);

        _repositoryMock.Verify(r => r.BlankTextsOlderThanAsync(
            It.IsAny<DateTime>(), RagExecutionTextRetentionOptions.DefaultBatchSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A background job must not take the scheduler down: a failed round is logged and the next day retries.
    /// </summary>
    [Fact]
    public async Task Execute_WhenTheRepositoryThrows_DoesNotPropagate()
    {
        _repositoryMock
            .Setup(r => r.BlankTextsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var job = CreateJob(new RagExecutionTextRetentionOptions());

        var act = () => job.Execute(_contextMock.Object);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// A retention so large that the cutoff falls before the first calendar day cannot be computed: that is a wrong
    /// setting too, and it must not take the scheduler down.
    /// </summary>
    [Fact]
    public async Task Execute_WithARetentionBeyondTheCalendar_DoesNotPropagate_AndTouchesNothing()
    {
        var job = CreateJob(new RagExecutionTextRetentionOptions { RetentionDays = int.MaxValue });

        var act = () => job.Execute(_contextMock.Object);

        await act.Should().NotThrowAsync();
        _repositoryMock.Verify(r => r.BlankTextsOlderThanAsync(
            It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A shutdown cancels the job's token. That is not a failed round, and Quartz has to see the cancellation.
    /// </summary>
    [Fact]
    public async Task Execute_WhenCancelled_LetsTheCancellationThrough()
    {
        _repositoryMock
            .Setup(r => r.BlankTextsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var job = CreateJob(new RagExecutionTextRetentionOptions());

        var act = () => job.Execute(_contextMock.Object);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
