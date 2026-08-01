using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Unit tests for the labeling-assist commands (#3433 Task 5):
/// AI-proposes-candidates (<see cref="GenerateLabelingCandidatesCommand"/>) and
/// human-verifies-merge (<see cref="MergeLabelsCommand"/>).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LabelingAssistTests
{
    private static string WriteDatasetToTempFile(EvaluationDataset dataset)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, dataset.ToJson());
        return path;
    }

    private static EvaluationDataset BuildSingleSampleDataset(string sampleId, string question)
    {
        var dataset = EvaluationDataset.Create(
            "labeling-assist-tests",
            "Temp dataset for labeling-assist unit tests");

        dataset.AddSample(new EvaluationSample
        {
            Id = sampleId,
            Question = question,
            ExpectedAnswer = "Some expected answer"
        });

        return dataset;
    }

    [Fact]
    public async Task Generate_DumpsTopNCandidatesFromRetrieval()
    {
        // Arrange
        var dataset = BuildSingleSampleDataset("sample-1", "How many players?");
        var datasetPath = WriteDatasetToTempFile(dataset);

        try
        {
            var snippets = new List<Snippet>
            {
                new("Setup text mentioning s1", "s1", 1, 0, 0.9f),
                new("Setup text mentioning s2", "s2", 2, 0, 0.8f)
            };
            var ragResponse = new QaResponse(
                answer: "Generated answer",
                snippets: snippets,
                confidence: 0.9);

            var mockRagService = new Mock<IRagService>();
            mockRagService
                .Setup(r => r.AskAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ragResponse);

            var handler = new GenerateLabelingCandidatesCommandHandler(
                mockRagService.Object,
                Mock.Of<ILogger<GenerateLabelingCandidatesCommandHandler>>());

            var command = new GenerateLabelingCandidatesCommand(datasetPath, TopN: 10);

            // Act
            var review = await handler.Handle(command, CancellationToken.None);

            // Assert
            review.Items.Should().HaveCount(1);
            var item = review.Items[0];
            item.SampleId.Should().Be("sample-1");
            item.Candidates.Should().HaveCount(2);
            item.Candidates[0].ChunkId.Should().Be("s1");
            item.Candidates[0].Relevant.Should().BeNull();
            item.Candidates[1].ChunkId.Should().Be("s2");
            item.Candidates[1].Relevant.Should().BeNull();

            mockRagService.Verify(r => r.AskAsync(
                "",
                "How many players?",
                null,
                false,
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(datasetPath);
        }
    }

    [Fact]
    public async Task Generate_RespectsTopN_TruncatesCandidateList()
    {
        // Arrange
        var dataset = BuildSingleSampleDataset("sample-1", "How many players?");
        var datasetPath = WriteDatasetToTempFile(dataset);

        try
        {
            var snippets = new List<Snippet>
            {
                new("text s1", "s1", 1, 0, 0.9f),
                new("text s2", "s2", 2, 0, 0.8f),
                new("text s3", "s3", 3, 0, 0.7f)
            };
            var ragResponse = new QaResponse(answer: "Generated answer", snippets: snippets, confidence: 0.9);

            var mockRagService = new Mock<IRagService>();
            mockRagService
                .Setup(r => r.AskAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ragResponse);

            var handler = new GenerateLabelingCandidatesCommandHandler(
                mockRagService.Object,
                Mock.Of<ILogger<GenerateLabelingCandidatesCommandHandler>>());

            var command = new GenerateLabelingCandidatesCommand(datasetPath, TopN: 2);

            // Act
            var review = await handler.Handle(command, CancellationToken.None);

            // Assert
            review.Items.Should().HaveCount(1);
            review.Items[0].Candidates.Should().HaveCount(2);
            review.Items[0].Candidates.Select(c => c.ChunkId).Should().Equal("s1", "s2");
        }
        finally
        {
            File.Delete(datasetPath);
        }
    }

    [Fact]
    public async Task Merge_CollectsRelevantTrueIntoRelevantChunkIds()
    {
        // Arrange
        var dataset = BuildSingleSampleDataset("sample-x", "What is the goal?");
        var datasetPath = WriteDatasetToTempFile(dataset);

        try
        {
            var review = new LabelingReview(new List<LabelingReviewItem>
            {
                new(
                    SampleId: "sample-x",
                    Question: "What is the goal?",
                    Candidates: new List<LabelingCandidate>
                    {
                        new(ChunkId: "s1", Page: 1, Score: 0.9f, Snippet: "text s1", Relevant: true),
                        new(ChunkId: "s2", Page: 2, Score: 0.8f, Snippet: "text s2", Relevant: false)
                    })
            });

            var handler = new MergeLabelsCommandHandler(Mock.Of<ILogger<MergeLabelsCommandHandler>>());
            var command = new MergeLabelsCommand(datasetPath, review);

            // Act
            var mergedDataset = await handler.Handle(command, CancellationToken.None);

            // Assert
            var mergedSample = mergedDataset.Samples.Should().ContainSingle().Subject;
            mergedSample.Id.Should().Be("sample-x");
            mergedSample.RelevantChunkIds.Should().BeEquivalentTo(new[] { "s1" });
        }
        finally
        {
            File.Delete(datasetPath);
        }
    }

    [Fact]
    public async Task Merge_SampleWithoutReviewItem_KeepsOriginalRelevantChunkIds()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("labeling-assist-tests", "Temp dataset for labeling-assist unit tests");
        dataset.AddSample(new EvaluationSample
        {
            Id = "sample-unreviewed",
            Question = "Unreviewed question?",
            ExpectedAnswer = "answer",
            RelevantChunkIds = new List<string> { "pre-existing-chunk" }
        });
        var datasetPath = WriteDatasetToTempFile(dataset);

        try
        {
            var review = new LabelingReview(new List<LabelingReviewItem>());
            var handler = new MergeLabelsCommandHandler(Mock.Of<ILogger<MergeLabelsCommandHandler>>());
            var command = new MergeLabelsCommand(datasetPath, review);

            // Act
            var mergedDataset = await handler.Handle(command, CancellationToken.None);

            // Assert
            var mergedSample = mergedDataset.Samples.Should().ContainSingle().Subject;
            mergedSample.RelevantChunkIds.Should().BeEquivalentTo(new[] { "pre-existing-chunk" });
        }
        finally
        {
            File.Delete(datasetPath);
        }
    }
}
