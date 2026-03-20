using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handles updating PDF upload limits for a specific tier.
/// Updates DailyLimit, WeeklyLimit, and PerGameLimit configurations.
/// Publishes ConfigurationUpdatedEvent for cache invalidation.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal sealed class UpdatePdfLimitsCommandHandler : ICommandHandler<UpdatePdfLimitsCommand, PdfLimitConfigDto>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationRepository _configRepository;
    private readonly ILogger<UpdatePdfLimitsCommandHandler> _logger;

    private const string Category = "UploadLimits";
    private const string EnvironmentValue = "All";
    private const string ValueTypeInt = "int";

    public UpdatePdfLimitsCommandHandler(
        IMediator mediator,
        IConfigurationRepository configRepository,
        ILogger<UpdatePdfLimitsCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfLimitConfigDto> Handle(UpdatePdfLimitsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tier = command.Tier.ToLowerInvariant();
        var dailyKey = $"UploadLimits:{tier}:DailyLimit";
        var weeklyKey = $"UploadLimits:{tier}:WeeklyLimit";
        var perGameKey = $"UploadLimits:{tier}:PerGameLimit";

        _logger.LogInformation(
            "Admin {AdminUserId} updating PDF limits for tier {Tier}: MaxPerDay={MaxPerDay}, MaxPerWeek={MaxPerWeek}, MaxPerGame={MaxPerGame}",
            command.AdminUserId, tier, command.MaxPerDay, command.MaxPerWeek, command.MaxPerGame);

        // Update or create configurations (events published automatically via Create/UpdateConfigValue commands)
        await UpsertConfigurationAsync(dailyKey, command.MaxPerDay,
            $"PDF {tier} tier daily upload limit", command.AdminUserId, cancellationToken).ConfigureAwait(false);
        await UpsertConfigurationAsync(weeklyKey, command.MaxPerWeek,
            $"PDF {tier} tier weekly upload limit", command.AdminUserId, cancellationToken).ConfigureAwait(false);
        await UpsertConfigurationAsync(perGameKey, command.MaxPerGame,
            $"PDF {tier} tier per-game upload limit", command.AdminUserId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully updated PDF limits for tier {Tier}",
            tier);

        // Return updated configuration
        return new PdfLimitConfigDto
        {
            Tier = tier,
            MaxPerDay = command.MaxPerDay,
            MaxPerWeek = command.MaxPerWeek,
            MaxPerGame = command.MaxPerGame,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = command.AdminUserId.ToString()
        };
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
            EnvironmentValue,
            activeOnly: false,
            cancellationToken).ConfigureAwait(false);

        if (existingConfig == null)
        {
            // Create new configuration
            var createCommand = new CreateConfigurationCommand(
                Key: key,
                Value: value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ValueType: ValueTypeInt,
                Description: description,
                Category: Category,
                Environment: EnvironmentValue,
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
