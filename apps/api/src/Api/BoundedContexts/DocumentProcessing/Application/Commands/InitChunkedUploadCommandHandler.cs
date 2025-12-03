using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for InitChunkedUploadCommand.
/// Creates a new chunked upload session and prepares the temp directory.
/// </summary>
public class InitChunkedUploadCommandHandler : ICommandHandler<InitChunkedUploadCommand, InitChunkedUploadResult>
{
    private readonly IChunkedUploadSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<InitChunkedUploadCommandHandler> _logger;
    private readonly string _uploadTempBasePath;

    public InitChunkedUploadCommandHandler(
        IChunkedUploadSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILogger<InitChunkedUploadCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
        _logger = logger;

        // Use a temp directory for chunked uploads
        _uploadTempBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");
    }

    public async Task<InitChunkedUploadResult> Handle(
        InitChunkedUploadCommand request,
        CancellationToken cancellationToken)
    {
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

            // Validate file size (max 500 MB for now)
            const long maxFileSizeBytes = 500L * 1024 * 1024;
            if (request.TotalFileSize > maxFileSizeBytes)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: $"File size exceeds maximum allowed ({maxFileSizeBytes / (1024 * 1024)} MB)"
                );
            }

            // Check for existing active sessions for this user (limit to 3 concurrent uploads)
            var activeSessions = await _sessionRepository.FindActiveByUserIdAsync(request.UserId, cancellationToken);
            if (activeSessions.Count >= 3)
            {
                return new InitChunkedUploadResult(
                    Success: false,
                    SessionId: null,
                    TotalChunks: 0,
                    ChunkSizeBytes: 0,
                    ExpiresAt: null,
                    ErrorMessage: "Maximum concurrent uploads (3) reached. Complete or cancel existing uploads first."
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
                tempDirectory: tempDirectory
            );

            await _sessionRepository.AddAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

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
        catch (Exception ex)
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
