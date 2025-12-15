using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Orchestrates chat export operations using pluggable formatter strategies.
/// </summary>
internal class ChatExportService : IChatExportService
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
        Guid userId,
        string format,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        try
        {
            // Check for cancellation before starting
            ct.ThrowIfCancellationRequested();

            // Step 1: Retrieve chat and validate ownership
            var chat = await _db.Chats
                .AsNoTrackingWithIdentityResolution() // PERF-05: Read-only query with entity identity preservation for relationships
                .Include(c => c.Game)
                .Include(c => c.Logs)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId, ct).ConfigureAwait(false);

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
            var stream = await formatter.FormatAsync(chat, dateFrom, dateTo).ConfigureAwait(false);

            // Step 4: Generate safe filename
            var gameName = chat.Game?.Name ?? "Unknown Game";
            var filename = GenerateSafeFilename(gameName, formatter.FileExtension, chatId);

            _logger.LogInformation(
                "Successfully exported chat {ChatId} for user {UserId} in {Format} format",
                chatId, userId, format);

            return ExportResult.SuccessResult(stream, formatter.ContentType, filename);
        }
        catch (OperationCanceledException)
        {
            // Let cancellation exceptions bubble up (don't catch)
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - Returns domain result object (ExportResult) instead of throwing, multi-tier error handling for export operations
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Service layer: Catches all exceptions to return domain result object
            // Detailed error logged, returned as failure result for caller handling
            _logger.LogError(ex, "Failed to export chat {ChatId} for user {UserId} in {Format} format",
                chatId, userId, format);
            return ExportResult.GenerationFailed($"Export generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a safe filename for the export, removing dangerous characters.
    /// Format: {game-name}-chat-{chatId-short}.{extension}
    /// </summary>
    private static string GenerateSafeFilename(string gameName, string extension, Guid chatId)
    {
        // Use short form of chatId (first 8 chars) for readability
        var shortChatId = chatId.ToString("N").Substring(0, 8);

        // Generate safe filename using centralized helper
        return StringHelper.GenerateSafeFilename(
            gameName,
            extension,
            suffix: $"chat-{shortChatId}",
            maxLength: 100);
    }
}
