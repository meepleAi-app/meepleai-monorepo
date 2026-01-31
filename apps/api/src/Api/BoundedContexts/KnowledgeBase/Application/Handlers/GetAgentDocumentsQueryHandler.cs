using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAgentDocumentsQuery.
/// Returns the selected documents for an agent's current configuration.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal sealed class GetAgentDocumentsQueryHandler
    : IRequestHandler<GetAgentDocumentsQuery, AgentDocumentsDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetAgentDocumentsQueryHandler> _logger;

    public GetAgentDocumentsQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetAgentDocumentsQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentDocumentsDto?> Handle(
        GetAgentDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Verify agent exists
        var agentExists = await _db.Agents
            .AnyAsync(a => a.Id == request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (!agentExists)
        {
            _logger.LogWarning("Agent not found: {AgentId}", request.AgentId);
            return null;
        }

        // Get current configuration for the agent
        var currentConfig = await _db.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == request.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        if (currentConfig == null)
        {
            _logger.LogWarning(
                "No current configuration found for agent {AgentId}",
                request.AgentId);
            return new AgentDocumentsDto(request.AgentId, Array.Empty<SelectedDocumentDto>());
        }

        // Parse document IDs from JSON
        var documentIds = new List<Guid>();
        if (!string.IsNullOrEmpty(currentConfig.SelectedDocumentIdsJson))
        {
            try
            {
                documentIds = JsonSerializer.Deserialize<List<Guid>>(currentConfig.SelectedDocumentIdsJson)
                    ?? new List<Guid>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse document IDs JSON for agent {AgentId}", request.AgentId);
                documentIds = new List<Guid>();
            }
        }

        if (documentIds.Count == 0)
        {
            return new AgentDocumentsDto(request.AgentId, Array.Empty<SelectedDocumentDto>());
        }

        // Fetch documents with game names
        var documentEntities = await _db.SharedGameDocuments
            .Where(d => documentIds.Contains(d.Id))
            .Include(d => d.SharedGame)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var documents = documentEntities.Select(doc => new SelectedDocumentDto(
            doc.Id,
            doc.SharedGameId,
            doc.PdfDocumentId,
            (SharedGameCatalog.Domain.Entities.SharedGameDocumentType)doc.DocumentType,
            doc.Version,
            doc.IsActive,
            ParseTags(doc.TagsJson),
            doc.SharedGame?.Title)).ToList();

        _logger.LogInformation(
            "Retrieved {DocumentCount} documents for agent {AgentId}",
            documents.Count, request.AgentId);

        return new AgentDocumentsDto(request.AgentId, documents);
    }

    private static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrEmpty(tagsJson))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
