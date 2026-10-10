using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF implementation of <see cref="IAgentProfileRepository"/>. ADR-056 UoW pattern:
/// methods stage changes; caller invokes <c>IUnitOfWork.SaveChangesAsync</c>.
/// Il profilo si carica tracciato e non si riattacca mai con <c>Update()</c>: su un grafo
/// detached con <c>xmin</c> l'Update rompe le scritture (ADR-060).
/// </summary>
internal sealed class AgentProfileRepository : IAgentProfileRepository
{
    private readonly MeepleAiDbContext _db;

    public AgentProfileRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<AgentProfile?> GetAsync(CancellationToken cancellationToken = default) =>
        _db.Set<AgentProfile>()
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == AgentProfile.SingletonId, cancellationToken);

    public async Task AddAsync(AgentProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _db.Set<AgentProfile>().AddAsync(profile, cancellationToken).ConfigureAwait(false);
    }
}
