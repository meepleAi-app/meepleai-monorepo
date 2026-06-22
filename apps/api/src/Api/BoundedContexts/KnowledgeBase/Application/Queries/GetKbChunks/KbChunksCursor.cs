using System.Globalization;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Opaque base64 cursor utility for chunks list pagination.
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics</b>: <c>(Position, Id)</c> tuple — position is the primary
/// sort key (chunk_index ASC), Id is the tiebreaker for deterministic ordering
/// when chunks share a position (rare but possible in legacy data).
/// </para>
///
/// <para>
/// <b>Wire format</b>: base64-url of UTF8 <c>"{position}:{id-no-hyphens}"</c>.
/// We use <c>:N</c> (no hyphens) for compactness; <c>Guid.Parse</c> accepts both.
/// Malformed cursors (any decode/parse failure) → caller should respond 400.
/// </para>
/// </remarks>
internal static class KbChunksCursor
{
    public sealed record CursorPayload(int Position, Guid Id);

    public static string Encode(CursorPayload payload)
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{payload.Position}:{payload.Id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Decodes a cursor payload. Returns <c>null</c> for null/empty input.
    /// Throws <see cref="FormatException"/> on malformed input — caller maps
    /// to HTTP 400 Bad Request.
    /// </summary>
    public static CursorPayload? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(cursor);
        }
        catch (FormatException)
        {
            throw new FormatException($"Invalid cursor encoding: {cursor}");
        }

        var decoded = Encoding.UTF8.GetString(bytes);
        var parts = decoded.Split(':');
        if (parts.Length != 2)
        {
            throw new FormatException($"Cursor payload malformed: expected 'position:id', got '{decoded}'");
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
        {
            throw new FormatException($"Cursor position not parseable as int: '{parts[0]}'");
        }

        if (!Guid.TryParse(parts[1], out var id))
        {
            throw new FormatException($"Cursor id not parseable as Guid: '{parts[1]}'");
        }

        return new CursorPayload(position, id);
    }
}
