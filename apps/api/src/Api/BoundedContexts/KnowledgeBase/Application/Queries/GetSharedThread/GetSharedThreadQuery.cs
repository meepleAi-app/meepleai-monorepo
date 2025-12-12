using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;

/// <summary>
/// Query to retrieve a shared chat thread via share link token.
/// </summary>
/// <param name="Token">JWT token from share link URL</param>
public sealed record GetSharedThreadQuery(string Token) : IRequest<GetSharedThreadResult?>;

/// <summary>
/// Result of shared thread retrieval.
/// </summary>
/// <param name="ThreadId">Chat thread identifier</param>
/// <param name="Title">Thread title</param>
/// <param name="Messages">Thread messages (read-only view)</param>
/// <param name="Role">Access level (view or comment)</param>
/// <param name="GameId">Associated game ID (if any)</param>
/// <param name="CreatedAt">Thread creation timestamp</param>
/// <param name="LastMessageAt">Last message timestamp</param>
public sealed record GetSharedThreadResult(
    Guid ThreadId,
    string? Title,
    IReadOnlyList<ChatMessageDto> Messages,
    ShareLinkRole Role,
    Guid? GameId,
    DateTime CreatedAt,
    DateTime? LastMessageAt
);
