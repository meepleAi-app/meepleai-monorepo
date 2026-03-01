using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles GetKnowledgeBaseStatusQuery.
/// Queries the most recent PDF for a game and maps its processing state
/// to the EmbeddingStatus enum expected by the frontend RAG readiness indicator.
/// Issue #4065: RAG readiness polling
/// </summary>
internal class GetKnowledgeBaseStatusQueryHandler : IQueryHandler<GetKnowledgeBaseStatusQuery, KnowledgeBaseStatusDto?>
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKnowledgeBaseStatusQueryHandler> _logger;

    public GetKnowledgeBaseStatusQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetKnowledgeBaseStatusQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KnowledgeBaseStatusDto?> Handle(
        GetKnowledgeBaseStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            // Get game name for the response
            var gameName = await _dbContext.Games
                .Where(g => g.Id == query.GameId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            // Get the most recent PDF for the game (any processing state)
            var pdf = await _dbContext.PdfDocuments
                .Where(p => p.GameId == query.GameId)
                .OrderByDescending(p => p.UploadedAt)
                .AsNoTracking()
                .Select(p => new
                {
                    p.ProcessingState,
                    p.ProcessingProgressJson,
                    p.ProcessingError,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pdf is null)
            {
                // No PDFs yet for this game — return Pending
                return new KnowledgeBaseStatusDto(
                    Status: "Pending",
                    Progress: 0,
                    TotalChunks: 0,
                    ProcessedChunks: 0,
                    ErrorMessage: null,
                    GameName: gameName);
            }

            var (status, progress, totalChunks, processedChunks, errorMessage) =
                MapProcessingState(pdf.ProcessingState, pdf.ProcessingProgressJson, pdf.ProcessingError);

            return new KnowledgeBaseStatusDto(
                Status: status,
                Progress: progress,
                TotalChunks: totalChunks,
                ProcessedChunks: processedChunks,
                ErrorMessage: errorMessage,
                GameName: gameName);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving knowledge base status for game {GameId}", query.GameId);
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Maps the 7-state ProcessingState field to the 6-state EmbeddingStatus enum
    /// expected by the frontend knowledge base status schema.
    /// </summary>
    private static (string status, int progress, int totalChunks, int processedChunks, string? errorMessage)
        MapProcessingState(
            string processingState,
            string? processingProgressJson,
            string? processingError)
    {
        // Deserialize stored ProcessingProgress JSON if present
        ProcessingProgress? prog = null;
        if (!string.IsNullOrEmpty(processingProgressJson))
        {
            try
            {
                prog = JsonSerializer.Deserialize<ProcessingProgress>(
                    processingProgressJson,
                    _jsonOptions);
            }
            catch
            {
                // Ignore deserialization errors — fall back to defaults
            }
        }

        // ProcessingState values: Pending, Uploading, Extracting, Chunking, Embedding, Indexing, Ready, Failed
        // EmbeddingStatus values: Pending, Extracting, Chunking, Embedding, Completed, Failed
        return processingState switch
        {
            "Pending" => ("Pending", 0, 0, 0, null),
            "Uploading" => ("Extracting", prog?.PercentComplete ?? 10, 0, 0, null),
            "Extracting" => (
                "Extracting",
                prog?.PercentComplete ?? 25,
                prog?.TotalPages ?? 0,
                prog?.PagesProcessed ?? 0,
                null),
            "Chunking" => (
                "Chunking",
                prog?.PercentComplete ?? 50,
                0,
                0,
                null),
            "Embedding" => (
                "Embedding",
                prog?.PercentComplete ?? 70,
                prog?.TotalPages ?? 0,
                prog?.PagesProcessed ?? 0,
                null),
            "Indexing" => (
                "Embedding",
                prog?.PercentComplete ?? 90,
                0,
                0,
                null),
            "Ready" => ("Completed", 100, 0, 0, null),
            "Failed" => ("Failed", 0, 0, 0, processingError ?? prog?.ErrorMessage ?? "PDF processing failed"),
            // Legacy ProcessingStatus values (deprecated field, kept for safety)
            "completed" => ("Completed", 100, 0, 0, null),
            "processing" => ("Extracting", 30, 0, 0, null),
            "failed" => ("Failed", 0, 0, 0, processingError ?? "PDF processing failed"),
            _ => ("Pending", 0, 0, 0, null),
        };
    }
}
