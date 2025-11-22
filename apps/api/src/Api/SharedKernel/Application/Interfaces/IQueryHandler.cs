using MediatR;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Interface for query handlers in CQRS pattern.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle</typeparam>
/// <typeparam name="TResponse">The type of the query result</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
