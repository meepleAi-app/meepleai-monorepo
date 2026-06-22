using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles retrieving a single play record with full details (authenticated, owner/player access).
/// Issue #3890: CQRS queries for play records.
/// Issue #2436 PR-C: Photos presigned read-path.
/// Issue #2437-2: Delegates DTO mapping to <see cref="PlayRecordDtoMapper"/> (shared with anonymous share-token query).
/// </summary>
internal class GetPlayRecordQueryHandler : IQueryHandler<GetPlayRecordQuery, PlayRecordDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public GetPlayRecordQueryHandler(MeepleAiDbContext context, IBlobStorageService blobStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<PlayRecordDto> Handle(GetPlayRecordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.Id == query.RecordId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new NotFoundException("PlayRecord", query.RecordId.ToString());

        if (entity.CreatedByUserId != query.UserId
            && !entity.Players.Any(p => p.UserId == query.UserId))
        {
            throw new ForbiddenException("You do not have permission to view this play record.");
        }

        return await PlayRecordDtoMapper.MapAsync(
            entity, _blobStorage, PlayRecordPhotoUrlResolver.DefaultExpirySeconds, cancellationToken)
            .ConfigureAwait(false);
    }
}
