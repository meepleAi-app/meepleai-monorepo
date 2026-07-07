using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// A per-campaign EN→IT glossary entry used to apply consistent term translations.
/// Can be auto-bootstrapped from existing translations or created/edited manually.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// C6 (2026-05-19): <see cref="FirstSeenBookId"/> records the book where a term
/// first appeared (nullable — may be unknown when bootstrapped or created manually).
/// #2638 / SI-7 (2026-07-07): <see cref="Contexts"/> persists ALL books/paragraphs
/// where a term appears (multi-context), stored as a JSONB array. <see cref="FirstSeenBookId"/>
/// is retained as the backward-compat pointer to the first context.
/// </summary>
public sealed class GamebookGlossaryEntry
{
    // Preserve non-ASCII paragraph markers (e.g. "§147") in the JSONB payload instead of escaping
    // them. JSONB stores Unicode natively, so unescaped UTF-8 is simpler to query and read.
    // UnsafeRelaxedJsonEscaping is safe here because the value is persisted to a JSONB column,
    // not embedded into an HTML / JS context. Mirrors SessionBookProgress.
    private static readonly JsonSerializerOptions ContextsSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string TermEn { get; private set; } = default!;
    public string TermIt { get; private set; } = default!;
    public GlossarySource Source { get; private set; }
    public Guid? FirstSeenBookId { get; private set; }

    /// <summary>
    /// Raw JSONB payload of the multi-context list. Backed by the <c>contexts</c> column.
    /// Prefer the <see cref="Contexts"/> projection for reads and the mutators
    /// (<see cref="AddContext"/> / <see cref="RemoveContext"/> / <see cref="ReplaceContexts"/>)
    /// for writes.
    /// </summary>
    public string ContextsJson { get; private set; } = "[]";

    /// <summary>Deserialized view of <see cref="ContextsJson"/>. Not mapped (see config Ignore).</summary>
    [JsonIgnore]
    public IReadOnlyList<GlossaryContext> Contexts =>
        JsonSerializer.Deserialize<List<GlossaryContext>>(ContextsJson) ?? new List<GlossaryContext>();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // EF parameterless constructor
    private GamebookGlossaryEntry() { }

    public static GamebookGlossaryEntry Create(
        Guid campaignId,
        string termEn,
        string termIt,
        GlossarySource source,
        Guid createdBy,
        Guid? firstSeenBookId = null,
        IEnumerable<GlossaryContext>? contexts = null)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("campaignId required", nameof(campaignId));
        if (string.IsNullOrWhiteSpace(termEn))
            throw new ArgumentException("termEn required", nameof(termEn));
        if (string.IsNullOrWhiteSpace(termIt))
            throw new ArgumentException("termIt required", nameof(termIt));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("createdBy required", nameof(createdBy));
        if (firstSeenBookId.HasValue && firstSeenBookId.Value == Guid.Empty)
            throw new ArgumentException("firstSeenBookId cannot be Guid.Empty when set", nameof(firstSeenBookId));

        string contextsJson;
        if (contexts is not null)
        {
            contextsJson = SerializeContexts(Dedup(contexts));
        }
        else if (firstSeenBookId.HasValue)
        {
            contextsJson = SerializeContexts(new[] { new GlossaryContext(firstSeenBookId.Value, null, null) });
        }
        else
        {
            contextsJson = "[]";
        }

        var now = DateTimeOffset.UtcNow;
        return new GamebookGlossaryEntry
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            TermEn = termEn.Trim(),
            TermIt = termIt.Trim(),
            Source = source,
            FirstSeenBookId = firstSeenBookId,
            ContextsJson = contextsJson,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
        };
    }

    /// <summary>
    /// Updates the Italian translation, flipping Source to Manual and stamping the audit fields.
    /// </summary>
    public void UpdateTermIt(string newValue, Guid editedBy)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("newValue required", nameof(newValue));
        if (editedBy == Guid.Empty)
            throw new ArgumentException("editedBy required", nameof(editedBy));

        TermIt = newValue.Trim();
        Source = GlossarySource.Manual;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = editedBy;
    }

    /// <summary>
    /// Adds a context if no context with the same <c>(BookId, ParagraphRef)</c>
    /// (case-insensitive on ParagraphRef) already exists. Stamps the audit fields
    /// only when a context is actually appended.
    /// </summary>
    public void AddContext(GlossaryContext ctx, Guid editedBy)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (editedBy == Guid.Empty)
            throw new ArgumentException("editedBy required", nameof(editedBy));

        var list = Contexts.ToList();
        if (list.Any(c => SameKey(c, ctx)))
            return;

        list.Add(ctx);
        ContextsJson = SerializeContexts(list);
        Stamp(editedBy);
    }

    /// <summary>
    /// Removes any context matching <c>(BookId, ParagraphRef)</c> (case-insensitive on
    /// ParagraphRef). Stamps the audit fields only when at least one context is removed.
    /// </summary>
    public void RemoveContext(GlossaryContext ctx, Guid editedBy)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (editedBy == Guid.Empty)
            throw new ArgumentException("editedBy required", nameof(editedBy));

        var list = Contexts.ToList();
        var removed = list.RemoveAll(c => SameKey(c, ctx));
        if (removed == 0)
            return;

        ContextsJson = SerializeContexts(list);
        Stamp(editedBy);
    }

    /// <summary>
    /// Replaces the full context set (dedup by <c>(BookId, ParagraphRef)</c>,
    /// case-insensitive on ParagraphRef) and stamps the audit fields.
    /// </summary>
    public void ReplaceContexts(IEnumerable<GlossaryContext> ctxs, Guid editedBy)
    {
        ArgumentNullException.ThrowIfNull(ctxs);
        if (editedBy == Guid.Empty)
            throw new ArgumentException("editedBy required", nameof(editedBy));

        ContextsJson = SerializeContexts(Dedup(ctxs));
        Stamp(editedBy);
    }

    private void Stamp(Guid editedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = editedBy;
    }

    private static bool SameKey(GlossaryContext a, GlossaryContext b)
        => a.BookId == b.BookId
           && string.Equals(a.ParagraphRef, b.ParagraphRef, StringComparison.OrdinalIgnoreCase);

    private static List<GlossaryContext> Dedup(IEnumerable<GlossaryContext> ctxs)
    {
        var result = new List<GlossaryContext>();
        foreach (var c in ctxs)
        {
            if (!result.Any(existing => SameKey(existing, c)))
                result.Add(c);
        }
        return result;
    }

    private static string SerializeContexts(IEnumerable<GlossaryContext> ctxs)
        => JsonSerializer.Serialize(ctxs, ContextsSerializerOptions);
}
