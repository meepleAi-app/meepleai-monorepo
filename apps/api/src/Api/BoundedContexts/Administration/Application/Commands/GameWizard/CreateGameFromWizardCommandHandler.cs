using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Handler for CreateGameFromWizardCommand.
/// Orchestrates game creation by delegating to the existing ImportGameFromBggCommand.
/// Returns enriched result with game title for wizard UI.
/// </summary>
internal sealed class CreateGameFromWizardCommandHandler
    : ICommandHandler<CreateGameFromWizardCommand, CreateGameFromWizardResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<CreateGameFromWizardCommandHandler> _logger;

    public CreateGameFromWizardCommandHandler(
        IMediator mediator,
        ISharedGameRepository sharedGameRepository,
        ILogger<CreateGameFromWizardCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateGameFromWizardResult> Handle(
        CreateGameFromWizardCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Admin wizard: Creating game from BGG ID {BggId} by user {UserId}",
            command.BggId, command.CreatedByUserId);

        // Delegate to existing ImportGameFromBggCommand
        var importCommand = new ImportGameFromBggCommand(command.BggId, command.CreatedByUserId);
        var sharedGameId = await _mediator.Send(importCommand, cancellationToken).ConfigureAwait(false);

        // Fetch the created game to get title for the result
        var game = await _sharedGameRepository.GetByIdAsync(sharedGameId, cancellationToken).ConfigureAwait(false);

        var title = game?.Title ?? $"BGG #{command.BggId}";

        _logger.LogInformation(
            "Admin wizard: Game created successfully - {GameId} ({Title})",
            sharedGameId, title);

        return new CreateGameFromWizardResult
        {
            SharedGameId = sharedGameId,
            Title = title,
            BggId = command.BggId,
            Status = "created"
        };
    }
}
