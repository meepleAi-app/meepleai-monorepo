using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;

/// <summary>
/// Handler for retrieving shared chat threads via share link token.
/// Validates token and returns thread with read-only message view.
/// </summary>
public sealed class GetSharedThreadQueryHandler : IRequestHandler<GetSharedThreadQuery, GetSharedThreadResult?>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IShareLinkRepository _shareLinkRepository;
    private readonly IMediator _mediator;

    public GetSharedThreadQueryHandler(
        IChatThreadRepository threadRepository,
        IShareLinkRepository shareLinkRepository,
        IMediator mediator)
    {
        _threadRepository = threadRepository;
        _shareLinkRepository = shareLinkRepository;
        _mediator = mediator;
    }

    public async Task<GetSharedThreadResult?> Handle(
        GetSharedThreadQuery request,
        CancellationToken cancellationToken)
    {
        // Validate share link token
        var validation = await _mediator.Send(
            new ValidateShareLinkQuery(request.Token),
            cancellationToken);

        if (validation == null || !validation.IsValid)
        {
            // Invalid or revoked token
            return null;
        }

        // Load chat thread via repository
        var thread = await _threadRepository.GetByIdAsync(validation.ThreadId, cancellationToken);

        if (thread == null)
        {
            // Thread not found (should not happen if share link was created properly)
            return null;
        }

        // Record access for analytics (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var shareLink = await _shareLinkRepository.GetByIdAsync(validation.ShareLinkId, CancellationToken.None);

                if (shareLink != null)
                {
                    shareLink.RecordAccess();
                    await _shareLinkRepository.UpdateAsync(shareLink, CancellationToken.None);
                }
            }
            catch
            {
                // Ignore analytics errors
            }
        }, CancellationToken.None);

        // Convert messages to DTO (filter deleted messages for shared view)
        var messageDtos = thread.Messages
            .Where(m => !m.IsDeleted) // Hide deleted messages in shared view
            .OrderBy(m => m.SequenceNumber)
            .Select(m => new ChatMessageDto(
                Id: m.Id,
                Content: m.Content,
                Role: m.Role,
                Timestamp: m.Timestamp,
                SequenceNumber: m.SequenceNumber,
                UpdatedAt: m.UpdatedAt,
                IsDeleted: false, // Already filtered
                DeletedAt: null,
                DeletedByUserId: null,
                IsInvalidated: m.IsInvalidated
            ))
            .ToList();

        return new GetSharedThreadResult(
            ThreadId: thread.Id,
            Title: thread.Title,
            Messages: messageDtos,
            Role: validation.Role,
            GameId: thread.GameId,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt
        );
    }
}
