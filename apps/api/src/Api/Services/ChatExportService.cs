using System.Text.RegularExpressions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Orchestrates chat export operations using pluggable formatter strategies.
/// </summary>
public class ChatExportService : IChatExportService
{
    private readonly MeepleAiDbContext _db;
    private readonly IEnumerable<IExportFormatter> _formatters;
    private readonly ILogger<ChatExportService> _logger;

    public ChatExportService(
        MeepleAiDbContext db,
        IEnumerable<IExportFormatter> formatters,
        ILogger<ChatExportService> logger)
    {
        _db = db;
        _formatters = formatters;
        _logger = logger;
    }

    public async Task<ExportResult> ExportChatAsync(
        Guid chatId,
        string userId,
        string format,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        try
        {
            // Step 1: Retrieve chat and validate ownership
            var chat = await _db.Chats
                .Include(c => c.Game)
                .Include(c => c.Logs)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId, ct);

            if (chat == null)
            {
                _logger.LogWarning("Chat {ChatId} not found or user {UserId} does not have access", chatId, userId);
                return ExportResult.NotFound();
            }

            // Step 2: Select appropriate formatter (case-insensitive)
            var formatter = _formatters.FirstOrDefault(f =>
                f.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

            if (formatter == null)
            {
                _logger.LogWarning("Unsupported export format requested: {Format}", format);
                return ExportResult.UnsupportedFormat(format);
            }

            // Step 3: Generate export (formatter handles date filtering)
            var stream = await formatter.FormatAsync(chat, dateFrom, dateTo);

            // Step 4: Generate safe filename
            var filename = GenerateSafeFilename(chat.Game.Name, formatter.FileExtension, chatId);

            _logger.LogInformation(
                "Successfully exported chat {ChatId} for user {UserId} in {Format} format",
                chatId, userId, format);

            return ExportResult.SuccessResult(stream, formatter.ContentType, filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export chat {ChatId} for user {UserId} in {Format} format",
                chatId, userId, format);
            return ExportResult.GenerationFailed($"Export generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a safe filename for the export, removing dangerous characters.
    /// Format: {game-name}-chat-{chatId-short}.{extension}
    /// </summary>
    private string GenerateSafeFilename(string gameName, string extension, Guid chatId)
    {
        // Remove/replace dangerous characters
        var safeName = gameName
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("..", "")
            .Replace("\r", "")
            .Replace("\n", "");

        // Remove control characters (ASCII 0-31 and 127)
        safeName = Regex.Replace(safeName, @"[\x00-\x1F\x7F]", "");

        // Trim and limit length
        safeName = safeName.Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "chat"; // Fallback for empty/invalid game names
        }
        safeName = safeName.Substring(0, Math.Min(safeName.Length, 50));

        // Use short form of chatId (first 8 chars) for readability
        var shortChatId = chatId.ToString("N").Substring(0, 8);

        return $"{safeName}-chat-{shortChatId}.{extension}";
    }
}
