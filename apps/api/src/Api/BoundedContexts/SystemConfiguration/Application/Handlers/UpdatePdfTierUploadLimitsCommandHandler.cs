using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles updating PDF upload tier limits configuration.
/// Creates configurations if they don't exist, updates existing ones otherwise.
/// Triggers cache invalidation and audit trail via domain events.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal class UpdatePdfTierUploadLimitsCommandHandler : ICommandHandler<UpdatePdfTierUploadLimitsCommand, PdfTierUploadLimitsDto>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationRepository _configRepository;

    // Configuration keys match PdfUploadQuotaService
    private const string FreeDailyKey = "UploadLimits:free:DailyLimit";
    private const string FreeWeeklyKey = "UploadLimits:free:WeeklyLimit";
    private const string NormalDailyKey = "UploadLimits:normal:DailyLimit";
    private const string NormalWeeklyKey = "UploadLimits:normal:WeeklyLimit";
    private const string PremiumDailyKey = "UploadLimits:premium:DailyLimit";
    private const string PremiumWeeklyKey = "UploadLimits:premium:WeeklyLimit";

    private const string Category = "UploadLimits";
    private const string Environment = "All";
    private const string ValueType = "int";

    public UpdatePdfTierUploadLimitsCommandHandler(
        IMediator mediator,
        IConfigurationRepository configRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<PdfTierUploadLimitsDto> Handle(
        UpdatePdfTierUploadLimitsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Process all six tier limits in parallel
        var tasks = new[]
        {
            UpsertConfigurationAsync(FreeDailyKey, command.FreeDailyLimit, "Free tier daily PDF upload limit", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(FreeWeeklyKey, command.FreeWeeklyLimit, "Free tier weekly PDF upload limit", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(NormalDailyKey, command.NormalDailyLimit, "Normal tier daily PDF upload limit", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(NormalWeeklyKey, command.NormalWeeklyLimit, "Normal tier weekly PDF upload limit", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(PremiumDailyKey, command.PremiumDailyLimit, "Premium tier daily PDF upload limit", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(PremiumWeeklyKey, command.PremiumWeeklyLimit, "Premium tier weekly PDF upload limit", command.UpdatedByUserId, cancellationToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Return updated limits
        return new PdfTierUploadLimitsDto(
            FreeDailyLimit: command.FreeDailyLimit,
            FreeWeeklyLimit: command.FreeWeeklyLimit,
            NormalDailyLimit: command.NormalDailyLimit,
            NormalWeeklyLimit: command.NormalWeeklyLimit,
            PremiumDailyLimit: command.PremiumDailyLimit,
            PremiumWeeklyLimit: command.PremiumWeeklyLimit,
            LastUpdatedAt: DateTime.UtcNow,
            LastUpdatedByUserId: command.UpdatedByUserId.ToString()
        );
    }

    private async Task UpsertConfigurationAsync(
        string key,
        int value,
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
                Value: value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ValueType: ValueType,
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
                NewValue: value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UpdatedByUserId: userId
            );

            await _mediator.Send(updateCommand, cancellationToken).ConfigureAwait(false);
        }
    }
}
