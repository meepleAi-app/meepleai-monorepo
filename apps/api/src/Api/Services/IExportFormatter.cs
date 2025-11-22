using Api.Infrastructure.Entities;

namespace Api.Services;

/// <summary>
/// Strategy interface for exporting chat conversations in different formats.
/// Implementations provide format-specific generation logic.
/// </summary>
public interface IExportFormatter
{
    /// <summary>
    /// Format identifier (e.g., "txt", "md", "pdf"). Case-insensitive matching.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// MIME content type for HTTP response (e.g., "text/plain", "application/pdf").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// File extension without dot (e.g., "txt", "md", "pdf").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Generates an export stream from chat data with optional date filtering.
    /// </summary>
    /// <param name="chat">Chat entity with loaded Game and Logs navigation properties.</param>
    /// <param name="dateFrom">Optional: Filter messages from this date (inclusive).</param>
    /// <param name="dateTo">Optional: Filter messages to this date (inclusive).</param>
    /// <returns>Stream containing formatted export data. Caller must dispose.</returns>
    Task<Stream> FormatAsync(ChatEntity chat, DateTime? dateFrom, DateTime? dateTo);
}
