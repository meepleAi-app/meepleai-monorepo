using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Command to soft-delete an EntityLink (Issue #5134).
///
/// Authorization rules (enforced by handler):
/// - Admin (IsAdmin=true)  → can delete any link
/// - User                  → can delete only their own links (OwnerUserId == RequestingUserId)
/// - BGG-imported links    → non-admin users may NOT delete them
/// </summary>
internal record DeleteEntityLinkCommand(
    Guid EntityLinkId,
    Guid RequestingUserId,
    bool IsAdmin
) : ICommand;
