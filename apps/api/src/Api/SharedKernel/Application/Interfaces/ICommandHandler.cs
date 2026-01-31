using MediatR;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Interface for command handlers in CQRS pattern.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
internal interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{
}

/// <summary>
/// Interface for command handlers that return a result.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
/// <typeparam name="TResponse">The type of the command result</typeparam>
internal interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}
