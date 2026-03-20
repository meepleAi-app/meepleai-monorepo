using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for PdfDocument state machine transitions (Issue #4208).
/// Tests all valid and invalid state transitions to ensure state machine correctness.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4208")]
public class PdfDocumentStateTransitionTests
{
    // ===== Valid Transitions Tests =====

    [Fact]
    public void TransitionTo_Pending_To_Uploading_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ProcessingState.Should().Be(PdfProcessingState.Pending);

        // Act
        document.TransitionTo(PdfProcessingState.Uploading);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Uploading);

    }

    [Fact]
    public void TransitionTo_Pending_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);

    }

    [Fact]
    public void TransitionTo_Uploading_To_Extracting_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);

        // Act
        document.TransitionTo(PdfProcessingState.Extracting);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Extracting);

    }

    [Fact]
    public void TransitionTo_Uploading_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
    }

    [Fact]
    public void TransitionTo_Extracting_To_Chunking_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);

        // Act
        document.TransitionTo(PdfProcessingState.Chunking);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Chunking);
    }

    [Fact]
    public void TransitionTo_Extracting_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
    }

    [Fact]
    public void TransitionTo_Chunking_To_Embedding_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Chunking);

        // Act
        document.TransitionTo(PdfProcessingState.Embedding);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Embedding);
    }

    [Fact]
    public void TransitionTo_Chunking_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Chunking);

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
    }

    [Fact]
    public void TransitionTo_Embedding_To_Indexing_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Embedding);

        // Act
        document.TransitionTo(PdfProcessingState.Indexing);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Indexing);
    }

    [Fact]
    public void TransitionTo_Embedding_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Embedding);

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
    }

    [Fact]
    public void TransitionTo_Indexing_To_Ready_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Indexing);

        // Act
        document.TransitionTo(PdfProcessingState.Ready);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Ready);

    }

    [Fact]
    public void TransitionTo_Indexing_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Indexing);

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
    }

    [Fact]
    public void TransitionTo_Failed_To_Extracting_Succeeds_DuringRetry()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);
        document.MarkAsFailed("Network error", ErrorCategory.Network, PdfProcessingState.Extracting);

        // Act - Retry should allow transition from Failed
        document.Retry();

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Extracting);
    }

    // ===== Invalid Transitions Tests =====

    [Fact]
    public void TransitionTo_Pending_To_Extracting_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Extracting));

        exception.Message.Should().Contain("Invalid state transition: Pending → Extracting");
    }

    [Fact]
    public void TransitionTo_Pending_To_Chunking_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Chunking));

        exception.Message.Should().Contain("Invalid state transition: Pending → Chunking");
    }

    [Fact]
    public void TransitionTo_Pending_To_Embedding_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Embedding));

        exception.Message.Should().Contain("Invalid state transition: Pending → Embedding");
    }

    [Fact]
    public void TransitionTo_Pending_To_Indexing_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Indexing));

        exception.Message.Should().Contain("Invalid state transition: Pending → Indexing");
    }

    [Fact]
    public void TransitionTo_Pending_To_Ready_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Ready));

        exception.Message.Should().Contain("Invalid state transition: Pending → Ready");
    }

    [Fact]
    public void TransitionTo_Ready_To_AnyState_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Ready);

        // Act & Assert - Ready is terminal, cannot transition
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Uploading));

        exception.Message.Should().Contain("Cannot transition from Ready state");
    }

    [Fact]
    public void TransitionTo_Uploading_To_Chunking_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);

        // Act & Assert - Must go through Extracting first
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Chunking));

        exception.Message.Should().Contain("Invalid state transition: Uploading → Chunking");
    }

    [Fact]
    public void TransitionTo_Extracting_To_Embedding_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);

        // Act & Assert - Must go through Chunking first
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Embedding));

        exception.Message.Should().Contain("Invalid state transition: Extracting → Embedding");
    }

    [Fact]
    public void TransitionTo_Extracting_To_Ready_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);

        // Act & Assert - Cannot skip to Ready
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Ready));

        exception.Message.Should().Contain("Invalid state transition: Extracting → Ready");
    }

    [Fact]
    public void TransitionTo_Chunking_To_Indexing_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Chunking);

        // Act & Assert - Must go through Embedding first
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Indexing));

        exception.Message.Should().Contain("Invalid state transition: Chunking → Indexing");
    }

    [Fact]
    public void TransitionTo_Embedding_To_Ready_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        AdvanceToState(document, PdfProcessingState.Embedding);

        // Act & Assert - Must go through Indexing first
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Ready));

        exception.Message.Should().Contain("Invalid state transition: Embedding → Ready");
    }

    [Fact]
    public void TransitionTo_NoBackwardsTransitions_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);
        document.TransitionTo(PdfProcessingState.Chunking);

        // Act & Assert - Cannot go backwards
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Extracting));

        exception.Message.Should().Contain("Invalid state transition: Chunking → Extracting");
    }

    [Fact]
    public void TransitionTo_FullPipeline_AllValidTransitions_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act - Complete pipeline
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);
        document.TransitionTo(PdfProcessingState.Chunking);
        document.TransitionTo(PdfProcessingState.Embedding);
        document.TransitionTo(PdfProcessingState.Indexing);
        document.TransitionTo(PdfProcessingState.Ready);

        // Assert
        document.ProcessingState.Should().Be(PdfProcessingState.Ready);

    }

    [Fact]
    public void TransitionTo_FailureAtAnyStage_Succeeds()
    {
        // Arrange & Act & Assert - Can fail from any state
        var states = new[]
        {
            PdfProcessingState.Pending,
            PdfProcessingState.Uploading,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            PdfProcessingState.Embedding,
            PdfProcessingState.Indexing
        };

        foreach (var state in states)
        {
            var document = CreateTestDocument();
            AdvanceToState(document, state);

            // Should be able to fail from this state
            document.TransitionTo(PdfProcessingState.Failed);
            document.ProcessingState.Should().Be(PdfProcessingState.Failed);
        }
    }

    // ===== Helper Methods =====

    private static PdfDocument CreateTestDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid(),
            LanguageCode.English
        );
    }

    private static void AdvanceToState(PdfDocument document, PdfProcessingState targetState)
    {
        var stateSequence = new[]
        {
            PdfProcessingState.Pending,
            PdfProcessingState.Uploading,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            PdfProcessingState.Embedding,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready
        };

        var targetIndex = Array.IndexOf(stateSequence, targetState);
        if (targetIndex == -1)
            throw new ArgumentException($"Invalid target state: {targetState}");

        for (int i = 0; i < targetIndex; i++)
        {
            var nextState = stateSequence[i + 1];
            document.TransitionTo(nextState);
        }
    }
}
