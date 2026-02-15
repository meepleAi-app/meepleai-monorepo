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
/// </summary>
internal class EnqueueEmailCommandHandler : ICommandHandler<EnqueueEmailCommand, Guid>
{
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
            htmlBody);

        await _emailQueueRepository.AddAsync(emailQueueItem, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Email enqueued {EmailId} for user {UserId}, template: {Template}",
            emailQueueItem.Id, command.UserId, command.TemplateName);

        return emailQueueItem.Id;
    }
}
