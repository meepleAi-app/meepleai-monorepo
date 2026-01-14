using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

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

            // Validate documents exist if any are provided
            if (request.DocumentIds.Count > 0)
            {
                var existingDocIds = await _db.SharedGameDocuments
                    .Where(d => request.DocumentIds.Contains(d.Id))
                    .Select(d => d.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var missingIds = request.DocumentIds.Except(existingDocIds).ToList();
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
            }

            // Validate mode requirements (Player/Ledger need at least one document)
            // AgentMode: 0=Chat, 1=Player, 2=Ledger
            if ((currentConfig.AgentMode == 1 || currentConfig.AgentMode == 2) &&
                request.DocumentIds.Count == 0)
            {
                var modeName = currentConfig.AgentMode == 1 ? "Player" : "Ledger";
                return new UpdateAgentDocumentsResult(
                    Success: false,
                    Message: $"{modeName} mode requires at least one document selected",
                    AgentId: request.AgentId,
                    DocumentCount: 0,
                    ErrorCode: "DOCUMENTS_REQUIRED"
                );
            }

            // Update the selected documents (stored as JSON)
            currentConfig.SelectedDocumentIdsJson = JsonSerializer.Serialize(request.DocumentIds);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated documents for agent {AgentId}: {DocumentCount} documents selected",
                request.AgentId,
                request.DocumentIds.Count);

            return new UpdateAgentDocumentsResult(
                Success: true,
                Message: $"Successfully updated {request.DocumentIds.Count} document(s) for agent",
                AgentId: request.AgentId,
                DocumentCount: request.DocumentIds.Count
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
