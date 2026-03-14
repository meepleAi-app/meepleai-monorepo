using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record RejectAccessRequestCommand(Guid Id, Guid AdminId, string? Reason = null) : ICommand<Unit>;
