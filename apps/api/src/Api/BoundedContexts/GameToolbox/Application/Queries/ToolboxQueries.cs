#pragma warning disable MA0048 // File name must match type name - Contains related queries
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Queries;

internal record GetToolboxQuery(Guid Id) : IRequest<ToolboxDto?>;

internal record GetToolboxByGameQuery(Guid GameId) : IRequest<ToolboxDto?>;

internal record GetToolboxTemplatesQuery(Guid? GameId = null) : IRequest<List<ToolboxTemplateDto>>;

internal record GetAvailableToolsQuery() : IRequest<List<AvailableToolDto>>;
