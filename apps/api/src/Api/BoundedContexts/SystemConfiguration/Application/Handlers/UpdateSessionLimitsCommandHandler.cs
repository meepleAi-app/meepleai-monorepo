using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles updating session tier limits configuration.
/// Creates configurations if they don't exist, updates existing ones otherwise.
/// Triggers cache invalidation and audit trail via domain events.
/// Issue #3070: Session limits backend implementation.
/// </summary>
internal class UpdateSessionLimitsCommandHandler : ICommandHandler<UpdateSessionLimitsCommand, SessionLimitsDto>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationRepository _configRepository;

    private const string FreeTierKey = "SessionLimits:free:MaxSessions";
    private const string NormalTierKey = "SessionLimits:normal:MaxSessions";
    private const string PremiumTierKey = "SessionLimits:premium:MaxSessions";
    private const string Category = "SessionLimits";
    private const string Environment = "All";
    private const string ValueType = "int";

    public UpdateSessionLimitsCommandHandler(
        IMediator mediator,
        IConfigurationRepository configRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<SessionLimitsDto> Handle(
        UpdateSessionLimitsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Process all three tier limits in parallel
        var tasks = new[]
        {
            UpsertConfigurationAsync(FreeTierKey, command.FreeTierLimit, "Maximum active sessions for Free tier users", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(NormalTierKey, command.NormalTierLimit, "Maximum active sessions for Normal tier users", command.UpdatedByUserId, cancellationToken),
            UpsertConfigurationAsync(PremiumTierKey, command.PremiumTierLimit, "Maximum active sessions for Premium tier users (-1 = unlimited)", command.UpdatedByUserId, cancellationToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Return updated limits
        return new SessionLimitsDto(
            FreeTierLimit: command.FreeTierLimit,
            NormalTierLimit: command.NormalTierLimit,
            PremiumTierLimit: command.PremiumTierLimit,
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
