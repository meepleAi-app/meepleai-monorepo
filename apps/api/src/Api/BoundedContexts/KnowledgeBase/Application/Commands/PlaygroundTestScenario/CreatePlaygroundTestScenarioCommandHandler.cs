using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.PlaygroundTestScenario;

/// <summary>
/// Handler for CreatePlaygroundTestScenarioCommand.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal sealed class CreatePlaygroundTestScenarioCommandHandler
    : IRequestHandler<CreatePlaygroundTestScenarioCommand, PlaygroundTestScenarioDto>
{
    private readonly IPlaygroundTestScenarioRepository _repository;
    private readonly ILogger<CreatePlaygroundTestScenarioCommandHandler> _logger;

    public CreatePlaygroundTestScenarioCommandHandler(
        IPlaygroundTestScenarioRepository repository,
        ILogger<CreatePlaygroundTestScenarioCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlaygroundTestScenarioDto> Handle(
        CreatePlaygroundTestScenarioCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = Domain.Entities.PlaygroundTestScenario.Create(
            request.Name,
            request.Description,
            request.Category,
            request.Messages,
            request.CreatedBy,
            request.ExpectedOutcome,
            request.AgentDefinitionId,
            request.Tags);

        await _repository.AddAsync(scenario, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created PlaygroundTestScenario {ScenarioId} '{Name}' by {UserId}",
            scenario.Id, scenario.Name, request.CreatedBy);

        return MapToDto(scenario);
    }

    internal static PlaygroundTestScenarioDto MapToDto(Domain.Entities.PlaygroundTestScenario scenario)
    {
        return new PlaygroundTestScenarioDto
        {
            Id = scenario.Id,
            Name = scenario.Name,
            Description = scenario.Description,
            Category = scenario.Category.ToString(),
            Messages = scenario.Messages.Select(m => new ScenarioMessageDto(m.Role, m.Content, m.DelayMs)).ToList(),
            CreatedBy = scenario.CreatedBy,
            CreatedAt = scenario.CreatedAt,
            UpdatedAt = scenario.UpdatedAt,
            IsActive = scenario.IsActive,
            ExpectedOutcome = scenario.ExpectedOutcome,
            AgentDefinitionId = scenario.AgentDefinitionId,
            Tags = scenario.Tags.ToList()
        };
    }
}
