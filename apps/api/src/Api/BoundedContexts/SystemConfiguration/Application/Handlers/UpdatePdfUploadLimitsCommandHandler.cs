using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles updating PDF upload limits configuration.
/// Creates configurations if they don't exist, updates existing ones otherwise.
/// Triggers cache invalidation and audit trail via domain events.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal class UpdatePdfUploadLimitsCommandHandler : ICommandHandler<UpdatePdfUploadLimitsCommand, PdfUploadLimitsDto>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationRepository _configRepository;

    private const string MaxFileSizeKey = "PdfUpload:MaxFileSizeBytes";
    private const string MaxPagesKey = "PdfUpload:MaxPagesPerDocument";
    private const string MaxDocumentsKey = "PdfUpload:MaxDocumentsPerGame";
    private const string AllowedMimeTypesKey = "PdfUpload:AllowedMimeTypes";
    private const string Category = "PdfUpload";
    private const string Environment = "All";

    public UpdatePdfUploadLimitsCommandHandler(
        IMediator mediator,
        IConfigurationRepository configRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<PdfUploadLimitsDto> Handle(
        UpdatePdfUploadLimitsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Convert MIME types array to comma-separated string for storage
        var mimeTypesValue = string.Join(",", command.AllowedMimeTypes);

        // Process all configurations in parallel
        var tasks = new[]
        {
            UpsertConfigurationAsync(
                MaxFileSizeKey,
                command.MaxFileSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "long",
                "Maximum file size in bytes for PDF uploads",
                command.UpdatedByUserId,
                cancellationToken),
            UpsertConfigurationAsync(
                MaxPagesKey,
                command.MaxPagesPerDocument.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "int",
                "Maximum number of pages allowed per PDF document",
                command.UpdatedByUserId,
                cancellationToken),
            UpsertConfigurationAsync(
                MaxDocumentsKey,
                command.MaxDocumentsPerGame.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "int",
                "Maximum number of PDF documents allowed per game",
                command.UpdatedByUserId,
                cancellationToken),
            UpsertConfigurationAsync(
                AllowedMimeTypesKey,
                mimeTypesValue,
                "string",
                "Comma-separated list of allowed MIME types for PDF uploads",
                command.UpdatedByUserId,
                cancellationToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Return updated limits
        return new PdfUploadLimitsDto(
            MaxFileSizeBytes: command.MaxFileSizeBytes,
            MaxPagesPerDocument: command.MaxPagesPerDocument,
            MaxDocumentsPerGame: command.MaxDocumentsPerGame,
            AllowedMimeTypes: command.AllowedMimeTypes,
            LastUpdatedAt: DateTime.UtcNow,
            LastUpdatedByUserId: command.UpdatedByUserId.ToString()
        );
    }

    private async Task UpsertConfigurationAsync(
        string key,
        string value,
        string valueType,
        string description,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Check if configuration already exists
        var existingConfig = await _configRepository.GetByKeyAsync(
            key,
            Environment,
            activeOnly: false,
            cancellationToken).ConfigureAwait(false);

        if (existingConfig == null)
        {
            // Create new configuration
            var createCommand = new CreateConfigurationCommand(
                Key: key,
                Value: value,
                ValueType: valueType,
                Description: description,
                Category: Category,
                Environment: Environment,
                RequiresRestart: false,
                CreatedByUserId: userId
            );

            await _mediator.Send(createCommand, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Update existing configuration
            var updateCommand = new UpdateConfigValueCommand(
                ConfigId: existingConfig.Id,
                NewValue: value,
                UpdatedByUserId: userId
            );

            await _mediator.Send(updateCommand, cancellationToken).ConfigureAwait(false);
        }
    }
}
