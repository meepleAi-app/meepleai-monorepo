using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAgentDocumentsQuery.
/// Returns the indexed documents selected for an agent's current configuration.
/// Issue #2399: Knowledge Base Document Selection.
///
/// SelectedDocumentIdsJson contains VectorDocument.Id values. This handler joins
/// VectorDocuments with PdfDocuments to return document metadata.
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

        // Verify agent exists and retrieve GameId for context
        var agent = await _db.Agents
            .AsNoTracking()
            .Where(a => a.Id == request.AgentId)
            .Select(a => new { a.Id, a.GameId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
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

        // Parse VectorDocument IDs from JSON
        var vectorDocumentIds = new List<Guid>();
        if (!string.IsNullOrEmpty(currentConfig.SelectedDocumentIdsJson))
        {
            try
            {
                vectorDocumentIds = JsonSerializer.Deserialize<List<Guid>>(currentConfig.SelectedDocumentIdsJson)
                    ?? new List<Guid>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse document IDs JSON for agent {AgentId}", request.AgentId);
                vectorDocumentIds = new List<Guid>();
            }
        }

        if (vectorDocumentIds.Count == 0)
        {
            return new AgentDocumentsDto(request.AgentId, Array.Empty<SelectedDocumentDto>());
        }

        // Join VectorDocuments with PdfDocuments using the stored VectorDocument.Id values.
        // SelectedDocumentIdsJson stores VectorDocument.Id — never PdfDocument.Id.
        var agentGameId = agent.GameId ?? Guid.Empty;

        var documents = await (
            from vd in _db.VectorDocuments.AsNoTracking()
            join pdf in _db.PdfDocuments.AsNoTracking()
                on vd.PdfDocumentId equals pdf.Id
            where vectorDocumentIds.Contains(vd.Id)
            select new SelectedDocumentDto(
                vd.Id,
                agentGameId,
                vd.PdfDocumentId,
                MapDocumentType(pdf.DocumentType),
                string.Empty,
                pdf.IsActiveForRag,
                Array.Empty<string>(),
                null)
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {DocumentCount} documents for agent {AgentId}",
            documents.Count, request.AgentId);

        return new AgentDocumentsDto(request.AgentId, documents);
    }

    private static SharedGameDocumentType MapDocumentType(string? documentType) =>
        documentType?.ToLowerInvariant() switch
        {
            "base" => SharedGameDocumentType.Rulebook,
            "errata" => SharedGameDocumentType.Errata,
            "homerule" or "custom" => SharedGameDocumentType.Homerule,
            _ => SharedGameDocumentType.Rulebook
        };
}
