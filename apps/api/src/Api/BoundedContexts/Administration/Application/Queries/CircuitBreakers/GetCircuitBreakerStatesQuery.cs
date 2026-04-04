using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.CircuitBreakers;

internal record GetCircuitBreakerStatesQuery : IQuery<IReadOnlyList<CircuitBreakerStateDto>>;

internal sealed class GetCircuitBreakerStatesQueryHandler
    : IQueryHandler<GetCircuitBreakerStatesQuery, IReadOnlyList<CircuitBreakerStateDto>>
{
    private readonly ICircuitBreakerStateTracker _tracker;

    public GetCircuitBreakerStatesQueryHandler(ICircuitBreakerStateTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public Task<IReadOnlyList<CircuitBreakerStateDto>> Handle(
        GetCircuitBreakerStatesQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tracker.GetAllStates());
    }
}
