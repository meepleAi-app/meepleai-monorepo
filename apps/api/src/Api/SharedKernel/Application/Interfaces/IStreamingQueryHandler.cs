using MediatR;
using System.Runtime.CompilerServices;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Interface for streaming query handlers in CQRS pattern.
/// Handlers return IAsyncEnumerable for progressive data delivery.
/// </summary>
/// <typeparam name="TQuery">The type of streaming query to handle</typeparam>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
public interface IStreamingQueryHandler<in TQuery, TResponse> : IStreamRequestHandler<TQuery, TResponse>
    where TQuery : IStreamingQuery<TResponse>
{
    // Inherited from IStreamRequestHandler:
    // IAsyncEnumerable<TResponse> Handle(TQuery request, [EnumeratorCancellation] CancellationToken cancellationToken);
}
