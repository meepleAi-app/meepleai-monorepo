using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve a chat thread by ID.
/// </summary>
public record GetChatThreadByIdQuery(
    Guid ThreadId
) : IQuery<ChatThreadDto?>;
