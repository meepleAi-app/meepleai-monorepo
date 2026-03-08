using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// PdfDocument aggregate root representing an uploaded PDF with extraction metadata.
/// Issue #2029: Added Language support for PDF language filtering
/// Issue #4215: Added granular 7-state processing pipeline
/// </summary>
internal sealed class PdfDocument : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public FileName FileName { get; private set; }
    public string FilePath { get; private set; }
    public FileSize FileSize { get; private set; }
    public string ContentType { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    // Issue #4215: New granular state tracking (7 states)
    public PdfProcessingState ProcessingState { get; private set; }

    // Deprecated: Keep for backward compatibility (migrate all usages in Issue #4216)
    public string ProcessingStatus { get; private set; }

    public DateTime? ProcessedAt { get; private set; }
    public int? PageCount { get; private set; }
    public string? ProcessingError { get; private set; }

    // Issue #4216: Retry mechanism and error categorization
    public int RetryCount { get; private set; }
    public int MaxRetries => 3;
    public ErrorCategory? ErrorCategory { get; private set; }
    public PdfProcessingState? FailedAtState { get; private set; }

    // Issue #2029: Language detection for PDF filtering
    public LanguageCode Language { get; private set; }

    // Issue #2051: Multi-document collection support
    public Guid? CollectionId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public int SortOrder { get; private set; }

    // Admin Wizard: Public library visibility (visible to all registered users)
    public bool IsPublic { get; private set; }

    // Issue #2732: Shared game document support
    public Guid? SharedGameId { get; private set; }
    public Guid? ContributorId { get; private set; }
    public Guid? SourceDocumentId { get; private set; }

    // Issue #3664: Private game PDF support
    public Guid? PrivateGameId { get; private set; }

    // PDF deduplication: SHA-256 hash of file content
    public string? ContentHash { get; private set; }

    // Issue #5443: Document classification for pipeline routing
    public DocumentCategory DocumentCategory { get; private set; }

    // Issue #5444: Self-referential FK for expansion/errata linkage to base rulebook
    public Guid? BaseDocumentId { get; private set; }

    // Issue #4219: Per-state timing tracking for metrics and ETA
    public DateTime? UploadingStartedAt { get; private set; }
    public DateTime? ExtractingStartedAt { get; private set; }
    public DateTime? ChunkingStartedAt { get; private set; }
    public DateTime? EmbeddingStartedAt { get; private set; }
    public DateTime? IndexingStartedAt { get; private set; }

    // Issue #4219: Progress tracking and ETA calculation
    public int ProgressPercentage => CalculateProgressPercentage();
    public TimeSpan? EstimatedTimeRemaining { get; private set; }
    public TimeSpan? TotalDuration => ProcessedAt.HasValue ? ProcessedAt.Value - UploadedAt : null;

#pragma warning disable CS8618
    private PdfDocument() : base() { }
#pragma warning restore CS8618

    public PdfDocument(
        Guid id,
        Guid gameId,
        FileName fileName,
        string filePath,
        FileSize fileSize,
        Guid uploadedByUserId,
        LanguageCode? language = null,
        Guid? collectionId = null,
        DocumentType? documentType = null,
        int sortOrder = 0,
        DocumentCategory documentCategory = DocumentCategory.Rulebook) : base(id)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (sortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative", nameof(sortOrder));

        GameId = gameId;
        FileName = fileName;
        FilePath = filePath;
        FileSize = fileSize;
        ContentType = "application/pdf";
        UploadedByUserId = uploadedByUserId;
        UploadedAt = DateTime.UtcNow;

        // Issue #4215: Initialize with new granular state
        ProcessingState = PdfProcessingState.Pending;
        ProcessingStatus = "pending"; // Backward compat

        // Issue #4216: Initialize retry tracking
        RetryCount = 0;

        Language = language ?? LanguageCode.English; // Default to English
        CollectionId = collectionId;
        DocumentType = documentType ?? ValueObjects.DocumentType.Base; // Default to base
        SortOrder = sortOrder;

        // Issue #5443: Document classification for pipeline routing
        DocumentCategory = documentCategory;
    }

    /// <summary>
    /// Reconstitutes a PdfDocument from persistence.
    /// Issue #2140: Replaces reflection-based property mutation
    /// Issue #2732: Added SharedGameId, ContributorId, SourceDocumentId
    /// Issue #3664: Added PrivateGameId
    /// Issue #4215: Added ProcessingState enum support
    /// Issue #4216: Added retry tracking fields
    /// Issue #4219: Added per-state timing fields and ETA
    /// </summary>
    public static PdfDocument Reconstitute(
        Guid id,
        Guid gameId,
        FileName fileName,
        string filePath,
        FileSize fileSize,
        Guid uploadedByUserId,
        DateTime uploadedAt,
        string processingStatus,
        DateTime? processedAt,
        int? pageCount,
        string? processingError,
        LanguageCode language,
        Guid? collectionId = null,
        DocumentType? documentType = null,
        int sortOrder = 0,
        bool isPublic = false,
        Guid? sharedGameId = null,
        Guid? contributorId = null,
        Guid? sourceDocumentId = null,
        Guid? privateGameId = null,
        PdfProcessingState? processingState = null,
        int retryCount = 0,
        ErrorCategory? errorCategory = null,
        PdfProcessingState? failedAtState = null,
        DateTime? uploadingStartedAt = null,
        DateTime? extractingStartedAt = null,
        DateTime? chunkingStartedAt = null,
        DateTime? embeddingStartedAt = null,
        DateTime? indexingStartedAt = null,
        string? contentHash = null,
        DocumentCategory? documentCategory = null,
        Guid? baseDocumentId = null)
    {
        var document = new PdfDocument
        {
            Id = id,
            GameId = gameId,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = "application/pdf",
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt,

            // Issue #4215: Prefer enum state, fallback to string parsing
            ProcessingState = processingState ?? ParseProcessingState(processingStatus),

#pragma warning disable CS0618 // Type or member is obsolete
            ProcessingStatus = processingStatus,
#pragma warning restore CS0618

            ProcessedAt = processedAt,
            PageCount = pageCount,
            ProcessingError = processingError,

            // Issue #4216: Retry tracking
            RetryCount = retryCount,
            ErrorCategory = errorCategory,
            FailedAtState = failedAtState,

            Language = language,
            CollectionId = collectionId,
            DocumentType = documentType ?? ValueObjects.DocumentType.Base,
            SortOrder = sortOrder,
            IsPublic = isPublic,
            SharedGameId = sharedGameId,
            ContributorId = contributorId,
            SourceDocumentId = sourceDocumentId,
            PrivateGameId = privateGameId,

            // Issue #4219: Timing fields
            UploadingStartedAt = uploadingStartedAt,
            ExtractingStartedAt = extractingStartedAt,
            ChunkingStartedAt = chunkingStartedAt,
            EmbeddingStartedAt = embeddingStartedAt,
            IndexingStartedAt = indexingStartedAt,

            // PDF deduplication
            ContentHash = contentHash,

            // Issue #5443: Document classification for pipeline routing
            DocumentCategory = documentCategory ?? DocumentCategory.Rulebook,

            // Issue #5444: Base document linkage
            BaseDocumentId = baseDocumentId
        };

        // Issue #4219: Calculate ETA after reconstitution
        document.UpdateETA();

        return document;
    }

    // Issue #4215: Deprecated methods - use TransitionTo() instead
    // Deprecated: Use TransitionTo() instead (remove in Issue #4216)
    public void MarkAsProcessing()
    {
        if (ProcessingState == PdfProcessingState.Ready)
            throw new InvalidOperationException("Cannot reprocess completed document");

        // Fix: Pending → Uploading (not Extracting) to match state machine
        TransitionTo(PdfProcessingState.Uploading);
    }

    // Deprecated: Use TransitionTo() instead (remove in Issue #4216)
    public void MarkAsCompleted(int pageCount)
    {
        if (pageCount < 1)
            throw new ArgumentException("Page count must be at least 1", nameof(pageCount));

        // Complete the pipeline from current state to Ready
        while (ProcessingState != PdfProcessingState.Ready)
        {
            var nextState = ProcessingState switch
            {
                PdfProcessingState.Pending => PdfProcessingState.Uploading,
                PdfProcessingState.Uploading => PdfProcessingState.Extracting,
                PdfProcessingState.Extracting => PdfProcessingState.Chunking,
                PdfProcessingState.Chunking => PdfProcessingState.Embedding,
                PdfProcessingState.Embedding => PdfProcessingState.Indexing,
                PdfProcessingState.Indexing => PdfProcessingState.Ready,
                _ => throw new InvalidOperationException($"Cannot complete from state {ProcessingState}")
            };
            TransitionTo(nextState);
        }

        PageCount = pageCount;
        ProcessingError = null;
        ProcessedAt = DateTime.UtcNow;
    }

    // Deprecated: Use MarkAsFailed(error, category, state) instead (remove in Issue #4216)
    public void MarkAsFailed(string error)
    {
        MarkAsFailed(error, Enums.ErrorCategory.Unknown, ProcessingState);
    }

    /// <summary>
    /// Marks the document as failed with error categorization.
    /// Issue #4216: Enhanced failure tracking with category and recovery point.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="category">The error category for retry strategy.</param>
    /// <param name="failedAtState">The state where failure occurred.</param>
    public void MarkAsFailed(string error, ErrorCategory category, PdfProcessingState failedAtState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        ProcessingError = error;
        ErrorCategory = category;
        FailedAtState = failedAtState;
        TransitionTo(PdfProcessingState.Failed);
        ProcessedAt = DateTime.UtcNow;

        // Issue #4220: Emit event for notification system
        AddDomainEvent(new PdfFailedEvent(Id, category, failedAtState, error, UploadedByUserId));
    }

    /// <summary>
    /// Determines if the document can be retried after failure.
    /// Issue #4216: Retry eligibility check.
    /// </summary>
    /// <returns>True if retry is allowed, false otherwise.</returns>
    public bool CanRetry()
    {
        return RetryCount < MaxRetries && ProcessingState == PdfProcessingState.Failed;
    }

    /// <summary>
    /// Retries processing from the failed state.
    /// Issue #4216: Manual retry mechanism.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when retry is not allowed.</exception>
    public void Retry()
    {
        if (!CanRetry())
        {
            throw new InvalidOperationException(
                $"Cannot retry: RetryCount={RetryCount}, MaxRetries={MaxRetries}, State={ProcessingState}");
        }

        RetryCount++;

        // Resume from failed state or restart from Extracting
        var resumeState = FailedAtState ?? PdfProcessingState.Extracting;
        TransitionTo(resumeState);

        // Clear error state
        ProcessingError = null;
        ProcessedAt = null;

        // Emit event to trigger pipeline resumption
        AddDomainEvent(new PdfRetryInitiatedEvent(Id, RetryCount, UploadedByUserId));
    }

    /// <summary>
    /// Transitions the document to a new processing state with validation.
    /// Issue #4215: Granular state machine for 7-state pipeline.
    /// </summary>
    /// <param name="newState">The target state.</param>
    /// <exception cref="InvalidOperationException">Thrown when transition is not allowed.</exception>
    public void TransitionTo(PdfProcessingState newState)
    {
        var previousState = ProcessingState;

        // Validate state transition
        ValidateStateTransition(previousState, newState);

        // Update state
        ProcessingState = newState;

        // Issue #4219: Record timing when entering a new state
        RecordStateStartTime(newState);

        // Sync deprecated property for backward compatibility
        ProcessingStatus = newState switch
        {
            PdfProcessingState.Pending => "pending",
            PdfProcessingState.Uploading or PdfProcessingState.Extracting or
            PdfProcessingState.Chunking or PdfProcessingState.Embedding or
            PdfProcessingState.Indexing => "processing",
            PdfProcessingState.Ready => "completed",
            PdfProcessingState.Failed => "failed",
            _ => "pending"
        };

        // Emit domain event for real-time updates (Issue #4218)
        AddDomainEvent(new PdfStateChangedEvent(Id, previousState, newState, UploadedByUserId));
    }

    /// <summary>
    /// Validates that a state transition is allowed.
    /// Issue #4215: Prevent invalid state transitions.
    /// </summary>
    private void ValidateStateTransition(PdfProcessingState current, PdfProcessingState next)
    {
        // Terminal states cannot transition (except Failed can retry)
        if (current == PdfProcessingState.Ready)
            throw new InvalidOperationException("Cannot transition from Ready state");

        // Allow retry: Failed → any recovery state
        if (current == PdfProcessingState.Failed)
            return; // Retry mechanism (Issue #4216) handles this

        // Validate forward progression only (no backwards except retry)
        var validTransitions = current switch
        {
            PdfProcessingState.Pending => new[] { PdfProcessingState.Uploading, PdfProcessingState.Failed },
            PdfProcessingState.Uploading => new[] { PdfProcessingState.Extracting, PdfProcessingState.Failed },
            PdfProcessingState.Extracting => new[] { PdfProcessingState.Chunking, PdfProcessingState.Failed },
            PdfProcessingState.Chunking => new[] { PdfProcessingState.Embedding, PdfProcessingState.Failed },
            PdfProcessingState.Embedding => new[] { PdfProcessingState.Indexing, PdfProcessingState.Failed },
            PdfProcessingState.Indexing => new[] { PdfProcessingState.Ready, PdfProcessingState.Failed },
            _ => Array.Empty<PdfProcessingState>()
        };

        if (!validTransitions.Contains(next))
        {
            throw new InvalidOperationException(
                $"Invalid state transition: {current} → {next}. Allowed: {string.Join(", ", validTransitions)}");
        }
    }

    /// <summary>
    /// Calculates progress percentage based on current processing state.
    /// Issue #4219: Progress tracking for UI display.
    /// </summary>
    /// <returns>Progress percentage (0-100)</returns>
    private int CalculateProgressPercentage()
    {
        return ProcessingState switch
        {
            PdfProcessingState.Pending => 0,
            PdfProcessingState.Uploading => 10,
            PdfProcessingState.Extracting => 30,
            PdfProcessingState.Chunking => 50,
            PdfProcessingState.Embedding => 70,
            PdfProcessingState.Indexing => 90,
            PdfProcessingState.Ready => 100,
            PdfProcessingState.Failed => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Updates the estimated time remaining based on current progress.
    /// Issue #4219: ETA calculation for user expectation management.
    /// MVP: Static calculation (2 seconds per page per remaining state).
    /// Future: ML-based predictor using historical data (Phase 2).
    /// </summary>
    public void UpdateETA()
    {
        // Terminal states have no remaining time
        if (ProcessingState == PdfProcessingState.Ready || ProcessingState == PdfProcessingState.Failed)
        {
            EstimatedTimeRemaining = TimeSpan.Zero;
            return;
        }

        // MVP: Static calculation
        // Assumptions:
        // - 2 seconds per page per state
        // - Remaining states = 6 - current state index
        var stateIndex = (int)ProcessingState; // Pending=0, Uploading=1, ..., Indexing=5
        var remainingStates = 6 - stateIndex;
        var avgSecondsPerState = 2 * (PageCount ?? 10); // Default to 10 pages if unknown

        EstimatedTimeRemaining = TimeSpan.FromSeconds(avgSecondsPerState * remainingStates);
    }

    /// <summary>
    /// Records the start time for the current state transition.
    /// Issue #4219: Per-state timing tracking.
    /// </summary>
    /// <param name="state">The state being transitioned to</param>
    private void RecordStateStartTime(PdfProcessingState state)
    {
        var now = DateTime.UtcNow;

        switch (state)
        {
            case PdfProcessingState.Uploading:
                UploadingStartedAt = now;
                break;
            case PdfProcessingState.Extracting:
                ExtractingStartedAt = now;
                break;
            case PdfProcessingState.Chunking:
                ChunkingStartedAt = now;
                break;
            case PdfProcessingState.Embedding:
                EmbeddingStartedAt = now;
                break;
            case PdfProcessingState.Indexing:
                IndexingStartedAt = now;
                break;
        }

        // Update ETA after state change
        UpdateETA();
    }

    /// <summary>
    /// Parses legacy string status to enum state.
    /// Issue #4215: Backward compatibility helper.
    /// </summary>
    private static PdfProcessingState ParseProcessingState(string status)
    {
        return status switch
        {
            "pending" => PdfProcessingState.Pending,
            "processing" => PdfProcessingState.Extracting, // Default to mid-pipeline
            "completed" => PdfProcessingState.Ready,
            "failed" => PdfProcessingState.Failed,
            _ => PdfProcessingState.Pending
        };
    }

    /// <summary>
    /// Sets the SHA-256 content hash for deduplication.
    /// </summary>
    public void SetContentHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ContentHash = hash;
    }

    /// <summary>
    /// Links this document to a base rulebook document.
    /// Issue #5444: Expansion/errata documents can reference their base rulebook.
    /// </summary>
    public void LinkToBaseDocument(Guid baseDocumentId)
    {
        if (baseDocumentId == Guid.Empty)
            throw new ArgumentException("Base document ID cannot be empty", nameof(baseDocumentId));

        if (baseDocumentId == Id)
            throw new ArgumentException("A document cannot reference itself as base document", nameof(baseDocumentId));

        BaseDocumentId = baseDocumentId;
    }

    /// <summary>
    /// Removes the link to a base document.
    /// Issue #5444: Allows unlinking from base rulebook.
    /// </summary>
    public void UnlinkBaseDocument()
    {
        BaseDocumentId = null;
    }

    // Issue #2029: Update detected language after processing
    public void UpdateLanguage(LanguageCode languageCode)
    {
        ArgumentNullException.ThrowIfNull(languageCode);
        Language = languageCode;
    }

    // Issue #2051: Assign document to collection
    public void AssignToCollection(Guid collectionId, DocumentType documentType, int sortOrder)
    {
        if (collectionId == Guid.Empty)
            throw new ArgumentException("Collection ID cannot be empty", nameof(collectionId));

        ArgumentNullException.ThrowIfNull(documentType);

        if (sortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative", nameof(sortOrder));

        CollectionId = collectionId;
        DocumentType = documentType;
        SortOrder = sortOrder;
    }

    // Issue #2051: Remove from collection
    public void RemoveFromCollection()
    {
        CollectionId = null;
        DocumentType = ValueObjects.DocumentType.Base; // Reset to default
        SortOrder = 0;
    }

    /// <summary>
    /// Makes this PDF visible in the public library (for registered users).
    /// </summary>
    public void MakePublic() => IsPublic = true;

    /// <summary>
    /// Makes this PDF private (only visible to uploader and admins).
    /// </summary>
    public void MakePrivate() => IsPublic = false;

    /// <summary>
    /// Links this PDF to a game.
    /// Issue #3372: Support linking PDF during game creation.
    /// </summary>
    /// <param name="gameId">The game ID to link to</param>
    public void LinkToGame(Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        GameId = gameId;
    }

    /// <summary>
    /// Creates a copy of this document for a shared game.
    /// Issue #2732: Document copying on share request approval
    /// </summary>
    /// <param name="source">Source document to copy</param>
    /// <param name="sharedGameId">Target shared game ID</param>
    /// <param name="contributorId">User who contributed this document</param>
    /// <param name="newFilePath">Storage path for the copied file</param>
    /// <returns>New PdfDocument instance for shared game</returns>
    public static PdfDocument CreateCopy(
        PdfDocument source,
        Guid sharedGameId,
        Guid contributorId,
        string newFilePath)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("Shared game ID cannot be empty", nameof(sharedGameId));

        if (contributorId == Guid.Empty)
            throw new ArgumentException("Contributor ID cannot be empty", nameof(contributorId));

        if (string.IsNullOrWhiteSpace(newFilePath))
            throw new ArgumentException("New file path cannot be empty", nameof(newFilePath));

        var copy = new PdfDocument
        {
            Id = Guid.NewGuid(),
            GameId = source.GameId,
            FileName = source.FileName,
            FilePath = newFilePath, // New storage path
            FileSize = source.FileSize,
            ContentType = "application/pdf",
            UploadedByUserId = contributorId, // Contributor becomes uploader
            UploadedAt = DateTime.UtcNow,

            // Issue #4215: Copy processing state
            ProcessingState = source.ProcessingState,

            ProcessingStatus = source.ProcessingStatus,

            ProcessedAt = source.ProcessedAt,
            PageCount = source.PageCount,
            ProcessingError = null, // Clear errors on copy
            Language = source.Language,
            CollectionId = null, // Not in collection initially
            DocumentType = source.DocumentType,
            SortOrder = 0, // Reset sort order
            IsPublic = true, // Shared game documents are public by default
            SharedGameId = sharedGameId,
            ContributorId = contributorId,
            SourceDocumentId = source.Id, // Track lineage

            // Issue #5443: Copy document category
            DocumentCategory = source.DocumentCategory,

            // Issue #5444: Copy base document linkage
            BaseDocumentId = source.BaseDocumentId,

            // Issue #4219: Copy timing fields for accurate metrics
            UploadingStartedAt = source.UploadingStartedAt,
            ExtractingStartedAt = source.ExtractingStartedAt,
            ChunkingStartedAt = source.ChunkingStartedAt,
            EmbeddingStartedAt = source.EmbeddingStartedAt,
            IndexingStartedAt = source.IndexingStartedAt
        };

        // Issue #4219: Calculate ETA for copied document
        copy.UpdateETA();

        return copy;
    }
}
