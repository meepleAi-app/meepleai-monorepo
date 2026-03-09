using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for CompleteChunkedUploadCommand.
/// Assembles chunks into a complete file and triggers PDF processing.
/// </summary>
internal class CompleteChunkedUploadCommandHandler : ICommandHandler<CompleteChunkedUploadCommand, CompleteChunkedUploadResult>
{
    private static readonly string UploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");
    internal const string DuplicateContentErrorMessage = "Un file identico è già stato caricato per questo gioco.";

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly ILogger<CompleteChunkedUploadCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfTableExtractor _tableExtractor;
    private readonly IMediator _mediator;

    public CompleteChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorageService,
        IBackgroundTaskService backgroundTaskService,
        ILogger<CompleteChunkedUploadCommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        IPdfTextExtractor pdfTextExtractor,
        IPdfTableExtractor tableExtractor,
        IMediator mediator,
        TimeProvider? timeProvider = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _tableExtractor = tableExtractor ?? throw new ArgumentNullException(nameof(tableExtractor));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CompleteChunkedUploadResult> Handle(
        CompleteChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            // Validate session and permissions
            var (validationSuccess, validationError, session) = await ValidateSessionAndPermissionsAsync(
                request.SessionId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (!validationSuccess)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session?.FileName,
                    ErrorMessage: validationError,
                    MissingChunks: session != null && !session.IsComplete ? session.GetMissingChunks() : null
                );
            }

            _logger.LogInformation(
                "Assembling chunked upload {SessionId} ({TotalChunks} chunks, {TotalSize} bytes)",
                request.SessionId, session!.TotalChunks, session.TotalFileSize);

            // Assemble chunks and store in blob storage
            (bool storageSuccess, BlobStorageResult? storageResult, string? sanitizedFileName, string? contentHash) = await AssembleAndStoreFileAsync(
                session, cancellationToken).ConfigureAwait(false);
            if (!storageSuccess)
            {
                return new CompleteChunkedUploadResult(
                    Success: false,
                    DocumentId: null,
                    FileName: session.FileName,
                    ErrorMessage: storageResult!.ErrorMessage ?? "Failed to store assembled file",
                    MissingChunks: null
                );
            }

            // Check for duplicate content on the same game
            if (contentHash != null)
            {
                var isDuplicate = await _dbContext.PdfDocuments.AnyAsync(
                    p => p.ContentHash == contentHash &&
                         ((session.GameId.HasValue && p.GameId == session.GameId) ||
                          (session.PrivateGameId.HasValue && p.PrivateGameId == session.PrivateGameId)),
                    cancellationToken).ConfigureAwait(false);

                if (isDuplicate)
                {
                    // Cleanup the stored file (handles both local and S3 storage)
                    if (storageResult!.FileId != null)
                    {
                        try
                        {
                            await _blobStorageService.DeleteAsync(
                                storageResult.FileId,
                                (session.PrivateGameId ?? session.GameId)?.ToString() ?? string.Empty,
                                cancellationToken).ConfigureAwait(false);
                        }
                        catch { /* best effort cleanup */ }
                    }

                    return new CompleteChunkedUploadResult(
                        Success: false,
                        DocumentId: null,
                        FileName: sanitizedFileName,
                        ErrorMessage: DuplicateContentErrorMessage,
                        MissingChunks: null
                    );
                }
            }

            // Create PDF document record and trigger background processing
            var pdfDocId = await CreatePdfRecordAndTriggerProcessingAsync(
                session, storageResult!, sanitizedFileName!, request.SessionId.ToString(), cancellationToken, contentHash).ConfigureAwait(false);

            return new CompleteChunkedUploadResult(
                Success: true,
                DocumentId: pdfDocId,
                FileName: sanitizedFileName,
                ErrorMessage: null,
                MissingChunks: null
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // COMMAND HANDLER PATTERN: CQRS handler boundary
        // Generic catch handles unexpected failures during chunked upload completion
        // (validation, assembly, storage, DB operations). Returns Result pattern with error.
        // Prevents exception propagation to API layer.
#pragma warning restore S125
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
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates upload session, permissions, and completeness.
    /// Returns (success, errorMessage, session).
    /// </summary>
    private async Task<(bool success, string? errorMessage, ChunkedUploadSession? uploadSession)> ValidateSessionAndPermissionsAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Get session
        var uploadSession = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (uploadSession == null)
        {
            return (false, "Upload session not found", null);
        }

        // Verify ownership
        if (uploadSession.UserId != userId)
        {
            return (false, "Access denied", uploadSession);
        }

        // Validate TempDirectory to prevent path traversal attacks
        if (!IsValidTempDirectory(uploadSession.TempDirectory))
        {
            _logger.LogWarning(
                "Invalid TempDirectory detected for session {SessionId}: {TempDirectory}",
                sessionId.ToString(), uploadSession.TempDirectory);

            return (false, "Invalid upload session", uploadSession);
        }

        // Check session status
        if (uploadSession.IsExpired)
        {
            uploadSession.MarkAsExpired();
            await _sessionRepository.UpdateAsync(uploadSession, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Cleanup temp files
            await CleanupTempDirectoryAsync(uploadSession.TempDirectory).ConfigureAwait(false);

            return (false, "Upload session has expired", uploadSession);
        }

        if (string.Equals(uploadSession.Status, "completed", StringComparison.Ordinal))
        {
            return (false, "Upload session already completed", uploadSession);
        }

        // Check if all chunks received
        if (!uploadSession.IsComplete)
        {
            var missingChunks = uploadSession.GetMissingChunks();
            return (false, $"Upload incomplete. Missing {missingChunks.Count} chunk(s).", uploadSession);
        }

        return (true, null, uploadSession);
    }

    /// <summary>
    /// Assembles chunks into a single file and stores in blob storage.
    /// Returns (success, storageResult, sanitizedFileName).
    /// </summary>
    private async Task<(bool success, BlobStorageResult? storageResult, string? sanitizedFileName, string? contentHash)> AssembleAndStoreFileAsync(
        ChunkedUploadSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Mark as assembling
        session.MarkAsAssembling();
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = PathSecurity.SanitizeFilename(session.FileName);

        // Assemble chunks into a single file
        var assembledFilePath = Path.Combine(session.TempDirectory, sanitizedFileName);
        await AssembleChunksAsync(session, assembledFilePath, cancellationToken).ConfigureAwait(false);

        // Compute content hash from assembled local file (before S3 upload)
        string? contentHash = null;
        if (File.Exists(assembledFilePath))
        {
            var hashStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (hashStream.ConfigureAwait(false))
            {
                var hashBytes = await SHA256.HashDataAsync(hashStream, cancellationToken).ConfigureAwait(false);
                contentHash = Convert.ToHexStringLower(hashBytes);
            }
        }

        // Store in blob storage
        BlobStorageResult storageResult;
        var assembledStream = new FileStream(assembledFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (assembledStream.ConfigureAwait(false))
        {
            storageResult = await _blobStorageService.StoreAsync(
                assembledStream,
                sanitizedFileName,
                (session.PrivateGameId ?? session.GameId)?.ToString() ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
        }

        if (!storageResult.Success)
        {
            session.MarkAsFailed(storageResult.ErrorMessage ?? "Failed to store assembled file");
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (false, storageResult, sanitizedFileName, null);
        }

        return (true, storageResult, sanitizedFileName, contentHash);
    }

    /// <summary>
    /// Creates PDF document record, marks session as completed, and triggers background processing.
    /// </summary>
    private async Task<Guid> CreatePdfRecordAndTriggerProcessingAsync(
        ChunkedUploadSession session,
        BlobStorageResult storageResult,
        string sanitizedFileName,
        string sessionId,
        CancellationToken cancellationToken,
        string? contentHash = null)
    {
        // Create PDF document record
        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.Parse(storageResult.FileId!),
            GameId = session.GameId,
            PrivateGameId = session.PrivateGameId,
            FileName = sanitizedFileName,
            FilePath = storageResult.FilePath!,
            FileSizeBytes = storageResult.FileSizeBytes,
            ContentType = "application/pdf",
            UploadedByUserId = session.UserId,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ProcessingStatus = "pending",
            ContentHash = contentHash
        };

        _dbContext.PdfDocuments.Add(pdfDoc);

        // Mark session as completed
        session.MarkAsCompleted();
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Chunked upload {SessionId} completed. Document {DocumentId} created.",
            sessionId, pdfDoc.Id);

        // Issue #5187: Auto-create EntityLink Game → KbCard for PDF-KB association (shared games only)
        await CreateKbCardEntityLinkSafelyAsync(pdfDoc.Id, session.GameId ?? Guid.Empty, session.UserId, cancellationToken).ConfigureAwait(false);

        // Cleanup temp files (async, non-blocking) via background service
        var tempDirToClean = session.TempDirectory;
        _backgroundTaskService.ExecuteWithCancellation(
            $"cleanup_{sessionId}",
            async (cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Small delay to ensure file handles are released
                await CleanupTempDirectoryAsync(tempDirToClean).ConfigureAwait(false);
            });

        // Trigger background processing (same as regular upload)
        _backgroundTaskService.ExecuteWithCancellation(
            storageResult.FileId!,
            (cancellationToken) => TriggerPdfProcessingAsync(storageResult.FileId!, storageResult.FilePath!, cancellationToken));

        return pdfDoc.Id;
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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Best-effort resource cleanup
        // Catches all exceptions during temp directory deletion (permissions, file locks, etc.).
        // Cleanup failure is non-critical; logs warning but doesn't throw to avoid disrupting
        // main operation flow. Fail-safe pattern for resource management.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDirectory}", tempDirectory);
        }
#pragma warning restore CA1031
        return Task.CompletedTask;
    }

    /// <summary>
    /// Triggers asynchronous PDF processing after chunked upload completion.
    /// Orchestrates extraction, chunking, embedding generation, and indexing.
    /// </summary>
    private async Task TriggerPdfProcessingAsync(string pdfId, string filePath, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var startTime = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            // Validate PDF document
            if (!Guid.TryParse(pdfId, out var pdfGuid))
            {
                _logger.LogError("Invalid PDF ID format {PdfId}", pdfId);
                return;
            }

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, cancellationToken).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
                return;
            }

            _logger.LogInformation("Starting PDF processing for chunked upload: {PdfId}", pdfId);

            // Step 1: Extract PDF text and structured content
            var (extractSuccess, fullText, totalPages) = await ExtractPdfTextAsync(
                pdfId, filePath, pdfDoc, db, scope, cancellationToken).ConfigureAwait(false);
            if (!extractSuccess) return;

            // Step 2: Chunk text for embedding
            var allDocumentChunks = await ChunkTextContentAsync(
                pdfId, fullText!, scope).ConfigureAwait(false);

            // Step 3: Generate embeddings
            var (embeddingsSuccess, embeddings) = await GenerateEmbeddingsAsync(
                pdfId, allDocumentChunks, pdfDoc, db, scope, cancellationToken).ConfigureAwait(false);
            if (!embeddingsSuccess) return;

            // Step 4: Index in vector store and save to PostgreSQL
            await IndexAndStoreChunksAsync(
                pdfId, pdfGuid, allDocumentChunks, embeddings!, pdfDoc, fullText!, db, scope, cancellationToken).ConfigureAwait(false);

            // Mark as completed
            pdfDoc.ProcessingStatus = "completed";
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var totalTime = (_timeProvider.GetUtcNow().UtcDateTime - startTime).TotalSeconds;
            _logger.LogInformation(
                "PDF processing completed for chunked upload {PdfId}: {TotalPages} pages, {ChunkCount} chunks, {TotalSeconds}s",
                pdfId, totalPages, allDocumentChunks.Count, totalTime);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND PROCESSING PATTERN: PDF processing failure recovery
        // Catches all exceptions during PDF text extraction, chunking, and embedding generation.
        // Delegates to HandleProcessingFailureAsync to update PDF status as "failed" in DB.
        // Background processing must handle errors gracefully without propagating to command handler.
#pragma warning restore S125
        catch (Exception ex)
        {
            await HandleProcessingFailureAsync(pdfId, db, ex, cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Extracts text and structured content from PDF file.
    /// </summary>
    private async Task<(bool success, string? fullText, int totalPages)> ExtractPdfTextAsync(
        string pdfId,
        string filePath,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var extractionStopwatch = Stopwatch.StartNew();
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (fileStream.ConfigureAwait(false))
        {
            var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, cancellationToken).ConfigureAwait(false);
            extractionStopwatch.Stop();

            _logger.LogDebug("Extraction completed in {ElapsedMs}ms for {PdfId}", extractionStopwatch.ElapsedMilliseconds, pdfId);

            if (!extractResult.Success)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = extractResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("Text extraction failed for {PdfId}: {Error}", pdfId, extractResult.ErrorMessage);
                return (false, null, 0);
            }

            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Extract structured content (tables, diagrams)
            await ExtractStructuredContentAsync(filePath, pdfDoc, db, scope, cancellationToken).ConfigureAwait(false);

            return (true, fullText, extractResult.TotalPages);
        }
    }

    /// <summary>
    /// Extracts structured content (tables, diagrams, atomic rules) from PDF.
    /// </summary>
    private async Task ExtractStructuredContentAsync(
        string filePath,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
        if (tableExtractor == null) return;

        var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!structuredResult.Success) return;

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
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chunks extracted text into document chunks for embedding.
    /// </summary>
    private async Task<List<DocumentChunkInput>> ChunkTextContentAsync(
        string pdfId,
        string fullText,
                IServiceScope scope
        )

    {
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

        allDocumentChunks = allDocumentChunks
            .Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
            .ToList();

        chunkingStopwatch.Stop();
        _logger.LogDebug("Chunking completed in {ElapsedMs}ms, {ChunkCount} chunks for {PdfId}",
            chunkingStopwatch.ElapsedMilliseconds, allDocumentChunks.Count, pdfId);

        return await Task.FromResult(allDocumentChunks).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates embeddings for document chunks.
    /// </summary>
    private async Task<(bool success, List<float[]>? embeddings)> GenerateEmbeddingsAsync(
        string pdfId,
        List<DocumentChunkInput> allDocumentChunks,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
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
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Embedding generation failed for {PdfId}: {Error}", pdfId, embeddingResult.ErrorMessage);
            return (false, null);
        }

        var embeddings = embeddingResult.Embeddings ?? new List<float[]>();

        if (embeddings.Count != allDocumentChunks.Count)
        {
            var mismatchMessage = $"Embedding service returned {embeddings.Count} vectors for {allDocumentChunks.Count} chunks";
            _logger.LogWarning("Embedding count mismatch for PDF {PdfId}: {Message}", pdfId, mismatchMessage);
            pdfDoc.ProcessingStatus = "failed";
            pdfDoc.ProcessingError = mismatchMessage;
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (false, null);
        }

        return (true, embeddings);
    }

    /// <summary>
    /// Indexes document chunks in Qdrant and saves text chunks to PostgreSQL for hybrid search.
    /// </summary>
    private async Task IndexAndStoreChunksAsync(
        string pdfId,
        Guid pdfGuid,
        List<DocumentChunkInput> allDocumentChunks,
        List<float[]> embeddings,
        PdfDocumentEntity pdfDoc,
        string fullText,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        // Index in Qdrant
        var indexingStopwatch = Stopwatch.StartNew();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

        var documentChunks = allDocumentChunks
            .Select((chunk, index) => new DocumentChunk
            {
                Text = chunk.Text,
                Embedding = embeddings[index],
                Page = chunk.Page,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd
            })
            .ToList();

        var indexResult = await qdrantService.IndexDocumentChunksAsync((pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? string.Empty, pdfId, documentChunks).ConfigureAwait(false);
        indexingStopwatch.Stop();

        if (!indexResult.Success)
        {
            pdfDoc.ProcessingStatus = "failed";
            pdfDoc.ProcessingError = indexResult.ErrorMessage;
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Qdrant indexing failed for {PdfId}: {Error}", pdfId, indexResult.ErrorMessage);
            return;
        }

        // Update vector document
        await UpdateOrCreateVectorDocumentAsync(pdfGuid, pdfDoc, fullText, indexResult.IndexedCount, db, cancellationToken).ConfigureAwait(false);

        // Save text chunks to PostgreSQL for hybrid search (FTS)
        await SaveTextChunksForHybridSearchAsync(pdfGuid, pdfDoc, allDocumentChunks, db, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates or creates VectorDocument entity after successful indexing.
    /// </summary>
    private async Task UpdateOrCreateVectorDocumentAsync(
        Guid pdfGuid,
        PdfDocumentEntity pdfDoc,
        string fullText,
        int indexedCount,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);
        if (vectorDoc == null)
        {
            vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                SharedGameId = pdfDoc.SharedGameId, // Issue #5185: propagate SharedGameId from PDF
                PdfDocumentId = pdfGuid,
                IndexingStatus = "completed",
                ChunkCount = indexedCount,
                TotalCharacters = fullText.Length,
                IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            db.VectorDocuments.Add(vectorDoc);
        }
        else
        {
            vectorDoc.IndexingStatus = "completed";
            vectorDoc.ChunkCount = indexedCount;
            vectorDoc.TotalCharacters = fullText.Length;
            vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksForHybridSearchAsync(
        Guid pdfGuid,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        // Delete existing chunks
        var existingChunks = await db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfGuid)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (existingChunks.Count > 0)
        {
            db.TextChunks.RemoveRange(existingChunks);
        }

        // Create new text chunk entities
        var textChunkEntities = allDocumentChunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                PdfDocumentId = pdfGuid,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                Content = chunk.Text,
                CharacterCount = chunk.Text.Length,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            })
            .ToList();

        db.TextChunks.AddRange(textChunkEntities);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles PDF processing failure with consistent error logging and status update.
    /// </summary>
    private async Task HandleProcessingFailureAsync(
        string pdfId,
        MeepleAiDbContext db,
        Exception ex,
        CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Failed to process PDF {PdfId}", pdfId);

        try
        {
            if (Guid.TryParse(pdfId, out var pdfGuid))
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, cancellationToken).ConfigureAwait(false);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = ex.Message;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ERROR RECOVERY PATTERN: Nested error handling during failure recovery
        // Catches exceptions when attempting to update PDF status to "failed" after processing error.
        // If status update fails (DB unavailable, etc.), only logs error - nothing more can be done.
        // Prevents cascading failures in error handling path.
#pragma warning restore S125
        catch (Exception saveEx)
        {
            _logger.LogError(saveEx, "Failed to update PDF status after processing error for {PdfId}", pdfId);
        }
#pragma warning restore CA1031
    }

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

