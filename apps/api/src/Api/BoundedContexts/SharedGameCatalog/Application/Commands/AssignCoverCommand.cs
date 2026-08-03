using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 — pins a cover source (and crop focal point) for a UI context on a
/// game, creating the assignment or updating the existing one for that context.
/// </summary>
internal record AssignCoverCommand(
    Guid GameId,
    CoverContext Context,
    CoverAssignmentSource Source,
    Guid AdminId,
    double FocalX = 0.5,
    double FocalY = 0.5) : ICommand<CoverAssignmentDto>;
