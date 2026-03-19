using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for PublishEmailTemplateCommand.
/// Activates the target template version and deactivates all other versions
/// of the same (name, locale) pair within a single transaction.
/// Issue #53: CQRS handlers for admin email template management.
/// </summary>
internal class PublishEmailTemplateCommandHandler : ICommandHandler<PublishEmailTemplateCommand, bool>
{
    private readonly IEmailTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PublishEmailTemplateCommandHandler> _logger;

    public PublishEmailTemplateCommandHandler(
        IEmailTemplateRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PublishEmailTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(PublishEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (template == null)
        {
            _logger.LogWarning("Email template {Id} not found for publishing", command.Id);
            return false;
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Deactivate all other versions of the same (name, locale)
            var allVersions = await _repository.GetByNameAsync(template.Name, cancellationToken).ConfigureAwait(false);
            foreach (var version in allVersions.Where(v => string.Equals(v.Locale, template.Locale, StringComparison.Ordinal) && v.Id != template.Id && v.IsActive))
            {
                version.Deactivate();
                await _repository.UpdateAsync(version, cancellationToken).ConfigureAwait(false);
            }

            // Activate the target version
            template.Activate();
            await _repository.UpdateAsync(template, cancellationToken).ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Published email template '{Name}' locale '{Locale}' version {Version} (ID: {Id})",
                template.Name, template.Locale, template.Version, template.Id);

            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
