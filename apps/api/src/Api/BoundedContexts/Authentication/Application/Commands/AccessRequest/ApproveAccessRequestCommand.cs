using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record ApproveAccessRequestCommand(Guid Id, Guid AdminId) : ICommand<Unit>;
