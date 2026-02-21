using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Queries.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.TurnOrder;

/// <summary>
/// Gets the current TurnOrder state for a session.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class GetTurnOrderQueryHandler : IQueryHandler<GetTurnOrderQuery, TurnOrderDto>
{
    private readonly ITurnOrderRepository _turnOrderRepository;

    public GetTurnOrderQueryHandler(ITurnOrderRepository turnOrderRepository)
    {
        _turnOrderRepository = turnOrderRepository ?? throw new ArgumentNullException(nameof(turnOrderRepository));
    }

    public async Task<TurnOrderDto> Handle(GetTurnOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var turnOrder = await _turnOrderRepository.GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("TurnOrder", query.SessionId.ToString());

        return TurnOrderMapper.ToDto(turnOrder);
    }
}
