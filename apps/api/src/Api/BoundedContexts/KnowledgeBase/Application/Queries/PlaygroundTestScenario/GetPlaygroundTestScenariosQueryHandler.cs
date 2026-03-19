using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.PlaygroundTestScenario;

/// <summary>
/// Handler for GetPlaygroundTestScenariosQuery.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal sealed class GetPlaygroundTestScenariosQueryHandler
    : IRequestHandler<GetPlaygroundTestScenariosQuery, List<PlaygroundTestScenarioDto>>
{
    private readonly IPlaygroundTestScenarioRepository _repository;

    public GetPlaygroundTestScenariosQueryHandler(IPlaygroundTestScenarioRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<PlaygroundTestScenarioDto>> Handle(
        GetPlaygroundTestScenariosQuery request,
        CancellationToken cancellationToken)
    {
        var scenarios = await _repository.GetAllAsync(
            request.Category,
            request.AgentDefinitionId,
            request.ActiveOnly,
            cancellationToken).ConfigureAwait(false);

        return scenarios
            .Select(CreatePlaygroundTestScenarioCommandHandler.MapToDto)
            .ToList();
    }
}
