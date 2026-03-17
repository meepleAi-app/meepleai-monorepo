using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

public sealed record AssignBggIdRequest(Guid SharedGameId, int BggId);

public sealed record AssignBggIdCommand(
    Guid SharedGameId, int BggId, Guid UserId) : ICommand<bool>;

internal sealed class AssignBggIdCommandValidator : AbstractValidator<AssignBggIdCommand>
{
    public AssignBggIdCommandValidator()
    {
        RuleFor(x => x.SharedGameId).NotEmpty();
        RuleFor(x => x.BggId).GreaterThan(0);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

internal sealed class AssignBggIdCommandHandler
    : ICommandHandler<AssignBggIdCommand, bool>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignBggIdCommandHandler> _logger;

    public AssignBggIdCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AssignBggIdCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        AssignBggIdCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check if another game already has this BGG ID
        if (await _repository.ExistsByBggIdAsync(command.BggId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("BGG ID {BggId} already assigned to another game", command.BggId);
            return false;
        }

        var game = await _repository
            .GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found for BGG ID assignment", command.SharedGameId);
            return false;
        }

        game.AssignBggId(command.BggId, command.UserId);
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Assigned BGG ID {BggId} to game '{Title}' ({GameId})",
            command.BggId, game.Title, game.Id);

        return true;
    }
}
