using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for CompleteChunkedUploadCommand.
/// Assembles chunks into a complete file and triggers PDF processing.
/// </summary>
public class CompleteChunkedUploadCommandHandler : ICommandHandler<CompleteChunkedUploadCommand, CompleteChunkedUploadResult>
{
    private static readonly string UploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly ILogger<CompleteChunkedUploadCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfTableExtractor _tableExtractor;

    public CompleteChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorageService,
        IBackgroundTaskService backgroundTaskService,
        ILogger<CompleteChunkedUploadCommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        IPdfTextExtractor pdfTextExtractor,
        IPdfTableExtractor tableExtractor,
        TimeProvider? timeProvider = null)
    {
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _backgroundTaskService = backgroundTaskService;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _pdfTextExtractor = pdfTextExtractor;
        _tableExtractor = tableExtractor;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CompleteChunkedUploadResult> Handle(
        CompleteChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get session
            var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

            if (session == null)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Upload session not found",
                    MissingChunks: null
                );
            }

            // Verify ownership
            if (session.UserId != request.UserId)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Access denied",
                    MissingChunks: null
                );
            }

            // Validate TempDirectory to prevent path traversal attacks
            if (!IsValidTempDirectory(session.TempDirectory))
            {
                _logger.LogWarning(
                    "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                    request.SessionId, session.TempDirectory);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Invalid upload session",
                    MissingChunks: null
                );
            }

            // Check session status
            if (session.IsExpired)
            {
                session.MarkAsExpired();
                await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Cleanup temp files
                await CleanupTempDirectoryAsync(session.TempDirectory).ConfigureAwait(false);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: null,
                    ErrorMessage: "Upload session has expired",
                    MissingChunks: null
                );
            }

            if (string.Equals(session.Status, "completed", StringComparison.Ordinal))
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: "Upload session is already completed",
                    MissingChunks: null
                );
            }

            // Check if all chunks received
            if (!session.IsComplete)
            {
                var missingChunks = session.GetMissingChunks();
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: $"Upload incomplete. Missing {missingChunks.Count} chunk(s).",
                    MissingChunks: missingChunks
                );
            }

            // Mark as assembling
            session.MarkAsAssembling();
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Assembling chunked upload {SessionId} ({TotalChunks} chunks, {TotalSize} bytes)",
                request.SessionId, session.TotalChunks, session.TotalFileSize);

            // Sanitize filename to prevent path traversal attacks
            var sanitizedFileName = PathSecurity.SanitizeFilename(session.FileName);

            // Assemble chunks into a single file
            var assembledFilePath = Path.Combine(session.TempDirectory, sanitizedFileName);
            await AssembleChunksAsync(session, assembledFilePath, cancellationToken).ConfigureAwait(false);

            // Store in blob storage (using already sanitized filename)

            BlobStorageResult storageResult;
            var assembledStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (assembledStream.ConfigureAwait(false))
            {
                storageResult = await _blobStorageService.StoreAsync(
                    assembledStream,
                    sanitizedFileName,
                    session.GameId.ToString(),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!storageResult.Success)
            {
                session.MarkAsFailed(storageResult.ErrorMessage ?? "Failed to store assembled file");
                await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: storageResult.ErrorMessage ?? "Failed to store assembled file",
                    MissingChunks: null
                );
            }

            // Create PDF document record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = Guid.Parse(storageResult.FileId!),
                GameId = session.GameId,
                FileName = sanitizedFileName,
                FilePath = storageResult.FilePath!,
                FileSizeBytes = storageResult.FileSizeBytes,
                ContentType = "application/pdf",
                UploadedByUserId = session.UserId,
                UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
                ProcessingStatus = "pending"
            };

            _dbContext.PdfDocuments.Add(pdfDoc);

            // Mark session as completed
            session.MarkAsCompleted();
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Chunked upload {SessionId} completed. Document {DocumentId} created.",
                request.SessionId, pdfDoc.Id);

            // Cleanup temp files (async, non-blocking) via background service
            var tempDirToClean = session.TempDirectory;
            _backgroundTaskService.ExecuteWithCancellation(
                $"cleanup_{request.SessionId}",
                async (ct) =>
                {
                    await Task.Delay(100, ct).ConfigureAwait(false); // Small delay to ensure file handles are released
                    await CleanupTempDirectoryAsync(tempDirToClean).ConfigureAwait(false);
                });

            // Trigger background processing (same as regular upload)
            _backgroundTaskService.ExecuteWithCancellation(
                storageResult.FileId!,
                (ct) => TriggerPdfProcessingAsync(storageResult.FileId!, storageResult.FilePath!, ct));

            return new CompleteChunkedUploadResult(
                Success: true,
                DocumentId: pdfDoc.Id,
                FileName: pdfDoc.FileName,
                ErrorMessage: null,
                MissingChunks: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete chunked upload {SessionId}", request.SessionId);

            return new CompleteChunkedUploadResult(
                Success: false,
                DocumentId: null,
                FileName: null,
                ErrorMessage: "Failed to complete upload",
                MissingChunks: null
            );
        }
    }

    /// <summary>
    /// Assembles all chunk files into a single file.
    /// </summary>
    private async Task AssembleChunksAsync(
        ChunkedUploadSession session,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var outputStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920);
        await using (outputStream.ConfigureAwait(false))
        // 80KB buffer for efficiency
        {
            for (int i = 0; i < session.TotalChunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkPath = session.GetChunkFilePath(i);

                if (!File.Exists(chunkPath))
                {
                    throw new InvalidOperationException($"Chunk {i} file not found: {chunkPath}");
                }

                var chunkStream = new FileStream(
                    chunkPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                await using (chunkStream.ConfigureAwait(false))
                {
                    await chunkStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogDebug("Assembled chunk {ChunkIndex}/{TotalChunks}", i + 1, session.TotalChunks);
            }

            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Assembled {TotalChunks} chunks into {OutputPath} ({Size} bytes)",
                session.TotalChunks, outputPath, new FileInfo(outputPath).Length);
        }
    }

    /// <summary>
    /// Cleans up the temporary directory after upload completion or failure.
    /// </summary>
    private Task CleanupTempDirectoryAsync(string tempDirectory)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
                _logger.LogDebug("Cleaned up temp directory: {TempDirectory}", tempDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDirectory}", tempDirectory);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Triggers PDF processing for chunked uploads.
    /// This mirrors the ProcessPdfAsync logic from UploadPdfCommandHandler.
    /// </summary>
    private async Task TriggerPdfProcessingAsync(string pdfId, string filePath, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var startTime = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            if (!Guid.TryParse(pdfId, out var pdfGuid))
            {
                _logger.LogError("Invalid PDF ID format {PdfId}", pdfId);
                return;
            }

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, ct).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
                return;
            }

            _logger.LogInformation("Starting PDF processing for chunked upload: {PdfId}", pdfId);

            // Step 1: Extract text
            var extractionStopwatch = Stopwatch.StartNew();
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (fileStream.ConfigureAwait(false))
            {
                var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, ct).ConfigureAwait(false);
                extractionStopwatch.Stop();

                _logger.LogDebug("Extraction completed in {ElapsedMs}ms for {PdfId}", extractionStopwatch.ElapsedMilliseconds, pdfId);

                if (!extractResult.Success)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = extractResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    _logger.LogError("Text extraction failed for {PdfId}: {Error}", pdfId, extractResult.ErrorMessage);
                    return;
                }

                // Combine all page chunks into full text
                var fullText = string.Join("\n\n", extractResult.PageChunks
                    .Where(pc => !pc.IsEmpty)
                    .Select(pc => pc.Text));

                pdfDoc.ExtractedText = fullText;
                pdfDoc.PageCount = extractResult.TotalPages;
                pdfDoc.CharacterCount = extractResult.TotalCharacters;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Extract structured content (tables, diagrams)
                var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
                if (tableExtractor != null)
                {
                    var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, ct).ConfigureAwait(false);
                    if (structuredResult.Success)
                    {
                        pdfDoc.ExtractedTables = System.Text.Json.JsonSerializer.Serialize(structuredResult.Tables);
                        pdfDoc.ExtractedDiagrams = System.Text.Json.JsonSerializer.Serialize(
                            structuredResult.Diagrams.Select(d => new
                            {
                                d.PageNumber,
                                d.DiagramType,
                                d.Description,
                                d.Width,
                                d.Height
                            }));
                        pdfDoc.AtomicRules = System.Text.Json.JsonSerializer.Serialize(structuredResult.AtomicRules);
                        pdfDoc.TableCount = structuredResult.TableCount;
                        pdfDoc.DiagramCount = structuredResult.DiagramCount;
                        pdfDoc.AtomicRuleCount = structuredResult.AtomicRuleCount;
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }

                var totalPages = extractResult.TotalPages;

                // Step 2: Chunk text
                var chunkingStopwatch = Stopwatch.StartNew();
                var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
                const int chunkSize = 512;
                const int chunkOverlap = 50;

                var allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
                    ?.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
                    .Select(chunk => new DocumentChunkInput
                    {
                        Text = chunk.Text,
                        Page = chunk.Page,
                        CharStart = chunk.CharStart,
                        CharEnd = chunk.CharEnd
                    })
                    .ToList()
                    ?? new List<DocumentChunkInput>();

                if (allDocumentChunks.Count == 0)
                {
                    foreach (var pageChunk in extractResult.PageChunks.Where(pc => !pc.IsEmpty))
                    {
                        var pageTextChunks = chunkingService.ChunkText(pageChunk.Text, chunkSize, chunkOverlap);

                        foreach (var textChunk in pageTextChunks.Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                        {
                            allDocumentChunks.Add(new DocumentChunkInput
                            {
                                Text = textChunk.Text,
                                Page = pageChunk.PageNumber,
                                CharStart = textChunk.CharStart,
                                CharEnd = textChunk.CharEnd
                            });
                        }
                    }
                }

                allDocumentChunks = allDocumentChunks
                    .Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
                    .ToList();

                chunkingStopwatch.Stop();
                _logger.LogDebug("Chunking completed in {ElapsedMs}ms, {ChunkCount} chunks for {PdfId}",
                    chunkingStopwatch.ElapsedMilliseconds, allDocumentChunks.Count, pdfId);

                // Step 3: Generate embeddings
                var embeddingStopwatch = Stopwatch.StartNew();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var texts = allDocumentChunks.Select(c => c.Text).ToList();
                var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts).ConfigureAwait(false);
                embeddingStopwatch.Stop();

                if (!embeddingResult.Success)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = embeddingResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    _logger.LogError("Embedding generation failed for {PdfId}: {Error}", pdfId, embeddingResult.ErrorMessage);
                    return;
                }

                var embeddings = embeddingResult.Embeddings ?? new List<float[]>();

                if (embeddings.Count != allDocumentChunks.Count)
                {
                    var mismatchMessage = $"Embedding service returned {embeddings.Count} vectors for {allDocumentChunks.Count} chunks";
                    _logger.LogWarning("Embedding count mismatch for PDF {PdfId}: {Message}", pdfId, mismatchMessage);
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = mismatchMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                // Step 4: Index in Qdrant
                var indexingStopwatch = Stopwatch.StartNew();
                var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

                var documentChunks = new List<DocumentChunk>();
                for (int i = 0; i < allDocumentChunks.Count; i++)
                {
                    documentChunks.Add(new DocumentChunk
                    {
                        Text = allDocumentChunks[i].Text,
                        Embedding = embeddings[i],
                        Page = allDocumentChunks[i].Page,
                        CharStart = allDocumentChunks[i].CharStart,
                        CharEnd = allDocumentChunks[i].CharEnd
                    });
                }

                var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId.ToString(), pdfId, documentChunks).ConfigureAwait(false);
                indexingStopwatch.Stop();

                if (!indexResult.Success)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = indexResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    _logger.LogError("Qdrant indexing failed for {PdfId}: {Error}", pdfId, indexResult.ErrorMessage);
                    return;
                }

                // Update vector document
                var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, ct).ConfigureAwait(false);
                if (vectorDoc == null)
                {
                    vectorDoc = new VectorDocumentEntity
                    {
                        Id = Guid.NewGuid(),
                        GameId = pdfDoc.GameId,
                        PdfDocumentId = pdfGuid,
                        IndexingStatus = "completed",
                        ChunkCount = indexResult.IndexedCount,
                        TotalCharacters = fullText.Length,
                        IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
                    };
                    db.VectorDocuments.Add(vectorDoc);
                }
                else
                {
                    vectorDoc.IndexingStatus = "completed";
                    vectorDoc.ChunkCount = indexResult.IndexedCount;
                    vectorDoc.TotalCharacters = fullText.Length;
                    vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
                }

                // Save text chunks to PostgreSQL for hybrid search (FTS)
                var existingChunks = await db.TextChunks
                    .Where(tc => tc.PdfDocumentId == pdfGuid)
                    .ToListAsync(ct).ConfigureAwait(false);
                if (existingChunks.Count > 0)
                {
                    db.TextChunks.RemoveRange(existingChunks);
                }

                var textChunkEntities = new List<TextChunkEntity>();
                for (int i = 0; i < allDocumentChunks.Count; i++)
                {
                    textChunkEntities.Add(new TextChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        GameId = pdfDoc.GameId,
                        PdfDocumentId = pdfGuid,
                        ChunkIndex = i,
                        PageNumber = allDocumentChunks[i].Page,
                        Content = allDocumentChunks[i].Text,
                        CharacterCount = allDocumentChunks[i].Text.Length,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                    });
                }

                db.TextChunks.AddRange(textChunkEntities);

                // Mark as completed
                pdfDoc.ProcessingStatus = "completed";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                var totalTime = (_timeProvider.GetUtcNow().UtcDateTime - startTime).TotalSeconds;
                _logger.LogInformation(
                    "PDF processing completed for chunked upload {PdfId}: {TotalPages} pages, {ChunkCount} chunks, {TotalSeconds}s",
                    pdfId, totalPages, allDocumentChunks.Count, totalTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process PDF {PdfId}", pdfId);

            try
            {
                if (Guid.TryParse(pdfId, out var pdfGuid))
                {
                    var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, ct).ConfigureAwait(false);
                    if (pdfDoc != null)
                    {
                        pdfDoc.ProcessingStatus = "failed";
                        pdfDoc.ProcessingError = ex.Message;
                        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to update PDF status after processing error for {PdfId}", pdfId);
            }
        }
    }

    /// <summary>
    /// Validates that the TempDirectory is within the expected upload base path
    /// and doesn't contain path traversal sequences.
    /// </summary>
    private static bool IsValidTempDirectory(string tempDirectory)
    {
        if (string.IsNullOrWhiteSpace(tempDirectory))
            return false;

        // Check for path traversal sequences
        if (tempDirectory.Contains("..") || tempDirectory.Contains("./"))
            return false;

        // Get the full path to resolve any relative components
        try
        {
            var fullPath = Path.GetFullPath(tempDirectory);
            var basePath = Path.GetFullPath(UploadTempBasePath);

            // Ensure the temp directory is under the base path
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If path resolution fails, consider it invalid
            return false;
        }
    }
}
