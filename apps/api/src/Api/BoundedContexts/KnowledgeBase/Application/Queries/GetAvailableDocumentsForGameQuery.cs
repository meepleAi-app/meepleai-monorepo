using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

// DTOs
internal record AvailableDocumentsDto(
    Guid? AgentId,
    List<DocumentSelectionItemDto> BaseDocuments,
    List<DocumentSelectionItemDto> AdditionalDocuments
);

internal record DocumentSelectionItemDto(
    Guid DocumentId,
    string FileName,
    string DocumentType,
    string ProcessingState,
    bool IsPrivate,
    bool IsSelected,
    int? PageCount
);

// Query
internal record GetAvailableDocumentsForGameQuery(
    Guid GameId,
    Guid UserId
) : IRequest<AvailableDocumentsDto>;

// Handler
internal sealed class GetAvailableDocumentsForGameQueryHandler
    : IRequestHandler<GetAvailableDocumentsForGameQuery, AvailableDocumentsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetAvailableDocumentsForGameQueryHandler> _logger;

    public GetAvailableDocumentsForGameQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetAvailableDocumentsForGameQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AvailableDocumentsDto> Handle(
        GetAvailableDocumentsForGameQuery request, CancellationToken cancellationToken)
    {
        // 1. Find agent for this game
        var agent = await _dbContext.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.GameId == request.GameId, cancellationToken)
            .ConfigureAwait(false);

        // 2. Get selected document IDs from agent config
        var selectedDocIds = new HashSet<Guid>();
        if (agent != null)
        {
            var config = await _dbContext.AgentConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.AgentId == agent.Id && c.IsCurrent,
                    cancellationToken)
                .ConfigureAwait(false);

            if (config != null && !string.IsNullOrEmpty(config.SelectedDocumentIdsJson))
            {
                try
                {
                    var ids = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson);
                    if (ids != null) selectedDocIds = ids.ToHashSet();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse SelectedDocumentIdsJson for agent {AgentId}", agent.Id);
                }
            }
        }

        // 3. Get all PDF documents for this game (shared + user's private)
        //    Privacy is determined by PrivateGameId != null (no IsPrivate column)
        var documents = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(d => d.GameId == request.GameId && d.IsActiveForRag)
            .Where(d => d.PrivateGameId == null || d.UploadedByUserId == request.UserId)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.FileName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 4. Project to DTOs (done in-memory because selectedDocIds is local)
        var documentDtos = documents
            .Select(d => new DocumentSelectionItemDto(
                d.Id,
                d.FileName,
                d.DocumentType ?? "base",
                d.ProcessingState ?? "Pending",
                d.PrivateGameId != null,
                selectedDocIds.Contains(d.Id),
                d.PageCount))
            .ToList();

        // 5. Split by type
        var baseDocuments = documentDtos
            .Where(d => d.DocumentType.Equals("base", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var additionalDocuments = documentDtos
            .Where(d => !d.DocumentType.Equals("base", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new AvailableDocumentsDto(agent?.Id, baseDocuments, additionalDocuments);
    }
}
