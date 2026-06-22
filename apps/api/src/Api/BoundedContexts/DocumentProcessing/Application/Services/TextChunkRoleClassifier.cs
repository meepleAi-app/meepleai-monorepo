using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Phase D4 (multi-book gamebook generalization, 2026-05-19): shared helper that
/// applies <see cref="IRoleClassifierService"/> to a batch of <see cref="TextChunkEntity"/>
/// instances before they are persisted, calling <see cref="TextChunkEntity.AssignRoleTags"/>
/// on each chunk in index order.
///
/// All four DocumentProcessing chunk-creation paths (PdfProcessingPipelineService,
/// IndexPdfCommandHandler, UploadPdfCommandHandler, CompleteChunkedUploadCommandHandler)
/// route their pre-persistence classification through this helper so behaviour and logging
/// stay consistent.
/// </summary>
internal static class TextChunkRoleClassifier
{
    /// <summary>
    /// Assigns <see cref="GameBookRole"/> tags to each chunk by invoking
    /// <paramref name="classifier"/> over the chunk source data (heading + body text)
    /// and calling <see cref="TextChunkEntity.AssignRoleTags(GameBookRole)"/> on the
    /// matching entity at the same index.
    ///
    /// When <paramref name="classifier"/> is null (e.g. legacy unit tests that pre-date
    /// Phase D), the call is a no-op and tags remain at <see cref="GameBookRole.None"/>.
    /// LLM/classifier faults must not block ingestion: exceptions are logged and
    /// swallowed (rule-based path is deterministic; only the LLM fallback inside the
    /// classifier may throw, and that path already degrades to RulesReference per chunk).
    /// </summary>
    public static async Task AssignRoleTagsAsync(
        IRoleClassifierService? classifier,
        IReadOnlyList<TextChunkEntity> textChunks,
        IReadOnlyList<DocumentChunkInput> sources,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(textChunks);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);

        if (classifier is null || textChunks.Count == 0)
        {
            return;
        }

        if (textChunks.Count != sources.Count)
        {
            logger.LogWarning(
                "[RoleClassifier] entity/source count mismatch (entities={EntityCount}, sources={SourceCount}); skipping role classification",
                textChunks.Count, sources.Count);
            return;
        }

        var classifierInputs = new ChunkInput[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            classifierInputs[i] = new ChunkInput(
                HeadingPath: sources[i].Heading ?? string.Empty,
                BodyText: sources[i].Text ?? string.Empty);
        }

        IReadOnlyList<GameBookRole> roles;
        try
        {
            roles = await classifier.ClassifyAsync(classifierInputs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types — classifier faults must NOT block ingestion.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(
                ex,
                "[RoleClassifier] classification failed for {Count} chunks; leaving role_tags at None",
                textChunks.Count);
            return;
        }

        if (roles.Count != textChunks.Count)
        {
            logger.LogWarning(
                "[RoleClassifier] classifier returned {ResultCount} results for {ExpectedCount} chunks; leaving role_tags at None",
                roles.Count, textChunks.Count);
            return;
        }

        for (var i = 0; i < textChunks.Count; i++)
        {
            textChunks[i].AssignRoleTags(roles[i]);
        }
    }

    /// <summary>
    /// Overload for callers whose source list is <see cref="DocumentChunk"/> (carries
    /// embedding too) rather than the input-shaped <see cref="DocumentChunkInput"/>.
    /// Both records expose the same <c>Heading</c> + <c>Text</c> surface used by the
    /// classifier.
    /// </summary>
    public static async Task AssignRoleTagsAsync(
        IRoleClassifierService? classifier,
        IReadOnlyList<TextChunkEntity> textChunks,
        IReadOnlyList<DocumentChunk> sources,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var inputs = new DocumentChunkInput[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            inputs[i] = new DocumentChunkInput
            {
                Text = sources[i].Text,
                Page = sources[i].Page,
                CharStart = sources[i].CharStart,
                CharEnd = sources[i].CharEnd,
                Heading = sources[i].Heading,
                Level = sources[i].Level,
                ParentChunkId = sources[i].ParentChunkId,
                ElementType = sources[i].ElementType,
            };
        }

        await AssignRoleTagsAsync(classifier, textChunks, inputs, logger, cancellationToken)
            .ConfigureAwait(false);
    }
}
