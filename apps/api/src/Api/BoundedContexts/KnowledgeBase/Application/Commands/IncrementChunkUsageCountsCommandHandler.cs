using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles <see cref="IncrementChunkUsageCountsCommand"/> by issuing a single bulk
/// <c>UPDATE</c> through EF Core's <c>ExecuteUpdate</c> against <c>text_chunks</c>.
///
/// Behaviour notes:
///  - Empty / null locator list → no-op, returns 0.
///  - Duplicate locators within the input are deduplicated before the UPDATE so a
///    chunk cited twice in the same assistant message contributes a single
///    increment (DEC-D2 downgraded distinct-message scope).
///  - Unknown (PdfDocumentId, ChunkIndex) tuples (e.g. legacy citations whose
///    underlying chunk was deleted) silently match zero rows — the caller does
///    not need to validate locators ahead of time.
///  - The command is invoked OUTSIDE the chat thread's transaction by design:
///    the counter is a forward-looking telemetry metric, never a correctness
///    invariant, so a transient DB failure here MUST NOT roll back message
///    persistence. The caller catches and logs.
/// </summary>
internal sealed class IncrementChunkUsageCountsCommandHandler
    : ICommandHandler<IncrementChunkUsageCountsCommand, int>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<IncrementChunkUsageCountsCommandHandler> _logger;

    public IncrementChunkUsageCountsCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<IncrementChunkUsageCountsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(
        IncrementChunkUsageCountsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Locators is null || command.Locators.Count == 0)
        {
            return 0;
        }

        // Deduplicate locators (DEC-D2 distinct-message scope).
        var distinctLocators = command.Locators
            .Distinct()
            .ToList();

        var pdfDocumentIds = distinctLocators
            .Select(l => l.PdfDocumentId)
            .Distinct()
            .ToList();
        var chunkIndices = distinctLocators
            .Select(l => l.ChunkIndex)
            .Distinct()
            .ToList();

        // Two-axis prefilter pulls the candidate window, then narrow client-side. The
        // composite (PdfDocumentId, ChunkIndex) IN cannot be expressed directly in EF —
        // a server-side AND/OR chain across N pairs blows up for large N. The candidate
        // window is bounded by the citation budget per chat turn (~10-20 chunks), so the
        // tracked-update overhead is negligible. We avoid ExecuteUpdate because the
        // EF InMemory provider used by unit tests does not support it; production
        // performance is dominated by the SaveChanges single-roundtrip, not the
        // per-row materialization.
        var locatorSet = distinctLocators.ToHashSet();

        var candidates = await _dbContext.TextChunks
            .Where(c => pdfDocumentIds.Contains(c.PdfDocumentId) && chunkIndices.Contains(c.ChunkIndex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var matched = candidates
            .Where(c => locatorSet.Contains(new ChunkUsageLocator(c.PdfDocumentId, c.ChunkIndex)))
            .ToList();

        if (matched.Count == 0)
        {
            _logger.LogDebug(
                "IncrementChunkUsageCountsCommand: no matching chunks for {LocatorCount} locator(s)",
                distinctLocators.Count);
            return 0;
        }

        foreach (var chunk in matched)
        {
            chunk.UsageCount += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "IncrementChunkUsageCountsCommand: incremented {Updated}/{Requested} chunks",
            matched.Count, distinctLocators.Count);

        return matched.Count;
    }
}
