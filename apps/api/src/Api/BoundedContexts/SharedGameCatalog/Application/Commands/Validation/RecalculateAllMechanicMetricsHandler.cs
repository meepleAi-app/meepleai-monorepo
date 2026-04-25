using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="RecalculateAllMechanicMetricsCommand"/> (ADR-051 Sprint 1 / Task 25).
/// Sprint 1 synchronous mass-recalc: enumerates every
/// <see cref="MechanicAnalysisStatus.Published"/> analysis id and dispatches one
/// <see cref="CalculateMechanicAnalysisMetricsCommand"/> per id through <see cref="IMediator"/>.
/// Returns the count of <i>successful</i> recalculations.
/// </summary>
/// <remarks>
/// <para>
/// The outer handler is a pure dispatcher: it owns no unit-of-work boundary. Each
/// <see cref="CalculateMechanicAnalysisMetricsCommand"/> handler internally commits via its own
/// UoW so partial progress is durable across per-id failures.
/// </para>
/// <para>
/// Per-id <see cref="NotFoundException"/> and <see cref="ConflictException"/> failures are logged
/// at <c>Warning</c> and skipped. They are expected/benign races: a row may have been deleted, had
/// its status changed, or had its golden-claim corpus removed between id-enumeration and per-id
/// dispatch. Other exception types (infrastructure, unexpected domain invariants) propagate and
/// abort the batch — the surviving success count is <i>not</i> returned in that case, by design,
/// because the caller cannot distinguish "partial-success but I/O is broken" from "happy path"
/// without an exception.
/// </para>
/// <para>
/// The id-only repository projection (<see cref="IMechanicAnalysisRepository.GetIdsByStatusAsync"/>)
/// avoids hydrating the full claim graph for each candidate — only the inner per-id handler needs
/// the graph, and it loads it freshly via <c>GetByIdWithClaimsAsync</c>.
/// </para>
/// </remarks>
internal sealed class RecalculateAllMechanicMetricsHandler
    : ICommandHandler<RecalculateAllMechanicMetricsCommand, int>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RecalculateAllMechanicMetricsHandler> _logger;

    public RecalculateAllMechanicMetricsHandler(
        IMechanicAnalysisRepository analysisRepository,
        IMediator mediator,
        ILogger<RecalculateAllMechanicMetricsHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(
        RecalculateAllMechanicMetricsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = await _analysisRepository
            .GetIdsByStatusAsync(MechanicAnalysisStatus.Published, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Starting mass recalculation of MechanicAnalysisMetrics for {CandidateCount} Published analyses.",
            ids.Count);

        var successCount = 0;
        foreach (var analysisId in ids)
        {
            try
            {
                await _mediator
                    .Send(new CalculateMechanicAnalysisMetricsCommand(analysisId), cancellationToken)
                    .ConfigureAwait(false);
                successCount++;
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping recalculation for MechanicAnalysis {AnalysisId}: {ExceptionType} - row no longer accessible.",
                    analysisId,
                    nameof(NotFoundException));
            }
            catch (ConflictException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping recalculation for MechanicAnalysis {AnalysisId}: {ExceptionType} - aggregate state precondition failed.",
                    analysisId,
                    nameof(ConflictException));
            }
        }

        _logger.LogInformation(
            "Completed mass recalculation of MechanicAnalysisMetrics: {SuccessCount}/{CandidateCount} succeeded.",
            successCount,
            ids.Count);

        return successCount;
    }
}
