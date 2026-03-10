using System.Net;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for PreviewEmailTemplateCommand.
/// Renders an email template by replacing {{placeholders}} with test data values.
/// Values are HTML-encoded to prevent XSS in the preview.
/// Issue #53: CQRS handlers for admin email template management.
/// </summary>
internal class PreviewEmailTemplateCommandHandler : ICommandHandler<PreviewEmailTemplateCommand, string>
{
    private readonly IEmailTemplateRepository _repository;
    private readonly ILogger<PreviewEmailTemplateCommandHandler> _logger;

    public PreviewEmailTemplateCommandHandler(
        IEmailTemplateRepository repository,
        ILogger<PreviewEmailTemplateCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> Handle(PreviewEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (template == null)
        {
            _logger.LogWarning("Email template {Id} not found for preview", command.Id);
            return string.Empty;
        }

        var rendered = template.HtmlBody;

        if (command.TestData != null)
        {
            foreach (var kvp in command.TestData)
            {
                var placeholder = $"{{{{{kvp.Key}}}}}";
                var encodedValue = WebUtility.HtmlEncode(kvp.Value);
                rendered = rendered.Replace(placeholder, encodedValue, StringComparison.OrdinalIgnoreCase);
            }
        }

        _logger.LogInformation(
            "Generated preview for email template '{Name}' locale '{Locale}' version {Version} (ID: {Id})",
            template.Name, template.Locale, template.Version, template.Id);

        return rendered;
    }
}
