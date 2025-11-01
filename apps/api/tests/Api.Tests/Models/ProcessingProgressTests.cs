using Api.Models;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Models;

public class ProcessingProgressTests
{
    private readonly ITestOutputHelper _output;

    [Theory]
    [InlineData(ProcessingStep.Uploading, 0, 0, 10)]
    [InlineData(ProcessingStep.Extracting, 0, 10, 20)]
    [InlineData(ProcessingStep.Extracting, 5, 10, 30)]
    [InlineData(ProcessingStep.Extracting, 10, 10, 40)]
    [InlineData(ProcessingStep.Chunking, 0, 10, 40)]
    [InlineData(ProcessingStep.Chunking, 5, 10, 50)]
    [InlineData(ProcessingStep.Chunking, 10, 10, 60)]
    [InlineData(ProcessingStep.Embedding, 0, 10, 60)]
    [InlineData(ProcessingStep.Embedding, 5, 10, 70)]
    [InlineData(ProcessingStep.Embedding, 10, 10, 80)]
    [InlineData(ProcessingStep.Indexing, 0, 10, 80)]
    [InlineData(ProcessingStep.Indexing, 5, 10, 90)]
    [InlineData(ProcessingStep.Indexing, 10, 10, 100)]
    [InlineData(ProcessingStep.Completed, 10, 10, 100)]
    [InlineData(ProcessingStep.Failed, 0, 0, 0)]
    public void CalculatePercentComplete_ReturnsCorrectPercentage(
        ProcessingStep step,
        int pagesProcessed,
        int totalPages,
        int expectedPercent)
    {
        // Act
        var percent = ProcessingProgress.CalculatePercentComplete(step, pagesProcessed, totalPages);

        // Assert
        percent.Should().Be(expectedPercent);
    }

    [Fact]
    public void CalculatePercentComplete_WithZeroTotalPages_ReturnsStepStartPercentage()
    {
        // Arrange - extracting step should start at 20%
        // Act
        var percent = ProcessingProgress.CalculatePercentComplete(ProcessingStep.Extracting, 0, 0);

        // Assert
        percent.Should().Be(20);
    }

    [Fact]
    public void CalculatePercentComplete_WithNegativeTotalPages_ReturnsStepStartPercentage()
    {
        // Arrange
        // Act
        var percent = ProcessingProgress.CalculatePercentComplete(ProcessingStep.Chunking, 0, -5);

        // Assert
        percent.Should().Be(40);
    }

    [Theory]
    [InlineData(0, 100)]  // 0% complete, should return null
    [InlineData(100, 100)] // 100% complete, should return null
    [InlineData(-10, 100)] // Negative percent, should return null
    [InlineData(150, 100)] // Over 100%, should return null
    public void EstimateTimeRemaining_WithEdgeCases_ReturnsNull(int percentComplete, double elapsedSeconds)
    {
        // Arrange
        var elapsed = TimeSpan.FromSeconds(elapsedSeconds);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(percentComplete, elapsed);

        // Assert
        estimate.Should().BeNull();
    }

    [Fact]
    public void EstimateTimeRemaining_WithHalfwayProgress_EstimatesCorrectly()
    {
        // Arrange - 50% complete after 60 seconds = 60 seconds remaining
        var elapsed = TimeSpan.FromSeconds(60);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(50, elapsed);

        // Assert
        estimate.Should().NotBeNull();
        estimate.Value.TotalSeconds, 0.1.Should().Be(60);
    }

    [Fact]
    public void EstimateTimeRemaining_WithQuarterProgress_EstimatesCorrectly()
    {
        // Arrange - 25% complete after 30 seconds = 90 seconds remaining (total 120s)
        var elapsed = TimeSpan.FromSeconds(30);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(25, elapsed);

        // Assert
        estimate.Should().NotBeNull();
        estimate.Value.TotalSeconds, 0.1.Should().Be(90);
    }

    [Fact]
    public void EstimateTimeRemaining_WithThreeQuartersProgress_EstimatesCorrectly()
    {
        // Arrange - 75% complete after 90 seconds = 30 seconds remaining (total 120s)
        var elapsed = TimeSpan.FromSeconds(90);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(75, elapsed);

        // Assert
        estimate.Should().NotBeNull();
        estimate.Value.TotalSeconds, 0.1.Should().Be(30);
    }

    [Fact]
    public void EstimateTimeRemaining_WithNearCompletion_EstimatesSmallRemainder()
    {
        // Arrange - 95% complete after 190 seconds = ~10 seconds remaining
        var elapsed = TimeSpan.FromSeconds(190);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(95, elapsed);

        // Assert
        estimate.Should().NotBeNull();
        estimate.Value.TotalSeconds > 0.Should().BeTrue();
        estimate.Value.TotalSeconds < 15.Should().BeTrue();
    }

    [Fact]
    public void EstimateTimeRemaining_WithVerySmallElapsed_HandlesGracefully()
    {
        // Arrange - 10% complete after 1 second = 9 seconds remaining
        var elapsed = TimeSpan.FromSeconds(1);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(10, elapsed);

        // Assert
        estimate.Should().NotBeNull();
        estimate.Value.TotalSeconds, 0.1.Should().Be(9);
    }

    [Fact]
    public void ProcessingProgress_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var progress = new ProcessingProgress
        {
            CurrentStep = ProcessingStep.Extracting,
            PercentComplete = 35,
            ElapsedTime = TimeSpan.FromMinutes(2),
            EstimatedTimeRemaining = TimeSpan.FromMinutes(3),
            PagesProcessed = 7,
            TotalPages = 20,
            StartedAt = startTime,
            CompletedAt = null,
            ErrorMessage = null
        };

        // Assert
        progress.CurrentStep.Should().Be(ProcessingStep.Extracting);
        progress.PercentComplete.Should().Be(35);
        progress.ElapsedTime.Should().Be(TimeSpan.FromMinutes(2));
        progress.EstimatedTimeRemaining.Should().Be(TimeSpan.FromMinutes(3));
        progress.PagesProcessed.Should().Be(7);
        progress.TotalPages.Should().Be(20);
        progress.StartedAt.Should().Be(startTime);
        progress.CompletedAt.Should().BeNull();
        progress.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ProcessingProgress_WithError_SetsErrorMessage()
    {
        // Arrange
        var progress = new ProcessingProgress
        {
            CurrentStep = ProcessingStep.Failed,
            PercentComplete = 45,
            ErrorMessage = "Extraction failed: file corrupted"
        };

        // Assert
        progress.CurrentStep.Should().Be(ProcessingStep.Failed);
        progress.ErrorMessage.Should().Be("Extraction failed: file corrupted");
    }

    [Fact]
    public void ProcessingProgress_WhenCompleted_HasCompletedAt()
    {
        // Arrange
        var completedTime = DateTime.UtcNow;
        var progress = new ProcessingProgress
        {
            CurrentStep = ProcessingStep.Completed,
            PercentComplete = 100,
            CompletedAt = completedTime
        };

        // Assert
        progress.CurrentStep.Should().Be(ProcessingStep.Completed);
        progress.PercentComplete.Should().Be(100);
        progress.CompletedAt.Should().Be(completedTime);
    }
}
