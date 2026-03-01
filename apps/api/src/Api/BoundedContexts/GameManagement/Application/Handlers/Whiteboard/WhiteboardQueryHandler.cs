using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.Queries.Whiteboard;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Whiteboard;

/// <summary>
/// Gets the current WhiteboardState for a session.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class GetWhiteboardStateQueryHandler : IQueryHandler<GetWhiteboardStateQuery, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;

    public GetWhiteboardStateQueryHandler(IWhiteboardStateRepository whiteboardRepository)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
    }

    public async Task<WhiteboardStateDto> Handle(GetWhiteboardStateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var whiteboard = await _whiteboardRepository.GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("WhiteboardState", query.SessionId.ToString());

        return WhiteboardMapper.ToDto(whiteboard);
    }
}
