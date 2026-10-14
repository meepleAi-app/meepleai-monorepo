using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentProfileAdmin;

internal sealed class GetAgentProfileAdminQueryHandler : IQueryHandler<GetAgentProfileAdminQuery, AgentProfileAdminDto>
{
    private readonly IAgentProfileRepository _repository;

    public GetAgentProfileAdminQueryHandler(IAgentProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AgentProfileAdminDto> Handle(GetAgentProfileAdminQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Solo un database senza le migrazioni non ha il profilo: la prima versione la pubblicano loro.
        var profile = await _repository.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AgentProfile", "system");
        return AgentProfileAdminDto.From(profile);
    }
}
