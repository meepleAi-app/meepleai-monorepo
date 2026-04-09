using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles <see cref="GetKbReadinessQuery"/>.
///
/// Resolution flow:
/// <list type="number">
///   <item>Resolve the legacy <c>GameEntity.Id</c> — accept either <c>GameEntity.Id</c> or <c>SharedGameEntity.Id</c>.
///         (The seeder uses the same Guid for both; in production they may differ.)</item>
///   <item>Load all <c>PdfDocumentEntity</c> rows for that legacy game.</item>
///   <item>Count PDFs whose string <c>ProcessingState</c> is <c>"Ready"</c> / <c>"Failed"</c>.</item>
///   <item>Probe <c>VectorDocumentEntity</c> for any completed index on one of the Ready PDFs
///         (1 row per PDF, UNIQUE on <c>PdfDocumentId</c>).</item>
///   <item>Classify the aggregate state and emit warnings for partially-failed sets.</item>
/// </list>
/// </summary>
internal sealed class GetKbReadinessQueryHandler : IQueryHandler<GetKbReadinessQuery, KbReadinessDto>
{
    // PdfDocumentEntity.ProcessingState is stored as string (see Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs).
    // The 7 values in ascending pipeline order (+ terminal Failed) are hard-coded here to avoid
    // cross-BC coupling to an enum that does not exist.
    private const string StateReady = "Ready";
    private const string StateFailed = "Failed";

    private static readonly string[] OrderedIntermediateStates =
    {
        "Pending",
        "Uploading",
        "Extracting",
        "Chunking",
        "Embedding",
        "Indexing",
    };

    private readonly MeepleAiDbContext _db;

    public GetKbReadinessQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<KbReadinessDto> Handle(GetKbReadinessQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: resolve the legacy GameEntity.Id.
        // Accept both shapes: caller may pass the SharedGame id or the legacy game id directly.
        var legacyGameId = await _db.Games
            .AsNoTracking()
            .Where(g => g.Id == request.GameId || g.SharedGameId == request.GameId)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (legacyGameId is null)
        {
            return new KbReadinessDto(false, "None", 0, 0, Array.Empty<string>());
        }

        // Step 2: load all PDFs for that legacy game.
        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == legacyGameId.Value)
            .Select(p => new { p.Id, p.ProcessingState })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfs.Count == 0)
        {
            return new KbReadinessDto(false, "None", 0, 0, Array.Empty<string>());
        }

        // Step 3: bucket PDFs by state.
        var readyPdfIds = pdfs
            .Where(p => string.Equals(p.ProcessingState, StateReady, StringComparison.Ordinal))
            .Select(p => p.Id)
            .ToList();

        var failedCount = pdfs.Count(p =>
            string.Equals(p.ProcessingState, StateFailed, StringComparison.Ordinal));

        // Step 4: check for a completed vector index on any Ready PDF.
        // VectorDocumentEntity is UNIQUE on PdfDocumentId (1 row per PDF), so .Any() is correct.
        var hasReadyVector = readyPdfIds.Count > 0
            && await _db.VectorDocuments
                .AsNoTracking()
                .AnyAsync(
                    v => readyPdfIds.Contains(v.PdfDocumentId) && v.IndexingStatus == "completed",
                    cancellationToken)
                .ConfigureAwait(false);

        // Step 5: classify state + warnings.
        var warnings = new List<string>();
        string state;

        if (hasReadyVector && failedCount > 0)
        {
            state = "PartiallyReady";
            warnings.Add(
                $"{failedCount} PDF document(s) failed to index — agent answers may be incomplete.");
        }
        else if (hasReadyVector)
        {
            state = "Ready";
        }
        else if (failedCount == pdfs.Count)
        {
            state = "Failed";
        }
        else if (readyPdfIds.Count > 0)
        {
            // All Ready PDFs present but no completed vector index yet.
            state = "VectorPending";
        }
        else
        {
            // No Ready PDFs, not all Failed — pick the most advanced intermediate state found.
            var distinctStates = pdfs
                .Select(p => p.ProcessingState ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var mostAdvanced = OrderedIntermediateStates
                .Reverse()
                .FirstOrDefault(s => distinctStates.Contains(s, StringComparer.Ordinal));

            state = mostAdvanced ?? distinctStates.FirstOrDefault() ?? "None";
        }

        return new KbReadinessDto(hasReadyVector, state, readyPdfIds.Count, failedCount, warnings);
    }
}
