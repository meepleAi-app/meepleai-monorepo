using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNight;

/// <summary>
/// Returns all rule dispute entries across every session played for a given game,
/// aggregated as a cross-session dispute history for post-game review.
/// Game Night Improvvisata — E3: Arbitro cross-session history.
/// </summary>
/// <param name="GameId">The game whose session history should be queried.</param>
public sealed record GetGameDisputeHistoryQuery(Guid GameId) : IRequest<GetGameDisputeHistoryResult>;

/// <summary>
/// Aggregated dispute history for a game, grouped by session.
/// </summary>
/// <param name="GameId">The queried game.</param>
/// <param name="Sessions">Per-session dispute summaries, ordered by session start time descending.</param>
/// <param name="TotalDisputes">Total number of disputes across all sessions.</param>
public sealed record GetGameDisputeHistoryResult(
    Guid GameId,
    IReadOnlyList<SessionDisputeGroup> Sessions,
    int TotalDisputes);

/// <summary>
/// Disputes belonging to a single session.
/// </summary>
public sealed record SessionDisputeGroup(
    Guid SessionId,
    DateTime? SessionStartedAt,
    IReadOnlyList<DisputeSummary> Disputes);

/// <summary>
/// Handles <see cref="GetGameDisputeHistoryQuery"/>.
/// Reads <c>DisputesJson</c> from the <c>live_game_sessions</c> table (written by
/// the SubmitRuleDisputeCommand handler) and projects them as a flat history.
/// Uses <see cref="MeepleAiDbContext"/> directly for a lightweight read-only projection.
/// </summary>
internal sealed class GetGameDisputeHistoryQueryHandler
    : IRequestHandler<GetGameDisputeHistoryQuery, GetGameDisputeHistoryResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetGameDisputeHistoryQueryHandler> _logger;

    public GetGameDisputeHistoryQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetGameDisputeHistoryQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetGameDisputeHistoryResult> Handle(
        GetGameDisputeHistoryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read all sessions for this game that have disputes (DisputesJson is not null/empty)
        var sessionEntities = await _dbContext.LiveGameSessions
            .AsNoTracking()
            .Where(s => s.GameId == request.GameId && s.DisputesJson != null && s.DisputesJson != "[]")
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.StartedAt,
                s.DisputesJson
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessionGroups = new List<SessionDisputeGroup>(sessionEntities.Count);
        var totalDisputes = 0;

        foreach (var entity in sessionEntities)
        {
            List<RuleDisputeEntry> disputes;
            try
            {
                disputes = string.IsNullOrWhiteSpace(entity.DisputesJson)
                    ? new List<RuleDisputeEntry>()
                    : JsonSerializer.Deserialize<List<RuleDisputeEntry>>(entity.DisputesJson, JsonOptions)
                      ?? new List<RuleDisputeEntry>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialise DisputesJson for session {SessionId}. Skipping.",
                    entity.Id);
                disputes = new List<RuleDisputeEntry>();
            }

            if (disputes.Count == 0)
                continue;

            totalDisputes += disputes.Count;

            var summaries = disputes
                .OrderBy(d => d.Timestamp)
                .Select(d => new DisputeSummary(
                    Id: d.Id,
                    Description: d.Description,
                    Verdict: d.Verdict,
                    RuleReferences: d.RuleReferences,
                    RaisedByPlayerName: d.RaisedByPlayerName,
                    Timestamp: d.Timestamp))
                .ToList();

            sessionGroups.Add(new SessionDisputeGroup(
                SessionId: entity.Id,
                SessionStartedAt: entity.StartedAt,
                Disputes: summaries));
        }

        return new GetGameDisputeHistoryResult(
            GameId: request.GameId,
            Sessions: sessionGroups,
            TotalDisputes: totalDisputes);
    }
}
