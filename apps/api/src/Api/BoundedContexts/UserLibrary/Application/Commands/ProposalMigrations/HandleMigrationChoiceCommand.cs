using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;

/// <summary>
/// Command to handle a user's migration choice after their private game proposal is approved.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
/// <param name="MigrationId">ID of the ProposalMigration</param>
/// <param name="UserId">ID of the user making the choice (must be owner)</param>
/// <param name="Choice">The migration choice (LinkToCatalog or KeepPrivate)</param>
internal record HandleMigrationChoiceCommand(
    Guid MigrationId,
    Guid UserId,
    PostApprovalMigrationChoice Choice
) : ICommand;
