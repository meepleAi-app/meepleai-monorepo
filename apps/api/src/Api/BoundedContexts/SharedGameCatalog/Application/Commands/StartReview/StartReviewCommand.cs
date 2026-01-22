using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to start the review process for a share request.
/// Acquires an exclusive lock for the reviewing admin.
/// </summary>
internal sealed record StartReviewCommand(
    Guid ShareRequestId,
    Guid ReviewingAdminId) : ICommand<Unit>;
