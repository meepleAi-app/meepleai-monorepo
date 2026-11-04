using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// #4401: step 1 of the PDF import wizard. Creates the game as a draft skeleton (title only), so
/// the rulebook can be uploaded FOR it: the chunked upload requires a game, and a PDF tied to its
/// game from the start is the one the retrieval finds and the upload indexes.
/// </summary>
/// <remarks>
/// The wizard fills the rest at step 4 (<see cref="ImportGameFromPdfCommand"/>) with the metadata
/// extracted from the PDF and reviewed by the admin. An abandoned wizard leaves a draft skeleton,
/// which the admin sees in the catalogue and can delete.
/// </remarks>
internal sealed record StartGameImportCommand(string Title, Guid RequestedBy) : ICommand<Guid>;

internal sealed class StartGameImportCommandValidator : AbstractValidator<StartGameImportCommand>
{
    public StartGameImportCommandValidator()
    {
        // The same bounds as the skeleton and the import's metadata title.
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Game title is required")
            .MaximumLength(300);
        RuleFor(x => x.RequestedBy).NotEmpty();
    }
}

internal sealed class StartGameImportCommandHandler : ICommandHandler<StartGameImportCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartGameImportCommandHandler> _logger;

    public StartGameImportCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<StartGameImportCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(StartGameImportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = SharedGame.CreateSkeleton(command.Title, command.RequestedBy, _timeProvider);
        await _repository.AddAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF import started: draft skeleton {GameId} created by {UserId}", game.Id, command.RequestedBy);
        return game.Id;
    }
}
