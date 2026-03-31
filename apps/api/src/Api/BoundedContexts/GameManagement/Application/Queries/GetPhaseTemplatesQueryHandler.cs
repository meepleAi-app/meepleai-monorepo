using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal sealed class GetPhaseTemplatesQueryHandler
    : IQueryHandler<GetPhaseTemplatesQuery, IReadOnlyList<PhaseTemplateDto>>
{
    private readonly IGamePhaseTemplateRepository _repository;

    public GetPhaseTemplatesQueryHandler(IGamePhaseTemplateRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<IReadOnlyList<PhaseTemplateDto>> Handle(GetPhaseTemplatesQuery query, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
        return templates
            .Select(t => new PhaseTemplateDto(t.Id, t.PhaseName, t.PhaseOrder, t.Description))
            .ToList()
            .AsReadOnly();
    }
}
