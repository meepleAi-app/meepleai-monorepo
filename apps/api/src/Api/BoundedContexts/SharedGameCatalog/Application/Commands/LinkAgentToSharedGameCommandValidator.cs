using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for LinkAgentToSharedGameCommand.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class LinkAgentToSharedGameCommandValidator : AbstractValidator<LinkAgentToSharedGameCommand>
{
    private readonly IAgentDefinitionRepository _agentRepository;

    public LinkAgentToSharedGameCommandValidator(IAgentDefinitionRepository agentRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));

        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("AgentId is required")
            .MustAsync(AgentExists).WithMessage("Agent definition does not exist");
    }

    private async Task<bool> AgentExists(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        return agent is not null;
    }
}
