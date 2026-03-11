using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for UpdateEmailTemplateCommand.
/// Updates subject and HTML body of an existing template.
/// Issue #53: CQRS handlers for admin email template management.
/// </summary>
internal class UpdateEmailTemplateCommandHandler : ICommandHandler<UpdateEmailTemplateCommand, bool>
{
    private readonly IEmailTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateEmailTemplateCommandHandler> _logger;

    public UpdateEmailTemplateCommandHandler(
        IEmailTemplateRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateEmailTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (template == null)
        {
            _logger.LogWarning("Email template {Id} not found for update", command.Id);
            return false;
        }

        template.UpdateContent(command.Subject, command.HtmlBody, command.ModifiedBy);

        await _repository.UpdateAsync(template, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated email template '{Name}' locale '{Locale}' version {Version} (ID: {Id})",
            template.Name, template.Locale, template.Version, template.Id);

        return true;
    }
}
