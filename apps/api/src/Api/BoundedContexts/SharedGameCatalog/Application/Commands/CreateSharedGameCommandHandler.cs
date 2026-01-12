using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for creating a new shared game in the catalog.
/// </summary>
internal sealed class CreateSharedGameCommandHandler : ICommandHandler<CreateSharedGameCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateSharedGameCommandHandler> _logger;

    public CreateSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(CreateSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Creating shared game: {Title} ({Year}), CreatedBy: {UserId}",
            command.Title, command.YearPublished, command.CreatedBy);

        // Check for duplicate BGG ID
        if (command.BggId.HasValue)
        {
            var exists = await _repository.ExistsByBggIdAsync(command.BggId.Value, cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                throw new InvalidOperationException($"A game with BGG ID {command.BggId.Value} already exists in the catalog");
            }
        }

        // Create rules value object if provided
        GameRules? rules = null;
        if (command.Rules is not null)
        {
            rules = GameRules.Create(command.Rules.Content, command.Rules.Language);
        }

        // Create aggregate
        var sharedGame = SharedGame.Create(
            command.Title,
            command.YearPublished,
            command.Description,
            command.MinPlayers,
            command.MaxPlayers,
            command.PlayingTimeMinutes,
            command.MinAge,
            command.ComplexityRating,
            command.AverageRating,
            command.ImageUrl,
            command.ThumbnailUrl,
            rules,
            command.CreatedBy,
            command.BggId);

        await _repository.AddAsync(sharedGame, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game created successfully: {GameId} - {Title}",
            sharedGame.Id, sharedGame.Title);

        return sharedGame.Id;
    }
}
