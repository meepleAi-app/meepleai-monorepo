using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Generates a summary share token. Organiser-only (#2349 ownership guard). Returns the token.
/// </summary>
internal sealed class GenerateGameNightShareTokenCommandHandler
    : ICommandHandler<GenerateGameNightShareTokenCommand, string>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateGameNightShareTokenCommandHandler(
        IGameNightEventRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<string> Handle(
        GenerateGameNightShareTokenCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can share this game night's summary.");

        var token = gameNight.GenerateShareToken();

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return token;
    }
}
