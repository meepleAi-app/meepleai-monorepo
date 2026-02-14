using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.PlaygroundTestScenario;

/// <summary>
/// Handler for GetPlaygroundTestScenarioByIdQuery.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal sealed class GetPlaygroundTestScenarioByIdQueryHandler
    : IRequestHandler<GetPlaygroundTestScenarioByIdQuery, PlaygroundTestScenarioDto?>
{
    private readonly IPlaygroundTestScenarioRepository _repository;

    public GetPlaygroundTestScenarioByIdQueryHandler(IPlaygroundTestScenarioRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PlaygroundTestScenarioDto?> Handle(
        GetPlaygroundTestScenarioByIdQuery request,
        CancellationToken cancellationToken)
    {
        var scenario = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return scenario is null ? null : CreatePlaygroundTestScenarioCommandHandler.MapToDto(scenario);
    }
}
