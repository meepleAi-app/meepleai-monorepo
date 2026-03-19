using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Queries;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;

/// <summary>
/// Tests for LoadDatasetCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LoadDatasetCommandHandlerTests
{
    private readonly Mock<ILogger<LoadDatasetCommandHandler>> _mockLogger;
    private readonly LoadDatasetCommandHandler _handler;
    private readonly string _testDatasetPath;

    public LoadDatasetCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<LoadDatasetCommandHandler>>();
        _handler = new LoadDatasetCommandHandler(_mockLogger.Object);
        _testDatasetPath = Path.Combine(Path.GetTempPath(), $"test_dataset_{Guid.NewGuid()}.json");
    }

    [Fact]
    public async Task Handle_WithValidDataset_LoadsSuccessfully()
    {
        // Arrange
        var datasetJson = @"{
            ""name"": ""test-dataset"",
            ""version"": ""1.0"",
            ""samples"": [
                {
                    ""id"": ""sample-1"",
                    ""question"": ""How many players?"",
                    ""expected_answer"": ""2-4 players"",
                    ""game_id"": """ + Guid.NewGuid() + @"""
                }
            ]
        }";

        await File.WriteAllTextAsync(_testDatasetPath, datasetJson);

        var command = new LoadDatasetCommand { FilePath = _testDatasetPath };

        try
        {
            // Act
            var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-dataset", result.Name);
            Assert.Equal("1.0", result.Version);
            Assert.Equal(1, result.Count);
        }
        finally
        {
            if (File.Exists(_testDatasetPath))
                File.Delete(_testDatasetPath);
        }
    }

    [Fact]
    public async Task Handle_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var command = new LoadDatasetCommand { FilePath = "/path/to/nonexistent.json" };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
