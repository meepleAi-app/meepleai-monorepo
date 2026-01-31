using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update the list of documents attached to a pending share request.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
/// <param name="ShareRequestId">The ID of the share request to update.</param>
/// <param name="UserId">The ID of the user making the request (for authorization).</param>
/// <param name="DocumentIds">The new list of document IDs to attach.</param>
internal record UpdateShareRequestDocumentsCommand(
    Guid ShareRequestId,
    Guid UserId,
    List<Guid> DocumentIds
) : ICommand;
