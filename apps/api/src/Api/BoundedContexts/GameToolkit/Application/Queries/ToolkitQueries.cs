#pragma warning disable MA0048 // File name must match type name - Contains related queries
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries;

internal record GetToolkitQuery(
    Guid ToolkitId
) : IQuery<GameToolkitDto?>;

internal record GetToolkitsByGameQuery(
    Guid GameId
) : IQuery<IReadOnlyList<GameToolkitDto>>;

internal record GetToolkitsByPrivateGameQuery(
    Guid PrivateGameId,
    Guid CallingUserId
) : IQuery<IReadOnlyList<GameToolkitDto>>;

internal record GetPublishedToolkitsQuery() : IQuery<IReadOnlyList<GameToolkitDto>>;

internal record GetApprovedTemplatesQuery(
    Api.BoundedContexts.GameToolkit.Domain.Enums.TemplateCategory? Category = null
) : IQuery<IReadOnlyList<GameToolkitDto>>;

internal record GetPendingReviewTemplatesQuery() : IQuery<IReadOnlyList<GameToolkitDto>>;
