using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #3481: Command to publish game to SharedGameCatalog.
/// Requires admin privileges (validated in handler).
/// </summary>
/// <param name="GameId">Game to publish</param>
/// <param name="Status">Target approval status</param>
internal record PublishGameCommand(
    Guid GameId,
    ApprovalStatus Status
) : ICommand<GameDto>;
