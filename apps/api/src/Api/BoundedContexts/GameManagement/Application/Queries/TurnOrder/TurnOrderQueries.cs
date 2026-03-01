using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.TurnOrder;

/// <summary>
/// Gets the current TurnOrder state for a session.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal sealed record GetTurnOrderQuery(Guid SessionId) : IQuery<TurnOrderDto>;
