using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Issue #5503: Returns model health/compatibility data for admin UI.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class GetModelHealthQueryHandler
    : IQueryHandler<GetModelHealthQuery, GetModelHealthResult>
{
    private readonly IModelCompatibilityRepository _repository;

    public GetModelHealthQueryHandler(IModelCompatibilityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GetModelHealthResult> Handle(
        GetModelHealthQuery request,
        CancellationToken cancellationToken)
    {
        var entries = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var models = entries.Select(e => new ModelHealthDto(
            e.ModelId,
            e.DisplayName,
            e.Provider,
            e.Alternatives,
            e.ContextWindow,
            e.Strengths,
            e.IsCurrentlyAvailable,
            e.IsDeprecated,
            e.LastVerifiedAt)).ToList();

        return new GetModelHealthResult(models);
    }
}
