using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record RequestAccessCommand(string Email) : ICommand<Unit>;
