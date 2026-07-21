using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="TextChunkRoleClassifier"/> — the shared helper invoked
/// from all four DocumentProcessing chunk-creation paths to populate
/// <c>TextChunkEntity.RoleTags</c> at ingest (Phase D4 of the multi-book gamebook
/// generalization plan, 2026-05-19).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class TextChunkRoleClassifierTests
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task AssignRoleTagsAsync_NullClassifier_NoOpAndLeavesTagsAtNone()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "anything" },
        };
        var sources = new List<DocumentChunkInput>
        {
            new() { Text = "anything", Heading = "Setup > Players" },
        };

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: null,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        chunks[0].RoleTags.Should().Be(GameBookRole.None,
            "a null classifier means classification was skipped entirely");
    }

    [Fact]
    public async Task AssignRoleTagsAsync_EmptyList_NoOp()
    {
        var classifierMock = new Mock<IRoleClassifierService>(MockBehavior.Strict);

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: Array.Empty<TextChunkEntity>(),
            sources: Array.Empty<DocumentChunkInput>(),
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        classifierMock.Verify(
            x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no-op when there are no chunks to classify");
    }

    [Fact]
    public async Task AssignRoleTagsAsync_HappyPath_AssignsTagsInOrder()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "Place pieces on the board." },
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "The wizard rolls 1d6 for damage." },
        };
        var sources = new List<DocumentChunkInput>
        {
            new() { Text = "Place pieces on the board.", Heading = "Setup > Components" },
            new() { Text = "The wizard rolls 1d6 for damage.", Heading = "Combat > Magic" },
        };

        var returnedRoles = new[]
        {
            GameBookRole.Tutorial | GameBookRole.Setup,
            GameBookRole.RulesReference,
        };

        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(
                It.Is<IReadOnlyList<ChunkInput>>(input =>
                    input.Count == 2
                    && input[0].HeadingPath == "Setup > Components"
                    && input[0].BodyText == "Place pieces on the board."
                    && input[1].HeadingPath == "Combat > Magic"
                    && input[1].BodyText == "The wizard rolls 1d6 for damage."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedRoles);

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        chunks[0].RoleTags.HasFlag(GameBookRole.Tutorial).Should().BeTrue();
        chunks[0].RoleTags.HasFlag(GameBookRole.Setup).Should().BeTrue();
        chunks[0].RoleTags.HasFlag(GameBookRole.RulesReference).Should().BeFalse();
        chunks[1].RoleTags.Should().Be(GameBookRole.RulesReference);
        classifierMock.VerifyAll();
    }

    // ---------------------------------------------------------------------
    // role_tags index-population: ClassifyRolesAsync computes the roles ONCE
    // (from DocumentChunk sources) so the caller can apply them to BOTH
    // text_chunks AND pgvector_embeddings — the vector arm previously got 0
    // because classification ran after pgvector ingestion. ApplyRoles is the
    // sync assignment half.
    // ---------------------------------------------------------------------

    private static DocumentChunk DocChunk(string text, string? heading = null) =>
        new() { Text = text, Heading = heading };

    [Fact]
    public async Task ClassifyRolesAsync_HappyPath_ReturnsRolesInOrder()
    {
        var sources = new List<DocumentChunk>
        {
            DocChunk("Place pieces on the board.", "Setup > Components"),
            DocChunk("The wizard rolls 1d6 for damage.", "Combat > Magic"),
        };
        var returnedRoles = new[] { GameBookRole.Setup, GameBookRole.RulesReference };

        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(
                It.Is<IReadOnlyList<ChunkInput>>(i =>
                    i.Count == 2 && i[0].HeadingPath == "Setup > Components" && i[0].BodyText == "Place pieces on the board."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedRoles);

        var roles = await TextChunkRoleClassifier.ClassifyRolesAsync(
            classifierMock.Object, sources, Logger, TestContext.Current.CancellationToken);

        roles.Should().Equal(GameBookRole.Setup, GameBookRole.RulesReference);
    }

    [Fact]
    public async Task ClassifyRolesAsync_NullClassifier_ReturnsAllNone()
    {
        var sources = new List<DocumentChunk> { DocChunk("a"), DocChunk("b") };

        var roles = await TextChunkRoleClassifier.ClassifyRolesAsync(
            classifier: null, sources, Logger, TestContext.Current.CancellationToken);

        roles.Should().Equal(GameBookRole.None, GameBookRole.None);
    }

    [Fact]
    public async Task ClassifyRolesAsync_ClassifierThrows_ReturnsAllNone_DoesNotBlockIngest()
    {
        var sources = new List<DocumentChunk> { DocChunk("a"), DocChunk("b") };
        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("classifier down"));

        var roles = await TextChunkRoleClassifier.ClassifyRolesAsync(
            classifierMock.Object, sources, Logger, TestContext.Current.CancellationToken);

        roles.Should().Equal(GameBookRole.None, GameBookRole.None);
    }

    [Fact]
    public async Task ClassifyRolesAsync_CountMismatch_ReturnsAllNone()
    {
        var sources = new List<DocumentChunk> { DocChunk("a"), DocChunk("b") };
        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { GameBookRole.Setup }); // 1 role for 2 chunks

        var roles = await TextChunkRoleClassifier.ClassifyRolesAsync(
            classifierMock.Object, sources, Logger, TestContext.Current.CancellationToken);

        roles.Should().Equal(GameBookRole.None, GameBookRole.None);
    }

    [Fact]
    public void ApplyRoles_AssignsInOrder()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "a" },
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "b" },
        };
        var roles = new[] { GameBookRole.Setup, GameBookRole.RulesReference };

        TextChunkRoleClassifier.ApplyRoles(chunks, roles);

        chunks[0].RoleTags.Should().Be(GameBookRole.Setup);
        chunks[1].RoleTags.Should().Be(GameBookRole.RulesReference);
    }

    [Fact]
    public void ApplyRoles_CountMismatch_NoOp()
    {
        var chunks = new[] { new TextChunkEntity { Id = Guid.NewGuid(), Content = "a" } };
        var roles = new[] { GameBookRole.Setup, GameBookRole.RulesReference };

        TextChunkRoleClassifier.ApplyRoles(chunks, roles);

        chunks[0].RoleTags.Should().Be(GameBookRole.None);
    }

    [Fact]
    public async Task AssignRoleTagsAsync_NullSourceFields_PassesEmptyStringToClassifier()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = string.Empty },
        };
        var sources = new List<DocumentChunkInput>
        {
            // Heading is nullable on DocumentChunkInput; Text defaults to "" but defensive
            // null-coalescing in the helper protects the ChunkInput constructor.
            new() { Heading = null, Text = string.Empty },
        };

        ChunkInput? captured = null;
        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChunkInput>, CancellationToken>((input, _) => captured = input[0])
            .ReturnsAsync(new[] { GameBookRole.RulesReference });

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.HeadingPath.Should().Be(string.Empty);
        captured.BodyText.Should().Be(string.Empty);
        chunks[0].RoleTags.Should().Be(GameBookRole.RulesReference);
    }

    [Fact]
    public async Task AssignRoleTagsAsync_ClassifierThrows_SwallowsExceptionAndLeavesTagsAtNone()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "x" },
        };
        var sources = new List<DocumentChunkInput>
        {
            new() { Text = "x", Heading = "Random" },
        };

        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated downstream failure"));

        // Must not throw — classifier faults are non-blocking for ingestion.
        var act = async () => await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        chunks[0].RoleTags.Should().Be(GameBookRole.None,
            "the role_tags column must remain at None when the classifier fails");
    }

    [Fact]
    public async Task AssignRoleTagsAsync_OperationCanceled_PropagatesCancellation()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "x" },
        };
        var sources = new List<DocumentChunkInput>
        {
            new() { Text = "x", Heading = "Random" },
        };

        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AssignRoleTagsAsync_CountMismatch_SkipsAndLeavesTagsAtNone()
    {
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "a" },
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "b" },
        };
        var sources = new List<DocumentChunkInput>
        {
            new() { Text = "a", Heading = "Setup" },
            // Intentionally one fewer source than entities.
        };

        var classifierMock = new Mock<IRoleClassifierService>(MockBehavior.Strict);

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        chunks[0].RoleTags.Should().Be(GameBookRole.None);
        chunks[1].RoleTags.Should().Be(GameBookRole.None);
        classifierMock.Verify(
            x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "must abort before calling the classifier when arrays don't align");
    }

    [Fact]
    public async Task AssignRoleTagsAsync_DocumentChunkOverload_DelegatesToInputOverload()
    {
        // Verifies the secondary overload (DocumentChunk source) — used by IndexPdfCommandHandler
        // whose pipeline carries embeddings alongside text.
        var chunks = new[]
        {
            new TextChunkEntity { Id = Guid.NewGuid(), Content = "Combat rules" },
        };
        var sources = new List<DocumentChunk>
        {
            new() { Text = "Combat rules", Heading = "Combat > Phases", Page = 3 },
        };

        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(
                It.Is<IReadOnlyList<ChunkInput>>(input =>
                    input.Count == 1
                    && input[0].HeadingPath == "Combat > Phases"
                    && input[0].BodyText == "Combat rules"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { GameBookRole.RulesReference });

        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            classifier: classifierMock.Object,
            textChunks: chunks,
            sources: sources,
            logger: Logger,
            cancellationToken: TestContext.Current.CancellationToken);

        chunks[0].RoleTags.Should().Be(GameBookRole.RulesReference);
        classifierMock.VerifyAll();
    }
}
