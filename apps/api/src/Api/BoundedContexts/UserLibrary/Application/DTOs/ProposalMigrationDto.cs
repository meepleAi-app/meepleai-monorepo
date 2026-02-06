using Api.BoundedContexts.UserLibrary.Domain.Enums;

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for ProposalMigration data.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
/// <param name="Id">Migration ID</param>
/// <param name="ShareRequestId">Reference to the approved ShareRequest</param>
/// <param name="PrivateGameId">Original PrivateGame ID</param>
/// <param name="PrivateGameTitle">Title of the private game for display</param>
/// <param name="SharedGameId">Newly created SharedGame ID</param>
/// <param name="SharedGameTitle">Title of the SharedGame for display</param>
/// <param name="Choice">Current migration choice status</param>
/// <param name="CreatedAt">When the migration was created (proposal approved)</param>
/// <param name="ChoiceAt">When the user made their choice</param>
public record ProposalMigrationDto(
    Guid Id,
    Guid ShareRequestId,
    Guid PrivateGameId,
    string PrivateGameTitle,
    Guid SharedGameId,
    string SharedGameTitle,
    PostApprovalMigrationChoice Choice,
    DateTime CreatedAt,
    DateTime? ChoiceAt
);
