using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Handles adding a glossary entry to a game's memory. Creates GameMemory if none exists.
/// </summary>
internal sealed class AddGlossaryEntryCommandHandler : ICommandHandler<AddGlossaryEntryCommand>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<AddGlossaryEntryCommandHandler> _logger;

    public AddGlossaryEntryCommandHandler(
        IGameMemoryRepository gameMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<AddGlossaryEntryCommandHandler> logger)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AddGlossaryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
            throw new ConflictException("Feature AgentMemory.Enabled is disabled");

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(command.GameId, command.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (memory == null)
        {
            memory = GameMemory.Create(command.GameId, command.OwnerId);

            try
            {
                memory.AddGlossaryEntry(command.Term, command.Definition, command.Language, command.Source);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                throw new ConflictException(ex.Message);
            }

            await _gameMemoryRepo.AddAsync(memory, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created new game memory for game {GameId}, owner {OwnerId} with glossary entry '{Term}' ({Language})",
                command.GameId, command.OwnerId, command.Term, command.Language);
        }
        else
        {
            try
            {
                memory.AddGlossaryEntry(command.Term, command.Definition, command.Language, command.Source);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                throw new ConflictException(ex.Message);
            }

            await _gameMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Added glossary entry '{Term}' ({Language}) to existing game memory for game {GameId}, owner {OwnerId}",
                command.Term, command.Language, command.GameId, command.OwnerId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
