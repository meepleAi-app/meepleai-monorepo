using System.Text.Json;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.ActivityFeed;

/// <summary>
/// Handles <see cref="GetActivityFeedQuery"/>: reads cross-entity activity from
/// <c>domain_event_logs</c> scoped to the requesting user (90-day retention,
/// ordered by <c>LoggedAt DESC</c>, limited by <see cref="GetActivityFeedQuery.Limit"/>).
///
/// EventType → EntityType mapping (stable contract, not raw AggregateType):
/// <list type="bullet">
///   <item>agent.created → Agent</item>
///   <item>chat.session.created → ChatSession</item>
///   <item>kb.doc.indexed → PdfDocument</item>
///   <item>session.created | session.finalized → Session</item>
///   <item>library.entry.removed | library.session.recorded → UserLibraryEntry</item>
///   <item>(default) → raw AggregateType or EventType</item>
/// </list>
///
/// BE-3 #1590 Task 8.
/// </summary>
internal sealed class GetActivityFeedQueryHandler
    : IQueryHandler<GetActivityFeedQuery, GetActivityFeedResult>
{
    private const int RetentionDays = 90;

    private readonly MeepleAiDbContext _db;

    public GetActivityFeedQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GetActivityFeedResult> Handle(
        GetActivityFeedQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var retentionCutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        var query = _db.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.UserId == request.UserId && e.LoggedAt >= retentionCutoff);

        if (request.Since.HasValue)
        {
            query = query.Where(e => e.LoggedAt < request.Since.Value);
        }

        var rows = await query
            .OrderByDescending(e => e.LoggedAt)
            .Take(request.Limit)
            .Select(e => new
            {
                e.Id,
                e.EventId,
                e.EventType,
                e.UserId,
                e.AggregateId,
                e.AggregateType,
                e.PayloadJson,
                e.OccurredAt,
                e.LoggedAt,
                e.PayloadVersion,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(r =>
        {
            var entityType = MapEntityType(r.EventType, r.AggregateType);
            var entityId = r.AggregateId ?? r.Id;
            var title = ExtractTitle(r.EventType, r.PayloadJson);

            return new ActivityItemDto(
                Id: r.Id,
                EventId: r.EventId,
                EventType: r.EventType,
                UserId: r.UserId!.Value,
                EntityType: entityType,
                EntityId: entityId,
                Title: title,
                Timestamp: r.OccurredAt,
                LoggedAt: r.LoggedAt,
                PayloadVersion: r.PayloadVersion
            );
        }).ToList();

        return new GetActivityFeedResult(items, items.Count);
    }

    /// <summary>
    /// Maps the stable <paramref name="eventType"/> alias to a clean entity type name.
    /// Falls back to <paramref name="aggregateType"/> (raw domain type) when unknown.
    /// </summary>
    private static string MapEntityType(string eventType, string? aggregateType)
    {
        return eventType switch
        {
            "agent.created" => "Agent",
            "chat.session.created" => "ChatSession",
            "kb.doc.indexed" => "PdfDocument",
            "session.created" or "session.finalized" => "Session",
            "library.entry.removed" or "library.session.recorded" => "UserLibraryEntry",
            _ => aggregateType ?? eventType,
        };
    }

    /// <summary>
    /// Best-effort title extraction from the camelCase JSON payload.
    /// Returns null when the field is absent or the payload cannot be parsed.
    /// </summary>
    private static string? ExtractTitle(string eventType, string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            switch (eventType)
            {
                case "agent.created":
                    return TryGetString(root, "agentName");

                case "chat.session.created":
                    return TryGetString(root, "agentName") ?? TryGetString(root, "gameName");

                case "kb.doc.indexed":
                    return TryGetString(root, "fileName");

                case "session.created":
                case "session.finalized":
                    return TryGetString(root, "gameName");

                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}
