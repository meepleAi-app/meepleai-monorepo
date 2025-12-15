using MediatR;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Marker interface for commands in CQRS pattern.
/// Commands represent intent to change system state.
/// </summary>
internal interface ICommand : IRequest
{
}

/// <summary>
/// Marker interface for commands that return a result.
/// </summary>
/// <typeparam name="TResponse">The type of the command result</typeparam>
internal interface ICommand<out TResponse> : IRequest<TResponse>
{
}
