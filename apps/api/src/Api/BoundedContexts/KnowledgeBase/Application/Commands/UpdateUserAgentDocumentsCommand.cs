using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// User-facing command to update which documents their agent uses for RAG.
/// Validates: game in user library, documents belong to game, max 1 Base document.
/// </summary>
internal record UpdateUserAgentDocumentsCommand(
    Guid GameId,
    Guid UserId,
    List<Guid> SelectedDocumentIds
) : IRequest<Unit>;

internal sealed class UpdateUserAgentDocumentsCommandHandler
    : IRequestHandler<UpdateUserAgentDocumentsCommand, Unit>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateUserAgentDocumentsCommandHandler> _logger;

    public UpdateUserAgentDocumentsCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<UpdateUserAgentDocumentsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        UpdateUserAgentDocumentsCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate game is in user's library
        var libraryEntry = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.SharedGameId == request.GameId && e.UserId == request.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        if (libraryEntry == null)
            throw new ForbiddenException("Gioco non presente nella tua libreria");

        // 2. Find agent for this game
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.GameId == request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
            throw new NotFoundException("Agent", request.GameId.ToString());

        // 3. Validate all documents belong to this game
        if (request.SelectedDocumentIds.Count > 0)
        {
            var validDocIds = await _dbContext.PdfDocuments
                .Where(d => d.GameId == request.GameId && request.SelectedDocumentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var validIdSet = validDocIds.Select(d => d.Id).ToHashSet();
            var invalidIds = request.SelectedDocumentIds.Where(id => !validIdSet.Contains(id)).ToList();
            if (invalidIds.Count > 0)
                throw new NotFoundException(
                    "Document",
                    string.Join(", ", invalidIds));

            // 4. Validate max 1 Base document
            var baseCount = validDocIds.Count(d =>
                string.Equals(d.DocumentType, "base", StringComparison.OrdinalIgnoreCase));
            if (baseCount > 1)
                throw new ConflictException(
                    "Puoi selezionare al massimo un regolamento base");
        }

        // 5. Update agent config
        var config = await _dbContext.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == agent.Id && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        if (config == null)
            throw new NotFoundException("AgentConfiguration", agent.Id.ToString());

        config.SelectedDocumentIdsJson = JsonSerializer.Serialize(request.SelectedDocumentIds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} updated agent {AgentId} documents for game {GameId}: {Count} docs",
            request.UserId, agent.Id, request.GameId, request.SelectedDocumentIds.Count);

        return Unit.Value;
    }
}
