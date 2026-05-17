using Api.BoundedContexts.KnowledgeBase.Application.Channels;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="ReindexGameKbCommand"/>.
///
/// Async implementation (Issue #941 / ADR-057):
///   1. Verify at least one indexable PDF exists for the game (early no-op if zero)
///   2. Enforce concurrency invariant: at most one active job per (GameId, UserId)
///   3. Create a <see cref="KbReindexJob"/> row in the DB (queued)
///   4. Write a <see cref="KbReindexJobRequest"/> to <see cref="KbReindexChannel"/>
///   5. Return 202-equivalent <see cref="KbJobResponse"/> with status="queued"
///
/// The actual reindex work happens in <c>KbReindexProcessorService</c>.
/// Callers poll <c>GET /games/{id}/kb/reindex/{jobId}/status</c>.
/// </summary>
internal sealed class ReindexGameKbCommandHandler : ICommandHandler<ReindexGameKbCommand, KbJobResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly IKbReindexJobRepository _jobRepository;
    private readonly KbReindexChannel _channel;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReindexGameKbCommandHandler> _logger;

    public ReindexGameKbCommandHandler(
        MeepleAiDbContext db,
        IKbReindexJobRepository jobRepository,
        KbReindexChannel channel,
        IUnitOfWork unitOfWork,
        ILogger<ReindexGameKbCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbJobResponse> Handle(ReindexGameKbCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Count indexable PDFs.
        var pdfCount = await _db.PdfDocuments
            .AsNoTracking()
            .CountAsync(p =>
                (p.SharedGameId == command.GameId || p.PrivateGameId == command.GameId)
                && p.ExtractedText != null,
                cancellationToken)
            .ConfigureAwait(false);

        if (pdfCount == 0)
        {
            // No work to do — short-circuit with a synthetic completed job.
            var noop = KbReindexJob.Create(command.GameId, command.UserId, totalPdfs: 0);
            noop.MarkCompleted();
            await _jobRepository.AddAsync(noop, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "ReindexGameKb: No indexable PDFs for game {GameId} — no-op job {JobId}",
                command.GameId, noop.Id);

            return new KbJobResponse(noop.Id, "completed", 0);
        }

        // 2. Concurrency invariant: reject duplicate active jobs for the same (game, user).
        var existing = await _jobRepository
            .GetActiveByGameAndUserAsync(command.GameId, command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // 409 Conflict — caller should poll the existing jobId.
            throw new ConflictException(
                $"A reindex job is already active for this game ({existing.Id}); poll its status before retrying");
        }

        // 3. Create the job row in `queued` state.
        var job = KbReindexJob.Create(command.GameId, command.UserId, pdfCount);
        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 4. Hand off to the background worker.
        await _channel.Writer
            .WriteAsync(new KbReindexJobRequest(job.Id, command.GameId, command.UserId), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "ReindexGameKb: Enqueued job {JobId} for game {GameId} ({Count} PDFs, user {UserId})",
            job.Id, command.GameId, pdfCount, command.UserId);

        // 5. Return 202-equivalent.
        return new KbJobResponse(job.Id, "queued", pdfCount);
    }
}
