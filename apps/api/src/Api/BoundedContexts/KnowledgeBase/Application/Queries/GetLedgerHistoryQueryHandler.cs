#pragma warning disable MA0002 // Dictionary without StringComparer
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles retrieval of ledger state change history.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class GetLedgerHistoryQueryHandler
    : IRequestHandler<GetLedgerHistoryQuery, LedgerHistoryDto>
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly ILogger<GetLedgerHistoryQueryHandler> _logger;

    public GetLedgerHistoryQueryHandler(
        IGameSessionStateRepository sessionStateRepository,
        ILogger<GetLedgerHistoryQueryHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _logger = logger;
    }

    public async Task<LedgerHistoryDto> Handle(
        GetLedgerHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving ledger history for session {SessionId} (limit: {Limit})",
            query.SessionId,
            query.Limit);

        // Get session state
        var sessionState = await _sessionStateRepository
            .GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (sessionState == null)
        {
            throw new InvalidOperationException(
                $"Game session {query.SessionId} not found");
        }

        // Get all snapshots (ordered by turn number descending)
        var snapshots = sessionState.Snapshots
            .OrderByDescending(s => s.TurnNumber)
            .Take(query.Limit)
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} snapshots for session {SessionId}",
            snapshots.Count,
            query.SessionId);

        // Map to DTOs
        var changes = snapshots.Select(snapshot => new StateChangeEntry
        {
            Timestamp = snapshot.CreatedAt,
            UpdatedBy = snapshot.CreatedBy,
            Description = snapshot.Description,
            Version = snapshot.TurnNumber,
            Changes = new Dictionary<string, object>
            {
                { "snapshot", $"Turn {snapshot.TurnNumber}" }
            }
        }).ToList();

        return new LedgerHistoryDto
        {
            SessionId = query.SessionId,
            Changes = changes,
            CurrentVersion = sessionState.Version,
            TotalChanges = sessionState.Snapshots.Count
        };
    }
}
