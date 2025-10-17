using Api.Models;
using Xunit;

namespace Api.Tests.Models;

public class ProcessingProgressTests
{
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
        Assert.Equal(expectedPercent, percent);
    }

    [Fact]
    public void CalculatePercentComplete_WithZeroTotalPages_ReturnsStepStartPercentage()
    {
        // Arrange - extracting step should start at 20%
        // Act
        var percent = ProcessingProgress.CalculatePercentComplete(ProcessingStep.Extracting, 0, 0);

        // Assert
        Assert.Equal(20, percent);
    }

    [Fact]
    public void CalculatePercentComplete_WithNegativeTotalPages_ReturnsStepStartPercentage()
    {
        // Arrange
        // Act
        var percent = ProcessingProgress.CalculatePercentComplete(ProcessingStep.Chunking, 0, -5);

        // Assert
        Assert.Equal(40, percent);
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
        Assert.Null(estimate);
    }

    [Fact]
    public void EstimateTimeRemaining_WithHalfwayProgress_EstimatesCorrectly()
    {
        // Arrange - 50% complete after 60 seconds = 60 seconds remaining
        var elapsed = TimeSpan.FromSeconds(60);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(50, elapsed);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(60, estimate.Value.TotalSeconds, 0.1);
    }

    [Fact]
    public void EstimateTimeRemaining_WithQuarterProgress_EstimatesCorrectly()
    {
        // Arrange - 25% complete after 30 seconds = 90 seconds remaining (total 120s)
        var elapsed = TimeSpan.FromSeconds(30);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(25, elapsed);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(90, estimate.Value.TotalSeconds, 0.1);
    }

    [Fact]
    public void EstimateTimeRemaining_WithThreeQuartersProgress_EstimatesCorrectly()
    {
        // Arrange - 75% complete after 90 seconds = 30 seconds remaining (total 120s)
        var elapsed = TimeSpan.FromSeconds(90);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(75, elapsed);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(30, estimate.Value.TotalSeconds, 0.1);
    }

    [Fact]
    public void EstimateTimeRemaining_WithNearCompletion_EstimatesSmallRemainder()
    {
        // Arrange - 95% complete after 190 seconds = ~10 seconds remaining
        var elapsed = TimeSpan.FromSeconds(190);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(95, elapsed);

        // Assert
        Assert.NotNull(estimate);
        Assert.True(estimate.Value.TotalSeconds > 0);
        Assert.True(estimate.Value.TotalSeconds < 15);
    }

    [Fact]
    public void EstimateTimeRemaining_WithVerySmallElapsed_HandlesGracefully()
    {
        // Arrange - 10% complete after 1 second = 9 seconds remaining
        var elapsed = TimeSpan.FromSeconds(1);

        // Act
        var estimate = ProcessingProgress.EstimateTimeRemaining(10, elapsed);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(9, estimate.Value.TotalSeconds, 0.1);
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
        Assert.Equal(ProcessingStep.Extracting, progress.CurrentStep);
        Assert.Equal(35, progress.PercentComplete);
        Assert.Equal(TimeSpan.FromMinutes(2), progress.ElapsedTime);
        Assert.Equal(TimeSpan.FromMinutes(3), progress.EstimatedTimeRemaining);
        Assert.Equal(7, progress.PagesProcessed);
        Assert.Equal(20, progress.TotalPages);
        Assert.Equal(startTime, progress.StartedAt);
        Assert.Null(progress.CompletedAt);
        Assert.Null(progress.ErrorMessage);
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
        Assert.Equal(ProcessingStep.Failed, progress.CurrentStep);
        Assert.Equal("Extraction failed: file corrupted", progress.ErrorMessage);
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
        Assert.Equal(ProcessingStep.Completed, progress.CurrentStep);
        Assert.Equal(100, progress.PercentComplete);
        Assert.Equal(completedTime, progress.CompletedAt);
    }
}
