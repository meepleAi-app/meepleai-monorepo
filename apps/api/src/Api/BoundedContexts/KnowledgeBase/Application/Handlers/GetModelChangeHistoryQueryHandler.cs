using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Issue #5503: Returns model change history for admin UI audit trail.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class GetModelChangeHistoryQueryHandler
    : IQueryHandler<GetModelChangeHistoryQuery, GetModelChangeHistoryResult>
{
    private readonly IModelCompatibilityRepository _repository;

    public GetModelChangeHistoryQueryHandler(IModelCompatibilityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GetModelChangeHistoryResult> Handle(
        GetModelChangeHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var entries = await _repository.GetChangeHistoryAsync(
            request.ModelId, request.Limit, cancellationToken).ConfigureAwait(false);

        var changes = entries.Select(e => new ModelChangeHistoryDto(
            e.Id,
            e.ModelId,
            e.ChangeType,
            e.PreviousModelId,
            e.NewModelId,
            e.AffectedStrategy,
            e.Reason,
            e.IsAutomatic,
            e.ChangedByUserId,
            e.OccurredAt)).ToList();

        return new GetModelChangeHistoryResult(changes);
    }
}
