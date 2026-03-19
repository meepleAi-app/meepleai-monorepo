using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;

/// <summary>
/// Handler for DeletePlaygroundTestScenarioCommand (soft delete via deactivation).
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal sealed class DeletePlaygroundTestScenarioCommandHandler
    : IRequestHandler<DeletePlaygroundTestScenarioCommand>
{
    private readonly IPlaygroundTestScenarioRepository _repository;
    private readonly ILogger<DeletePlaygroundTestScenarioCommandHandler> _logger;

    public DeletePlaygroundTestScenarioCommandHandler(
        IPlaygroundTestScenarioRepository repository,
        ILogger<DeletePlaygroundTestScenarioCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeletePlaygroundTestScenarioCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (scenario is null)
            throw new NotFoundException($"PlaygroundTestScenario with ID '{request.Id}' not found");

        scenario.Deactivate();

        await _repository.UpdateAsync(scenario, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deactivated PlaygroundTestScenario {ScenarioId} '{Name}'",
            scenario.Id, scenario.Name);
    }
}
