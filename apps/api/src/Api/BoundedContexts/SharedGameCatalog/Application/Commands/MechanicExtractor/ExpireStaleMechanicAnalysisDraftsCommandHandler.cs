using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="ExpireStaleMechanicAnalysisDraftsCommand"/> (#4503). Lists the ids of the <c>Draft</c> analyses
/// older than <see cref="MechanicAnalysisStaleDraftOptions.Threshold"/> (id-only projection, one batch) and dispatches
/// one <see cref="ExpireStaleMechanicAnalysisDraftCommand"/> per id, the shape <c>RecalculateAllMechanicMetricsHandler</c>
/// already uses in this context.
/// </summary>
/// <remarks>
/// A row that vanished or left <c>Draft</c> between the listing and its own command (404 / 409) is skipped, because it is
/// no longer an orphan. Losing the write itself to the pipeline (<see cref="DbUpdateConcurrencyException"/>) is the same
/// case, and ends the pass because the scope's <c>DbContext</c> still holds the failed row. Any other failure propagates: the job logs it and the next run starts from a fresh scope, so a
/// failed write never leaves state behind for the writes after it.
/// </remarks>
internal sealed class ExpireStaleMechanicAnalysisDraftsCommandHandler
    : ICommandHandler<ExpireStaleMechanicAnalysisDraftsCommand, ExpireStaleDraftsResult>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<MechanicAnalysisStaleDraftOptions> _options;
    private readonly ILogger<ExpireStaleMechanicAnalysisDraftsCommandHandler> _logger;

    public ExpireStaleMechanicAnalysisDraftsCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IMediator mediator,
        TimeProvider timeProvider,
        IOptions<MechanicAnalysisStaleDraftOptions> options,
        ILogger<ExpireStaleMechanicAnalysisDraftsCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExpireStaleDraftsResult> Handle(
        ExpireStaleMechanicAnalysisDraftsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var threshold = _options.Value.Threshold;
        if (threshold <= TimeSpan.Zero)
        {
            // Zero or less would make every Draft "stale", live pipelines included.
            _logger.LogError(
                "{Section}:Threshold is {Threshold}; a stale-draft threshold must be positive. Expiring nothing.",
                MechanicAnalysisStaleDraftOptions.SectionKey,
                threshold);
            return new ExpireStaleDraftsResult(Found: 0, Expired: 0);
        }

        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - threshold;
        var ids = await _analysisRepository
            .GetDraftIdsCreatedBeforeAsync(cutoff, MechanicAnalysisStaleDraftOptions.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        var expired = 0;
        foreach (var analysisId in ids)
        {
            try
            {
                await _mediator
                    .Send(new ExpireStaleMechanicAnalysisDraftCommand(analysisId), cancellationToken)
                    .ConfigureAwait(false);
                expired++;
            }
            catch (NotFoundException ex)
            {
                _logger.LogInformation(
                    ex,
                    "Stale-draft expiry skipped MechanicAnalysis {AnalysisId}: the row is gone.",
                    analysisId);
            }
            catch (ConflictException ex)
            {
                _logger.LogInformation(
                    ex,
                    "Stale-draft expiry skipped MechanicAnalysis {AnalysisId}: it left Draft in the meantime.",
                    analysisId);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // The pipeline closed the row between the reload and the save: not a failure. The failed row stays
                // tracked in this scope, so any write after it would fail with it: the pass stops here. The row is
                // no longer Draft, so the next run does not list it again.
                _logger.LogInformation(
                    ex,
                    "Stale-draft expiry lost a write race on MechanicAnalysis {AnalysisId}; stopping this pass.",
                    analysisId);
                break;
            }
        }

        if (ids.Count > 0)
        {
            _logger.LogWarning(
                "Stale-draft expiry: {Expired}/{Found} mechanic analyses left in Draft for more than {Threshold} were rejected ({Reason}).",
                expired,
                ids.Count,
                threshold,
                MechanicAnalysis.AutoRejectionReasons.StaleDraftExpired);
        }

        return new ExpireStaleDraftsResult(Found: ids.Count, Expired: expired);
    }
}
