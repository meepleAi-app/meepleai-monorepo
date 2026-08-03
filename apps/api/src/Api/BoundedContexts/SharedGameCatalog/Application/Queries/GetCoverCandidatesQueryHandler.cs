using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Epic #3470 (Slice 1c-3) — builds the admin cover-picker read shape. For each cover
/// source with a MATERIALIZED R2 key it mints a presigned preview URL (omitting the
/// source when its blob cannot be resolved, e.g. in local/dev where R2 is absent), and
/// reports which source is currently pinned per UI context. PDF-page enumeration and
/// on-demand Wikidata materialization are out of scope here (Slice 2).
/// </summary>
internal sealed class GetCoverCandidatesQueryHandler
    : IRequestHandler<GetCoverCandidatesQuery, CoverCandidatesDto?>
{
    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public GetCoverCandidatesQueryHandler(MeepleAiDbContext context, IBlobStorageService blobStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<CoverCandidatesDto?> Handle(GetCoverCandidatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var game = await _context.SharedGames
            .AsNoTracking()
            .Include(g => g.CoverAssignments)
            .FirstOrDefaultAsync(g => g.Id == query.GameId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            return null;
        }

        // Resolve every materialized source's preview URL in parallel. Each call
        // is an independent network round-trip (GetPresignedUrlForRawKeyAsync issues
        // an S3 HEAD before signing), the blob client is thread-safe, and none of
        // these touch the DbContext — so WhenAll cuts the worst-case latency from
        // sum-of-four to max-of-four. Task order is preserved, keeping the fixed
        // Pdf → Bgg → Wikidata → Manual candidate ordering.
        var resolved = await Task.WhenAll(
            ResolveCandidateAsync(CoverAssignmentSource.Pdf, game.PdfCoverR2Key, license: null, attribution: null, sourceUrl: null),
            ResolveCandidateAsync(CoverAssignmentSource.Bgg, game.BggCoverR2Key, license: null, attribution: null, sourceUrl: null),
            ResolveCandidateAsync(CoverAssignmentSource.Wikidata, game.WikidataCoverR2Key,
                game.WikidataCoverLicense, game.WikidataCoverAttribution, game.WikidataCoverSourceUrl),
            ResolveCandidateAsync(CoverAssignmentSource.Manual, game.ManualCoverR2Key,
                game.ManualCoverLicense, game.ManualCoverAttribution, game.ManualCoverSourceUrl))
            .ConfigureAwait(false);

        var candidates = resolved.Where(c => c is not null).Select(c => c!).ToList();

        var assignments = new CoverContextAssignmentsDto(
            Card: AssignmentForContext(game, CoverContext.Card),
            Hero: AssignmentForContext(game, CoverContext.Hero),
            Social: AssignmentForContext(game, CoverContext.Social));

        return new CoverCandidatesDto(game.Id, candidates, assignments);
    }

    private async Task<CoverCandidateDto?> ResolveCandidateAsync(
        CoverAssignmentSource source,
        string? dbKey,
        string? license,
        string? attribution,
        string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(dbKey))
        {
            return null;
        }

        var physicalKey = CoverKeyBuilder.PhysicalKeyFor(source.ToCoverKind(), dbKey);
        var previewUrl = await _blobStorage.GetPresignedUrlForRawKeyAsync(physicalKey).ConfigureAwait(false);
        if (previewUrl is null)
        {
            return null;
        }

        return new CoverCandidateDto(source, previewUrl, license, attribution, sourceUrl);
    }

    private static CoverContextAssignmentDto? AssignmentForContext(SharedGameEntity game, CoverContext context)
    {
        var assignment = game.CoverAssignments.FirstOrDefault(a => a.Context == context);
        return assignment is null
            ? null
            : new CoverContextAssignmentDto(assignment.Source, assignment.FocalX, assignment.FocalY);
    }
}
