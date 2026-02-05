using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get count of pending agent typologies awaiting approval (Admin only).
/// Lightweight endpoint for admin dashboard badge.
/// Issue #3381: Typology Approval Workflow Endpoint.
/// </summary>
internal record GetPendingTypologiesCountQuery() : IRequest<int>;
