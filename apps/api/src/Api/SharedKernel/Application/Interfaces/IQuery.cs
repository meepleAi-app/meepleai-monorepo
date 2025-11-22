using MediatR;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Marker interface for queries in CQRS pattern.
/// Queries represent intent to retrieve data without changing system state.
/// </summary>
/// <typeparam name="TResponse">The type of the query result</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
