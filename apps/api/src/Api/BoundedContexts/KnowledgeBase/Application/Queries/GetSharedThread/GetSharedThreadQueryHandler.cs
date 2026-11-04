using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;

/// <summary>
/// Handler for retrieving shared chat threads via share link token.
/// Validates token and returns thread with read-only message view.
/// </summary>
internal sealed class GetSharedThreadQueryHandler : IRequestHandler<GetSharedThreadQuery, GetSharedThreadResult?>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IShareLinkRepository _shareLinkRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetSharedThreadQueryHandler> _logger;

    public GetSharedThreadQueryHandler(
        IChatThreadRepository threadRepository,
        IShareLinkRepository shareLinkRepository,
        IMediator mediator,
        ILogger<GetSharedThreadQueryHandler> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetSharedThreadResult?> Handle(
        GetSharedThreadQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate share link token
        var validation = await _mediator.Send(
            new ValidateShareLinkQuery(request.Token),
            cancellationToken).ConfigureAwait(false);

        if (validation == null || !validation.IsValid)
        {
            // Invalid or revoked token
            return null;
        }

        // Load chat thread via repository
        var thread = await _threadRepository.GetByIdAsync(validation.ThreadId, cancellationToken).ConfigureAwait(false);

        if (thread == null)
        {
            // Thread not found (should not happen if share link was created properly)
            return null;
        }

        // Conta la visita e, nello stesso statement, controlla che il link sia ancora valido sulla riga (#4573):
        // fra la validazione del token e qui il proprietario può averlo revocato, e la riga è la verità anche
        // quando la chiave nella blacklist Redis si è persa. Non è più solo analytics, quindi un errore NON si
        // inghiotte: se il controllo non riesce non si sa se il link è revocato, e la chat non si serve
        // (come GetSharedLibraryQueryHandler, #4558). Sincrono per non uscire dallo scope DI (#2150).
        if (!await _shareLinkRepository.TryRecordAccessAsync(validation.ShareLinkId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Share link {ShareLinkId} became invalid while being visited (revoked or expired)",
                validation.ShareLinkId);
            return null;
        }

        _logger.LogDebug("Recorded share link access for ShareLinkId {ShareLinkId}", validation.ShareLinkId);

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