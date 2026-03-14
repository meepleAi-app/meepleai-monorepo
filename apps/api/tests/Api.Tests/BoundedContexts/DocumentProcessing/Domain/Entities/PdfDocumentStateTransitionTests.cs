using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Equal(PdfProcessingState.Pending, document.ProcessingState);

        // Act
        document.TransitionTo(PdfProcessingState.Uploading);

        // Assert
        Assert.Equal(PdfProcessingState.Uploading, document.ProcessingState);

    }

    [Fact]
    public void TransitionTo_Pending_To_Failed_Succeeds()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.TransitionTo(PdfProcessingState.Failed);

        // Assert
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);

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
        Assert.Equal(PdfProcessingState.Extracting, document.ProcessingState);

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
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Chunking, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Embedding, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Indexing, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Ready, document.ProcessingState);

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
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
        Assert.Equal(PdfProcessingState.Extracting, document.ProcessingState);
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

        Assert.Contains("Invalid state transition: Pending → Extracting", exception.Message);
    }

    [Fact]
    public void TransitionTo_Pending_To_Chunking_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Chunking));

        Assert.Contains("Invalid state transition: Pending → Chunking", exception.Message);
    }

    [Fact]
    public void TransitionTo_Pending_To_Embedding_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Embedding));

        Assert.Contains("Invalid state transition: Pending → Embedding", exception.Message);
    }

    [Fact]
    public void TransitionTo_Pending_To_Indexing_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Indexing));

        Assert.Contains("Invalid state transition: Pending → Indexing", exception.Message);
    }

    [Fact]
    public void TransitionTo_Pending_To_Ready_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(PdfProcessingState.Ready));

        Assert.Contains("Invalid state transition: Pending → Ready", exception.Message);
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

        Assert.Contains("Cannot transition from Ready state", exception.Message);
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

        Assert.Contains("Invalid state transition: Uploading → Chunking", exception.Message);
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

        Assert.Contains("Invalid state transition: Extracting → Embedding", exception.Message);
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

        Assert.Contains("Invalid state transition: Extracting → Ready", exception.Message);
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

        Assert.Contains("Invalid state transition: Chunking → Indexing", exception.Message);
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

        Assert.Contains("Invalid state transition: Embedding → Ready", exception.Message);
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

        Assert.Contains("Invalid state transition: Chunking → Extracting", exception.Message);
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
        Assert.Equal(PdfProcessingState.Ready, document.ProcessingState);

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
            Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
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
