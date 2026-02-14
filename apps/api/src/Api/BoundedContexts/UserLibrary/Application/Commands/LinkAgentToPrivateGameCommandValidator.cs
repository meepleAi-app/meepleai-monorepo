using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Validator for LinkAgentToPrivateGameCommand.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class LinkAgentToPrivateGameCommandValidator : AbstractValidator<LinkAgentToPrivateGameCommand>
{
    private readonly IAgentDefinitionRepository _agentRepository;

    public LinkAgentToPrivateGameCommandValidator(IAgentDefinitionRepository agentRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));

        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("AgentId is required")
            .MustAsync(AgentExists).WithMessage("Agent definition does not exist");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }

    private async Task<bool> AgentExists(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        return agent is not null;
    }
}
