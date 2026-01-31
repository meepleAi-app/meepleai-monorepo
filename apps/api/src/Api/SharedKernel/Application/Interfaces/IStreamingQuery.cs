using MediatR;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Interface for streaming queries in CQRS pattern.
/// Streaming queries return IAsyncEnumerable for progressive data delivery (e.g., SSE, WebSockets).
/// </summary>
/// <typeparam name="TResponse">The type of each item in the stream</typeparam>
internal interface IStreamingQuery<out TResponse> : IStreamRequest<TResponse>
{
}
