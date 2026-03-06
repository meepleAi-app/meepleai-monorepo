using Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AdminStrategy;

/// <summary>
/// Issue #5314: Admin Strategy CRUD queries.
/// </summary>
public sealed record ListAdminStrategiesQuery : IRequest<IReadOnlyList<AdminStrategyResult>>;

public sealed record GetAdminStrategyByIdQuery(Guid Id) : IRequest<AdminStrategyResult?>;
