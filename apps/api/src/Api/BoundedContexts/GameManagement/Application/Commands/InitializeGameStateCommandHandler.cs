using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles initialization of game session state from template.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal class InitializeGameStateCommandHandler : ICommandHandler<InitializeGameStateCommand, GameSessionStateDto>
{
    private readonly IGameSessionStateRepository _stateRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public InitializeGameStateCommandHandler(
        IGameSessionStateRepository stateRepository,
        MeepleAiDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionStateDto> Handle(InitializeGameStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate session exists
        var sessionExists = await _dbContext.GameSessions
            .AnyAsync(s => s.Id == command.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (!sessionExists)
            throw new NotFoundException("GameSession", command.GameSessionId.ToString());

        // Validate template exists
        var templateExists = await _dbContext.GameStateTemplates
            .AnyAsync(t => t.Id == command.TemplateId, cancellationToken)
            .ConfigureAwait(false);

        if (!templateExists)
            throw new NotFoundException("GameStateTemplate", command.TemplateId.ToString());

        // Check if state already exists for this session
        var existingState = await _stateRepository.GetBySessionIdAsync(command.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (existingState != null)
            throw new InvalidOperationException($"GameSession {command.GameSessionId} already has state initialized");

        // Get initial state from command or use empty object
        var initialState = command.InitialState ?? JsonDocument.Parse("{}");

        // Create state
        var state = GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: command.GameSessionId,
            templateId: command.TemplateId,
            initialState: initialState,
            createdBy: "system"
        );

        // Persist
        await _stateRepository.AddAsync(state, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return state.ToDto();
    }
}
