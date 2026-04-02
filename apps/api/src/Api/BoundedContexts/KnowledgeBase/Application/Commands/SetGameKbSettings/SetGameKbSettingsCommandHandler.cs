using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SetGameKbSettings;

/// <summary>
/// Handles SetGameKbSettingsCommand.
/// Upserts per-game KB settings overrides to SystemConfiguration.
/// KB-10: Admin per-game KB settings backend.
/// </summary>
internal sealed class SetGameKbSettingsCommandHandler : IRequestHandler<SetGameKbSettingsCommand>
{
    // Sentinel user ID for system-initiated or admin configuration entries.
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IConfigurationRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetGameKbSettingsCommandHandler> _logger;

    public SetGameKbSettingsCommandHandler(
        IConfigurationRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<SetGameKbSettingsCommandHandler> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SetGameKbSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var configKey = $"KB:Game:{command.GameId}:Settings";
        var payload = new GameKbSettingsPayload(
            command.MaxChunks,
            command.ChunkSize,
            command.CacheEnabled,
            command.Language);
        var configValue = JsonSerializer.Serialize(payload);

        _logger.LogInformation(
            "Upserting KB settings for game {GameId} at key {Key}",
            command.GameId, configKey);

        var existing = await _configRepository
            .GetByKeyAsync(configKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var newConfig = new Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration(
                id: Guid.NewGuid(),
                key: new ConfigKey(configKey),
                value: configValue,
                valueType: "json",
                createdByUserId: SystemUserId,
                description: $"KB settings overrides for game {command.GameId}",
                category: "KnowledgeBase");

            await _configRepository.AddAsync(newConfig, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.UpdateValue(configValue, SystemUserId);
            await _configRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record GameKbSettingsPayload(
        int? MaxChunks,
        int? ChunkSize,
        bool? CacheEnabled,
        string? Language);
}
