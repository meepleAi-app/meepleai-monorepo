using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal partial class UploadPdfCommandHandler
{
    private static async Task<(bool IsValid, string? ErrorMessage)> ValidatePdfStructureAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        const int headerCheckBytes = 1024; // Read first 1KB to find PDF header
        const int trailerCheckBytes = 1024; // Read last 1KB to find PDF trailer

        try
        {
            // Check minimum file size (PDF must have at least header + trailer)
            if (stream.Length < 50)
            {
                return (false, "Invalid PDF file: File is too small to be a valid PDF (minimum 50 bytes required).");
            }

            // Read beginning of file for PDF header
            stream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[Math.Min(headerCheckBytes, (int)stream.Length)];
            var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), cancellationToken).ConfigureAwait(false);

            // Check for PDF header signature (%PDF-1.x)
            var headerText = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, Math.Min(10, headerBytesRead));
            if (!headerText.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return (false, $"Invalid PDF file: Missing PDF header signature. File appears to be corrupted or not a valid PDF.");
            }

            // Read end of file for PDF trailer
            var trailerStart = Math.Max(0, stream.Length - trailerCheckBytes);
            stream.Seek(trailerStart, SeekOrigin.Begin);
            var trailerBuffer = new byte[Math.Min(trailerCheckBytes, (int)(stream.Length - trailerStart))];
            var trailerBytesRead = await stream.ReadAsync(trailerBuffer.AsMemory(0, trailerBuffer.Length), cancellationToken).ConfigureAwait(false);

            // Check for PDF EOF marker (%%EOF)
            var trailerText = System.Text.Encoding.ASCII.GetString(trailerBuffer, 0, trailerBytesRead);
            if (!trailerText.Contains("%%EOF", StringComparison.Ordinal))
            {
                return (false, $"Invalid PDF file: Missing PDF end-of-file marker (%%EOF). File appears to be incomplete or malformed.");
            }

            // Reset stream position for subsequent operations
            stream.Seek(0, SeekOrigin.Begin);

            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Reset stream position even on error
            try { stream.Seek(0, SeekOrigin.Begin); } catch { /* Ignore seek errors */ }
            return (false, $"Failed to validate PDF structure: {ex.Message}");
        }
    }

    /// <summary>
    /// Best-effort enqueue into the Quartz-based processing queue.
    /// If the fire-and-forget Task.Run fails silently, the Quartz job acts as a reliable fallback.
    /// Conflicts (PDF already queued) are expected and silently ignored.
    /// </summary>
    private async Task EnqueueForProcessingSafelyAsync(Guid pdfDocumentId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(
                new Queue.EnqueuePdfCommand(pdfDocumentId, userId, Priority: (int)ProcessingPriority.Normal),
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PDF {PdfId} enqueued for Quartz processing as fallback", pdfDocumentId);
        }
#pragma warning disable CA1031 // Best-effort enqueue — conflicts and cancellations are expected
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not enqueue PDF {PdfId} for Quartz processing (may already be queued or request cancelled)", pdfDocumentId);
        }
#pragma warning restore CA1031
    }

    private async Task InvalidateCacheSafelyAsync(string gameId, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await _cacheService.InvalidateGameAsync(gameId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Cache invalidation is best-effort optimization;
        // failures must not interrupt PDF processing workflow.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
    }

    /// <summary>
    /// BGAI-043: Records PDF upload metrics in fire-and-forget pattern
    /// </summary>
    /// <summary>
    /// Auto-creates an EntityLink (Game → KbCard) for the uploaded PDF.
    /// Issue #5187: Idempotent — silently swallows DuplicateEntityLinkException on retry uploads.
    /// Only creates the link for regular game uploads (not private games, identified by gameId != Guid.Empty).
    /// </summary>
    private async Task CreateKbCardEntityLinkSafelyAsync(
        Guid pdfDocumentId,
        Guid gameId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty)
            return;

        try
        {
            var cmd = new CreateEntityLinkCommand(
                SourceEntityType: MeepleEntityType.Game,
                SourceEntityId: gameId,
                TargetEntityType: MeepleEntityType.KbCard,
                TargetEntityId: pdfDocumentId,
                LinkType: EntityLinkType.RelatedTo,
                Scope: EntityLinkScope.User,
                OwnerUserId: ownerUserId
            );

            await _mediator.Send(cmd, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "EntityLink Game/{GameId} → KbCard/{PdfId} created for user {UserId}",
                gameId, pdfDocumentId, ownerUserId);
        }
        catch (DuplicateEntityLinkException ex)
        {
            // Idempotent: link already exists (e.g., retry upload). This is expected.
            _logger.LogDebug(
                ex,
                "EntityLink Game/{GameId} → KbCard/{PdfId} already exists — skipping",
                gameId, pdfDocumentId);
        }
        catch (Exception ex)
        {
            // Non-critical: log but do not fail the upload
            _logger.LogWarning(
                ex,
                "Failed to create EntityLink for PDF {PdfId} → Game {GameId}. Upload still succeeded.",
                pdfDocumentId, gameId);
        }
    }

    /// <summary>
    /// Maps a PdfProcessingState string value to a progress percentage (Issue #5186).
    /// Mirrors PdfDocument.CalculateProgressPercentage() for entity-level mapping.
    /// </summary>
    private static int MapEntityStateToProgress(string state) => state switch
    {
        "Uploading" => 10,
        "Extracting" => 30,
        "Chunking" => 50,
        "Embedding" => 70,
        "Indexing" => 90,
        "Ready" => 100,
        _ => 0 // Pending, Failed
    };

    /// <summary>
    /// Computes SHA-256 hash of the file content for deduplication.
    /// </summary>
    private static async Task<string> ComputeContentHashAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hashBytes);
    }

    private void RecordUploadMetricSafely(string status, long? fileSizeBytes)
    {
        _ = Task.Run(() =>
        {
            try
            {
                MeepleAiMetrics.RecordPdfUploadAttempt(status, fileSizeBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record PDF upload metric for status {Status}", status);
            }
        });
    }
}

// Helper class for document chunk input
internal class DocumentChunkInput
{
    public required string Text { get; init; }
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}
