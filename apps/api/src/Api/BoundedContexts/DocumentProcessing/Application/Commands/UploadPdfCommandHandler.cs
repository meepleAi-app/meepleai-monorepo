using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
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
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal class UploadPdfCommandHandler : ICommandHandler<UploadPdfCommand, PdfUploadResult>
{
    internal const string DuplicateContentErrorMessage = "Un file identico è già stato caricato per questo gioco.";

    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadPdfCommandHandler> _logger;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly TimeProvider _timeProvider;
    private readonly long _maxFileSizeBytes;
    private readonly IMediator _mediator;
    private readonly Api.BoundedContexts.UserLibrary.Domain.Repositories.IPrivateGameRepository? _privateGameRepository;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.Ordinal) { "application/pdf" };

    public UploadPdfCommandHandler(
        MeepleAiDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<UploadPdfCommandHandler> logger,
        IBackgroundTaskService backgroundTaskService,
        IAiResponseCacheService cacheService,
        IBlobStorageService blobStorageService,
        IPdfUploadQuotaService quotaService,
        ITierEnforcementService tierEnforcementService,
        IOptions<PdfProcessingOptions> pdfOptions,
        IMediator mediator,
        Api.BoundedContexts.UserLibrary.Domain.Repositories.IPrivateGameRepository? privateGameRepository = null,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        ArgumentNullException.ThrowIfNull(pdfOptions);
        _maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _privateGameRepository = privateGameRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PdfUploadResult> Handle(UploadPdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var file = command.File;
        var userId = command.UserId;

        // E2-3: Tier enforcement — check upload quota before proceeding
        var canUpload = await _tierEnforcementService
            .CanPerformAsync(userId, TierAction.UploadPdf, cancellationToken)
            .ConfigureAwait(false);

        if (!canUpload)
        {
            var usage = await _tierEnforcementService
                .GetUsageAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            throw new TierLimitExceededException(
                nameof(TierAction.UploadPdf),
                usage.PdfThisMonth,
                usage.PdfThisMonthMax);
        }

        // E2-3: Tier enforcement — check file size against tier limit
        var tierLimits = await _tierEnforcementService
            .GetLimitsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (file.Length > tierLimits.MaxPdfSizeBytes)
        {
            var maxMb = tierLimits.MaxPdfSizeBytes / (1024 * 1024);
            throw new TierLimitExceededException(
                "PdfFileSize",
                $"File size ({file.Length / (1024 * 1024)} MB) exceeds your tier limit ({maxMb} MB). Upgrade to upload larger files.");
        }

        // Validate file input
        var validationResult = await ValidateFileInputAsync(file, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return new PdfUploadResult(false, validationResult.ErrorMessage!, null);
        }

        var fileName = validationResult.SanitizedFileName!;

        // Issue #3664: Handle private game PDF uploads
        if (command.PrivateGameId.HasValue)
        {
            // Compute content hash and check for existing KBs with same content
            var privateContentHash = await ComputeContentHashAsync(file, cancellationToken).ConfigureAwait(false);

            // Check 1: User's own PDFs with same content (any game)
            var privateUserMatch = await _db.PdfDocuments
                .AsNoTracking()
                .Where(p => p.ContentHash == privateContentHash && p.UploadedByUserId == userId)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.ProcessingState,
                    p.SharedGameId,
                    GameName = _db.Games.Where(g => g.Id == p.GameId).Select(g => g.Name).FirstOrDefault(),
                    TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (privateUserMatch != null)
            {
                var kbInfo = new ExistingKbInfoDto(
                    PdfDocumentId: privateUserMatch.Id,
                    Source: "user",
                    FileName: privateUserMatch.FileName,
                    ProcessingState: privateUserMatch.ProcessingState ?? "Pending",
                    TotalChunks: privateUserMatch.TotalChunks,
                    OriginalGameName: privateUserMatch.GameName,
                    SharedGameId: null);
                return new PdfUploadResult(true, "Existing KB found in your uploads", null, kbInfo);
            }

            // Check 2: SharedGameCatalog PDFs with same content
            var privateSharedMatch = await _db.PdfDocuments
                .AsNoTracking()
                .Where(p => p.ContentHash == privateContentHash && p.SharedGameId != null)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.ProcessingState,
                    p.SharedGameId,
                    GameName = _db.SharedGames.Where(sg => sg.Id == p.SharedGameId).Select(sg => sg.Title).FirstOrDefault(),
                    TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (privateSharedMatch != null)
            {
                var kbInfo = new ExistingKbInfoDto(
                    PdfDocumentId: privateSharedMatch.Id,
                    Source: "shared",
                    FileName: privateSharedMatch.FileName,
                    ProcessingState: privateSharedMatch.ProcessingState ?? "Pending",
                    TotalChunks: privateSharedMatch.TotalChunks,
                    OriginalGameName: privateSharedMatch.GameName,
                    SharedGameId: privateSharedMatch.SharedGameId);
                return new PdfUploadResult(true, "Existing KB found in shared catalog", null, kbInfo);
            }

            return await HandlePrivateGamePdfUploadAsync(
                command.PrivateGameId.Value, userId, file, fileName, privateContentHash, cancellationToken).ConfigureAwait(false);
        }

        // Check user quota and permissions (also resolves/creates game)
        var (quotaAllowed, quotaError, _, resolvedGameId) = await CheckUserQuotaAsync(
            userId, command.GameId, command.Metadata, file, cancellationToken).ConfigureAwait(false);

        if (!quotaAllowed || !resolvedGameId.HasValue)
        {
            return new PdfUploadResult(false, quotaError!, null);
        }

        var gameId = resolvedGameId.Value.ToString();

        // Compute content hash and check for existing KBs with same content
        var contentHash = await ComputeContentHashAsync(file, cancellationToken).ConfigureAwait(false);

        // Check 1: User's own PDFs with same content (any game)
        var userMatch = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ContentHash == contentHash && p.UploadedByUserId == userId)
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.ProcessingState,
                p.SharedGameId,
                GameName = _db.Games.Where(g => g.Id == p.GameId).Select(g => g.Name).FirstOrDefault(),
                TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (userMatch != null)
        {
            var kbInfo = new ExistingKbInfoDto(
                PdfDocumentId: userMatch.Id,
                Source: "user",
                FileName: userMatch.FileName,
                ProcessingState: userMatch.ProcessingState ?? "Pending",
                TotalChunks: userMatch.TotalChunks,
                OriginalGameName: userMatch.GameName,
                SharedGameId: null);
            return new PdfUploadResult(true, "Existing KB found in your uploads", null, kbInfo);
        }

        // Check 2: SharedGameCatalog PDFs with same content
        var sharedMatch = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ContentHash == contentHash && p.SharedGameId != null)
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.ProcessingState,
                p.SharedGameId,
                GameName = _db.SharedGames.Where(sg => sg.Id == p.SharedGameId).Select(sg => sg.Title).FirstOrDefault(),
                TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (sharedMatch != null)
        {
            var kbInfo = new ExistingKbInfoDto(
                PdfDocumentId: sharedMatch.Id,
                Source: "shared",
                FileName: sharedMatch.FileName,
                ProcessingState: sharedMatch.ProcessingState ?? "Pending",
                TotalChunks: sharedMatch.TotalChunks,
                OriginalGameName: sharedMatch.GameName,
                SharedGameId: sharedMatch.SharedGameId);
            return new PdfUploadResult(true, "Existing KB found in shared catalog", null, kbInfo);
        }

        // Store file and create database record
        var (storageSuccess, storageResult, pdfDoc) = await StoreFileAndCreateRecordAsync(
            file, fileName, gameId, userId, null, cancellationToken, contentHash).ConfigureAwait(false);

        if (!storageSuccess)
        {
            return new PdfUploadResult(false, storageResult.ErrorMessage ?? "Failed to store file", null);
        }

        // Apply priority from command (admin upload with priority override)
        if (!string.IsNullOrWhiteSpace(command.Priority))
        {
            var priorityEnum = string.Equals(command.Priority, "urgent", StringComparison.OrdinalIgnoreCase)
                ? ProcessingPriority.Urgent
                : ProcessingPriority.High;

            pdfDoc!.ProcessingPriority = priorityEnum.ToString();

            // Also update the ProcessingJob priority int for Quartz queue ordering
            var job = await _db.ProcessingJobs
                .FirstOrDefaultAsync(j => j.PdfDocumentId == pdfDoc.Id, cancellationToken).ConfigureAwait(false);
            if (job != null)
            {
                job.Priority = (int)priorityEnum;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Reserve quota and start background processing
        return await ReserveQuotaAndStartProcessingAsync(
            userId, gameId, file, storageResult, pdfDoc!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates file input (size, type, structure, filename).
    /// Returns validation result with sanitized filename.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage, string? SanitizedFileName)> ValidateFileInputAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            RecordUploadMetricSafely("validation_failed_empty", null);
            return (false, "No file provided. Please select a PDF file to upload.", null);
        }

        if (file.Length > _maxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = _maxFileSizeBytes / 1024 / 1024;
            RecordUploadMetricSafely("validation_failed_size", file.Length);
            return (false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB. Try compressing the PDF or splitting into smaller files.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            RecordUploadMetricSafely("validation_failed_type", file.Length);
            return (false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed. Please ensure your file has a .pdf extension.", null);
        }

        // Validate PDF file structure (Issue #1688)
        using (var validationStream = file.OpenReadStream())
        {
            var (isValid, validationError) = await ValidatePdfStructureAsync(validationStream, cancellationToken).ConfigureAwait(false);
            if (!isValid)
            {
                RecordUploadMetricSafely("validation_failed_structure", file.Length);
                return (false, validationError!, null);
            }
        }

        // SEC-738: Sanitize filename
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "Invalid file name. The file must have a valid name.", null);
        }

        try
        {
            fileName = PathSecurity.SanitizeFilename(fileName);
        }
        catch (ArgumentException ex)
        {
            return (false, $"Invalid file name: {ex.Message}", null);
        }

        return (true, null, fileName);
    }

    /// <summary>
    /// Checks user quota and permissions for PDF upload.
    /// Returns (allowed, errorMessage, userTier, gameId).
    /// </summary>
    private async Task<(bool Allowed, string? ErrorMessage, string? UserTier, Guid? GameId)> CheckUserQuotaAsync(
        Guid userId,
        string? gameId,
        PdfUploadMetadata? metadata,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find or create game based on input
            var (gameSuccess, gameError, resolvedGameId) = await FindOrCreateGameAsync(
                gameId, metadata, cancellationToken).ConfigureAwait(false);

            if (!gameSuccess || !resolvedGameId.HasValue)
            {
                return (false, gameError!, null, null);
            }

            // Get user and validate quota
            var user = await GetUserForQuotaCheckAsync(userId, file, cancellationToken).ConfigureAwait(false);
            if (!user.HasValue)
            {
                return (false, "User not found. Please ensure you are authenticated.", null, null);
            }

            var (uId, uTier, uRole) = user.Value;
            var (allowed, error, tier, _) = await ValidateUserQuotaAsync(uId, uTier, uRole, file, cancellationToken).ConfigureAwait(false);

            return (allowed, error, tier, resolvedGameId);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is ObjectDisposedException or Npgsql.NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            RecordUploadMetricSafely("db_unavailable", file.Length);
            _logger.LogError(ex, "Database unavailable during PDF upload");
            return (false, "Database unavailable. Please retry the upload.", null, null);
        }
    }

    private async Task<(Guid Id, string Tier, string Role)?> GetUserForQuotaCheckAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Tier, u.Role })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            RecordUploadMetricSafely("user_not_found", file.Length);
            _logger.LogError("User {UserId} not found during PDF upload", userId);
            return null;
        }

        return (user.Id, user.Tier, user.Role);
    }

    private async Task<(bool Allowed, string? ErrorMessage, string? UserTier, Guid? GameId)> ValidateUserQuotaAsync(
        Guid userId,
        string tier,
        string role,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userTier = UserTier.Parse(tier);
        var userRole = Role.Parse(role);

        var quotaResult = await _quotaService.CheckQuotaAsync(
            userId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!quotaResult.Allowed)
        {
            RecordUploadMetricSafely("quota_exceeded", file.Length);
            _logger.LogWarning(
                "PDF upload denied for user {UserId} ({Tier}): {Reason}",
                userId,
                userTier.Value,
                quotaResult.ErrorMessage);
            return (false, quotaResult.ErrorMessage!, userTier.Value, null);
        }

        _logger.LogDebug(
            "PDF upload quota check passed for user {UserId} ({Tier}): Daily {DailyUsed}/{DailyLimit}, Weekly {WeeklyUsed}/{WeeklyLimit}",
            userId,
            userTier.Value,
            quotaResult.DailyUploadsUsed,
            quotaResult.DailyLimit,
            quotaResult.WeeklyUploadsUsed,
            quotaResult.WeeklyLimit);

        return (true, null, userTier.Value, null);
    }

    /// <summary>
    /// Finds an existing game or creates a new one based on provided gameId or metadata.
    /// Returns (success, errorMessage, gameId).
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage, Guid? GameId)> FindOrCreateGameAsync(
        string? gameId,
        PdfUploadMetadata? metadata,
        CancellationToken cancellationToken)
    {
        // Legacy path: gameId provided (backward compatibility)
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            if (!Guid.TryParse(gameId, out var parsedGameId))
            {
                return (false, "Invalid game ID format.", null);
            }

            // Support both games.Id (library entry) and games.SharedGameId (catalog reference)
            var existingGame = await _db.Games
                .Where(g => g.Id == parsedGameId || g.SharedGameId == parsedGameId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (existingGame == null)
            {
                // Fallback: check shared_games catalog and auto-create games entry
                var sharedGame = await _db.SharedGames
                    .Where(sg => sg.Id == parsedGameId && !sg.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                if (sharedGame == null)
                {
                    return (false, "Game not found. Please select a valid game before uploading.", null);
                }

                // Auto-create a games entry linked to the shared game
                existingGame = new GameEntity
                {
                    Id = Guid.NewGuid(),
                    Name = sharedGame.Title,
                    SharedGameId = sharedGame.Id,
                    BggId = sharedGame.BggId,
                    ImageUrl = sharedGame.ImageUrl,
                    IconUrl = sharedGame.ThumbnailUrl,
                    MinPlayers = sharedGame.MinPlayers,
                    MaxPlayers = sharedGame.MaxPlayers,
                    MinPlayTimeMinutes = sharedGame.PlayingTimeMinutes,
                    MaxPlayTimeMinutes = sharedGame.PlayingTimeMinutes,
                    YearPublished = sharedGame.YearPublished,
                    Language = sharedGame.RulesLanguage,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                };

                _db.Games.Add(existingGame);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Auto-created game {GameId} from shared game {SharedGameId} ({Title}) for PDF upload",
                    existingGame.Id, sharedGame.Id, sharedGame.Title);
            }

            _logger.LogInformation("Using existing game {GameId} for PDF upload (matched by {InputId})",
                existingGame.Id, parsedGameId);
            return (true, null, existingGame.Id);  // Return games.Id, not input SharedGameId
        }

        // New path: metadata provided (auto-create)
        if (metadata != null)
        {
            // Validate metadata
            if (string.IsNullOrWhiteSpace(metadata.GameName))
            {
                return (false, "Game name is required when using metadata.", null);
            }

            // Search for existing game with same characteristics
            var existingGame = await _db.Games
                .Where(g =>
                    g.Name == metadata.GameName &&
                    g.VersionType == metadata.VersionType &&
                    g.Language == metadata.Language &&
                    g.VersionNumber == metadata.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (existingGame != null)
            {
                _logger.LogInformation(
                    "Found existing game {GameId} for {GameName} ({VersionType}, {Language}, v{Version})",
                    existingGame.Id, metadata.GameName, metadata.VersionType, metadata.Language, metadata.VersionNumber);

                return (true, null, existingGame.Id);
            }

            // Create new game
            var newGame = new GameEntity
            {
                Id = Guid.NewGuid(),
                Name = metadata.GameName,
                VersionType = metadata.VersionType,
                Language = metadata.Language,
                VersionNumber = metadata.VersionNumber,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _db.Games.Add(newGame);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created new game {GameId} for {GameName} ({VersionType}, {Language}, v{Version})",
                newGame.Id, metadata.GameName, metadata.VersionType, metadata.Language, metadata.VersionNumber);

            return (true, null, newGame.Id);
        }

        // Neither gameId nor metadata provided
        return (false, "Either gameId or game metadata (gameName, versionType, language, versionNumber) must be provided.", null);
    }

    /// <summary>
    /// Stores file in blob storage and creates database record.
    /// Returns (success, storageResult, pdfDoc).
    /// </summary>
    private async Task<(bool Success, BlobStorageResult Result, PdfDocumentEntity? PdfDoc)> StoreFileAndCreateRecordAsync(
        IFormFile file,
        string fileName,
        string? gameId,
        Guid userId,
        Guid? privateGameId,
        CancellationToken cancellationToken,
        string? contentHash = null)
    {
        try
        {
            // Store file in blob storage
            BlobStorageResult storageResult;
            using (var stream = file.OpenReadStream())
            {
                storageResult = await _blobStorageService.StoreAsync(stream, fileName, gameId ?? privateGameId?.ToString() ?? string.Empty, cancellationToken).ConfigureAwait(false);
            }

            if (!storageResult.Success || string.IsNullOrWhiteSpace(storageResult.FileId))
            {
                RecordUploadMetricSafely("storage_failed", file.Length);
                return (false, storageResult, null);
            }

            _logger.LogInformation("Saved PDF file to {FilePath}", storageResult.FilePath);

            // Create database record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = Guid.Parse(storageResult.FileId!),
                GameId = !string.IsNullOrEmpty(gameId) ? Guid.Parse(gameId) : null,
                FileName = fileName,
                FilePath = storageResult.FilePath!,
                FileSizeBytes = storageResult.FileSizeBytes,
                ContentType = file.ContentType,
                UploadedByUserId = userId,
                UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,

                PrivateGameId = privateGameId, // Issue #3664
                ContentHash = contentHash
            };

            _db.PdfDocuments.Add(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created PDF document record {PdfId} for game {GameId}", storageResult.FileId, gameId);

            // Initialize progress tracking
            pdfDoc.ProcessingProgress = new ProcessingProgress
            {
                CurrentStep = ProcessingStep.Uploading,
                PercentComplete = 20,
                ElapsedTime = TimeSpan.Zero,
                EstimatedTimeRemaining = null,
                PagesProcessed = 0,
                TotalPages = 0,
                StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
                CompletedAt = null,
                ErrorMessage = null
            };
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (true, storageResult, pdfDoc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            RecordUploadMetricSafely("error_io", file?.Length);
            _logger.LogError(ex, "File I/O error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: I/O error occurred.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordUploadMetricSafely("error_access", file?.Length);
            _logger.LogError(ex, "Access denied during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: Access denied to storage location.", ex);
        }
        catch (DbUpdateException ex)
        {
            RecordUploadMetricSafely("error_database", file?.Length);
            _logger.LogError(ex, "Database error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF metadata: Database error occurred.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CQRS HANDLER BOUNDARY: Wrap all unexpected exceptions during PDF upload
        // into domain-specific PdfStorageException for consistent error handling upstream
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            RecordUploadMetricSafely("error_unexpected", file?.Length);
            _logger.LogError(ex, "Unexpected error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException($"Failed to upload PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reserves quota and starts background PDF processing via IPdfUploadBackgroundProcessor.
    /// </summary>
    private async Task<PdfUploadResult> ReserveQuotaAndStartProcessingAsync(
        Guid userId,
        string gameId,
        IFormFile file,
        BlobStorageResult storageResult,
        PdfDocumentEntity pdfDoc,
        CancellationToken cancellationToken)
    {
        var reservationResult = await _quotaService.ReserveQuotaAsync(userId, storageResult.FileId!, cancellationToken).ConfigureAwait(false);

        if (!reservationResult.Reserved)
        {
            await CleanupAfterQuotaFailureAsync(storageResult.FileId!, gameId, pdfDoc, cancellationToken).ConfigureAwait(false);
            RecordUploadMetricSafely("quota_reservation_failed", file.Length);
            return new PdfUploadResult(false, reservationResult.ErrorMessage!, null);
        }

        _logger.LogInformation(
            "Quota reserved for user {UserId}, PDF {PdfId}, expires at {ExpiresAt}",
            userId, storageResult.FileId, reservationResult.ExpiresAt);

        // Start background processing via fire-and-forget (immediate attempt)
        // IPdfUploadBackgroundProcessor resolves via a new scope so it doesn't share the request DbContext
        _backgroundTaskService.ExecuteWithCancellation(
            storageResult.FileId!,
            ct => RunProcessorInScopeAsync(storageResult.FileId!, pdfDoc.FilePath, userId, ct));

        // Also enqueue in the Quartz-based queue as a reliable fallback.
        // If the fire-and-forget Task completes first, the Quartz job will find
        // the PDF already in Ready state and skip it. If Task.Run fails silently,
        // the Quartz job will process it from Pending.
        await EnqueueForProcessingSafelyAsync(pdfDoc.Id, userId, cancellationToken).ConfigureAwait(false);

        await InvalidateCacheSafelyAsync(gameId, "PDF upload", cancellationToken).ConfigureAwait(false);

        RecordUploadMetricSafely("success", file.Length);

        // Issue #5187: Auto-create EntityLink Game → KbCard for PDF-KB association (shared games only)
        await CreateKbCardEntityLinkSafelyAsync(pdfDoc.Id, pdfDoc.GameId ?? Guid.Empty, userId, cancellationToken).ConfigureAwait(false);

        return new PdfUploadResult(true, "PDF uploaded successfully", new PdfDocumentDto(
            Id: pdfDoc.Id,
            GameId: pdfDoc.GameId,
            FileName: pdfDoc.FileName,
            FilePath: pdfDoc.FilePath,
            FileSizeBytes: pdfDoc.FileSizeBytes,

            UploadedAt: pdfDoc.UploadedAt,
            ProcessedAt: pdfDoc.ProcessedAt,
            PageCount: pdfDoc.PageCount,
            ProcessingState: pdfDoc.ProcessingState,
            ProgressPercentage: MapEntityStateToProgress(pdfDoc.ProcessingState),
            RetryCount: pdfDoc.RetryCount,
            DocumentCategory: pdfDoc.DocumentCategory,
            BaseDocumentId: pdfDoc.BaseDocumentId,
            IsActiveForRag: pdfDoc.IsActiveForRag,
            HasAcceptedDisclaimer: pdfDoc.CopyrightDisclaimerAcceptedAt.HasValue,
            VersionLabel: pdfDoc.VersionLabel
        ));
    }

    /// <summary>
    /// Resolves IPdfUploadBackgroundProcessor in a dedicated scope and runs processing.
    /// Necessary because the processor is scoped and must not share the request's DbContext.
    /// </summary>
    private async Task RunProcessorInScopeAsync(string pdfId, string filePath, Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IPdfUploadBackgroundProcessor>();
        await processor.ProcessAsync(pdfId, filePath, userId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans up blob storage and database record after quota reservation failure.
    /// </summary>
    private async Task CleanupAfterQuotaFailureAsync(
        string fileId,
        string gameId,
        PdfDocumentEntity pdfDoc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _blobStorageService.DeleteAsync(fileId, gameId, cancellationToken).ConfigureAwait(false);
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to cleanup after quota reservation failure for PDF {PdfId}", fileId);
        }
    }

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
    /// Handles PDF upload for private games (Issue #3664).
    /// Validates ownership and stores PDF linked to PrivateGame.
    /// </summary>
    private async Task<PdfUploadResult> HandlePrivateGamePdfUploadAsync(
        Guid privateGameId,
        Guid userId,
        IFormFile file,
        string fileName,
        string? contentHash,
        CancellationToken cancellationToken)
    {
        // Validate private game exists and user is owner using repository pattern
        if (_privateGameRepository == null)
        {
            _logger.LogError("PrivateGameRepository not injected for private game PDF upload");
            return new PdfUploadResult(false, "Service configuration error", null);
        }

        var privateGame = await _privateGameRepository.GetByIdAsync(privateGameId, cancellationToken).ConfigureAwait(false);

        if (privateGame == null)
        {
            _logger.LogWarning("Private game {PrivateGameId} not found for PDF upload", privateGameId);
            return new PdfUploadResult(false, "Private game not found", null);
        }

        if (privateGame.OwnerId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to upload PDF for private game {PrivateGameId} owned by {OwnerId}",
                userId, privateGameId, privateGame.OwnerId);
            return new PdfUploadResult(false, "You can only upload PDFs for your own private games", null);
        }

        // Private games don't have a shared GameEntity — GameId is null, PrivateGameId is set
        // Store file and create database record with PrivateGameId (GameId = null)
        var (storageSuccess, storageResult, pdfDoc) = await StoreFileAndCreateRecordAsync(
            file, fileName, null, userId, privateGameId, cancellationToken, contentHash).ConfigureAwait(false);

        if (!storageSuccess)
        {
            return new PdfUploadResult(false, storageResult.ErrorMessage ?? "Failed to store file", null);
        }

        // Start background processing (no quota needed for private game PDFs)
        _backgroundTaskService.ExecuteWithCancellation(
            pdfDoc!.Id.ToString(),
            ct => RunProcessorInScopeAsync(pdfDoc.Id.ToString(), pdfDoc.FilePath, userId, ct));

        // Quartz fallback (same pattern as regular game path)
        await EnqueueForProcessingSafelyAsync(pdfDoc.Id, userId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF uploaded successfully for private game {PrivateGameId}: {FileName} ({FileSize} bytes)",
            privateGameId, fileName, file.Length);

        var documentDto = new PdfDocumentDto(
            Id: pdfDoc.Id,
            GameId: pdfDoc.GameId,
            FileName: pdfDoc.FileName,
            FilePath: pdfDoc.FilePath,
            FileSizeBytes: pdfDoc.FileSizeBytes,

            UploadedAt: pdfDoc.UploadedAt,
            ProcessedAt: pdfDoc.ProcessedAt,
            PageCount: pdfDoc.PageCount,
            ProcessingState: pdfDoc.ProcessingState,
            ProgressPercentage: MapEntityStateToProgress(pdfDoc.ProcessingState),
            RetryCount: pdfDoc.RetryCount,
            DocumentCategory: pdfDoc.DocumentCategory,
            BaseDocumentId: pdfDoc.BaseDocumentId,
            IsActiveForRag: pdfDoc.IsActiveForRag,
            HasAcceptedDisclaimer: pdfDoc.CopyrightDisclaimerAcceptedAt.HasValue,
            VersionLabel: pdfDoc.VersionLabel
        );

        return new PdfUploadResult(true, "PDF upload started successfully", documentDto);
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
