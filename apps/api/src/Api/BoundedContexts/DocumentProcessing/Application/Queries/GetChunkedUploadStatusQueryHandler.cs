using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetChunkedUploadStatusQuery.
/// Returns the current status of a chunked upload session.
/// </summary>
public class GetChunkedUploadStatusQueryHandler : IQueryHandler<GetChunkedUploadStatusQuery, ChunkedUploadStatusResult?>
{
    private readonly IChunkedUploadSessionRepository _sessionRepository;

    public GetChunkedUploadStatusQueryHandler(IChunkedUploadSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<ChunkedUploadStatusResult?> Handle(
        GetChunkedUploadStatusQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return null;
        }

        // Verify ownership
        if (session.UserId != request.UserId)
        {
            return null;
        }

        return new ChunkedUploadStatusResult(
            SessionId: session.Id,
            FileName: session.FileName,
            TotalFileSize: session.TotalFileSize,
            TotalChunks: session.TotalChunks,
            ReceivedChunks: session.ReceivedChunks,
            ProgressPercentage: session.ProgressPercentage,
            Status: session.Status,
            CreatedAt: session.CreatedAt,
            ExpiresAt: session.ExpiresAt,
            MissingChunks: session.GetMissingChunks()
        );
    }
}
