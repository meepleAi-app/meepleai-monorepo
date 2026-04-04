using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record BulkApproveAccessRequestsCommand(
    IReadOnlyList<Guid> Ids,
    Guid AdminId) : ICommand<BulkApproveResultDto>;
