using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for EnqueueEmailCommand.
/// Renders email template and enqueues for async delivery.
/// Issue #4417: Email notification queue.
/// Issue #4429: Email throttling (per-user rate limit + per-PDF dedup).
/// </summary>
internal class EnqueueEmailCommandHandler : ICommandHandler<EnqueueEmailCommand, Guid>
{
    private const int MaxEmailsPerUserPerHour = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromHours(1);

    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnqueueEmailCommandHandler> _logger;

    public EnqueueEmailCommandHandler(
        IEmailQueueRepository emailQueueRepository,
        IEmailTemplateService emailTemplateService,
        IUnitOfWork unitOfWork,
        ILogger<EnqueueEmailCommandHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository;
        _emailTemplateService = emailTemplateService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Guid> Handle(EnqueueEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = DateTime.UtcNow;
        var since = now - RateLimitWindow;

        // Issue #4429: Per-PDF deduplication - skip if same user+subject within 1 hour
        // Subject includes fileName (set by PdfNotificationEventHandler) so each PDF is unique
        var isDuplicate = await _emailQueueRepository
            .ExistsSimilarRecentAsync(command.UserId, command.Subject, now - DeduplicationWindow, cancellationToken)
            .ConfigureAwait(false);

        if (isDuplicate)
        {
            _logger.LogWarning(
                "Email throttled (duplicate): user {UserId}, subject \"{Subject}\" already enqueued within {Window}",
                command.UserId, command.Subject, DeduplicationWindow);
            return Guid.Empty;
        }

        // Issue #4429: Per-user rate limit - max 10 emails per user per hour
        var recentCount = await _emailQueueRepository
            .GetRecentCountByUserIdAsync(command.UserId, since, cancellationToken)
            .ConfigureAwait(false);

        if (recentCount >= MaxEmailsPerUserPerHour)
        {
            _logger.LogWarning(
                "Email throttled (rate limit): user {UserId} has {Count}/{Max} emails in last {Window}",
                command.UserId, recentCount, MaxEmailsPerUserPerHour, RateLimitWindow);
            return Guid.Empty;
        }

        var htmlBody = command.TemplateName switch
        {
            "document_ready" => _emailTemplateService.RenderDocumentReady(
                command.UserName, command.FileName, command.DocumentUrl ?? string.Empty),
            "document_failed" => _emailTemplateService.RenderDocumentFailed(
                command.UserName, command.FileName, command.ErrorMessage ?? "Unknown error"),
            "retry_available" => _emailTemplateService.RenderRetryAvailable(
                command.UserName, command.FileName, command.RetryCount ?? 1),
            _ => throw new ArgumentException($"Unknown email template: {command.TemplateName}", nameof(command))
        };

        var emailQueueItem = EmailQueueItem.Create(
            command.UserId,
            command.To,
            command.Subject,
            htmlBody,
            command.CorrelationId);

        await _emailQueueRepository.AddAsync(emailQueueItem, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Email enqueued {EmailId} for user {UserId}, template: {Template}",
            emailQueueItem.Id, command.UserId, command.TemplateName);

        return emailQueueItem.Id;
    }
}
