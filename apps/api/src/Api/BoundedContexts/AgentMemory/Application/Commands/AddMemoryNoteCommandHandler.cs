using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Handles adding a note to a game's memory. Creates GameMemory if none exists.
/// </summary>
internal sealed class AddMemoryNoteCommandHandler : ICommandHandler<AddMemoryNoteCommand>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<AddMemoryNoteCommandHandler> _logger;

    public AddMemoryNoteCommandHandler(
        IGameMemoryRepository gameMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<AddMemoryNoteCommandHandler> logger)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AddMemoryNoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
            throw new InvalidOperationException("Feature AgentMemory.Enabled is disabled");

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(command.GameId, command.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (memory == null)
        {
            memory = GameMemory.Create(command.GameId, command.OwnerId);
            memory.AddNote(command.Content, command.OwnerId);
            await _gameMemoryRepo.AddAsync(memory, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created new game memory for game {GameId}, owner {OwnerId} with note",
                command.GameId, command.OwnerId);
        }
        else
        {
            memory.AddNote(command.Content, command.OwnerId);
            await _gameMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Added note to existing game memory for game {GameId}, owner {OwnerId}",
                command.GameId, command.OwnerId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
