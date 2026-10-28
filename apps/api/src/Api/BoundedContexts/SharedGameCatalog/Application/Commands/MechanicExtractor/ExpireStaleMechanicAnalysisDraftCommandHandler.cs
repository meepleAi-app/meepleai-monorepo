using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="ExpireStaleMechanicAnalysisDraftCommand"/> (#4503). Same persistence shape as the executor's
/// own auto-rejections: <c>AutoRejectFromDraft</c> with the creating admin as actor, then one <c>SaveChangesAsync</c>.
/// </summary>
/// <remarks>
/// The state is checked on the freshly loaded aggregate, not on what the batch saw a moment ago: a pipeline that was
/// slow rather than dead may have reached <c>InReview</c> in between, and then the row is not an orphan.
/// </remarks>
internal sealed class ExpireStaleMechanicAnalysisDraftCommandHandler
    : ICommandHandler<ExpireStaleMechanicAnalysisDraftCommand>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ExpireStaleMechanicAnalysisDraftCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(ExpireStaleMechanicAnalysisDraftCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdWithClaimsAsync(request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException("MechanicAnalysis", request.AnalysisId.ToString());
        }

        if (analysis.Status != MechanicAnalysisStatus.Draft)
        {
            throw new ConflictException(
                $"MechanicAnalysis {request.AnalysisId} is no longer in Draft (now {analysis.Status}); nothing to expire.");
        }

        analysis.AutoRejectFromDraft(
            MechanicAnalysis.AutoRejectionReasons.StaleDraftExpired,
            analysis.CreatedBy,
            _timeProvider.GetUtcNow().UtcDateTime);

        _analysisRepository.Update(analysis);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
