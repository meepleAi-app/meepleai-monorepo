using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository dell'aggregato singleton <see cref="AgentProfile"/> (ADR-095, #4167).
/// Pattern UoW di ADR-056: i metodi che scrivono preparano soltanto le modifiche; il chiamante invoca
/// <see cref="Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IAgentProfileRepository
{
    /// <summary>
    /// Il profilo con tutte le versioni, tracciato: le modifiche fatte dall'aggregato si salvano con
    /// la unit of work. <c>null</c> solo su un database a cui manca il seed.
    /// </summary>
    Task<AgentProfile?> GetAsync(CancellationToken cancellationToken = default);

    Task AddAsync(AgentProfile profile, CancellationToken cancellationToken = default);
}
