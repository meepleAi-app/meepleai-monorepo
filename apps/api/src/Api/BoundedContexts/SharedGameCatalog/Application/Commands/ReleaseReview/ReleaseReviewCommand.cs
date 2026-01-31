using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to manually release a review lock without making a decision.
/// Returns the share request to its previous state (Pending or ChangesRequested).
/// </summary>
internal sealed record ReleaseReviewCommand(
    Guid ShareRequestId,
    Guid ReviewingAdminId) : ICommand<Unit>;
