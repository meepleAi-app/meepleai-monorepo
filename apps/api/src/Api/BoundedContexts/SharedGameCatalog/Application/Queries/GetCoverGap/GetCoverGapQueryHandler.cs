using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

/// <summary>
/// Handler di <see cref="GetCoverGapQuery"/>. Parte dai giochi con tutte e quattro le chiavi cover
/// nulle, porta i PDF collegati e deriva una causa per gioco.
/// </summary>
internal sealed class GetCoverGapQueryHandler
    : IQueryHandler<GetCoverGapQuery, PagedResult<CoverGapGameDto>>
{
    /// <summary>
    /// Soglia oltre la quale un fallimento di estrazione si spiega con la dimensione. Allineata al
    /// limite storico del servizio Unstructured (50MB): i fallimenti precedenti al fix #3589 hanno
    /// <c>ErrorCategory = "Service"</c> con un messaggio fuorviante ("Failed to connect to
    /// Unstructured service"), quindi la sola categoria non basta a riconoscerli.
    /// </summary>
    private const long LargePdfThresholdBytes = 52_428_800;

    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetCoverGapQueryHandler> _logger;

    public GetCoverGapQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetCoverGapQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<CoverGapGameDto>> Handle(
        GetCoverGapQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Il filtro soft-delete è GLOBALE (SharedGameEntityConfiguration.HasQueryFilter):
        // riaggiungerlo qui sarebbe ridondante.
        var gapGames = _context.SharedGames
            .AsNoTracking()
            .Where(g => string.IsNullOrWhiteSpace(g.PdfCoverR2Key)
                     && string.IsNullOrWhiteSpace(g.BggCoverR2Key)
                     && string.IsNullOrWhiteSpace(g.WikidataCoverR2Key)
                     && string.IsNullOrWhiteSpace(g.ManualCoverR2Key));

        // I PDF si collegano via la tabella ponte shared_game_documents: è la relazione canonica.
        // PdfDocumentEntity.SharedGameId è nullable e popolata solo su alcuni percorsi, quindi non
        // è affidabile come join primario.
        var rows = await (
            from g in gapGames
            join sgd in _context.SharedGameDocuments on g.Id equals sgd.SharedGameId into links
            from sgd in links.DefaultIfEmpty()
            join p in _context.PdfDocuments on sgd.PdfDocumentId equals p.Id into pdfs
            from p in pdfs.DefaultIfEmpty()
            select new
            {
                g.Id,
                g.Title,
                g.BggId,
                PdfFileName = p != null ? p.FileName : null,
                PdfSize = p != null ? (long?)p.FileSizeBytes : null,
                CoverStatus = p != null ? p.CoverGenerationStatus : null,
                ErrorCategory = p != null ? p.ErrorCategory : null,
                ProcessingState = p != null ? p.ProcessingState : null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // La classificazione gira in memoria dopo la proiezione: i rami non si traducono bene in
        // SQL e l'insieme è di decine di righe (160 giochi in staging, 24 senza cover).
        var classified = rows
            .GroupBy(r => new { r.Id, r.Title, r.BggId })
            .Select(grp =>
            {
                // Un gioco può avere più PDF: tieni la riga con la causa più azionabile.
                var best = grp
                    .OrderByDescending(r => Rank(
                        Classify(r.CoverStatus, r.ErrorCategory, r.ProcessingState, r.PdfSize)))
                    .First();

                var cause = Classify(best.CoverStatus, best.ErrorCategory, best.ProcessingState, best.PdfSize);

                return new CoverGapGameDto(
                    grp.Key.Id,
                    grp.Key.Title,
                    grp.Key.BggId,
                    cause,
                    best.PdfFileName,
                    best.PdfSize,
                    best.ErrorCategory);
            })
            .Where(d => request.Cause is null || string.Equals(d.Cause, request.Cause, StringComparison.Ordinal))
            .OrderBy(d => d.Cause, StringComparer.Ordinal)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = classified.Count;
        var items = classified
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        _logger.LogDebug(
            "GetCoverGap: {Total} giochi senza cover (filtro causa: {Cause}).",
            total, request.Cause ?? "(nessuno)");

        return new PagedResult<CoverGapGameDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <summary>Deriva la causa dallo stato del PDF collegato (null su tutto = nessun PDF).</summary>
    private static string Classify(
        string? coverStatus,
        string? errorCategory,
        string? processingState,
        long? sizeBytes)
    {
        if (coverStatus is null && processingState is null)
        {
            return CoverGapCauses.NoSource;
        }

        if (string.Equals(errorCategory, "PayloadTooLarge", StringComparison.Ordinal)
            || (string.Equals(processingState, "Failed", StringComparison.Ordinal)
                && sizeBytes > LargePdfThresholdBytes))
        {
            return CoverGapCauses.PdfTooLarge;
        }

        // 'Skipped' è scritto direttamente sul campo da BackfillPdfCoversJob, non via
        // PdfDocument.MarkCoverSkipped() (metodo morto).
        if (string.Equals(coverStatus, "Skipped", StringComparison.Ordinal))
        {
            return CoverGapCauses.HeuristicRejected;
        }

        return CoverGapCauses.Other;
    }

    /// <summary>Precedenza quando un gioco ha più PDF: la causa più azionabile vince.</summary>
    private static int Rank(string cause) => cause switch
    {
        CoverGapCauses.PdfTooLarge => 3,
        CoverGapCauses.HeuristicRejected => 2,
        CoverGapCauses.Other => 1,
        _ => 0,
    };
}
