using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for exporting chat conversations in various formats.
/// </summary>
public interface IChatExportService
{
    /// <summary>
    /// Exports a chat conversation to the specified format.
    /// </summary>
    /// <param name="chatId">ID of the chat to export.</param>
    /// <param name="userId">ID of the user requesting the export (for ownership validation).</param>
    /// <param name="format">Export format (txt, md, pdf). Case-insensitive.</param>
    /// <param name="dateFrom">Optional: Filter messages from this date (inclusive).</param>
    /// <param name="dateTo">Optional: Filter messages to this date (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Export result with stream or error details.</returns>
    Task<ExportResult> ExportChatAsync(
        Guid chatId,
        string userId,
        string format,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
