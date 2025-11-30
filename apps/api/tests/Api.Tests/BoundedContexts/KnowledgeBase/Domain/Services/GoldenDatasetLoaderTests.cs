using Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for GoldenDatasetLoader.
/// BGAI-059: Golden dataset loading, filtering, and sampling logic validation.
/// </summary>
public class GoldenDatasetLoaderTests
{
    private readonly Mock<ILogger<GoldenDatasetLoader>> _mockLogger;

    public GoldenDatasetLoaderTests()
    {
        _mockLogger = new Mock<ILogger<GoldenDatasetLoader>>();
    }

    [Fact]
    public async Task LoadAllAsync_WithValidDataset_ReturnsTestCases()
    {
        // Arrange
        var logMessages = new List<string>();
        Exception? capturedException = null;
        _mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var exception = invocation.Arguments[3] as Exception;
                var formatter = invocation.Arguments[4] as Delegate;
                var message = formatter?.DynamicInvoke(invocation.Arguments[2], invocation.Arguments[3])?.ToString() ?? "";
                logMessages.Add($"[{logLevel}] {message}");
                if (exception != null)
                {
                    capturedException = exception;
                    logMessages.Add($"  Exception: {exception.GetType().Name}: {exception.Message}");
                }
            }));

        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);

        // Debug: If test fails, this will show the logs
        var debugInfo = $"Expected at least 3 test cases, got {testCases.Count}.\n" +
                       $"Logs:\n{string.Join("\n", logMessages)}";

        Assert.True(testCases.Count >= 3, debugInfo);
    }

    [Fact]
    public async Task LoadAllAsync_CachesResults_OnSecondCall()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var firstCall = await loader.LoadAllAsync(TestContext.Current.CancellationToken);
        var secondCall = await loader.LoadAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(firstCall, secondCall); // Should return same instance (cached)
        Assert.Equal(firstCall.Count, secondCall.Count);
    }

    [Fact]
    public async Task LoadByGameAsync_WithValidGameId_ReturnsFilteredCases()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadByGameAsync("terraforming-mars", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        Assert.NotEmpty(testCases);
        Assert.All(testCases, tc => Assert.Equal("terraforming-mars", tc.GameId));
    }

    [Fact]
    public async Task LoadByGameAsync_WithNonExistentGame_ReturnsEmpty()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadByGameAsync("non-existent-game", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        Assert.Empty(testCases);
    }

    [Fact]
    public async Task LoadByGameAsync_WithEmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => loader.LoadByGameAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadByDifficultyAsync_WithValidDifficulty_ReturnsFilteredCases()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadByDifficultyAsync("easy", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        Assert.NotEmpty(testCases);
        Assert.All(testCases, tc => Assert.Equal("easy", tc.Difficulty));
    }

    [Fact]
    public async Task LoadByDifficultyAsync_WithMediumDifficulty_ReturnsFilteredCases()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadByDifficultyAsync("medium", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        // May be empty if no medium cases in current dataset
        Assert.All(testCases, tc => Assert.Equal("medium", tc.Difficulty));
    }

    [Fact]
    public async Task LoadByDifficultyAsync_WithEmptyDifficulty_ThrowsArgumentException()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => loader.LoadByDifficultyAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadByCategoryAsync_WithValidCategory_ReturnsFilteredCases()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act
        var testCases = await loader.LoadByCategoryAsync("gameplay", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        // May be empty if no gameplay cases in current dataset
        Assert.All(testCases, tc => Assert.Equal("gameplay", tc.Category));
    }

    [Fact]
    public async Task LoadByCategoryAsync_WithEmptyCategory_ThrowsArgumentException()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => loader.LoadByCategoryAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SampleAsync_WithCountLargerThanTotal_ReturnsAll()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root
        var allCases = await loader.LoadAllAsync(TestContext.Current.CancellationToken);

        // Act
        var sampled = await loader.SampleAsync(allCases.Count + 100, stratified: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(allCases.Count, sampled.Count);
    }

    [Fact]
    public async Task SampleAsync_WithStratifiedTrue_ReturnsSampledCases()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root
        var sampleSize = 2;

        // Act
        var sampled = await loader.SampleAsync(sampleSize, stratified: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sampled);
        Assert.True(sampled.Count <= sampleSize); // May be less if not enough cases
    }

    [Fact]
    public async Task SampleAsync_WithStratifiedFalse_ReturnsRandomSample()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root
        var sampleSize = 2;

        // Act
        var sampled = await loader.SampleAsync(sampleSize, stratified: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sampled);
        Assert.True(sampled.Count <= sampleSize);
    }

    [Fact]
    public async Task SampleAsync_WithZeroCount_ThrowsArgumentException()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => loader.SampleAsync(0, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SampleAsync_WithNegativeCount_ThrowsArgumentException()
    {
        // Arrange
        var loader = new GoldenDatasetLoader(_mockLogger.Object); // Use auto-detected repo root

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => loader.SampleAsync(-1, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAllAsync_WithNonExistentFile_ReturnsEmpty()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_dataset.json");
        var loader = new GoldenDatasetLoader(_mockLogger.Object, nonExistentPath);

        // Act
        var testCases = await loader.LoadAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(testCases);
        Assert.Empty(testCases);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GoldenDatasetLoader(null!));
    }
}
