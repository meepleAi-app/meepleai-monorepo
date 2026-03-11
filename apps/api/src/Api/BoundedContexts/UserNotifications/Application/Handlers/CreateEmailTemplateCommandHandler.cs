using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for CreateEmailTemplateCommand.
/// Creates a new email template with auto-assigned version number.
/// Issue #53: CQRS handlers for admin email template management.
/// </summary>
internal class CreateEmailTemplateCommandHandler : ICommandHandler<CreateEmailTemplateCommand, Guid>
{
    private readonly IEmailTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEmailTemplateCommandHandler> _logger;

    public CreateEmailTemplateCommandHandler(
        IEmailTemplateRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateEmailTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = EmailTemplate.Create(
            command.Name,
            command.Locale,
            command.Subject,
            command.HtmlBody,
            command.CreatedBy);

        await _repository.AddAsync(template, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created email template '{Name}' locale '{Locale}' version {Version} with ID {Id}",
            template.Name, template.Locale, template.Version, template.Id);

        return template.Id;
    }
}
