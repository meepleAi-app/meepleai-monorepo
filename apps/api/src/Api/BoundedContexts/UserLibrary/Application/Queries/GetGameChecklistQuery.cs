using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to fetch game-specific setup checklist items with optional wizard steps.
/// </summary>
internal record GetGameChecklistQuery(
    Guid GameId,
    bool IncludeWizard = false
) : IQuery<ChecklistDto>;
