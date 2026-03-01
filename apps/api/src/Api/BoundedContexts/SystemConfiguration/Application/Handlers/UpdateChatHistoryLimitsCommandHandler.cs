using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles updating chat history tier limits configuration.
/// Creates configurations if they don't exist, updates existing ones otherwise.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
internal class UpdateChatHistoryLimitsCommandHandler : ICommandHandler<UpdateChatHistoryLimitsCommand, ChatHistoryLimitsDto>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationRepository _configRepository;

    private const string Category = "ChatHistory";
    private const string Environment = "All";
    private const string ValueType = "int";

    public UpdateChatHistoryLimitsCommandHandler(
        IMediator mediator,
        IConfigurationRepository configRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task<ChatHistoryLimitsDto> Handle(
        UpdateChatHistoryLimitsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tasks = new[]
        {
            UpsertConfigurationAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                command.FreeTierLimit,
                "Maximum chat sessions stored for Free tier users",
                command.UpdatedByUserId,
                cancellationToken),
            UpsertConfigurationAsync(
                GetChatHistoryLimitsQueryHandler.NormalTierKey,
                command.NormalTierLimit,
                "Maximum chat sessions stored for Normal tier users",
                command.UpdatedByUserId,
                cancellationToken),
            UpsertConfigurationAsync(
                GetChatHistoryLimitsQueryHandler.PremiumTierKey,
                command.PremiumTierLimit,
                "Maximum chat sessions stored for Premium tier users",
                command.UpdatedByUserId,
                cancellationToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return new ChatHistoryLimitsDto(
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
        var existingConfig = await _configRepository.GetByKeyAsync(
            key,
            Environment,
            activeOnly: false,
            cancellationToken).ConfigureAwait(false);

        if (existingConfig == null)
        {
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
            var updateCommand = new UpdateConfigValueCommand(
                ConfigId: existingConfig.Id,
                NewValue: value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UpdatedByUserId: userId
            );

            await _mediator.Send(updateCommand, cancellationToken).ConfigureAwait(false);
        }
    }
}
