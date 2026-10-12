using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF implementation of <see cref="IAssistantProposalRepository"/>. Le proposte da modificare si caricano
/// tracciate e non si riattaccano mai con <c>Update()</c>: su un'entità detached con <c>xmin</c> l'Update
/// rompe le scritture (ADR-060).
/// </summary>
internal sealed class AssistantProposalRepository : IAssistantProposalRepository
{
    private readonly MeepleAiDbContext _db;

    public AssistantProposalRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // AsTracking esplicito: MeepleAiDbContext è NoTracking per default (PERF-06, #3866).
    public Task<AssistantProposal?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<AssistantProposal>().AsTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<AssistantProposal?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<AssistantProposal>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssistantProposal>> ListOpenBySessionAsync(
        Guid liveSessionId, CancellationToken cancellationToken = default) =>
        await _db.Set<AssistantProposal>()
            .AsTracking()
            .Where(p => p.LiveSessionId == liveSessionId
                && (p.Status == AssistantProposalStatus.Pending || p.Status == AssistantProposalStatus.Applying))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssistantProposal>> ListByThreadAsync(
        Guid chatThreadId, CancellationToken cancellationToken = default) =>
        await _db.Set<AssistantProposal>()
            .AsNoTracking()
            .Where(p => p.ChatThreadId == chatThreadId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(AssistantProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await _db.Set<AssistantProposal>().AddAsync(proposal, cancellationToken).ConfigureAwait(false);
    }
}
