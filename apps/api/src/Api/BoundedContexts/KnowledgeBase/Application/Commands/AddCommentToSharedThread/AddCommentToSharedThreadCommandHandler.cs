using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Globalization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;

/// <summary>
/// Handler for adding comments to shared chat threads.
/// Validates share link token, enforces comment role requirement, and applies rate limiting.
/// </summary>
internal sealed class AddCommentToSharedThreadCommandHandler
    : IRequestHandler<AddCommentToSharedThreadCommand, AddCommentToSharedThreadResult?>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IMediator _mediator;
    private readonly IDistributedCache _cache;
    private readonly IUnitOfWork _unitOfWork;

    // Rate limiting: 10 comments per hour per share link
    private const int MaxCommentsPerHour = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    public AddCommentToSharedThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IMediator mediator,
        IDistributedCache cache,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(threadRepository);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _threadRepository = threadRepository;
        _mediator = mediator;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public async Task<AddCommentToSharedThreadResult?> Handle(
        AddCommentToSharedThreadCommand request,
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

        // Verify comment role (view-only links cannot add comments)
        if (validation.Role != ShareLinkRole.Comment)
        {
            throw new InvalidOperationException(
                "Share link does not have comment permissions");
        }

        // Check rate limiting
        var rateLimitKey = $"share_link_comments:{validation.ShareLinkId}";
        var commentCountStr = await _cache.GetStringAsync(rateLimitKey, cancellationToken).ConfigureAwait(false);
        var commentCount = commentCountStr != null ? int.Parse(commentCountStr, CultureInfo.InvariantCulture) : 0;

        if (commentCount >= MaxCommentsPerHour)
        {
            throw new InvalidOperationException(
                $"Rate limit exceeded: maximum {MaxCommentsPerHour} comments per hour");
        }

        // Load chat thread via repository
        var thread = await _threadRepository.GetByIdAsync(validation.ThreadId, cancellationToken).ConfigureAwait(false);

        if (thread == null)
        {
            return null;
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Message content cannot be empty", nameof(request));
        }

        if (request.Content.Length > 4000)
        {
            throw new ArgumentException("Message content exceeds maximum length (4000 characters)", nameof(request));
        }

        // Add user message to thread (domain method)
        thread.AddUserMessage(request.Content);

        // Save changes. ChatThreadRepository.UpdateAsync is stage-only (it marks the entity as
        // modified without SaveChanges), so the comment is only persisted once the unit of work
        // is committed here.
        await _threadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Update rate limit counter
        await _cache.SetStringAsync(
            rateLimitKey,
            (commentCount + 1).ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = RateLimitWindow
            },
            cancellationToken).ConfigureAwait(false);

        var lastMessage = thread.Messages.OrderByDescending(m => m.SequenceNumber).First();

        return new AddCommentToSharedThreadResult(
            MessageId: lastMessage.Id,
            Timestamp: lastMessage.Timestamp
        );
    }
}
