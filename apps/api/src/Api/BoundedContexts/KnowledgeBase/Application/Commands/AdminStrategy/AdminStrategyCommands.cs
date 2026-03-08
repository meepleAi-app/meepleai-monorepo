using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;

/// <summary>
/// Issue #5314: Admin Strategy CRUD commands.
/// </summary>
public sealed record CreateAdminStrategyCommand(
    string Name,
    string Description,
    string StepsJson,
    Guid AdminUserId) : IRequest<AdminStrategyResult>;

public sealed record UpdateAdminStrategyCommand(
    Guid Id,
    string Name,
    string Description,
    string StepsJson,
    Guid AdminUserId) : IRequest<AdminStrategyResult>;

public sealed record DeleteAdminStrategyCommand(
    Guid Id,
    Guid AdminUserId) : IRequest<bool>;

public sealed record AdminStrategyResult(
    Guid Id,
    string Name,
    string Description,
    string StepsJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
