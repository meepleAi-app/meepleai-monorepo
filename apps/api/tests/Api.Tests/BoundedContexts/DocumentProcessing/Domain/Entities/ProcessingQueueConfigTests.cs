using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingQueueConfigTests
{
    [Fact]
    public void CreateDefault_ReturnsUnpausedConfigWithDefaultWorkers()
    {
        var config = ProcessingQueueConfig.CreateDefault();

        config.Id.Should().Be(ProcessingQueueConfig.SingletonId);
        config.IsPaused.Should().BeFalse();
        config.MaxConcurrentWorkers.Should().Be(ProcessingQueueConfig.DefaultMaxConcurrentWorkers);
    }

    [Fact]
    public void Update_SetsPause()
    {
        var config = ProcessingQueueConfig.CreateDefault();
        var userId = Guid.NewGuid();

        config.Update(isPaused: true, maxConcurrentWorkers: null, updatedBy: userId);

        config.IsPaused.Should().BeTrue();
        config.UpdatedBy.Should().Be(userId);
    }

    [Fact]
    public void Update_SetsMaxConcurrentWorkers()
    {
        var config = ProcessingQueueConfig.CreateDefault();

        config.Update(isPaused: null, maxConcurrentWorkers: 7, updatedBy: Guid.NewGuid());

        config.MaxConcurrentWorkers.Should().Be(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Update_InvalidConcurrency_ThrowsArgumentOutOfRange(int invalidValue)
    {
        var config = ProcessingQueueConfig.CreateDefault();

        var act = () => config.Update(isPaused: null, maxConcurrentWorkers: invalidValue, updatedBy: Guid.NewGuid());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Update_ValidConcurrency_Succeeds(int validValue)
    {
        var config = ProcessingQueueConfig.CreateDefault();

        config.Update(isPaused: null, maxConcurrentWorkers: validValue, updatedBy: Guid.NewGuid());

        config.MaxConcurrentWorkers.Should().Be(validValue);
    }

    [Fact]
    public void Reconstitute_RestoresState()
    {
        var updatedBy = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow;

        var config = ProcessingQueueConfig.Reconstitute(
            ProcessingQueueConfig.SingletonId,
            isPaused: true,
            maxConcurrentWorkers: 8,
            updatedAt: updatedAt,
            updatedBy: updatedBy);

        config.Id.Should().Be(ProcessingQueueConfig.SingletonId);
        config.IsPaused.Should().BeTrue();
        config.MaxConcurrentWorkers.Should().Be(8);
        config.UpdatedAt.Should().Be(updatedAt);
        config.UpdatedBy.Should().Be(updatedBy);
    }
}
