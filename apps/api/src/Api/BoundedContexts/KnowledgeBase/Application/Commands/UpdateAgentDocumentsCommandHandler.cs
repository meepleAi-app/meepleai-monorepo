using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for UpdateAgentDocumentsCommand.
/// Updates the selected documents for an agent's current configuration.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal sealed class UpdateAgentDocumentsCommandHandler
    : IRequestHandler<UpdateAgentDocumentsCommand, UpdateAgentDocumentsResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<UpdateAgentDocumentsCommandHandler> _logger;

    public UpdateAgentDocumentsCommandHandler(
        MeepleAiDbContext db,
        ILogger<UpdateAgentDocumentsCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateAgentDocumentsResult> Handle(
        UpdateAgentDocumentsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Verify agent exists
            var agentExists = await _db.Agents
                .AnyAsync(a => a.Id == request.AgentId, cancellationToken)
                .ConfigureAwait(false);

            if (!agentExists)
            {
                _logger.LogWarning("Agent not found: {AgentId}", request.AgentId);
                return new UpdateAgentDocumentsResult(
                    Success: false,
                    Message: $"Agent with ID {request.AgentId} not found",
                    AgentId: request.AgentId,
                    DocumentCount: 0,
                    ErrorCode: "AGENT_NOT_FOUND"
                );
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
                return new UpdateAgentDocumentsResult(
                    Success: false,
                    Message: $"No active configuration found for agent {request.AgentId}",
                    AgentId: request.AgentId,
                    DocumentCount: 0,
                    ErrorCode: "NO_CONFIGURATION"
                );
            }

            // DocumentIds from the UI are PdfDocument.Id values.
            // SelectedDocumentIdsJson must store VectorDocument.Id values so that
            // SendAgentMessageCommandHandler can query VectorDocuments directly.
            // Resolve PdfDocument.Id → VectorDocument.Id here.
            var vectorDocumentIds = new List<Guid>();
            if (request.DocumentIds.Count > 0)
            {
                // Step 1: validate all PdfDocument IDs exist
                var existingPdfIds = await _db.PdfDocuments
                    .Where(d => request.DocumentIds.Contains(d.Id))
                    .Select(d => d.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var missingIds = request.DocumentIds.Except(existingPdfIds).ToList();
                if (missingIds.Count > 0)
                {
                    _logger.LogWarning(
                        "Some documents not found: {MissingIds}",
                        string.Join(", ", missingIds));
                    return new UpdateAgentDocumentsResult(
                        Success: false,
                        Message: $"Documents not found: {string.Join(", ", missingIds)}",
                        AgentId: request.AgentId,
                        DocumentCount: 0,
                        ErrorCode: "DOCUMENTS_NOT_FOUND"
                    );
                }

                // Step 2: resolve PdfDocument.Id → VectorDocument.Id
                // PDFs that have not yet been indexed will not have a matching VectorDocument;
                // those are silently excluded (they cannot participate in RAG until indexed).
                vectorDocumentIds = await _db.VectorDocuments
                    .Where(vd => existingPdfIds.Contains(vd.PdfDocumentId))
                    .Select(vd => vd.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (vectorDocumentIds.Count < existingPdfIds.Count)
                {
                    var indexedPdfIds = await _db.VectorDocuments
                        .Where(vd => existingPdfIds.Contains(vd.PdfDocumentId))
                        .Select(vd => vd.PdfDocumentId)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var notYetIndexed = existingPdfIds.Except(indexedPdfIds).ToList();
                    _logger.LogWarning(
                        "Skipping {Count} PDF(s) not yet indexed (no VectorDocument): {PdfIds}",
                        notYetIndexed.Count,
                        string.Join(", ", notYetIndexed));
                }
            }

            // Validate mode requirements (Player/Ledger need at least one document)
            // AgentMode: 0=Chat, 1=Player, 2=Ledger
            if ((currentConfig.AgentMode == 1 || currentConfig.AgentMode == 2) &&
                vectorDocumentIds.Count == 0)
            {
                var modeName = currentConfig.AgentMode == 1 ? "Player" : "Ledger";
                return new UpdateAgentDocumentsResult(
                    Success: false,
                    Message: $"{modeName} mode requires at least one indexed document selected",
                    AgentId: request.AgentId,
                    DocumentCount: 0,
                    ErrorCode: "DOCUMENTS_REQUIRED"
                );
            }

            // Store VectorDocument.Id values — the canonical type for SelectedDocumentIdsJson
            currentConfig.SelectedDocumentIdsJson = JsonSerializer.Serialize(vectorDocumentIds);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated documents for agent {AgentId}: {DocumentCount} documents selected",
                request.AgentId,
                request.DocumentIds.Count);

            return new UpdateAgentDocumentsResult(
                Success: true,
                Message: $"Successfully updated {vectorDocumentIds.Count} document(s) for agent",
                AgentId: request.AgentId,
                DocumentCount: vectorDocumentIds.Count
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // HANDLER BOUNDARY: Catch infrastructure failures to return proper Result type
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Error updating documents for agent {AgentId}", request.AgentId);
            return new UpdateAgentDocumentsResult(
                Success: false,
                Message: "An error occurred while updating agent documents",
                AgentId: request.AgentId,
                DocumentCount: 0,
                ErrorCode: "INTERNAL_ERROR"
            );
        }
    }
}
