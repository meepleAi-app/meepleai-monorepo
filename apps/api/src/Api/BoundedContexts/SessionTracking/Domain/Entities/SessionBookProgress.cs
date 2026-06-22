using System.Text.Encodings.Web;
using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Per-campaign, per-book progress marker for the gamebook companion.
///
/// Task C1 (Gamebook multi-book generalization, spec 2026-05-19): generalises the
/// hardcoded "Press Start + Rules" tuple previously embedded in
/// <see cref="GamebookCampaignSession"/> to support an arbitrary number of books
/// per campaign, indexed by the new <c>GameBook</c> aggregate (Phase A).
///
/// One row per (campaign, book) pair. <c>HistoryJson</c> stores the ordered list
/// of visited locations (paragraph IDs, section refs, etc.) as a JSON array;
/// <c>LastLocation</c> mirrors the most recent entry for fast read access.
/// </summary>
public sealed class SessionBookProgress
{
    // Preserve non-ASCII paragraph markers (e.g. "§147") in the JSONB payload instead of escaping
    // them to "§147". JSONB stores Unicode natively, so unescaped UTF-8 is simpler to query
    // and read in pgAdmin / log dumps. UnsafeRelaxedJsonEscaping is safe here because the value
    // is persisted to a JSONB column, not embedded into HTML / JS contexts.
    private static readonly JsonSerializerOptions HistorySerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Guid Id { get; private set; }
    public Guid CampaignSessionId { get; private set; }
    public Guid GameBookId { get; private set; }
    public string LastLocation { get; private set; } = default!;
    public string HistoryJson { get; private set; } = "[]";
    public DateTimeOffset LastVisitedAt { get; private set; }
    public string? NotesJson { get; private set; }

    private SessionBookProgress() { }

    public static SessionBookProgress Create(Guid campaignSessionId, Guid gameBookId, string initialLocation)
    {
        if (campaignSessionId == Guid.Empty)
        {
            throw new ArgumentException("required", nameof(campaignSessionId));
        }

        if (gameBookId == Guid.Empty)
        {
            throw new ArgumentException("required", nameof(gameBookId));
        }

        if (string.IsNullOrWhiteSpace(initialLocation))
        {
            throw new ArgumentException("required", nameof(initialLocation));
        }

        var trimmed = initialLocation.Trim();
        return new SessionBookProgress
        {
            Id = Guid.NewGuid(),
            CampaignSessionId = campaignSessionId,
            GameBookId = gameBookId,
            LastLocation = trimmed,
            HistoryJson = JsonSerializer.Serialize(new[] { trimmed }, HistorySerializerOptions),
            LastVisitedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateLocation(string newLocation)
    {
        if (string.IsNullOrWhiteSpace(newLocation))
        {
            throw new ArgumentException("required", nameof(newLocation));
        }

        var trimmed = newLocation.Trim();
        var history = JsonSerializer.Deserialize<List<string>>(HistoryJson) ?? new List<string>();
        if (history.Count == 0 || !string.Equals(history[^1], trimmed, StringComparison.Ordinal))
        {
            history.Add(trimmed);
        }

        LastLocation = trimmed;
        HistoryJson = JsonSerializer.Serialize(history, HistorySerializerOptions);
        LastVisitedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateNotes(string? notesJson)
    {
        NotesJson = notesJson;
        LastVisitedAt = DateTimeOffset.UtcNow;
    }
}
