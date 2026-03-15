using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for setting RAG public access on a SharedGame.
/// </summary>
internal sealed class SetRagPublicAccessCommandHandler : ICommandHandler<SetRagPublicAccessCommand>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetRagPublicAccessCommandHandler> _logger;

    public SetRagPublicAccessCommandHandler(
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<SetRagPublicAccessCommandHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SetRagPublicAccessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _sharedGameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"SharedGame {command.SharedGameId} not found");

        game.SetRagPublicAccess(command.IsRagPublic);
        _sharedGameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Set RAG public access for SharedGame {SharedGameId} to {IsRagPublic}",
            command.SharedGameId, command.IsRagPublic);
    }
}
