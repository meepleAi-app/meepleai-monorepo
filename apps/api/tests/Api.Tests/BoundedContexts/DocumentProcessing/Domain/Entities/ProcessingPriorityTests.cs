using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingPriorityTests
{
    [Fact]
    public void Priority_Enum_HasCorrectValues()
    {
        ((int)ProcessingPriority.Low).Should().Be(0);
        ((int)ProcessingPriority.Normal).Should().Be(10);
        ((int)ProcessingPriority.High).Should().Be(20);
        ((int)ProcessingPriority.Urgent).Should().Be(30);
    }

    [Fact]
    public void Priority_HigherValue_IsHigherPriority()
    {
        // Higher int value = higher priority = dequeued first
        ((int)ProcessingPriority.Urgent).Should().BeGreaterThan((int)ProcessingPriority.High);
        ((int)ProcessingPriority.High).Should().BeGreaterThan((int)ProcessingPriority.Normal);
        ((int)ProcessingPriority.Normal).Should().BeGreaterThan((int)ProcessingPriority.Low);
    }

    [Fact]
    public void ProcessingJob_Create_WithPriority_SetsPriority()
    {
        var job = ProcessingJob.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            (int)ProcessingPriority.High, 0);

        job.Priority.Should().Be((int)ProcessingPriority.High);
    }

    [Fact]
    public void ProcessingJob_UpdatePriority_BumpsToUrgent()
    {
        var job = ProcessingJob.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            (int)ProcessingPriority.Normal, 0);

        job.UpdatePriority((int)ProcessingPriority.Urgent);

        job.Priority.Should().Be((int)ProcessingPriority.Urgent);
    }
}
