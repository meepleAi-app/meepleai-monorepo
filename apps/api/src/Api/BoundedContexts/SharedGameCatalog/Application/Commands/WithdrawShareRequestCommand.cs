using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to withdraw a pending share request.
/// Only the request owner can withdraw their own pending requests.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
/// <param name="ShareRequestId">The ID of the share request to withdraw.</param>
/// <param name="UserId">The ID of the user withdrawing the request (for authorization).</param>
internal record WithdrawShareRequestCommand(
    Guid ShareRequestId,
    Guid UserId
) : ICommand;
