using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to create a share request for contributing a game from user's library
/// to the shared catalog.
/// </summary>
/// <param name="UserId">The ID of the user creating the request.</param>
/// <param name="SourceGameId">The ID of the game in user's library to share.</param>
/// <param name="Notes">Optional notes from the user about the contribution.</param>
/// <param name="AttachedDocumentIds">Optional list of document IDs to attach (e.g., rulebook PDFs).</param>
internal record CreateShareRequestCommand(
    Guid UserId,
    Guid SourceGameId,
    string? Notes = null,
    List<Guid>? AttachedDocumentIds = null
) : ICommand<CreateShareRequestResponse>;

/// <summary>
/// Response returned after successfully creating a share request.
/// </summary>
/// <param name="ShareRequestId">The ID of the created share request.</param>
/// <param name="Status">The initial status of the request (Pending).</param>
/// <param name="ContributionType">Whether this is a new game or additional content.</param>
/// <param name="CreatedAt">When the request was created.</param>
public record CreateShareRequestResponse(
    Guid ShareRequestId,
    ShareRequestStatus Status,
    ContributionType ContributionType,
    DateTime CreatedAt);
