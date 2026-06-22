using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds <see cref="GameBook"/> aggregates for demo/example games used by the
/// multi-book gamebook generalization (2026-05-19). Three game profiles cover
/// the FM-1..FM-23 invariants:
///
///   1. Nanolith       — 4 community books (Press Start, Rules, Storybook, Encounter)
///                       validates: N&gt;1 narrative + mixed physical/KB books
///   2. Fighting Fantasy — 1 single multi-role book (Tutorial+Rules+Narrative+Encounter)
///                       validates: all-in-one mono-book (Case A from spec §4.4)
///   3. Maracaibo      — 2 books (Rulebook with fused tutorial/rules + Story Book physical)
///                       validates: 2-book split with mixed KB/physical (Case B)
///
/// Orchestrator integration is deferred. Until wired into <c>CatalogSeedLayer</c>,
/// this seeder is invoked only from admin endpoints / E2E test fixtures that already
/// know the shared game id + PDF document id mappings. Wiring requires a SharedGameId
/// + PdfDocumentId lookup pass that is non-trivial (FF is not in any manifest, Maracaibo
/// PDF id is generated at runtime by <c>PdfSeeder</c>). See the multi-config E2E spec
/// for the calling convention.
/// </summary>
internal class GameBookSeeder
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GameBookSeeder> _logger;

    public GameBookSeeder(IGameBookRepository repo, IUnitOfWork uow, ILogger<GameBookSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(logger);
        _repo = repo;
        _uow = uow;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the four canonical Nanolith books (Press Start, Rules, Storybook, Encounter).
    /// </summary>
    /// <param name="nanolithSharedGameId">SharedGame id of the Nanolith catalog entry.</param>
    /// <param name="pressStartPdfId">PdfDocument id of the "Press Start" tutorial KB doc.</param>
    /// <param name="rulesPdfId">PdfDocument id of the "Rules" reference KB doc.</param>
    /// <param name="adminUserId">System/admin user used as <c>createdBy</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedNanolithAsync(
        Guid nanolithSharedGameId,
        Guid pressStartPdfId,
        Guid rulesPdfId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var nanolithRef = GameRef.Shared(nanolithSharedGameId);
        var books = new[]
        {
            GameBook.CreateCommunity(nanolithRef, "Press Start",
                GameBookRole.Tutorial | GameBookRole.Setup, ParagraphScheme.None, "en",
                sequentialRead: false, kbSourceDocId: pressStartPdfId,
                physicalOnly: false, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Rules",
                GameBookRole.RulesReference, ParagraphScheme.None, "en",
                sequentialRead: false, kbSourceDocId: rulesPdfId,
                physicalOnly: false, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Storybook",
                GameBookRole.Narrative, ParagraphScheme.ParagraphNumber, "en",
                sequentialRead: true, kbSourceDocId: null,
                physicalOnly: true, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Encounter Book",
                GameBookRole.Encounter, ParagraphScheme.Section, "en",
                sequentialRead: false, kbSourceDocId: null,
                physicalOnly: true, createdBy: adminUserId),
        };

        foreach (var book in books)
        {
            await _repo.AddAsync(book, cancellationToken).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} Nanolith GameBooks", books.Length);
    }

    /// <summary>
    /// Seeds a single Fighting Fantasy gamebook ("City of Thieves") with all four
    /// roles fused into one book (Case A from spec §4.4 — validates the "no UI book
    /// picker for N=1 narrative" code path).
    /// </summary>
    public async Task SeedFightingFantasyAsync(
        Guid ffSharedGameId,
        Guid pdfId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var ffRef = GameRef.Shared(ffSharedGameId);
        var book = GameBook.CreateCommunity(
            ffRef,
            "City of Thieves",
            GameBookRole.Tutorial | GameBookRole.RulesReference
                | GameBookRole.Narrative | GameBookRole.Encounter,
            ParagraphScheme.ParagraphNumber,
            "en",
            sequentialRead: false,
            kbSourceDocId: pdfId,
            physicalOnly: false,
            createdBy: adminUserId);

        await _repo.AddAsync(book, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded Fighting Fantasy GameBook");
    }

    /// <summary>
    /// Seeds two Maracaibo books: a Rulebook with Tutorial+RulesReference roles
    /// (KB-backed) and a physical Story Book with Narrative role only.
    /// Case B from spec §4.4 — validates Setup queries routed via Tutorial role
    /// on a fused tutorial/rules book + N=1 narrative path.
    /// </summary>
    public async Task SeedMaracaiboAsync(
        Guid maracaiboSharedGameId,
        Guid rulebookPdfId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var maracaiboRef = GameRef.Shared(maracaiboSharedGameId);
        var books = new[]
        {
            GameBook.CreateCommunity(maracaiboRef, "Rulebook",
                GameBookRole.Tutorial | GameBookRole.RulesReference,
                ParagraphScheme.None, "en",
                sequentialRead: false, kbSourceDocId: rulebookPdfId,
                physicalOnly: false, createdBy: adminUserId),

            GameBook.CreateCommunity(maracaiboRef, "Story Book",
                GameBookRole.Narrative,
                ParagraphScheme.ParagraphNumber, "en",
                sequentialRead: true, kbSourceDocId: null,
                physicalOnly: true, createdBy: adminUserId),
        };

        foreach (var book in books)
        {
            await _repo.AddAsync(book, cancellationToken).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} Maracaibo GameBooks", books.Length);
    }

    /// <summary>
    /// Issue #1389: orchestration wrapper invoked by <c>CatalogSeedLayer</c> after
    /// <c>CatalogSeeder</c> and <c>PdfSeeder</c> have run. Resolves the Nanolith
    /// <c>SharedGameId</c> by title, locates the Press Start + Rules PDF documents
    /// uploaded by <c>PdfSeeder</c>, then delegates to <see cref="SeedNanolithAsync"/>.
    /// Skips gracefully (log + return) when any prerequisite is missing — the
    /// orchestration is best-effort and must never fail the whole seed pipeline.
    /// Idempotent: returns immediately if Nanolith GameBooks are already present.
    /// </summary>
    /// <param name="db">Database context for the SharedGame + PdfDocument lookups.</param>
    /// <param name="adminUserId">System/admin user used as <c>createdBy</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedNanolithIfReadyAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        // 1. Resolve Nanolith SharedGame by title (catalog manifest has bggId=null so
        //    we cannot use the BGG-id index).
        var nanolith = await db.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted && g.Title == "Nanolith")
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (nanolith is null)
        {
            _logger.LogInformation(
                "[GameBookSeeder] Nanolith not present in catalog — skipping (run CatalogSeeder first to populate it).");
            return;
        }

        // 2. Idempotency guard — skip if Nanolith GameBooks already exist.
        // SeedNanolithAsync uses a single SaveChangesAsync (atomic insert of 4 books),
        // so a count >0 means a full prior seed (no partial-state edge case to recover).
        // If SeedNanolithAsync is ever refactored to per-book saves, change this guard
        // to `existing.Count == 4` and re-seed missing entries instead of full-skipping.
        var existing = await _repo
            .ListByGameRefAsync(GameRef.Shared(nanolith.Id), ownerUserId: null, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _logger.LogDebug(
                "[GameBookSeeder] Nanolith already has {Count} GameBook(s) — skipping idempotently.",
                existing.Count);
            return;
        }

        // 3. Resolve the two KB-backed PDFs (Press Start tutorial + Rules reference).
        //    Nanolith has bggId=null so PdfSeeder may not have processed any PDFs for it;
        //    we degrade gracefully if either is missing (the seed pipeline must not block
        //    on optional dogfood assets).
        var pdfs = await db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.SharedGameId == nanolith.Id)
            .Select(p => new { p.Id, p.FileName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pressStart = pdfs.FirstOrDefault(p =>
            p.FileName?.Contains("press", StringComparison.OrdinalIgnoreCase) == true
            || p.FileName?.Contains("start", StringComparison.OrdinalIgnoreCase) == true);

        var rules = pdfs.FirstOrDefault(p =>
            p.FileName?.Contains("rule", StringComparison.OrdinalIgnoreCase) == true);

        if (pressStart is null || rules is null)
        {
            // Warning level: this is an "AC-scoped seed prerequisite missing" signal
            // (Nanolith is in catalog but its PDFs were not uploaded by PdfSeeder).
            // Easy to miss in non-dev logs at Information level, so escalate.
            // FileName substring match (`press`, `start`, `rule`) is brittle to renames —
            // future PDF named `start-here-guide.pdf` would steal a slot. Tracked for a
            // manifest-driven lookup follow-up.
            _logger.LogWarning(
                "[GameBookSeeder] Nanolith PDFs not ready (pressStart={HasPressStart}, rules={HasRules}) — skipping; re-run after PDF ingestion completes.",
                pressStart is not null, rules is not null);
            return;
        }

        await SeedNanolithAsync(nanolith.Id, pressStart.Id, rules.Id, adminUserId, cancellationToken)
            .ConfigureAwait(false);
    }
}
