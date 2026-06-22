using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles public (anonymous) retrieval of a play record by its share token.
/// No user identity check — possessing the token IS the authorization.
/// Delegates DTO mapping (photo presigning, outcome computation, scoring-config deserialization)
/// to <see cref="PlayRecordDtoMapper"/>, shared with <see cref="GetPlayRecordQueryHandler"/>.
/// Issue #2437-2.
/// </summary>
internal class GetPlayRecordByShareTokenQueryHandler : IQueryHandler<GetPlayRecordByShareTokenQuery, PlayRecordDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public GetPlayRecordByShareTokenQueryHandler(MeepleAiDbContext context, IBlobStorageService blobStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<PlayRecordDto> Handle(GetPlayRecordByShareTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.ShareToken == query.ShareToken && r.IsShared, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new NotFoundException("PlayRecord", query.ShareToken);

        return await PlayRecordDtoMapper.MapAsync(
            entity, _blobStorage, PlayRecordPhotoUrlResolver.DefaultExpirySeconds, cancellationToken)
            .ConfigureAwait(false);
    }
}
