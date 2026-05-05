using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="UpdateAgentConfigurationCommand"/>. Issue #658 (Phase δ).
/// Applies a partial update to the LLM configuration of an
/// <see cref="Domain.Entities.AgentDefinition"/> and returns the updated view.
/// </summary>
/// <remarks>
/// Persistence pattern mirrors <c>UpdateUserAgentCommandHandler</c>: the repository
/// <c>UpdateAsync</c> calls <c>DbContext.SaveChangesAsync</c> internally so no explicit
/// <c>IUnitOfWork</c> is required.
/// Range validation is delegated to <see cref="AgentDefinitionConfig.Create"/>; invalid
/// inputs surface as <see cref="ArgumentException"/> which the route maps to <c>400</c>.
/// SelectedDocumentIds is accepted on the wire but not persisted in MVP (same as PR #695).
/// </remarks>
internal sealed class UpdateAgentConfigurationCommandHandler
    : IRequestHandler<UpdateAgentConfigurationCommand, AgentConfigurationDto?>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ILogger<UpdateAgentConfigurationCommandHandler> _logger;

    public UpdateAgentConfigurationCommandHandler(
        IAgentDefinitionRepository repository,
        ILogger<UpdateAgentConfigurationCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentConfigurationDto?> Handle(
        UpdateAgentConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _repository
            .GetByIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent is null)
        {
            return null;
        }

        var newModel = string.IsNullOrWhiteSpace(request.ModelId)
            ? agent.Config.Model
            : request.ModelId;
        var newMaxTokens = request.MaxTokens ?? agent.Config.MaxTokens;
        var newTemperature = request.Temperature.HasValue
            ? (float)request.Temperature.Value
            : agent.Config.Temperature;

        // AgentDefinitionConfig.Create enforces model length, maxTokens range (100-32000),
        // and temperature range (0.0-2.0); throws ArgumentException on violation which
        // the route catches and maps to 400.
        var newConfig = AgentDefinitionConfig.Create(newModel, newMaxTokens, newTemperature);
        agent.UpdateConfig(newConfig);

        await _repository.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated AgentDefinition {Id} configuration (Model='{Model}', MaxTokens={MaxTokens}, Temperature={Temperature})",
            agent.Id,
            agent.Config.Model,
            agent.Config.MaxTokens,
            agent.Config.Temperature);

        return GetAgentConfigurationQueryHandler.BuildViewDto(agent);
    }
}
