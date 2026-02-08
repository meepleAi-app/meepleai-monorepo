using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.ProposalMigrations;

/// <summary>
/// Query to retrieve all pending ProposalMigrations for a user.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
/// <param name="UserId">ID of the user requesting their pending migrations</param>
internal record GetPendingMigrationsQuery(
    Guid UserId
) : IQuery<List<ProposalMigrationDto>>;
