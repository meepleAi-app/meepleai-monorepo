using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="ResetSmokeAaronCommand"/>. Deletes all transactional
/// data owned by the smoke-aaron test persona so the Bruno smoke collection
/// (Issue #943 / EPIC #906) can run from a deterministic empty state.
///
/// Security (triple-gate, all must hold):
///   1. <c>TestEndpoints:Enabled == true</c> (config flag, default false)
///   2. <c>IWebHostEnvironment.IsProduction() == false</c>
///   3. Target user UUID is hardcoded — no parameter influences which user is deleted
///
/// Any gate failing → <see cref="ForbiddenException"/>.
/// </summary>
internal sealed class ResetSmokeAaronCommandHandler
    : IRequestHandler<ResetSmokeAaronCommand, ResetSmokeAaronResult>
{
    /// <summary>
    /// Fixed UUID of the <c>smoke-aaron@meepleai.test</c> persona seeded by
    /// <c>tests/fixtures/smoke-test-users.sql</c>. The handler ONLY ever deletes
    /// data owned by this UUID — never user-supplied.
    /// </summary>
    public static readonly Guid SmokeAaronUserId =
        Guid.Parse("00000000-0000-4000-8000-000000005a01");

    private readonly MeepleAiDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ResetSmokeAaronCommandHandler> _logger;

    public ResetSmokeAaronCommandHandler(
        MeepleAiDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<ResetSmokeAaronCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResetSmokeAaronResult> Handle(
        ResetSmokeAaronCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnforceTripleGate();

        _logger.LogWarning(
            "Resetting smoke-aaron data (userId={UserId}) — environment={EnvironmentName}",
            SmokeAaronUserId, _environment.EnvironmentName);

        // Delete order respects FK constraints + nullable references:
        //   1. ChatThreadCollection (junction, no inbound refs)
        //   2. ChatThread / ChatSession (parent of messages — EF cascades messages)
        //   3. KbReindexJob (Issue #941 — owned by user)
        //   4. PdfDocument (uploaded by user — independent of PrivateGame)
        //   5. PrivateGame (sets AgentDefinitionId to null per SetNull behavior)
        // AgentDefinitions are intentionally left untouched: they have no UserId
        // field, and PrivateGame deletion does NOT cascade to them. Orphan
        // AgentDefinitions accumulate but do not pollute smoke state because each
        // smoke run creates fresh PrivateGames with fresh auto-created agents.

        // Use Remove + SaveChangesAsync (rather than ExecuteDeleteAsync) so the
        // handler works against both real Postgres and the EF InMemory provider
        // used in unit tests. The smoke-aaron volume is small (a handful of
        // rows per category) — bulk-delete performance isn't material here.

        var threadIds = await _db.ChatThreads
            .Where(t => t.UserId == SmokeAaronUserId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var junctionRows = await _db.ChatThreadCollections
            .Where(j => threadIds.Contains(j.ChatThreadId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ChatThreadCollections.RemoveRange(junctionRows);

        var threads = await _db.ChatThreads
            .Where(t => t.UserId == SmokeAaronUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ChatThreads.RemoveRange(threads);

        var sessions = await _db.ChatSessions
            .Where(s => s.UserId == SmokeAaronUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ChatSessions.RemoveRange(sessions);

        var jobs = await _db.KbReindexJobs
            .Where(j => j.UserId == SmokeAaronUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.KbReindexJobs.RemoveRange(jobs);

        var pdfs = await _db.PdfDocuments
            .Where(p => p.UploadedByUserId == SmokeAaronUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.PdfDocuments.RemoveRange(pdfs);

        var games = await _db.PrivateGames
            .Where(g => g.OwnerId == SmokeAaronUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.PrivateGames.RemoveRange(games);

        // Save directly via DbContext — this is a test-only reset endpoint that
        // intentionally bypasses the UoW pattern. Aligns with the simplicity
        // goal: minimum surface, maximum auditability.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var chatThreads = threads.Count;
        var chatSessions = sessions.Count;
        var kbReindexJobs = jobs.Count;
        var pdfDocuments = pdfs.Count;
        var privateGames = games.Count;

        var result = new ResetSmokeAaronResult(
            PrivateGames: privateGames,
            Agents: 0, // AgentDefinitions deliberately not deleted (orphans tolerated)
            ChatThreads: chatThreads + chatSessions,
            PdfDocuments: pdfDocuments,
            KbReindexJobs: kbReindexJobs);

        _logger.LogWarning(
            "Reset smoke-aaron complete: privateGames={PG}, chatThreads={CT}, pdfDocuments={PDF}, kbReindexJobs={KB}",
            result.PrivateGames, result.ChatThreads, result.PdfDocuments, result.KbReindexJobs);

        return result;
    }

    private void EnforceTripleGate()
    {
        // Gate 1: explicit config opt-in.
        var enabled = _configuration.GetValue<bool>("TestEndpoints:Enabled", false);
        if (!enabled)
        {
            _logger.LogWarning(
                "ResetSmokeAaron rejected: TestEndpoints:Enabled is false (environment={EnvironmentName})",
                _environment.EnvironmentName);
            throw new ForbiddenException("Test endpoints are disabled in this environment");
        }

        // Gate 2: production tripwire — checked INDEPENDENTLY of the config flag.
        // If a misconfigured deploy left Enabled=true in production, this gate
        // still blocks. Both gates are required, neither alone is sufficient.
        if (_environment.IsProduction())
        {
            _logger.LogError(
                "ResetSmokeAaron rejected: IWebHostEnvironment reports Production, even though TestEndpoints:Enabled=true. Misconfiguration detected.");
            throw new ForbiddenException("Reset is not permitted in production environments");
        }

        // Gate 3 (target hardcoding) is enforced by the absence of any user-id
        // parameter on the command itself — the SmokeAaronUserId constant is
        // the only target this handler can ever touch.
    }
}
