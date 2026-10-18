using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for InitChunkedUploadCommand.
/// Creates a new chunked upload session and prepares the temp directory.
/// </summary>
internal class InitChunkedUploadCommandHandler : ICommandHandler<InitChunkedUploadCommand, InitChunkedUploadResult>
{
    /// <summary>
    /// How many chunked uploads one user may have open at once. Read by <c>GetUploadLimitsQuery</c>
    /// so the web shows this number instead of a copy of it (#4449).
    /// </summary>
    internal const int MaxActiveSessionsPerUser = 3;

    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<InitChunkedUploadCommandHandler> _logger;
    private readonly string _uploadTempBasePath;
    private readonly long _maxFileSizeBytes;

    public InitChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILogger<InitChunkedUploadCommandHandler> logger,
        IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        _maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use a temp directory for chunked uploads
        _uploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");
    }

    public async Task<InitChunkedUploadResult> Handle(
        InitChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            // Validate file extension
            if (!request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: "Only PDF files are allowed"
                );
            }

            // #4449: the single PDF limit. InitChunkedUploadCommandValidator refuses first; this is the
            // handler's own guard, and it used to be a 500 MB number of its own.
            if (request.TotalFileSize > _maxFileSizeBytes)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: $"File size exceeds maximum allowed ({PdfSizeLimit.Describe(_maxFileSizeBytes)})"
                );
            }

            // Validate that exactly one of GameId or PrivateGameId is provided
            if (!request.GameId.HasValue && !request.PrivateGameId.HasValue)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: "Either GameId or PrivateGameId must be provided."
                );
            }

            // #4395: the game must exist. The completed upload ties the PDF to it (SharedGameId is a foreign
            // key), so an unknown id used to fail only after the whole file was uploaded and stored,
            // leaving the blob behind.
            var gameExists = request.PrivateGameId.HasValue
                ? await _dbContext.PrivateGames.AnyAsync(g => g.Id == request.PrivateGameId.Value, cancellationToken).ConfigureAwait(false)
                : await _dbContext.SharedGames.AnyAsync(g => g.Id == request.GameId!.Value, cancellationToken).ConfigureAwait(false);
            if (!gameExists)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: "Game not found."
                );
            }

            // Check for existing active sessions for this user (at most MaxActiveSessionsPerUser at once)
            var activeSessions = await _sessionRepository.FindActiveByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
            if (activeSessions.Count >= MaxActiveSessionsPerUser)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: $"Maximum concurrent uploads ({MaxActiveSessionsPerUser}) reached. Complete or cancel existing uploads first."
                );
            }

            // Create session ID and temp directory
            var sessionId = Guid.NewGuid();
            var tempDirectory = Path.Combine(_uploadTempBasePath, sessionId.ToString());

            // Ensure temp directory exists
            Directory.CreateDirectory(tempDirectory);

            // Create session entity
            var session = new ChunkedUploadSession(
                id: sessionId,
                gameId: request.GameId,
                userId: request.UserId,
                fileName: request.FileName,
                totalFileSize: request.TotalFileSize,
                tempDirectory: tempDirectory,
                privateGameId: request.PrivateGameId
            );

            await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Chunked upload session {SessionId} initialized for user {UserId}, file {FileName} ({FileSize} bytes, {TotalChunks} chunks)",
                sessionId, request.UserId, request.FileName, request.TotalFileSize, session.TotalChunks);

            return new InitChunkedUploadResult(
                Success: true,
                SessionId: sessionId,
                TotalChunks: session.TotalChunks,
                ChunkSizeBytes: ChunkedUploadSession.MaxChunkSizeBytes,
                ExpiresAt: session.ExpiresAt,
                ErrorMessage: null
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // COMMAND HANDLER PATTERN: CQRS handler boundary
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result/Response pattern.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to initialize chunked upload session for user {UserId}", request.UserId);
            return new InitChunkedUploadResult(
                Success: false,
                SessionId: null,
                TotalChunks: 0,
                ChunkSizeBytes: 0,
                ExpiresAt: null,
                ErrorMessage: "Failed to initialize upload session"
            );
        }
    }
}
