using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Handles updating a group's gaming preferences.
/// </summary>
internal sealed class UpdateGroupPreferencesCommandHandler : ICommandHandler<UpdateGroupPreferencesCommand>
{
    private readonly IGroupMemoryRepository _groupRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<UpdateGroupPreferencesCommandHandler> _logger;

    public UpdateGroupPreferencesCommandHandler(
        IGroupMemoryRepository groupRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<UpdateGroupPreferencesCommandHandler> logger)
    {
        _groupRepo = groupRepo ?? throw new ArgumentNullException(nameof(groupRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateGroupPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
            throw new InvalidOperationException("Feature AgentMemory.Enabled is disabled");

        var group = await _groupRepo.GetByIdAsync(command.GroupId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Group memory {command.GroupId} not found");

        PreferredComplexity? complexity = command.PreferredComplexity != null
            ? Enum.Parse<PreferredComplexity>(command.PreferredComplexity, ignoreCase: true)
            : null;

        var preferences = new GroupPreferences
        {
            MaxDuration = command.MaxDuration,
            PreferredComplexity = complexity,
            CustomNotes = command.CustomNotes
        };

        group.UpdatePreferences(preferences);

        await _groupRepo.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated preferences for group {GroupId}", command.GroupId);
    }
}
